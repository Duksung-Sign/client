using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Mediapipe.Unity.Sample.Holistic;
using Mediapipe.Unity;
using Mediapipe;

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

    [Header("Data Validation")]
    [Tooltip("실제 데이터가 연속으로 몇 프레임 필요한지")]
    public int requiredConsecutiveFrames = 5;
    [Tooltip("수집 시작 전 대기 시간 (초)")]
    public float collectionStartDelay = 2f;
    [Tooltip("MediaPipe 안정화 대기 시간 (초)")]
    public float mediapipeStabilizationTime = 5f;
    [Tooltip("최소 손 visibility 임계값")]
    public float minHandVisibility = 0f;

    [Header("Data Collection")]
    public bool collectPose = true;
    public bool collectFace = true;
    public bool collectHands = true;

    [Header("Face Optimization")]
    public FaceReductionMode faceMode = FaceReductionMode.KeyPointsOnly;
    public int faceSkipRatio = 5;

    [Header("Pose Optimization")]
    public PoseReductionMode poseMode = PoseReductionMode.KeyPointsOnly;
    public int poseSkipRatio = 3;

    [Header("Test Mode")]
    public bool useDummyData = false;
    [Tooltip("더미 데이터 사용 강제 여부")]
    public bool forceDummyData = false;

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

    public enum PoseReductionMode
    {
        Disabled,
        KeyPointsOnly,
        ReducedPoints,
        FullPose
    }

    // === 새로운 안정성 관련 변수들 ===
    private int consecutiveRealDataFrames = 0;
    private bool isMediaPipeStable = false;
    private float mediapipeStartTime;
    private Dictionary<string, int> dataQualityStats = new Dictionary<string, int>();

    private int frameCount = 0;
    private string actualSaveDirectory;
    private List<List<float>> frameSequence = new List<List<float>>();

    // *** 크래시 방지용 추가 변수들 ***
    private bool isInitialized = false;
    private bool isDestroying = false;
    private readonly object lockObject = new object(); // 스레드 안전성

    private bool gestureHandled = false;

    // MediaPipe 랜드마크 데이터 캐싱용 변수들 (현재는 Current* 프로퍼티 직접 사용)
    private readonly object landmarkLock = new object();

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

    // MediaPipe Pose Landmark의 주요 포인트 인덱스 (수어에 중요한 부분)
    private readonly int[] poseKeyIndices = {
        11, 12,  // 어깨
        13, 14,  // 팔꿈치
        15, 16,  // 손목
        23, 24,  // 엉덩이
    };

    void Start()
    {
        try
        {
            if (isDestroying) return;

            Debug.Log("HolisticJsonSender 초기화 시작");

            // 통계 초기화
            InitializeDataQualityStats();

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
                Debug.Log($"Face mode: {faceMode}, Pose mode: {poseMode}");

                SetupMediaPipeEventHandlers();
                mediapipeStartTime = Time.time;
            }
            else
            {
                Debug.LogWarning("HolisticTrackingSolution not found! 더미 데이터 모드로 전환");
                useDummyData = true;
                forceDummyData = true;
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

    private void InitializeDataQualityStats()
    {
        dataQualityStats["realData"] = 0;
        dataQualityStats["dummyData"] = 0;
        dataQualityStats["lowQuality"] = 0;
    }

    private void SetupMediaPipeEventHandlers()
    {
        Debug.Log("MediaPipe 연동 준비 완료 - Current 프로퍼티 사용");
        // MediaPipe 안정화를 위한 코루틴 시작
        StartCoroutine(WaitForMediaPipeStabilization());
    }

    private IEnumerator WaitForMediaPipeStabilization()
    {
        Debug.Log($"MediaPipe 안정화 대기 중... ({mediapipeStabilizationTime}초)");

        yield return new WaitForSeconds(mediapipeStabilizationTime);

        // 실제 데이터 체크
        if (!forceDummyData && CheckMediaPipeDataAvailability())
        {
            isMediaPipeStable = true;
            useDummyData = false;
            Debug.Log("MediaPipe 안정화 완료 - 실제 데이터 사용 가능");
        }
        else
        {
            Debug.LogWarning("MediaPipe 데이터 불안정 - 더미 데이터 모드 유지");
            useDummyData = true;
        }
    }

    private bool CheckMediaPipeDataAvailability()
    {
        try
        {
            bool hasLeftHand = holisticSolution?.CurrentLeftHandLandmarks?.Landmark != null
                            && holisticSolution.CurrentLeftHandLandmarks.Landmark.Count > 0;
            bool hasRightHand = holisticSolution?.CurrentRightHandLandmarks?.Landmark != null
                             && holisticSolution.CurrentRightHandLandmarks.Landmark.Count > 0;

            return hasLeftHand || hasRightHand;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"MediaPipe 데이터 가용성 체크 실패: {e.Message}");
            return false;
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
            if (useDummyData || forceDummyData)
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

            // 최적화된 포즈 데이터
            if (collectPose && poseMode != PoseReductionMode.Disabled)
            {
                frame.pose = GenerateOptimizedPoseData();
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

    // === 개선된 데이터 추출 메서드 ===
    private SimpleFrame ExtractOptimizedDataSafely()
    {
        try
        {
            var frame = new SimpleFrame
            {
                frame = frameCount,
                time = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            };

            // 1) MediaPipe 로부터 포즈도 가져오기
            var poseData = FindPoseDataSafely();
            bool poseValid = poseData != null && poseData.Count > 5;     // 포인트 5개 이상이면 유효

            // 2) 손 데이터 추출
            var leftHandData = FindLeftHandDataSafely();
            var rightHandData = FindRightHandDataSafely();

            bool leftHandValid = IsValidHandData(leftHandData);
            bool rightHandValid = IsValidHandData(rightHandData);

            // 3) 손 OR 포즈 중 하나라도 있으면 실제 데이터로 인정
            bool hasValidData = leftHandValid || rightHandValid || poseValid;

            // 4) 품질 판정
            DataQuality dataQuality = hasValidData
                ? (leftHandValid && rightHandValid ? DataQuality.High : DataQuality.Medium)
                : DataQuality.Low;
            consecutiveRealDataFrames = hasValidData
                ? consecutiveRealDataFrames + 1
                : 0;

            // 5) 실제 데이터 또는 더미 대입
            frame.leftHand = leftHandValid ? leftHandData : GenerateDummyHand();
            frame.rightHand = rightHandValid ? rightHandData : GenerateDummyHand();
            // 포즈는 dummy 없이 무조건 넣어두기 (fallback)
            frame.pose = poseData ?? new List<SimpleLandmark>();

            // 6) 통계 갱신 & 로깅
            UpdateDataQualityStats(dataQuality);
            if (enableLogging && frameCount % 50 == 0)
            {
                Debug.Log($"[데이터 품질:{dataQuality}] Hands ok:{leftHandValid | rightHandValid}, Pose ok:{poseValid}");
                LogDataQualityStats();
            }

            return frame;
        }
        catch (Exception e)
        {
            Debug.LogError($"데이터 추출 오류: {e.Message}");
            return null;
        }
    }


    private enum DataQuality
    {
        Low,    // 더미 데이터만
        Medium, // 한쪽 손만 실제 데이터
        High    // 양손 모두 실제 데이터
    }

    private bool IsValidHandData(List<SimpleLandmark> handData)
    {
        return handData.Count >= 10;
    }

    private void UpdateDataQualityStats(DataQuality quality)
    {
        switch (quality)
        {
            case DataQuality.High:
                dataQualityStats["realData"]++;
                break;
            case DataQuality.Medium:
                dataQualityStats["realData"]++;
                break;
            case DataQuality.Low:
                dataQualityStats["dummyData"]++;
                break;
        }
    }

    private void LogDataQualityStats()
    {
        int total = dataQualityStats["realData"] + dataQualityStats["dummyData"];
        if (total > 0)
        {
            float realDataPercentage = (float)dataQualityStats["realData"] / total * 100f;
            Debug.Log($"[통계] 실제 데이터: {realDataPercentage:F1}%, 더미 데이터: {100f - realDataPercentage:F1}%");
        }
    }

    // === 개선된 배틀 수집 시작 메서드 ===
    public void StartGestureCollection()
    {
        if (isDestroying || !isInitialized)
        {
            Debug.LogWarning("HolisticJsonSender가 초기화되지 않았거나 종료 중입니다");
            return;
        }

        // 데이터 품질 체크
        if (!forceDummyData && !useDummyData && consecutiveRealDataFrames < requiredConsecutiveFrames)
        {
            Debug.LogWarning($"실제 데이터가 불안정합니다. 연속 실제 데이터: {consecutiveRealDataFrames}/{requiredConsecutiveFrames}");
            StartCoroutine(WaitForStableDataThenCollect());
            return;
        }

        StartCoroutine(DelayedStartCollection());
    }

    private IEnumerator DelayedStartCollection()
    {
        Debug.Log($"스킬 버튼 누름 → {collectionStartDelay:F1}초 후에 수집 시작");
        yield return new WaitForSeconds(collectionStartDelay);
        StartCollectionImmediate();
    }

    private IEnumerator WaitForStableDataThenCollect()
    {
        Debug.Log($"안정적인 데이터 대기 중... (목표: {requiredConsecutiveFrames}프레임)");

        float waitTime = 0f;
        const float maxWaitTime = 5f; // 최대 5초 대기

        while (waitTime < maxWaitTime && consecutiveRealDataFrames < requiredConsecutiveFrames)
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        if (consecutiveRealDataFrames >= requiredConsecutiveFrames)
        {
            Debug.Log("안정적인 데이터 확보 완료!");
            StartCollectionImmediate();
        }
        else
        {
            Debug.LogWarning("안정적인 데이터 확보 실패 - 강제로 수집 시작");
            StartCollectionImmediate();
        }
    }

    private void StartCollectionImmediate()
    {
        lock (lockObject)
        {
            ResetBattleCollection();
            frameSequence = new List<List<float>>();
            battleFramesCollected = 0;
            isCollectingForBattle = true;

            // 새 턴이 시작되면 응답 처리 플래그도 리셋
            gestureHandled = false;

            Debug.Log($"=== 스킬 사용! {sequenceFrameCount}프레임 수집 시작 === (데이터 품질: 연속 {consecutiveRealDataFrames}프레임)");
        }
    }

    private List<SimpleLandmark> FindOptimizedPoseDataSafely()
    {
        try
        {
            var fullPoseData = FindPoseDataSafely();
            return fullPoseData != null && fullPoseData.Count > 0
                ? OptimizePoseData(fullPoseData)
                : GenerateOptimizedPoseData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"포즈 데이터 추출 실패: {e.Message}");
            return GenerateOptimizedPoseData();
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

    private List<SimpleLandmark> OptimizePoseData(List<SimpleLandmark> fullPoseData)
    {
        var optimized = new List<SimpleLandmark>();
        try
        {
            switch (poseMode)
            {
                case PoseReductionMode.KeyPointsOnly:
                    foreach (int index in poseKeyIndices)
                    {
                        if (index < fullPoseData.Count)
                        {
                            optimized.Add(fullPoseData[index]);
                        }
                    }
                    break;
                case PoseReductionMode.ReducedPoints:
                    for (int i = 0; i < fullPoseData.Count; i += poseSkipRatio)
                    {
                        optimized.Add(fullPoseData[i]);
                    }
                    break;
                case PoseReductionMode.FullPose:
                    optimized = new List<SimpleLandmark>(fullPoseData);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"포즈 데이터 최적화 오류: {e.Message}");
        }
        return optimized;
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
        var faceData = new List<SimpleLandmark>();
        try
        {
            var landmarks = holisticSolution?.CurrentFaceLandmarks?.Landmark;
            if (landmarks != null)
            {
                foreach (var landmark in landmarks)
                {
                    faceData.Add(new SimpleLandmark
                    {
                        x = landmark.X,
                        y = landmark.Y,
                        z = landmark.Z,
                        visibility = landmark.Visibility
                    });
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"얼굴 데이터 추출 오류: {e.Message}");
        }
        return faceData;
    }

    private List<SimpleLandmark> FindPoseDataSafely()
    {
        var poseData = new List<SimpleLandmark>();
        try
        {
            var landmarks = holisticSolution?.CurrentPoseLandmarks?.Landmark;
            if (landmarks != null)
            {
                foreach (var landmark in landmarks)
                {
                    poseData.Add(new SimpleLandmark
                    {
                        x = landmark.X,
                        y = landmark.Y,
                        z = landmark.Z,
                        visibility = landmark.Visibility
                    });
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"포즈 데이터 추출 오류: {e.Message}");
        }

        return poseData;
    }

    private List<SimpleLandmark> GenerateOptimizedPoseData()
    {
        var landmarks = new List<SimpleLandmark>();

        try
        {
            int pointCount = poseMode switch
            {
                PoseReductionMode.KeyPointsOnly => poseKeyIndices.Length,
                PoseReductionMode.ReducedPoints => 33 / poseSkipRatio,
                PoseReductionMode.FullPose => 33,
                _ => 0
            };

            for (int i = 0; i < pointCount; i++)
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

    private List<SimpleLandmark> FindLeftHandDataSafely()
    {
        var handData = new List<SimpleLandmark>();
        try
        {
            var landmarks = holisticSolution?.CurrentLeftHandLandmarks?.Landmark;
            if (landmarks != null)
            {
                foreach (var landmark in landmarks)
                {
                    handData.Add(new SimpleLandmark
                    {
                        x = landmark.X,
                        y = landmark.Y,
                        z = landmark.Z,
                        visibility = landmark.Visibility
                    });
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"왼손 데이터 추출 오류: {e.Message}");
        }

        return handData;
    }

    private List<SimpleLandmark> FindRightHandDataSafely()
    {
        var handData = new List<SimpleLandmark>();
        try
        {
            var landmarks = holisticSolution?.CurrentRightHandLandmarks?.Landmark;
            if (landmarks != null)
            {
                foreach (var landmark in landmarks)
                {
                    handData.Add(new SimpleLandmark
                    {
                        x = landmark.X,
                        y = landmark.Y,
                        z = landmark.Z,
                        visibility = landmark.Visibility
                    });
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"오른손 데이터 추출 오류: {e.Message}");
        }

        return handData;
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
            lock (lockObject)
            {
                if (!isCollectingForBattle) return;

                // 초기 5프레임만 손 유효 여부 로그
                bool leftValid = frame.leftHand.Count >= 5;
                bool rightValid = frame.rightHand.Count >= 5;
                if (enableLogging && battleFramesCollected < 5)
                {
                    Debug.Log($"[배틀] 프레임 {battleFramesCollected + 1}: LeftValid={leftValid}, RightValid={rightValid}");
                }

                var flattenedFrame = FlattenFrameData(frame);
                if (flattenedFrame == null || flattenedFrame.Count != 148)
                {
                    Debug.LogWarning($"잘못된 프레임 데이터: {flattenedFrame?.Count ?? 0} elements (예상: 148)");
                    return;
                }

                frameSequence.Add(flattenedFrame);
                battleFramesCollected++;

                if (battleFramesCollected >= sequenceFrameCount)
                {
                    Debug.Log($"{sequenceFrameCount}프레임 수집 완료! 서버로 전송합니다.");
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

    // === 새로운 메서드들 ===
    public bool IsMediaPipeStable()
    {
        return isMediaPipeStable;
    }

    public int GetConsecutiveRealDataFrames()
    {
        return consecutiveRealDataFrames;
    }

    public bool IsMediaPipeReady()
    {
        return isMediaPipeStable && consecutiveRealDataFrames >= requiredConsecutiveFrames;
    }

    private List<float> FlattenFrameData(SimpleFrame frame)
    {
        try
        {
            var flattened = new List<float>();

            AddHandData(flattened, frame.leftHand, "Left");
            AddHandData(flattened, frame.rightHand, "Right");
            AddPoseKeypoints(flattened, frame.pose);

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
            int[] poseIndices = { 11, 12, 13, 14, 15, 16, 23, 24 };

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

        if (saveToTextFile)
            SaveSequenceToFile(json);

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

        yield return request.SendWebRequest();

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
            if (gestureHandled) return;
            gestureHandled = true;

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

    [ContextMenu("Switch to Real Data")]
    public void SwitchToRealData()
    {
        if (isDestroying) return;

        try
        {
            useDummyData = false;
            forceDummyData = false;
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

    [ContextMenu("Check MediaPipe Status")]
    public void CheckMediaPipeStatus()
    {
        Debug.Log("=== MediaPipe 솔루션 상태 체크 ===");

        if (holisticSolution == null)
        {
            Debug.LogWarning("HolisticTrackingSolution이 null입니다");
            return;
        }

        if (holisticSolution.PublicGraphRunner == null)
        {
            Debug.LogWarning("GraphRunner가 null입니다 - MediaPipe가 아직 초기화되지 않았습니다");
            return;
        }

        bool hasFaceData = holisticSolution.CurrentFaceLandmarks?.Landmark != null && holisticSolution.CurrentFaceLandmarks.Landmark.Count > 0;
        bool hasPoseData = holisticSolution.CurrentPoseLandmarks?.Landmark != null && holisticSolution.CurrentPoseLandmarks.Landmark.Count > 0;
        bool hasLeftHandData = holisticSolution.CurrentLeftHandLandmarks?.Landmark != null && holisticSolution.CurrentLeftHandLandmarks.Landmark.Count > 0;
        bool hasRightHandData = holisticSolution.CurrentRightHandLandmarks?.Landmark != null && holisticSolution.CurrentRightHandLandmarks.Landmark.Count > 0;

        Debug.Log($"현재 랜드마크 데이터 상태:");
        Debug.Log($"- Face: {(hasFaceData ? holisticSolution.CurrentFaceLandmarks.Landmark.Count + "개" : "없음")}");
        Debug.Log($"- Pose: {(hasPoseData ? holisticSolution.CurrentPoseLandmarks.Landmark.Count + "개" : "없음")}");
        Debug.Log($"- Left Hand: {(hasLeftHandData ? holisticSolution.CurrentLeftHandLandmarks.Landmark.Count + "개" : "없음")}");
        Debug.Log($"- Right Hand: {(hasRightHandData ? holisticSolution.CurrentRightHandLandmarks.Landmark.Count + "개" : "없음")}");

        if (!hasFaceData && !hasPoseData && !hasLeftHandData && !hasRightHandData)
        {
            Debug.LogWarning("모든 랜드마크 데이터가 비어있습니다. MediaPipe가 아직 시작되지 않았거나 카메라 입력이 없을 수 있습니다.");
        }

        Debug.Log($"GraphRunner 상태: {holisticSolution.PublicGraphRunner.GetType().Name}");
    }

    [ContextMenu("Test Real Data Extraction")]
    public void TestRealDataExtraction()
    {
        Debug.Log("=== 실제 데이터 추출 테스트 ===");

        var faceData = FindFaceDataSafely();
        var poseData = FindPoseDataSafely();
        var leftHandData = FindLeftHandDataSafely();
        var rightHandData = FindRightHandDataSafely();

        var lh = FindLeftHandDataSafely();
        if (lh.Count > 0)
        {
            Debug.Log($"LeftHand[0] vis={lh[0].visibility:F3}  x={lh[0].x:F3}, y={lh[0].y:F3}");
        }

        Debug.Log($"추출 결과:");
        Debug.Log($"- Face: {faceData.Count}개 포인트");
        Debug.Log($"- Pose: {poseData.Count}개 포인트");
        Debug.Log($"- Left Hand: {leftHandData.Count}개 포인트");
        Debug.Log($"- Right Hand: {rightHandData.Count}개 포인트");

        if (faceData.Count == 0 && poseData.Count == 0 && leftHandData.Count == 0 && rightHandData.Count == 0)
        {
            Debug.LogWarning("모든 실제 데이터가 0개입니다. MediaPipe가 아직 초기화되지 않았거나 카메라 입력이 없을 수 있습니다.");
        }
        else
        {
            if (faceData.Count > 0)
            {
                var firstFace = faceData[0];
                Debug.Log($"Face 샘플: x={firstFace.x:F3}, y={firstFace.y:F3}, z={firstFace.z:F3}, v={firstFace.visibility:F3}");
            }
            if (poseData.Count > 0)
            {
                var firstPose = poseData[0];
                Debug.Log($"Pose 샘플: x={firstPose.x:F3}, y={firstPose.y:F3}, z={firstPose.z:F3}, v={firstPose.visibility:F3}");
            }
        }
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
        Debug.Log($"Face mode: {faceMode}, Pose mode: {poseMode}");
        Debug.Log($"더미 데이터 모드: {useDummyData}");
    }

    [ContextMenu("Reset Data Quality Stats")]
    public void ResetDataQualityStats()
    {
        InitializeDataQualityStats();
        consecutiveRealDataFrames = 0;
        Debug.Log("데이터 품질 통계 리셋 완료");
    }

    [ContextMenu("Force Real Data Collection")]
    public void ForceRealDataCollection()
    {
        useDummyData = false;
        forceDummyData = false;
        consecutiveRealDataFrames = requiredConsecutiveFrames;
        Debug.Log("실제 데이터 수집 강제 활성화");
    }

    [ContextMenu("Show Data Quality Report")]
    public void ShowDataQualityReport()
    {
        Debug.Log("=== 데이터 품질 리포트 ===");
        Debug.Log($"MediaPipe 안정화: {isMediaPipeStable}");
        Debug.Log($"연속 실제 데이터 프레임: {consecutiveRealDataFrames}");
        Debug.Log($"필요한 연속 프레임: {requiredConsecutiveFrames}");
        Debug.Log($"더미 데이터 모드: {useDummyData}");
        Debug.Log($"강제 더미 데이터: {forceDummyData}");
        LogDataQualityStats();
    }

    void OnDestroy()
    {
        isDestroying = true;

        try
        {
            Debug.Log("HolisticJsonSender 정리 시작");

            StopAllCoroutines();
            CancelInvoke();

            lock (lockObject)
            {
                isCollectingForBattle = false;
                battleFramesCollected = 0;

                frameSequence?.Clear();
                frameSequence = null;
            }

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