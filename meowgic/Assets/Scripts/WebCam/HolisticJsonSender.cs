using UnityEngine;
using System.Text;
using System.Net;
using System.Net.Sockets;
using Mediapipe.Unity;
using Mediapipe;
using Mediapipe.Unity.Sample.Holistic;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class HolisticJsonSender : MonoBehaviour
{
    [Serializable]
    public class ServerResponse
    {
        public string prediction;
        public Dictionary<string, float> probabilities;
    }

    [Serializable]
    public class FrameData
    {
        public List<float[]> sequence = new List<float[]>();
    }

    public HolisticDataProcessor dataProcessor;

    private HolisticTrackingSolution holisticSolution;

    public string serverIP = "3.39.24.148";
    public int serverPort = 8000;
    public string serverEndpoint = "/predict";

    public event System.Action<string> OnGestureRecognitionComplete;

    private bool isCollectingGesture = false;
    private List<SignLanguageData> collectedFrames = new List<SignLanguageData>();
    private int targetFrameCount = 30;
    private float frameCollectionInterval = 0.1f; 
    private float lastFrameTime = 0f;  
    private Coroutine gestureCollectionCoroutine;

    void Start()
    {
        if (dataProcessor == null)
        {
            Debug.LogError("HolisticDataProcessor가 할당되지 않았습니다.");
            this.enabled = false;
            return;
        }

        holisticSolution = GetComponent<HolisticTrackingSolution>();
        if (holisticSolution == null)
        {
            holisticSolution = FindObjectOfType<HolisticTrackingSolution>();
        }

        if (holisticSolution != null)
        {
            holisticSolution.OnHolisticLandmarksOutput.RemoveListener(OnHolisticLandmarksOutput);
            holisticSolution.OnHolisticLandmarksOutput.AddListener(OnHolisticLandmarksOutput);
            Debug.Log("[HolisticJsonSender] Event listener registered successfully!");
        }
        else
        {
            Debug.LogError("[HolisticJsonSender] HolisticTrackingSolution not found!");
            this.enabled = false;
            return;
        }
    }

    public void OnHolisticLandmarksOutput(
        NormalizedLandmarkList poseLandmarks,
        NormalizedLandmarkList faceLandmarks,
        NormalizedLandmarkList leftHandLandmarks,
        NormalizedLandmarkList rightHandLandmarks)
    {
        try
        {
            if (dataProcessor == null || !isCollectingGesture) return;

            // 프레임 간격 체크 - 설정한 간격이 지나지 않았으면 수집하지 않음
            if (Time.time - lastFrameTime < frameCollectionInterval)
                return;

            SignLanguageData processedData = dataProcessor.ProcessHolisticData(
                poseLandmarks, faceLandmarks, leftHandLandmarks, rightHandLandmarks
            );

            if (collectedFrames.Count < targetFrameCount)
            {
                collectedFrames.Add(processedData);
                lastFrameTime = Time.time;  // 프레임 수집 시간 기록
                Debug.Log($"Frame collected: {collectedFrames.Count}/{targetFrameCount}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[HolisticJsonSender] Error in OnHolisticLandmarksOutput: {e.Message}");
        }
    }

    public bool IsMediaPipeReady()
    {
        if (holisticSolution == null)
        {
            holisticSolution = FindObjectOfType<HolisticTrackingSolution>();
            if (holisticSolution == null) return false;
        }

        bool hasLeftHand = holisticSolution.CurrentLeftHandLandmarks?.Landmark != null
                        && holisticSolution.CurrentLeftHandLandmarks.Landmark.Count > 0;
        bool hasRightHand = holisticSolution.CurrentRightHandLandmarks?.Landmark != null
                         && holisticSolution.CurrentRightHandLandmarks.Landmark.Count > 0;

        return hasLeftHand || hasRightHand;
    }

    public void StartGestureCollection()
    {
        Debug.Log("Starting gesture collection...");

        if (gestureCollectionCoroutine != null)
        {
            StopCoroutine(gestureCollectionCoroutine);
        }

        collectedFrames.Clear();
        lastFrameTime = 0f;  // 타이머 초기화
        gestureCollectionCoroutine = StartCoroutine(CollectGestureWithDelay());
    }

    private IEnumerator CollectGestureWithDelay()
    {
        Debug.Log("손을 카메라 앞에 위치시키세요...");

        float waitTimeout = 10f;
        while (!IsMediaPipeReady() && waitTimeout > 0)
        {
            waitTimeout -= Time.deltaTime;
            yield return null;
        }

        if (waitTimeout <= 0)
        {
            Debug.LogWarning("손 감지 실패. 제스처 수집 취소.");
            OnGestureRecognitionComplete?.Invoke("none");
            yield break;
        }

        Debug.Log("손 감지됨! 준비하세요...");
        yield return new WaitForSeconds(1f);

        Debug.Log("3...");
        yield return new WaitForSeconds(1f);

        Debug.Log("2...");
        yield return new WaitForSeconds(1f);

        Debug.Log("1...");
        yield return new WaitForSeconds(1f);

        Debug.Log("제스처 시작!");
        isCollectingGesture = true;
        lastFrameTime = Time.time;  // 수집 시작 시간 기록

        yield return StartCoroutine(CollectGestureAndRecognize());
    }

    private IEnumerator CollectGestureAndRecognize()
    {
        Debug.Log($"Collecting {targetFrameCount} frames...");

        // 타임아웃을 충분히 늘림 (프레임 간격 * 프레임 수 + 여유시간)
        float timeoutTimer = (frameCollectionInterval * targetFrameCount) + 5f;

        while (collectedFrames.Count < targetFrameCount && timeoutTimer > 0)
        {
            timeoutTimer -= Time.deltaTime;
            yield return null;
        }

        isCollectingGesture = false;

        if (collectedFrames.Count >= targetFrameCount)
        {
            Debug.Log($"Collected {collectedFrames.Count} frames. Sending to server...");
            yield return StartCoroutine(SendToServerAndGetPrediction());
        }
        else
        {
            Debug.LogWarning($"Timeout! Only collected {collectedFrames.Count} frames.");
            OnGestureRecognitionComplete?.Invoke("none");
        }
    }

    private IEnumerator SendToServerAndGetPrediction()
    {
        var frameData = new FrameData();

        foreach (var frame in collectedFrames)
        {
            float[] vector = ConvertToVector(frame);
            frameData.sequence.Add(vector);
        }

        string json = JsonConvert.SerializeObject(frameData, Formatting.Indented);

        // ===== JSON 파일 저장 =====
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"gesture_data_{timestamp}.json";
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            System.IO.File.WriteAllText(filePath, json);
            Debug.Log($"JSON 저장 완료: {filePath}");
            Debug.Log($"프레임 수: {frameData.sequence.Count}, 차원: {frameData.sequence[0].Length}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"JSON 저장 실패: {e.Message}");
        }
        // =========================

        string url = $"http://{serverIP}:{serverPort}{serverEndpoint}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<ServerResponse>(request.downloadHandler.text);
                    Debug.Log($"Server prediction: {response.prediction}");

                    OnGestureRecognitionComplete?.Invoke(response.prediction);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Response parsing error: {e.Message}");
                    OnGestureRecognitionComplete?.Invoke("none");
                }
            }
            else
            {
                Debug.LogError($"Server request failed: {request.error}");
                OnGestureRecognitionComplete?.Invoke("none");
            }
        }
    }

    private float[] ConvertToVector(SignLanguageData data)
    {
        List<float> vector = new List<float>();

        // 1. 왼손 좌표 (63개 = 21 * 3)
        for (int i = 0; i < 21; i++)
        {
            vector.Add(GetFieldValue(data, $"left_hand_{i}_x"));
            vector.Add(GetFieldValue(data, $"left_hand_{i}_y"));
            vector.Add(GetFieldValue(data, $"left_hand_{i}_z"));
        }

        // 2. 오른손 좌표 (63개 = 21 * 3)
        for (int i = 0; i < 21; i++)
        {
            vector.Add(GetFieldValue(data, $"right_hand_{i}_x"));
            vector.Add(GetFieldValue(data, $"right_hand_{i}_y"));
            vector.Add(GetFieldValue(data, $"right_hand_{i}_z"));
        }

        // 3. 포즈 좌표 (27개 = 9 * 3)
        string[] poseJoints = { "l_shoulder", "r_shoulder", "l_elbow", "r_elbow",
                               "l_wrist", "r_wrist", "l_hip", "r_hip", "hip_center" };
        foreach (var joint in poseJoints)
        {
            vector.Add(GetFieldValue(data, $"{joint}_x"));
            vector.Add(GetFieldValue(data, $"{joint}_y"));
            vector.Add(GetFieldValue(data, $"{joint}_z"));
        }

        // 4. 얼굴 좌표 (63개 = 21 * 3)
        for (int i = 0; i < 21; i++)
        {
            vector.Add(GetFieldValue(data, $"face_{i}_x"));
            vector.Add(GetFieldValue(data, $"face_{i}_y"));
            vector.Add(GetFieldValue(data, $"face_{i}_z"));
        }

        // 5. 관절 각도 (8개)
        vector.Add(data.l_elbow_cos);
        vector.Add(data.l_elbow_sin);
        vector.Add(data.l_shoulder_cos);
        vector.Add(data.l_shoulder_sin);
        vector.Add(data.r_elbow_cos);
        vector.Add(data.r_elbow_sin);
        vector.Add(data.r_shoulder_cos);
        vector.Add(data.r_shoulder_sin);

        // 6. 손가락 상태 (8개)
        vector.Add(data.left_index_state);
        vector.Add(data.left_middle_state);
        vector.Add(data.left_ring_state);
        vector.Add(data.left_pinky_state);
        vector.Add(data.right_index_state);
        vector.Add(data.right_middle_state);
        vector.Add(data.right_ring_state);
        vector.Add(data.right_pinky_state);

        // 7. 손바닥 법선 (6개)
        vector.Add(data.left_palm_nx);
        vector.Add(data.left_palm_ny);
        vector.Add(data.left_palm_nz);
        vector.Add(data.right_palm_nx);
        vector.Add(data.right_palm_ny);
        vector.Add(data.right_palm_nz);

        // 8. 속도 (2개)
        vector.Add(data.speed_your);
        vector.Add(data.speed_friend);

        // 9. 마스크 플래그 (4개)
        vector.Add(data.lh_flag);
        vector.Add(data.rh_flag);
        vector.Add(data.pose_flag);
        vector.Add(data.face_flag);

        // 총 244개 확인
        if (vector.Count != 244)
        {
            Debug.LogWarning($"Vector dimension mismatch! Expected: 244, Actual: {vector.Count}");
            while (vector.Count < 244) vector.Add(0f);
        }

        return vector.ToArray();
    }

    private float GetFieldValue(SignLanguageData data, string fieldName)
    {
        var field = typeof(SignLanguageData).GetField(fieldName);
        if (field != null)
        {
            object value = field.GetValue(data);
            if (value is float)
                return (float)value;
            else if (value is int)
                return (float)(int)value;
        }
        return 0f;
    }

    void OnDestroy()
    {
        if (holisticSolution != null)
        {
            holisticSolution.OnHolisticLandmarksOutput.RemoveListener(OnHolisticLandmarksOutput);
        }

        if (gestureCollectionCoroutine != null)
        {
            StopCoroutine(gestureCollectionCoroutine);
        }
    }
}