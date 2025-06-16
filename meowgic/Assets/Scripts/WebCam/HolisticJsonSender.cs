using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Mediapipe.Unity.Sample.Holistic;
using Mediapipe.Unity;

public class HolisticJsonSender : MonoBehaviour
{
    [Serializable]
    public class ServerResponse
    {
        public string prediction;
        public ProbabilityData probabilities;
    }

    [Serializable]
    public class ProbabilityData
    {
        public float hello;
        public float sorry;
        public float thank;
        public float none;
    }

    [Serializable]
    public class SimpleLandmark
    {
        public float x, y, z;
        public float visibility;
    }

    [Serializable]
    public class SimpleFrame
    {
        public int frame;
        public long time;
        public List<SimpleLandmark> pose = new List<SimpleLandmark>();
        public List<SimpleLandmark> faceKey = new List<SimpleLandmark>();
        public List<SimpleLandmark> leftHand = new List<SimpleLandmark>();
        public List<SimpleLandmark> rightHand = new List<SimpleLandmark>();
    }

    [Serializable]
    public class SequenceData
    {
        public float[][] sequence;
    }

    [Header("Battle Mode (배틀 전용)")]
    private bool isCollectingForBattle = false;
    private int battleFramesCollected = 0;
    private BattleManager battleManager;

    [Header("Target")]
    public HolisticTrackingSolution holisticSolution;

    [Header("Settings")]
    public bool enableLogging = true;
    public bool sendToServer = false;
    public string serverUrl = "http://127.0.0.1:5000/pose";
    public int frameSkip = 1;

    [Header("Data Collection")]
    public bool collectPose = true;
    public bool collectFace = true;
    public bool collectHands = false;

    [Header("Face Optimization")]
    public FaceReductionMode faceMode = FaceReductionMode.KeyPointsOnly;
    public int faceSkipRatio = 5;

    [Header("Test Mode")]
    public bool useDummyData = false;

    [Header("Sequence Mode (Sign Language)")]
    public bool useSequenceMode = true;
    public int sequenceFrameCount = 30; // 30프레임씩 모아서 전송
    public bool showSequenceDetails = false;

    [Header("Debug")]
    public bool saveToTextFile = true;
    public string saveDirectory = "";

    public enum FaceReductionMode
    {
        Disabled,
        KeyPointsOnly,
        ReducedPoints,
        FullFace
    }

    private int frameCount = 0;
    private string actualSaveDirectory;
    private List<List<float>> frameSequence = new List<List<float>>();

    // *** 크래시 방지용 추가 변수들 ***
    private bool isInitialized = false;
    private bool isDestroying = false;
    private readonly object lockObject = new object(); // 스레드 안전성

    // MediaPipe Face Landmark의 주요 포인트 인덱스
    private readonly int[] faceKeyIndices = {
        0, 17, 18, 200, 199, 175,
        46, 53, 52, 65, 55, 70,
        276, 283, 282, 295, 285, 300,
        33, 7, 163, 144, 145, 153, 154, 155, 133, 173, 157, 158, 159, 160, 161, 246,
        362, 398, 384, 385, 386, 387, 388, 466, 263, 249, 390, 373, 374, 380, 381, 382,
        1, 2, 5, 4, 6, 168, 8, 9, 10, 151, 195, 197, 196, 3, 51, 48, 115, 131, 134, 102, 49, 220, 305, 281, 360, 279,
        61, 84, 17, 314, 405, 320, 307, 375, 321, 308, 324, 318,
        78, 95, 88, 178, 87, 14, 317, 402, 318, 324, 308
    };

