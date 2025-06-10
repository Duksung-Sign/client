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
    public bool useDummyData = true;

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
            Debug.LogError("HolisticTrackingSolution not found!");
            return;
        }

        // 저장 디렉토리 설정
        if (saveToTextFile)
        {
            actualSaveDirectory = string.IsNullOrEmpty(saveDirectory) ?
                Application.persistentDataPath : saveDirectory;

            if (!System.IO.Directory.Exists(actualSaveDirectory))
            {
                System.IO.Directory.CreateDirectory(actualSaveDirectory);
            }

            Debug.Log($"JSON files will be saved to: {actualSaveDirectory}");
        }

        if (useDummyData)
        {
            Debug.Log($"Starting with dummy data - Sequence mode: {useSequenceMode}");
            InvokeRepeating(nameof(GenerateOptimizedTestData), 0.1f, 0.1f);
        }
        else
        {
            Debug.Log("Starting optimized extraction method");
            StartCoroutine(SafeExtractionCoroutine());
        }

        battleManager = FindObjectOfType<BattleManager>();
        if (battleManager != null)
        {
            Debug.Log("BattleManager found - 배틀 모드 활성화");
        }
    }

    private IEnumerator SafeExtractionCoroutine()
    {
        yield return new WaitForSeconds(3f);
        Debug.Log("Starting optimized data extraction...");
        InvokeRepeating(nameof(SafeExtractData), 0.1f, 0.1f);
    }

    private void GenerateOptimizedTestData()
    {
        frameCount++;

        if (frameCount % frameSkip != 0) return;

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

        ProcessOptimizedFrame(frame);
    }

    private List<SimpleLandmark> GenerateOptimizedFaceData()
    {
        var landmarks = new List<SimpleLandmark>();

        switch (faceMode)
        {
            case FaceReductionMode.KeyPointsOnly:
                for (int i = 0; i < faceKeyIndices.Length; i++)
                {
                    landmarks.Add(new SimpleLandmark
                    {
                        x = UnityEngine.Random.Range(0.3f, 0.7f),
                        y = UnityEngine.Random.Range(0.2f, 0.6f),
                        z = UnityEngine.Random.Range(-0.1f, 0.1f),
                        visibility = UnityEngine.Random.Range(0.8f, 1.0f)
                    });
                }
                break;

            case FaceReductionMode.ReducedPoints:
                for (int i = 0; i < 468; i += faceSkipRatio)
                {
                    landmarks.Add(new SimpleLandmark
                    {
                        x = UnityEngine.Random.Range(0.3f, 0.7f),
                        y = UnityEngine.Random.Range(0.2f, 0.6f),
                        z = UnityEngine.Random.Range(-0.1f, 0.1f),
                        visibility = UnityEngine.Random.Range(0.8f, 1.0f)
                    });
                }
                break;

            case FaceReductionMode.FullFace:
                for (int i = 0; i < 468; i++)
                {
                    landmarks.Add(new SimpleLandmark
                    {
                        x = UnityEngine.Random.Range(0.3f, 0.7f),
                        y = UnityEngine.Random.Range(0.2f, 0.6f),
                        z = UnityEngine.Random.Range(-0.1f, 0.1f),
                        visibility = UnityEngine.Random.Range(0.8f, 1.0f)
                    });
                }
                break;
        }

        return landmarks;
    }

    private void SafeExtractData()
    {
        frameCount++;

        if (frameCount % frameSkip != 0) return;

        try
        {
            var frame = ExtractOptimizedDataSafely();

            if (frame != null)
            {
                ProcessOptimizedFrame(frame);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Optimized extraction failed: {e.Message}");
            useDummyData = true;
            CancelInvoke(nameof(SafeExtractData));
            InvokeRepeating(nameof(GenerateOptimizedTestData), 0.1f, 0.1f);
        }
    }

    private SimpleFrame ExtractOptimizedDataSafely()
    {
        var frame = new SimpleFrame
        {
            frame = frameCount,
            time = DateTimeOffset.Now.ToUnixTimeMilliseconds()
        };

        try
        {
            if (collectPose)
            {
                var poseData = FindPoseDataSafely();
                if (poseData != null && poseData.Count > 0)
                {
                    frame.pose = poseData;
                    if (enableLogging && frameCount % 30 == 0)
                        Debug.Log($"Extracted real pose data: {poseData.Count} landmarks");
                }
                else
                {
                    frame.pose = GenerateDummyPose();
                }
            }

            if (collectFace && faceMode != FaceReductionMode.Disabled)
            {
                var faceData = FindOptimizedFaceDataSafely();
                if (faceData != null && faceData.Count > 0)
                {
                    frame.faceKey = faceData;
                    if (enableLogging && frameCount % 30 == 0)
                        Debug.Log($"Extracted optimized face data: {faceData.Count} landmarks");
                }
                else
                {
                    frame.faceKey = GenerateOptimizedFaceData();
                }
            }

            // 수어를 위해 손 데이터는 항상 처리
            frame.leftHand = GenerateDummyHand();
            frame.rightHand = GenerateDummyHand();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"ExtractOptimizedDataSafely error: {e.Message}");
            return null;
        }

        return frame;
    }

    private List<SimpleLandmark> FindOptimizedFaceDataSafely()
    {
        var landmarks = new List<SimpleLandmark>();

        try
        {
            var fullFaceData = FindFaceDataSafely();

            if (fullFaceData != null && fullFaceData.Count > 0)
            {
                landmarks = OptimizeFaceData(fullFaceData);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"FindOptimizedFaceDataSafely error: {e.Message}");
        }

        return landmarks;
    }

    private List<SimpleLandmark> OptimizeFaceData(List<SimpleLandmark> fullFaceData)
    {
        var optimized = new List<SimpleLandmark>();

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
                optimized = fullFaceData;
                break;
        }

        return optimized;
    }

    private List<SimpleLandmark> FindFaceDataSafely()
    {
        return new List<SimpleLandmark>();
    }

    private List<SimpleLandmark> FindPoseDataSafely()
    {
        return new List<SimpleLandmark>();
    }

    private List<SimpleLandmark> GenerateDummyPose()
    {
        var landmarks = new List<SimpleLandmark>();
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
        return landmarks;
    }

    private List<SimpleLandmark> GenerateDummyHand()
    {
        var landmarks = new List<SimpleLandmark>();
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
        return landmarks;
    }

    private void ProcessOptimizedFrame(SimpleFrame frame)
    {
        if (isCollectingForBattle)
        {
            var flattenedFrame = FlattenFrameData(frame);
            frameSequence.Add(flattenedFrame);
            battleFramesCollected++;

            if (enableLogging)
            {
                Debug.Log($"[배틀] 수어 프레임 수집: {battleFramesCollected}/{sequenceFrameCount}");
            }

            // 30프레임 모이면 즉시 서버 전송하고 수집 중지
            if (battleFramesCollected >= sequenceFrameCount)
            {
                Debug.Log("30프레임 수집 완료! 서버로 전송합니다.");
                SendSequenceToServer();
                StopBattleCollection();
            }
        }
    }

    public void StartBattleSkillCollection()
    {
        frameSequence.Clear();
        battleFramesCollected = 0;
        isCollectingForBattle = true;

        Debug.Log("=== 스킬 사용! 30프레임 수집 시작 ===");
    }

    // 수집 완료 후 호출
    private void StopBattleCollection()
    {
        isCollectingForBattle = false;
        battleFramesCollected = 0;

        Debug.Log("=== 30프레임 수집 완료! 수집 중지 ===");
    }

    // 수집 상태 확인 메서드들
    public bool IsCollectingForSkill()
    {
        return isCollectingForBattle;
    }

    public int GetCollectedFrameCount()
    {
        return battleFramesCollected;
    }

    public float GetCollectionProgress()
    {
        return (float)battleFramesCollected / sequenceFrameCount;
    }

    private List<float> FlattenFrameData(SimpleFrame frame)
    {
        var flattened = new List<float>();

        // 왼손 21개 점 xyz = 63개 값
        AddHandData(flattened, frame.leftHand, "Left");

        // 오른손 21개 점 xyz = 63개 값
        AddHandData(flattened, frame.rightHand, "Right");

        // 포즈에서 필요한 부분 추가 (어깨, 팔꿈치 등)
        AddPoseKeypoints(flattened, frame.pose);

        // 총 148개 값이 되도록 조정
        while (flattened.Count < 148)
        {
            flattened.Add(0f);
        }

        if (flattened.Count > 148)
        {
            flattened = flattened.GetRange(0, 148);
        }

        return flattened;
    }

    private void AddHandData(List<float> flattened, List<SimpleLandmark> handLandmarks, string handName)
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
                // 데이터 없으면 0으로 채움
                flattened.Add(0f);
                flattened.Add(0f);
                flattened.Add(0f);
            }
        }
    }

    private void AddPoseKeypoints(List<float> flattened, List<SimpleLandmark> pose)
    {
        // MediaPipe 포즈 랜드마크에서 필요한 부분만 추출
        // 11: 왼쪽 어깨, 12: 오른쪽 어깨, 13: 왼쪽 팔꿈치, 14: 오른쪽 팔꿈치
        // 15: 왼쪽 손목, 16: 오른쪽 손목, 23: 왼쪽 엉덩이, 24: 오른쪽 엉덩이
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

    private void SendSequenceToServer()
    {
        if (frameSequence.Count == 0)
        {
            Debug.LogWarning("No frames in sequence to send");
            return;
        }

        // List<List<float>>를 float[][]로 수동 변환
        float[][] sequenceArray = new float[frameSequence.Count][];
        for (int i = 0; i < frameSequence.Count; i++)
        {
            sequenceArray[i] = frameSequence[i].ToArray();
        }

        var sequenceData = new SequenceData { sequence = sequenceArray };
        string json = JsonUtility.ToJson(sequenceData, true);

        // JSON이 비어있다면 수동으로 생성
        if (string.IsNullOrEmpty(json) || json == "{}")
        {
            Debug.LogWarning("JsonUtility failed, creating manual JSON...");
            json = CreateManualJson(frameSequence);
        }

        if (enableLogging)
        {
            Debug.Log($"=== Sending Sequence ===");
            Debug.Log($"Frames: {frameSequence.Count}, Each frame: 148 values");
            Debug.Log($"Total data points: {frameSequence.Count * 148}");
            Debug.Log($"JSON size: {json.Length} characters");
            Debug.Log($"JSON preview: {json.Substring(0, Math.Min(200, json.Length))}...");

            if (frameSequence.Count > 0 && frameSequence[0].Count > 0)
            {
                Debug.Log($"First frame sample: [{string.Join(", ", frameSequence[0].Take(10).Select(f => f.ToString("F3")))}...]");
            }
        }

        if (saveToTextFile)
        {
            SaveSequenceToFile(json);
        }

        if (sendToServer)
        {
            StartCoroutine(SendSequence(json));
        }
    }

    private string CreateManualJson(List<List<float>> sequence)
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

    private void SaveSequenceToFile(string json)
    {
        try
        {
            string fileName = $"sequence_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.json";
            string filePath = System.IO.Path.Combine(actualSaveDirectory, fileName);

            System.IO.File.WriteAllText(filePath, json);
            Debug.Log($"Sequence JSON saved to: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save sequence JSON: {e.Message}");
        }
    }

    private void SaveJsonToFile(string json, int frameNumber)
    {
        try
        {
            string fileName = $"holistic_frame_{frameNumber:D6}.json";
            string filePath = System.IO.Path.Combine(actualSaveDirectory, fileName);

            System.IO.File.WriteAllText(filePath, json);

            if (frameNumber <= 5 || frameNumber % 100 == 0)
            {
                Debug.Log($"JSON saved to: {filePath}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save JSON file: {e.Message}");
        }
    }

    private IEnumerator SendSequence(string json)
    {
        using var request = new UnityWebRequest(serverUrl, "POST")
        {
            uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer()
        };

        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;

            if (!string.IsNullOrEmpty(responseText))
            {
                /*
                try
                {*/
                    ServerResponse response = JsonUtility.FromJson<ServerResponse>(responseText);
                    if (response == null)
                    {
                        if (enableLogging)
                            Debug.LogWarning("ServerResponse is null after parsing!");
                    }
                    else if (response.probabilities == null)
                    {
                        if (enableLogging)
                            Debug.LogWarning("Probabilities is null after parsing!");
                    }
                    else
                    {
                        ProcessServerResponse(response);
                    }
                /*}
                
                catch (Exception e)
                {
                    if (enableLogging)
                        Debug.LogWarning($"Response parsing failed: {e.Message}");
                }
                */
            }

            if (enableLogging)
                Debug.Log("Sequence sent successfully!");
        }
        else if (enableLogging)
        {
            Debug.LogWarning($"Sequence send failed: {request.error}");
        }
    }

    private void ProcessServerResponse(ServerResponse response)
    {
        if (battleManager != null)
        {
            battleManager.OnGestureRecognized(response.prediction);
        }

    }

    private IEnumerator SendFrame(string json)
    {
        using var request = new UnityWebRequest(serverUrl, "POST")
        {
            uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer()
        };

        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 2;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success && enableLogging)
        {
            Debug.LogWarning($"Send failed: {request.error}");
        }
    }

    /// <summary>
    /// 배틀 매니저에서 호출할 수 있는 공개 메서드
    /// </summary>
    public void StartGestureCollection()
    {
        StartBattleSkillCollection();
    }

    public bool IsCollectingFrames()
    {
        return isCollectingForBattle && battleFramesCollected < sequenceFrameCount;
    }

    public int GetCurrentFrameCount()
    {
        return battleFramesCollected;
    }

    [ContextMenu("Switch to Real Data")]
    public void SwitchToRealData()
    {
        useDummyData = false;
        CancelInvoke(nameof(GenerateOptimizedTestData));
        StartCoroutine(SafeExtractionCoroutine());
        Debug.Log("Switched to real data extraction");
    }

    [ContextMenu("Switch to Dummy Data")]
    public void SwitchToDummyData()
    {
        useDummyData = true;
        CancelInvoke(nameof(SafeExtractData));
        InvokeRepeating(nameof(GenerateOptimizedTestData), 0.1f, 0.1f);
        Debug.Log("Switched to dummy data generation");
    }

    [ContextMenu("Toggle Sequence Mode")]
    public void ToggleSequenceMode()
    {
        useSequenceMode = !useSequenceMode;
        frameSequence.Clear();
        Debug.Log($"Sequence mode: {useSequenceMode}");
    }

    [ContextMenu("Test Sequence Format")]
    public void TestSequenceFormat()
    {
        var testFrame = new SimpleFrame
        {
            frame = 1,
            time = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            leftHand = GenerateDummyHand(),
            rightHand = GenerateDummyHand(),
            pose = GenerateDummyPose()
        };

        var flattened = FlattenFrameData(testFrame);
        Debug.Log($"=== Test Sequence Format ===");
        Debug.Log($"Flattened frame size: {flattened.Count}");
        Debug.Log($"Expected: 148, Actual: {flattened.Count}");
        Debug.Log($"Left hand (0-62): [{string.Join(", ", flattened.Take(6).Select(f => f.ToString("F3")))}...]");
        Debug.Log($"Right hand (63-125): [{string.Join(", ", flattened.Skip(63).Take(6).Select(f => f.ToString("F3")))}...]");
        Debug.Log($"Pose data (126+): [{string.Join(", ", flattened.Skip(126).Take(6).Select(f => f.ToString("F3")))}...]");
    }

    [ContextMenu("Force Send Current Sequence")]
    public void ForceSendCurrentSequence()
    {
        if (frameSequence.Count > 0)
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

    [ContextMenu("Test Different Face Modes")]
    public void TestFaceModes()
    {
        Debug.Log("=== Face Mode Comparison ===");
        Debug.Log($"KeyPointsOnly: ~{faceKeyIndices.Length} points");
        Debug.Log($"ReducedPoints (skip {faceSkipRatio}): ~{468 / faceSkipRatio} points");
        Debug.Log($"FullFace: 468 points (큰 데이터 주의!)");
    }

    [ContextMenu("Show Battle Stats")]
    public void ShowBattleStats()
    {
        Debug.Log($"=== 배틀 모드 상태 ===");
        Debug.Log($"스킬 수집 중: {isCollectingForBattle}");
        Debug.Log($"수집된 프레임: {battleFramesCollected}/{sequenceFrameCount}");
        Debug.Log($"수집 진행률: {GetCollectionProgress() * 100:F1}%");
        Debug.Log($"BattleManager 연결: {(battleManager != null ? "연결됨" : "없음")}");
    }

    [ContextMenu("Open Save Directory")]
    public void OpenSaveDirectory()
    {
        if (!string.IsNullOrEmpty(actualSaveDirectory) && System.IO.Directory.Exists(actualSaveDirectory))
        {
            System.Diagnostics.Process.Start(actualSaveDirectory);
            Debug.Log($"Opening directory: {actualSaveDirectory}");
        }
        else
        {
            Debug.LogWarning("Save directory not found or not set");
        }
    }

    [ContextMenu("Clear Saved Files")]
    public void ClearSavedFiles()
    {
        if (!string.IsNullOrEmpty(actualSaveDirectory) && System.IO.Directory.Exists(actualSaveDirectory))
        {
            var jsonFiles = System.IO.Directory.GetFiles(actualSaveDirectory, "*.json");
            foreach (var file in jsonFiles)
            {
                System.IO.File.Delete(file);
            }
            Debug.Log($"Cleared {jsonFiles.Length} JSON files from {actualSaveDirectory}");
        }
    }

    void OnDestroy()
    {
        CancelInvoke();
    }
}