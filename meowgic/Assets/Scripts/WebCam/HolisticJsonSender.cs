using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Mediapipe.Unity.Sample.Holistic;
using Mediapipe.Unity;

public class OptimizedHolisticExtractor : MonoBehaviour
{
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
        public List<SimpleLandmark> faceKey = new List<SimpleLandmark>(); // 주요 얼굴 포인트만
        public List<SimpleLandmark> leftHand = new List<SimpleLandmark>();
        public List<SimpleLandmark> rightHand = new List<SimpleLandmark>();
    }

    [Header("Target")]
    public HolisticTrackingSolution holisticSolution;

    [Header("Settings")]
    public bool enableLogging = true;
    public bool sendToServer = false;
    public string serverUrl = "http://127.0.0.1:5000/pose";
    public int frameSkip = 5;

    [Header("Data Collection")]
    public bool collectPose = true;
    public bool collectFace = true;
    public bool collectHands = false;

    [Header("Face Optimization")]
    public FaceReductionMode faceMode = FaceReductionMode.KeyPointsOnly;
    public int faceSkipRatio = 5; // 5개마다 1개씩만 선택

    [Header("Test Mode")]
    public bool useDummyData = true;

    public enum FaceReductionMode
    {
        Disabled,           // Face 수집 안함
        KeyPointsOnly,      // 주요 포인트만 (약 20개)
        ReducedPoints,      // 일정 간격으로 건너뛰기 (약 94개)
        FullFace           // 전체 468개 (주의!)
    }

    private int frameCount = 0;

    // MediaPipe Face Landmark의 주요 포인트 인덱스 (468개 중 핵심만)
    private readonly int[] faceKeyIndices = {
        // 얼굴 윤곽 (Jaw line)
        0, 17, 18, 200, 199, 175, 
        // 눈썹 (Eyebrows)
        46, 53, 52, 65, 55, 70,
        276, 283, 282, 295, 285, 300,
        // 눈 (Eyes)
        33, 7, 163, 144, 145, 153, 154, 155, 133, 173, 157, 158, 159, 160, 161, 246,
        362, 398, 384, 385, 386, 387, 388, 466, 263, 249, 390, 373, 374, 380, 381, 382,
        // 코 (Nose)
        1, 2, 5, 4, 6, 168, 8, 9, 10, 151, 195, 197, 196, 3, 51, 48, 115, 131, 134, 102, 49, 220, 305, 281, 360, 279,
        // 입 (Mouth)
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

        if (useDummyData)
        {
            Debug.Log("Starting with optimized dummy data");
            InvokeRepeating(nameof(GenerateOptimizedTestData), 0.2f, 0.2f);
        }
        else
        {
            Debug.Log("Starting optimized extraction method");
            StartCoroutine(SafeExtractionCoroutine());
        }
    }

    private IEnumerator SafeExtractionCoroutine()
    {
        yield return new WaitForSeconds(3f);
        Debug.Log("Starting optimized data extraction...");
        InvokeRepeating(nameof(SafeExtractData), 0.2f, 0.2f);
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

        // 손 데이터 (각각 21개)
        if (collectHands)
        {
            frame.leftHand = GenerateDummyHand();
            frame.rightHand = GenerateDummyHand();
        }

        ProcessOptimizedFrame(frame);
    }

    private List<SimpleLandmark> GenerateOptimizedFaceData()
    {
        var landmarks = new List<SimpleLandmark>();

        switch (faceMode)
        {
            case FaceReductionMode.KeyPointsOnly:
                // 주요 포인트만 (약 20-30개)
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
                // 일정 간격으로 건너뛰기 (468개 → 약 94개)
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
                // 전체 468개 (주의: 매우 큰 데이터!)
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
            InvokeRepeating(nameof(GenerateOptimizedTestData), 0.2f, 0.2f);
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

            if (collectHands)
            {
                frame.leftHand = GenerateDummyHand();
                frame.rightHand = GenerateDummyHand();
            }
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
            // 실제 Face 데이터 추출 시도
            var fullFaceData = FindFaceDataSafely();

            if (fullFaceData != null && fullFaceData.Count > 0)
            {
                // 모드에 따라 최적화
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
                // 주요 인덱스만 선택
                foreach (int index in faceKeyIndices)
                {
                    if (index < fullFaceData.Count)
                    {
                        optimized.Add(fullFaceData[index]);
                    }
                }
                break;

            case FaceReductionMode.ReducedPoints:
                // 일정 간격으로 건너뛰기
                for (int i = 0; i < fullFaceData.Count; i += faceSkipRatio)
                {
                    optimized.Add(fullFaceData[i]);
                }
                break;

            case FaceReductionMode.FullFace:
                // 전체 데이터 (주의!)
                optimized = fullFaceData;
                break;
        }

        return optimized;
    }

    private List<SimpleLandmark> FindFaceDataSafely()
    {
        // 실제 Face annotation controller에서 데이터 추출
        // (실제 구현은 FindPoseDataSafely와 비슷)
        return new List<SimpleLandmark>();
    }

    private List<SimpleLandmark> FindPoseDataSafely()
    {
        // 기존 코드와 동일
        return new List<SimpleLandmark>();
    }

    // 더미 데이터 생성 메서드들
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
        string json = JsonUtility.ToJson(frame, true);

        if (enableLogging)
        {
            var totalLandmarks = frame.pose.Count + frame.faceKey.Count + frame.leftHand.Count + frame.rightHand.Count;
            Debug.Log($"Frame {frame.frame}: Total {totalLandmarks} landmarks " +
                     $"(Pose:{frame.pose.Count}, Face:{frame.faceKey.Count}, " +
                     $"LHand:{frame.leftHand.Count}, RHand:{frame.rightHand.Count})");

            if (frameCount % 50 == 0)
            {
                Debug.Log($"JSON size: {json.Length} characters");
                Debug.Log($"Face mode: {faceMode} ({frame.faceKey.Count} points)");
                Debug.Log($"JSON Preview:\n{json.Substring(0, Math.Min(300, json.Length))}...");
            }
        }

        if (sendToServer)
        {
            StartCoroutine(SendFrame(json));
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
        InvokeRepeating(nameof(GenerateOptimizedTestData), 0.2f, 0.2f);
        Debug.Log("Switched to dummy data generation");
    }

    [ContextMenu("Test Different Face Modes")]
    public void TestFaceModes()
    {
        Debug.Log("=== Face Mode Comparison ===");
        Debug.Log($"KeyPointsOnly: ~{faceKeyIndices.Length} points");
        Debug.Log($"ReducedPoints (skip {faceSkipRatio}): ~{468 / faceSkipRatio} points");
        Debug.Log($"FullFace: 468 points (큰 데이터 주의!)");
    }

    [ContextMenu("Show Stats")]
    public void ShowStats()
    {
        Debug.Log($"=== Optimized Stats ===");
        Debug.Log($"Frame count: {frameCount}");
        Debug.Log($"Face mode: {faceMode}");
        Debug.Log($"Using dummy data: {useDummyData}");
        Debug.Log($"Collect settings - Pose: {collectPose}, Face: {collectFace}, Hands: {collectHands}");
    }

    void OnDestroy()
    {
        CancelInvoke();
    }
}