    void Start()
    {
        try
        {
            if (isDestroying) return;

            Debug.Log("HolisticJsonSender 초기화 시작");

            // 기존 InvokeRepeating 모두 정리
            CancelInvoke();
            StopAllCoroutines();

            if (holisticSolution == null)
            {
                holisticSolution = FindObjectOfType<HolisticTrackingSolution>();
            }

            if (holisticSolution != null)
            {
                Debug.Log($"Found HolisticTrackingSolution: {holisticSolution.name}");
                Debug.Log($"Face mode: {faceMode}");
            }
            else
            {
                Debug.LogWarning("HolisticTrackingSolution not found! 더미 데이터 모드로 전환");
                useDummyData = true;
            }

            // 저장 디렉토리 설정
            SetupSaveDirectory();

            battleManager = FindObjectOfType<BattleManager>();
            if (battleManager != null)
            {
                Debug.Log("BattleManager found - 배틀 모드 활성화");
            }

            // 초기화 완료 후 데이터 수집 시작
            isInitialized = true;
            StartDataCollection();

            Debug.Log("HolisticJsonSender 초기화 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"HolisticJsonSender 초기화 실패: {e.Message}");
            isInitialized = false;
        }
    }

    private void SetupSaveDirectory()
    {
        if (saveToTextFile)
        {
            try
            {
                actualSaveDirectory = string.IsNullOrEmpty(saveDirectory) ?
                    Application.persistentDataPath : saveDirectory;

                if (!System.IO.Directory.Exists(actualSaveDirectory))
                {
                    System.IO.Directory.CreateDirectory(actualSaveDirectory);
                }

                Debug.Log($"JSON files will be saved to: {actualSaveDirectory}");
            }
            catch (Exception e)
            {
                Debug.LogError($"저장 디렉토리 설정 실패: {e.Message}");
                saveToTextFile = false;
            }
        }
    }

    private void StartDataCollection()
    {
        if (isDestroying || !isInitialized) return;

        try
        {
            if (useDummyData)
            {
                Debug.Log("더미 데이터 모드 시작");
                InvokeRepeating(nameof(SafeGenerateTestData), 1f, 0.1f);
            }
            else
            {
                Debug.Log("실제 데이터 추출 모드 시작");
                StartCoroutine(DelayedDataCollection());
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"데이터 수집 시작 실패: {e.Message}");
        }
    }

    private IEnumerator DelayedDataCollection()
    {
        yield return new WaitForSeconds(3f);

        if (!isDestroying && isInitialized)
        {
            Debug.Log("실제 데이터 추출 시작");
            InvokeRepeating(nameof(SafeExtractData), 0f, 0.1f);
        }
    }

    private void SafeGenerateTestData()
    {
        if (isDestroying || !isInitialized) return;

        try
        {
            frameCount++;
            if (frameCount % frameSkip != 0) return;

            var frame = CreateTestFrame();
            if (frame != null)
            {
                ProcessOptimizedFrame(frame);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"더미 데이터 생성 오류: {e.Message}");
        }
    }

    private SimpleFrame CreateTestFrame()
    {
        try
        {
            var frame = new SimpleFrame
            {
                frame = frameCount,
                time = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            };

            // 포즈 데이터 (33개)
            if (collectPose)
            {
                frame.pose = GenerateDummyPose();
            }

            // 최적화된 얼굴 데이터
            if (collectFace && faceMode != FaceReductionMode.Disabled)
            {
                frame.faceKey = GenerateOptimizedFaceData();
            }

            // 손 데이터 (각각 21개) - 수어를 위해 항상 생성
            frame.leftHand = GenerateDummyHand();
            frame.rightHand = GenerateDummyHand();

            return frame;
        }
        catch (Exception e)
        {
            Debug.LogError($"테스트 프레임 생성 오류: {e.Message}");
            return null;
        }
    }

    private void SafeExtractData()
    {
        if (isDestroying || !isInitialized) return;

        try
        {
            frameCount++;
            if (frameCount % frameSkip != 0) return;

            var frame = ExtractOptimizedDataSafely();
            if (frame != null)
            {
                ProcessOptimizedFrame(frame);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"데이터 추출 실패, 더미 데이터로 전환: {e.Message}");
            SwitchToDummyDataSafely();
        }
    }

    private void SwitchToDummyDataSafely()
    {
        try
        {
            useDummyData = true;
            CancelInvoke(nameof(SafeExtractData));

            if (!isDestroying && isInitialized)
            {
                InvokeRepeating(nameof(SafeGenerateTestData), 0.1f, 0.1f);
                Debug.Log("더미 데이터 모드로 안전하게 전환");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"더미 데이터 전환 실패: {e.Message}");
        }
    }

    private SimpleFrame ExtractOptimizedDataSafely()
    {
        try
        {
            var frame = new SimpleFrame
            {
                frame = frameCount,
                time = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            };

            if (collectPose)
            {
                var poseData = FindPoseDataSafely();
                frame.pose = poseData ?? GenerateDummyPose();
            }

            if (collectFace && faceMode != FaceReductionMode.Disabled)
            {
                var faceData = FindOptimizedFaceDataSafely();
                frame.faceKey = faceData ?? GenerateOptimizedFaceData();
            }

            // 수어를 위해 손 데이터는 항상 처리 (실제 데이터 추출은 복잡하므로 더미 사용)
            frame.leftHand = GenerateDummyHand();
            frame.rightHand = GenerateDummyHand();

            return frame;
        }
        catch (Exception e)
        {
            Debug.LogError($"데이터 추출 오류: {e.Message}");
            return null;
        }
    }

    private List<SimpleLandmark> FindOptimizedFaceDataSafely()
    {
        try
        {
            var fullFaceData = FindFaceDataSafely();
            return fullFaceData != null && fullFaceData.Count > 0
                ? OptimizeFaceData(fullFaceData)
                : GenerateOptimizedFaceData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"얼굴 데이터 추출 실패: {e.Message}");
            return GenerateOptimizedFaceData();
        }
    }

    private List<SimpleLandmark> OptimizeFaceData(List<SimpleLandmark> fullFaceData)
    {
        var optimized = new List<SimpleLandmark>();

        try
        {
            switch (faceMode)
            {
                case FaceReductionMode.KeyPointsOnly:
                    foreach (int index in faceKeyIndices)
                    {
                        if (index < fullFaceData.Count)
                        {
                            optimized.Add(fullFaceData[index]);
                        }
                    }
                    break;

                case FaceReductionMode.ReducedPoints:
                    for (int i = 0; i < fullFaceData.Count; i += faceSkipRatio)
                    {
                        optimized.Add(fullFaceData[i]);
                    }
                    break;

                case FaceReductionMode.FullFace:
                    optimized = new List<SimpleLandmark>(fullFaceData);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"얼굴 데이터 최적화 오류: {e.Message}");
        }

        return optimized;
    }

    private List<SimpleLandmark> FindFaceDataSafely()
    {
        // 실제 MediaPipe 데이터 추출 로직 (현재는 빈 리스트 반환)
        return new List<SimpleLandmark>();
    }

    private List<SimpleLandmark> FindPoseDataSafely()
    {
        // 실제 MediaPipe 데이터 추출 로직 (현재는 빈 리스트 반환)
        return new List<SimpleLandmark>();
    }

    private List<SimpleLandmark> GenerateOptimizedFaceData()
    {
        var landmarks = new List<SimpleLandmark>();

        try
        {
            int pointCount = faceMode switch
            {
                FaceReductionMode.KeyPointsOnly => faceKeyIndices.Length,
                FaceReductionMode.ReducedPoints => 468 / faceSkipRatio,
                FaceReductionMode.FullFace => 468,
                _ => 0
            };

            for (int i = 0; i < pointCount; i++)
            {
                landmarks.Add(new SimpleLandmark
                {
                    x = UnityEngine.Random.Range(0.3f, 0.7f),
                    y = UnityEngine.Random.Range(0.2f, 0.6f),
                    z = UnityEngine.Random.Range(-0.1f, 0.1f),
                    visibility = UnityEngine.Random.Range(0.8f, 1.0f)
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"얼굴 더미 데이터 생성 오류: {e.Message}");
        }

        return landmarks;
    }

    private List<SimpleLandmark> GenerateDummyPose()
    {
        var landmarks = new List<SimpleLandmark>();

        try
        {
            for (int i = 0; i < 33; i++)
            {
                landmarks.Add(new SimpleLandmark
                {
                    x = UnityEngine.Random.Range(0.2f, 0.8f),
                    y = UnityEngine.Random.Range(0.2f, 0.8f),
                    z = UnityEngine.Random.Range(-0.3f, 0.3f),
                    visibility = UnityEngine.Random.Range(0.7f, 1.0f)
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"포즈 더미 데이터 생성 오류: {e.Message}");
        }

        return landmarks;
    }

    private List<SimpleLandmark> GenerateDummyHand()
    {
        var landmarks = new List<SimpleLandmark>();

        try
        {
            for (int i = 0; i < 21; i++)
            {
                landmarks.Add(new SimpleLandmark
                {
                    x = UnityEngine.Random.Range(0.1f, 0.9f),
                    y = UnityEngine.Random.Range(0.3f, 0.7f),
                    z = UnityEngine.Random.Range(-0.2f, 0.2f),
                    visibility = UnityEngine.Random.Range(0.6f, 1.0f)
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"손 더미 데이터 생성 오류: {e.Message}");
        }

        return landmarks;
    }

    private void ProcessOptimizedFrame(SimpleFrame frame)
    {
        if (isDestroying || !isInitialized || frame == null) return;

        try
        {
            // 스레드 안전성을 위한 lock
            lock (lockObject)
            {
                if (!isCollectingForBattle) return;

                // 오버플로우 방지 - 더 엄격한 체크
                if (frameSequence.Count >= sequenceFrameCount + 10)
                {
                    Debug.LogError($"프레임 시퀀스 오버플로우! 수집된 프레임: {frameSequence.Count}, 목표: {sequenceFrameCount}");
                    ResetBattleCollection();
                    return;
                }

                var flattenedFrame = FlattenFrameData(frame);
                if (flattenedFrame == null || flattenedFrame.Count != 148)
                {
                    Debug.LogWarning($"잘못된 프레임 데이터: {flattenedFrame?.Count ?? 0} elements (예상: 148)");
                    return;
                }

                frameSequence.Add(flattenedFrame);
                battleFramesCollected++;

                if (enableLogging && battleFramesCollected <= 5)
                {
                    Debug.Log($"[배틀] 수어 프레임 수집: {battleFramesCollected}/{sequenceFrameCount}");
                }

                if (battleFramesCollected >= sequenceFrameCount)
                {
                    Debug.Log("30프레임 수집 완료! 서버로 전송합니다.");
                    SendSequenceToServer();
                    StopBattleCollection();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"프레임 처리 오류: {e.Message}");
            ResetBattleCollection();
        }
    }

    // 배틀 수집 상태 안전하게 리셋
    private void ResetBattleCollection()
    {
        lock (lockObject)
        {
            try
            {
                frameSequence?.Clear();
                battleFramesCollected = 0;
                isCollectingForBattle = false;
                Debug.Log("배틀 수집 상태 리셋됨");
            }
            catch (Exception e)
            {
                Debug.LogError($"배틀 수집 리셋 오류: {e.Message}");
            }
        }
    }

    public void StartGestureCollection()
    {
        if (isDestroying || !isInitialized)
        {
            Debug.LogWarning("HolisticJsonSender가 초기화되지 않았거나 종료 중입니다");
            return;
        }

        try
        {
            lock (lockObject)
            {
                ResetBattleCollection();
                frameSequence = new List<List<float>>();
                battleFramesCollected = 0;
                isCollectingForBattle = true;

                Debug.Log("=== 스킬 사용! 30프레임 수집 시작 ===");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"제스처 수집 시작 오류: {e.Message}");
        }
    }

    private void StopBattleCollection()
    {
        lock (lockObject)
        {
            isCollectingForBattle = false;
            battleFramesCollected = 0;
            Debug.Log("=== 30프레임 수집 완료! 수집 중지 ===");
        }
    }

    public bool IsCollectingFrames()
    {
        if (isDestroying || !isInitialized) return false;

        lock (lockObject)
        {
            return isCollectingForBattle && battleFramesCollected < sequenceFrameCount;
        }
    }

    public int GetCurrentFrameCount()
    {
        lock (lockObject)
        {
            return battleFramesCollected;
        }
    }

    public float GetCollectionProgress()
    {
        lock (lockObject)
        {
            return sequenceFrameCount > 0 ? (float)battleFramesCollected / sequenceFrameCount : 0f;
        }
    }

    private List<float> FlattenFrameData(SimpleFrame frame)
    {
        try
        {
            var flattened = new List<float>();

            // 왼손 21개 점 xyz = 63개 값
            AddHandData(flattened, frame.leftHand, "Left");

            // 오른손 21개 점 xyz = 63개 값
            AddHandData(flattened, frame.rightHand, "Right");

            // 포즈에서 필요한 부분 추가 (어깨, 팔꿈치 등)
            AddPoseKeypoints(flattened, frame.pose);

            // 총 148개 값이 되도록 조정 (안전한 방법)
            int targetSize = 148;
            if (flattened.Count < targetSize)
            {
                int padding = targetSize - flattened.Count;
                for (int i = 0; i < padding; i++)
                {
                    flattened.Add(0f);
                }
            }
            else if (flattened.Count > targetSize)
            {
                flattened = flattened.GetRange(0, targetSize);
            }

            return flattened;
        }
        catch (Exception e)
        {
            Debug.LogError($"프레임 데이터 평면화 오류: {e.Message}");
            return null;
        }
    }

    private void AddHandData(List<float> flattened, List<SimpleLandmark> handLandmarks, string handName)
    {
        try
        {
            for (int i = 0; i < 21; i++)
            {
                if (handLandmarks != null && i < handLandmarks.Count)
                {
                    flattened.Add(handLandmarks[i].x);
                    flattened.Add(handLandmarks[i].y);
                    flattened.Add(handLandmarks[i].z);
                }
                else
                {
                    flattened.Add(0f);
                    flattened.Add(0f);
                    flattened.Add(0f);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{handName} 손 데이터 추가 오류: {e.Message}");
        }
    }

    private void AddPoseKeypoints(List<float> flattened, List<SimpleLandmark> pose)
    {
        try
        {
            int[] poseIndices = { 11, 12, 13, 14, 15, 16, 23, 24 }; // 8개 포인트 = 24개 값

            foreach (int index in poseIndices)
            {
                if (pose != null && index < pose.Count)
                {
                    flattened.Add(pose[index].x);
                    flattened.Add(pose[index].y);
                    flattened.Add(pose[index].z);
                }
                else
                {
                    flattened.Add(0f);
                    flattened.Add(0f);
                    flattened.Add(0f);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"포즈 키포인트 추가 오류: {e.Message}");
        }
    }

    private void SendSequenceToServer()
    {
        if (isDestroying || frameSequence == null || frameSequence.Count == 0)
        {
            Debug.LogWarning("전송할 시퀀스 데이터가 없습니다");
            return;
        }

        try
        {
            StartCoroutine(SafeSendSequence());
        }
        catch (Exception e)
        {
            Debug.LogError($"시퀀스 전송 시작 오류: {e.Message}");
        }
    }

    private IEnumerator SafeSendSequence()
    {
        if (isDestroying) yield break;

        // 1) 서버 전송용 JSON 준비 (try‐catch만 이 부분에)
        string json = "{}";
        try
        {
            float[][] sequenceArray;
            lock (lockObject)
            {
                sequenceArray = new float[frameSequence.Count][];
                for (int i = 0; i < frameSequence.Count; i++)
                    sequenceArray[i] = frameSequence[i].ToArray();
            }

            var sequenceData = new SequenceData { sequence = sequenceArray };
            json = JsonUtility.ToJson(sequenceData, true);

            if (string.IsNullOrEmpty(json) || json == "{}")
                json = CreateManualJson(frameSequence);

            if (enableLogging)
                Debug.Log($"시퀀스 준비 완료 - 프레임: {sequenceArray.Length}, JSON 크기: {json.Length}");
        }
        catch (Exception e)
        {
            Debug.LogError($"시퀀스 준비 중 오류: {e.Message}");
        }

        // 2) 파일 저장 (예외 처리 불필요하면 그대로)
        if (saveToTextFile)
            SaveSequenceToFile(json);

        // 3) 실제 전송 — yield return 은 여기에만!
        if (sendToServer && !string.IsNullOrEmpty(serverUrl))
            yield return StartCoroutine(SendSequenceToServerCoroutine(json));
    }


    private IEnumerator SendSequenceToServerCoroutine(string json)
    {
        UnityWebRequest request = null;

        try
        {
            request = new UnityWebRequest(serverUrl, "POST")
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer()
            };

            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
        }
        catch (Exception e)
        {
            Debug.LogError($"웹 요청 생성 오류: {e.Message}");
            yield break;
        }

        // yield return을 try-catch 밖에서 실행
        yield return request.SendWebRequest();

        // 응답 처리는 별도로 try-catch 적용
        try
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;

                if (!string.IsNullOrEmpty(responseText))
                {
                    try
                    {
                        ServerResponse response = JsonUtility.FromJson<ServerResponse>(responseText);
                        if (response != null)
                        {
                            ProcessServerResponse(response);
                        }
                        else
                        {
                            Debug.LogWarning("서버 응답 파싱 실패");
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Debug.LogWarning($"응답 파싱 오류: {parseEx.Message}");
                    }
                }

                if (enableLogging)
                    Debug.Log("시퀀스 전송 성공!");
            }
            else
            {
                Debug.LogWarning($"시퀀스 전송 실패: {request.error}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"서버 응답 처리 오류: {e.Message}");
        }
        finally
        {
            // 리소스 정리
            try
            {
                request?.Dispose();
            }
            catch (Exception disposeEx)
            {
                Debug.LogWarning($"웹 요청 정리 오류: {disposeEx.Message}");
            }
        }
    }

    private void ProcessServerResponse(ServerResponse response)
    {
        try
        {
            if (battleManager != null && !isDestroying)
            {
                battleManager.OnGestureRecognized(response.prediction);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"서버 응답 처리 오류: {e.Message}");
        }
    }

    private string CreateManualJson(List<List<float>> sequence)
    {
        try
        {
            var json = new System.Text.StringBuilder();
            json.Append("{\"sequence\":[");

            for (int i = 0; i < sequence.Count; i++)
            {
                if (i > 0) json.Append(",");
                json.Append("[");

                var frame = sequence[i];
                for (int j = 0; j < frame.Count; j++)
                {
                    if (j > 0) json.Append(",");
                    json.Append(frame[j].ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
                }

                json.Append("]");
            }

            json.Append("]}");
            return json.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError($"수동 JSON 생성 오류: {e.Message}");
            return "{}";
        }
    }

    private void SaveSequenceToFile(string json)
    {
        try
        {
            if (string.IsNullOrEmpty(actualSaveDirectory)) return;

            string fileName = $"sequence_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.json";
            string filePath = System.IO.Path.Combine(actualSaveDirectory, fileName);

            System.IO.File.WriteAllText(filePath, json);
            if (enableLogging)
                Debug.Log($"시퀀스 JSON 저장: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"시퀀스 JSON 저장 실패: {e.Message}");
        }
    }

    public void ForceSendCurrentSequence()
    {
        if (isDestroying || !isInitialized) return;

        try
        {
            lock (lockObject)
            {
                if (frameSequence != null && frameSequence.Count > 0)
                {
                    Debug.Log($"[배틀] 강제 전송: {frameSequence.Count}프레임");
                    SendSequenceToServer();
                    StopBattleCollection();
                }
                else
                {
                    Debug.LogWarning("[배틀] 전송할 프레임이 없습니다");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"강제 전송 오류: {e.Message}");
        }
    }

    // Context Menu 메서드들 (안전하게 수정)
    [ContextMenu("Switch to Real Data")]
    public void SwitchToRealData()
    {
        if (isDestroying) return;

        try
        {
            useDummyData = false;
            CancelInvoke();
            StartCoroutine(DelayedDataCollection());
            Debug.Log("실제 데이터 추출로 전환");
        }
        catch (Exception e)
        {
            Debug.LogError($"실제 데이터 전환 오류: {e.Message}");
        }
    }

    [ContextMenu("Switch to Dummy Data")]
    public void SwitchToDummyData()
    {
        if (isDestroying) return;

        SwitchToDummyDataSafely();
    }

    [ContextMenu("Show Battle Stats")]
    public void ShowBattleStats()
    {
        Debug.Log($"=== 배틀 모드 상태 ===");
        Debug.Log($"초기화됨: {isInitialized}");
        Debug.Log($"종료 중: {isDestroying}");
        Debug.Log($"스킬 수집 중: {isCollectingForBattle}");
        Debug.Log($"수집된 프레임: {battleFramesCollected}/{sequenceFrameCount}");
        Debug.Log($"수집 진행률: {GetCollectionProgress() * 100:F1}%");
        Debug.Log($"BattleManager 연결: {(battleManager != null ? "연결됨" : "없음")}");
    }

    void OnDestroy()
    {
        isDestroying = true;

        try
        {
            Debug.Log("HolisticJsonSender 정리 시작");

            // 모든 코루틴과 InvokeRepeating 중지
            StopAllCoroutines();
            CancelInvoke();

            // 수집 상태 초기화
            lock (lockObject)
            {
                isCollectingForBattle = false;
                battleFramesCollected = 0;

                // 메모리 정리
                frameSequence?.Clear();
                frameSequence = null;
            }

            // 참조 정리
            holisticSolution = null;
            battleManager = null;
            isInitialized = false;

            Debug.Log("HolisticJsonSender 정리 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"HolisticJsonSender 정리 중 오류: {e.Message}");
        }
    }
}