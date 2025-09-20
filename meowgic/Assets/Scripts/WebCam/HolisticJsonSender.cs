using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Mediapipe.Unity.Sample.Holistic;
using Mediapipe.Unity;
using Mediapipe;
using System.IO;
using Newtonsoft.Json;

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
        public float thx;
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

    [Header("Target")]
    public HolisticTrackingSolution holisticSolution;

    [Header("Settings")]
    public bool enableLogging = true;
    public bool sendToServer = false;
    public string serverUrl = "http://127.0.0.1:5000/pose";
    public int frameSkip = 1;

    [Header("UI Integration")]
    public bool enableUIFeedback = true;

    public event System.Action<string> OnGestureRecognitionComplete;

    [Header("Data Validation")]
    [Tooltip("실제 데이터가 연속으로 몇 프레임 필요한지")]
    public int requiredConsecutiveFrames = 5;
    [Tooltip("수집 시작 전 대기 시간 (초)")]
    public float collectionStartDelay = 2f;
    [Tooltip("MediaPipe 안정화 대기 시간 (초)")]
    public float mediapipeStabilizationTime = 5f;
    [Tooltip("최소 손 visibility 임계값")]
    public float minHandVisibility = 0.7f;
    [Tooltip("보간을 위한 최대 프레임 간격")]
    public int maxInterpolationFrames = 3;

    [Header("Data Collection")]
    public bool collectPose = true;
    public bool collectFace = true;
    public bool collectHands = true;

    [Header("Face Optimization")]
    public FaceReductionMode faceMode = FaceReductionMode.KeyPointsOnly;

    [Header("Pose Optimization")]
    public PoseReductionMode poseMode = PoseReductionMode.KeyPointsOnly;

    [Header("Sequence Mode (Sign Language)")]
    public bool useSequenceMode = true;
    public int sequenceFrameCount = 30;

    [Header("Debug")]
    public bool saveToTextFile = true;
    public string saveDirectory = "";

    public enum FaceReductionMode
    {
        Disabled,
        KeyPointsOnly,
        FullFace
    }

    public enum PoseReductionMode
    {
        Disabled,
        KeyPointsOnly,
        FullPose
    }

    private int consecutiveRealDataFrames = 0;
    private bool isMediaPipeStable = false;
    private float mediapipeStartTime;
    private Dictionary<string, int> dataQualityStats = new Dictionary<string, int>();

    private int frameCount = 0;
    private string actualSaveDirectory;
    private List<List<float>> frameSequence = new List<List<float>>();

    private bool isInitialized = false;
    private bool isDestroying = false;
    private readonly object lockObject = new object();
    private bool gestureHandled = false;

    private List<SimpleLandmark> lastValidLeftHand = null;
    private List<SimpleLandmark> lastValidRightHand = null;
    private List<SimpleLandmark> lastValidPose = null;
    private List<SimpleLandmark> lastValidFace = null;
    private int framesSinceLastValidData = 0;

    private SimpleFrame previousSimpleFrame = null;

    private readonly int[] faceKeyIndices = {
        1, 33, 263, 61,
        0, 13, 14, 17, 70, 107, 336, 296, 133, 362, 2, 98, 327, 152, 234, 454,
        291
    };


    private readonly int[] poseKeyIndices = {
        11, 12, 13, 14, 15, 16, 23, 24,
        24
    };

    void Start()
    {
        try
        {
            if (isDestroying) return;

            Debug.Log("HolisticJsonSender 초기화 시작");

            InitializeDataQualityStats();
            CancelInvoke();
            StopAllCoroutines();

            if (holisticSolution == null)
            {
                holisticSolution = FindObjectOfType<HolisticTrackingSolution>();
            }

            if (holisticSolution != null)
            {
                Debug.Log($"Found HolisticTrackingSolution: {holisticSolution.name}");
                SetupMediaPipeEventHandlers();
                mediapipeStartTime = Time.time;
            }
            else
            {
                Debug.LogError("HolisticTrackingSolution not found! Cannot proceed without MediaPipe.");
                enabled = false;
                return;
            }

            SetupSaveDirectory();
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
        dataQualityStats["validFrames"] = 0;
        dataQualityStats["interpolatedFrames"] = 0;
        dataQualityStats["skippedFrames"] = 0;
    }

    private void SetupMediaPipeEventHandlers()
    {
        Debug.Log("MediaPipe 연동 준비 완료");
        StartCoroutine(WaitForMediaPipeStabilization());
    }

    private IEnumerator WaitForMediaPipeStabilization()
    {
        Debug.Log($"MediaPipe 안정화 대기 중... ({mediapipeStabilizationTime}초)");
        yield return new WaitForSeconds(mediapipeStabilizationTime);

        if (CheckMediaPipeDataAvailability())
        {
            isMediaPipeStable = true;
            Debug.Log("MediaPipe 안정화 완료 - 실제 데이터 사용 가능");
        }
        else
        {
            Debug.LogError("MediaPipe 데이터를 사용할 수 없습니다. 다시 시도해주세요.");
            enabled = false;
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
            Debug.Log("실제 데이터 추출 모드 시작");
            StartCoroutine(DelayedDataCollection());
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
            Debug.LogError($"데이터 추출 실패: {e.Message}");
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

            // 현재 프레임 데이터 추출
            var poseData = FindPoseDataSafely();
            var leftHandData = FindLeftHandDataSafely();
            var rightHandData = FindRightHandDataSafely();
            var faceData = FindFaceDataSafely();

            // 데이터 유효성 검사
            bool poseValid = IsValidPoseData(poseData);
            bool leftHandValid = IsValidHandData(leftHandData);
            bool rightHandValid = IsValidHandData(rightHandData);
            bool faceValid = IsValidFaceData(faceData);

            // 유효한 데이터 캐싱
            if (poseValid) lastValidPose = new List<SimpleLandmark>(poseData);
            if (leftHandValid) lastValidLeftHand = new List<SimpleLandmark>(leftHandData);
            if (rightHandValid) lastValidRightHand = new List<SimpleLandmark>(rightHandData);
            if (faceValid) lastValidFace = new List<SimpleLandmark>(faceData);

            // 데이터 할당
            frame.pose = GetBestAvailablePoseData(poseData, poseValid);
            frame.leftHand = GetBestAvailableHandData(leftHandData, leftHandValid, true);
            frame.rightHand = GetBestAvailableHandData(rightHandData, rightHandValid, false);
            frame.faceKey = GetBestAvailableFaceData(faceData, faceValid);

            // 통계 업데이트
            bool hasValidData = leftHandValid || rightHandValid || poseValid || faceValid;

            if (hasValidData)
            {
                consecutiveRealDataFrames++;
                framesSinceLastValidData = 0;
                dataQualityStats["validFrames"]++;
            }
            else if (framesSinceLastValidData <= maxInterpolationFrames)
            {
                framesSinceLastValidData++;
                dataQualityStats["interpolatedFrames"]++;
            }
            else
            {
                // 너무 오래 유효한 데이터가 없으면 프레임 스킵
                dataQualityStats["skippedFrames"]++;
                return null;
            }

            if (enableLogging && frameCount % 50 == 0)
            {
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

    private bool IsValidHandData(List<SimpleLandmark> handData)
    {
        if (handData == null || handData.Count < 21) return false;

        float avgVisibility = handData.Average(l => l.visibility);
        return avgVisibility >= minHandVisibility;
    }

    private bool IsValidPoseData(List<SimpleLandmark> poseData)
    {
        if (poseData == null || poseData.Count < 25) return false;

        float avgVisibility = 0f;
        foreach (int idx in poseKeyIndices.Take(8))
        {
            if (idx < poseData.Count)
                avgVisibility += poseData[idx].visibility;
        }
        avgVisibility /= 8;

        return avgVisibility >= 0.5f;
    }

    private bool IsValidFaceData(List<SimpleLandmark> faceData)
    {
        return faceData != null && faceData.Count >= 468;
    }

    private List<SimpleLandmark> GetBestAvailableHandData(List<SimpleLandmark> currentData, bool isValid, bool isLeft)
    {
        if (isValid) return currentData;

        var lastValid = isLeft ? lastValidLeftHand : lastValidRightHand;
        if (lastValid != null && framesSinceLastValidData <= maxInterpolationFrames)
        {
            return ApplySmallNoise(lastValid);
        }

        return null;
    }

    private List<SimpleLandmark> GetBestAvailablePoseData(List<SimpleLandmark> currentData, bool isValid)
    {
        if (isValid) return OptimizePoseData(currentData);

        if (lastValidPose != null && framesSinceLastValidData <= maxInterpolationFrames)
        {
            return ApplySmallNoise(lastValidPose);
        }

        return null;
    }

    private List<SimpleLandmark> GetBestAvailableFaceData(List<SimpleLandmark> currentData, bool isValid)
    {
        if (isValid) return OptimizeFaceData(currentData);

        if (lastValidFace != null && framesSinceLastValidData <= maxInterpolationFrames)
        {
            return lastValidFace;
        }

        return null;
    }

    private List<SimpleLandmark> ApplySmallNoise(List<SimpleLandmark> landmarks)
    {
        var noisyLandmarks = new List<SimpleLandmark>();
        foreach (var lm in landmarks)
        {
            noisyLandmarks.Add(new SimpleLandmark
            {
                x = lm.x + UnityEngine.Random.Range(-0.001f, 0.001f),
                y = lm.y + UnityEngine.Random.Range(-0.001f, 0.001f),
                z = lm.z + UnityEngine.Random.Range(-0.0005f, 0.0005f),
                visibility = lm.visibility
            });
        }
        return noisyLandmarks;
    }

    private void LogDataQualityStats()
    {
        int total = dataQualityStats["validFrames"] + dataQualityStats["interpolatedFrames"] + dataQualityStats["skippedFrames"];
        if (total > 0)
        {
            float validPercentage = (float)dataQualityStats["validFrames"] / total * 100f;
            float interpolatedPercentage = (float)dataQualityStats["interpolatedFrames"] / total * 100f;
            float skippedPercentage = (float)dataQualityStats["skippedFrames"] / total * 100f;

            Debug.Log($"[통계] 유효: {validPercentage:F1}%, 보간: {interpolatedPercentage:F1}%, 스킵: {skippedPercentage:F1}%");
            Debug.Log($"[품질] 연속 유효 프레임: {consecutiveRealDataFrames}");
        }
    }

    public bool IsMediaPipeStable()
    {
        return isMediaPipeStable;
    }

    public bool IsMediaPipeReady()
    {
        return isMediaPipeStable && consecutiveRealDataFrames >= requiredConsecutiveFrames;
    }

    public void StartGestureCollection()
    {
        if (isDestroying || !isInitialized)
        {
            Debug.LogWarning("HolisticJsonSender가 초기화되지 않았거나 종료 중입니다");
            return;
        }

        if (!IsMediaPipeReady())
        {
            Debug.LogWarning($"MediaPipe가 준비되지 않았습니다. 연속 실제 데이터: {consecutiveRealDataFrames}/{requiredConsecutiveFrames}");
            StartCoroutine(WaitForStableDataThenCollect());
            return;
        }

        StartCoroutine(DelayedStartCollection());
    }

    private IEnumerator WaitForStableDataThenCollect()
    {
        Debug.Log($"안정적인 데이터 대기 중... (목표: {requiredConsecutiveFrames}프레임)");

        float waitTime = 0f;
        const float maxWaitTime = 5f;

        while (waitTime < maxWaitTime && !IsMediaPipeReady())
        {
            yield return new WaitForSeconds(0.1f);
            waitTime += 0.1f;
        }

        if (IsMediaPipeReady())
        {
            Debug.Log("안정적인 데이터 확보 완료!");
            StartCollectionImmediate();
        }
        else
        {
            Debug.LogError("안정적인 데이터를 확보할 수 없습니다. MediaPipe 상태를 확인해주세요.");
        }
    }

    private IEnumerator DelayedStartCollection()
    {
        Debug.Log($"스킬 버튼 누름 → {collectionStartDelay:F1}초 후에 수집 시작");
        yield return new WaitForSeconds(collectionStartDelay);
        StartCollectionImmediate();
    }

    private void StartCollectionImmediate()
    {
        lock (lockObject)
        {
            ResetBattleCollection();
            frameSequence = new List<List<float>>();
            battleFramesCollected = 0;
            isCollectingForBattle = true;
            gestureHandled = false;

            previousSimpleFrame = null;
            Debug.Log($"=== 스킬 사용! {sequenceFrameCount}프레임 수집 시작 ===");
        }
    }

    private List<SimpleLandmark> OptimizePoseData(List<SimpleLandmark> fullPoseData)
    {
        var optimized = new List<SimpleLandmark>();
        try
        {
            if (poseMode == PoseReductionMode.KeyPointsOnly)
            {
                foreach (int index in poseKeyIndices.Take(8))
                {
                    if (index < fullPoseData.Count)
                    {
                        optimized.Add(fullPoseData[index]);
                    }
                }
                if (fullPoseData.Count > 24)
                {
                    var hipCenter = new SimpleLandmark
                    {
                        x = (fullPoseData[23].x + fullPoseData[24].x) / 2f,
                        y = (fullPoseData[23].y + fullPoseData[24].y) / 2f,
                        z = (fullPoseData[23].z + fullPoseData[24].z) / 2f,
                        visibility = (fullPoseData[23].visibility + fullPoseData[24].visibility) / 2f
                    };
                    optimized.Add(hipCenter);
                }
            }
            else
            {
                optimized = new List<SimpleLandmark>(fullPoseData);
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
            if (faceMode == FaceReductionMode.KeyPointsOnly)
            {
                foreach (int index in faceKeyIndices)
                {
                    if (index < fullFaceData.Count)
                    {
                        optimized.Add(fullFaceData[index]);
                    }
                }
            }
            else
            {
                optimized = new List<SimpleLandmark>(fullFaceData);
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

    private void ProcessOptimizedFrame(SimpleFrame frame)
    {
        if (isDestroying || !isInitialized || frame == null) return;

        try
        {
            lock (lockObject)
            {
                if (!isCollectingForBattle) return;

                var flattenedFrame = FlattenFrameDataCoordinatesOnly(frame, previousSimpleFrame);

                if (flattenedFrame == null || flattenedFrame.Count != 244)
                {
                    Debug.LogWarning($"프레임 데이터 생성/차원 오류로 스킵: {flattenedFrame?.Count ?? 0}");
                    return;
                }

                frameSequence.Add(flattenedFrame);
                battleFramesCollected++;

                previousSimpleFrame = frame;

                Debug.Log($"프레임 {battleFramesCollected}/{sequenceFrameCount} 수집됨");

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

    private List<float> FlattenFrameDataCoordinatesOnly(SimpleFrame currentFrame, SimpleFrame prevFrame)
    {
        try
        {
            if (!TryNormalizeUpperBodyData(currentFrame.pose, ref currentFrame.leftHand, ref currentFrame.rightHand, ref currentFrame.faceKey))
            {
                Debug.LogWarning("데이터 정규화 실패. 해당 프레임을 건너뜁니다.");
                return null; 
            }

            var flattened = new List<float>();

            AddAllLandmarkData(flattened, currentFrame.leftHand, 21);
            AddAllLandmarkData(flattened, currentFrame.rightHand, 21);
            AddAllLandmarkData(flattened, currentFrame.pose, 9);
            AddAllLandmarkData(flattened, currentFrame.faceKey, 21);

            AddJointAngles(flattened, currentFrame.pose);

            AddFingerStates(flattened, currentFrame.leftHand, currentFrame.rightHand);

            AddPalmNormals(flattened, currentFrame.leftHand, currentFrame.rightHand);

            AddVelocityData(flattened, currentFrame, prevFrame);

            AddMaskFlags(flattened, currentFrame.leftHand, currentFrame.rightHand, currentFrame.pose, currentFrame.faceKey);

            if (flattened.Count != 244)
            {
                Debug.LogError($"[FATAL] 최종 차원 불일치! 예상: 244, 실제: {flattened.Count}.");
                while (flattened.Count < 244) flattened.Add(0f);
                if (flattened.Count > 244) flattened = flattened.GetRange(0, 244);
            }

            return flattened;
        }
        catch (Exception e)
        {
            Debug.LogError($"[FATAL] 244차원 벡터 생성 중 치명적 오류 발생: {e.Message}\n{e.StackTrace}");
            return new List<float>(new float[244]);
        }
    }

    private bool TryNormalizeUpperBodyData(
        List<SimpleLandmark> pose,
        ref List<SimpleLandmark> leftHand,
        ref List<SimpleLandmark> rightHand,
        ref List<SimpleLandmark> face)
    {
        if (pose == null || pose.Count < 9) return false;

        var leftShoulder = new Vector3(pose[0].x, pose[0].y, pose[0].z);
        var rightShoulder = new Vector3(pose[1].x, pose[1].y, pose[1].z);

        Vector3 origin = (leftShoulder + rightShoulder) / 2f; 
        Vector3 shoulderVec = (rightShoulder - leftShoulder).normalized; 

        Vector3 forward = Vector3.Cross(shoulderVec, Vector3.up).normalized; 
        Vector3 up = Vector3.Cross(forward, shoulderVec).normalized; 

        if (forward == Vector3.zero || up == Vector3.zero) return false;
        Quaternion torsoRotation = Quaternion.LookRotation(forward, up);

        if (leftHand != null) leftHand = TransformLandmarks(leftHand, origin, torsoRotation);
        if (rightHand != null) rightHand = TransformLandmarks(rightHand, origin, torsoRotation);
        if (face != null) face = TransformLandmarks(face, origin, torsoRotation);
        if (pose != null) pose = TransformLandmarks(pose, origin, torsoRotation);

        return true;
    }

    private List<SimpleLandmark> TransformLandmarks(List<SimpleLandmark> original, Vector3 origin, Quaternion rotation)
    {
        var transformed = new List<SimpleLandmark>(original.Count);
        Quaternion inverseRotation = Quaternion.Inverse(rotation);

        foreach (var lm in original)
        {
            Vector3 originalPos = new Vector3(lm.x, lm.y, lm.z);
            Vector3 newPos = inverseRotation * (originalPos - origin);

            transformed.Add(new SimpleLandmark
            {
                x = newPos.x,
                y = newPos.y,
                z = newPos.z,
                visibility = lm.visibility
            });
        }
        return transformed;
    }


    private void AddAllLandmarkData(List<float> flattened, List<SimpleLandmark> landmarks, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (landmarks != null && i < landmarks.Count)
            {
                flattened.Add(landmarks[i].x);
                flattened.Add(landmarks[i].y);
                flattened.Add(landmarks[i].z);
            }
            else
            {
                flattened.AddRange(new float[] { 0, 0, 0 });
            }
        }
    }

    private void AddJointAngles(List<float> flattened, List<SimpleLandmark> pose)
    {
        try
        {
            if (pose != null && pose.Count >= 9)
            {
                Vector3 leftShoulder = new Vector3(pose[0].x, pose[0].y, pose[0].z);
                Vector3 leftElbow = new Vector3(pose[2].x, pose[2].y, pose[2].z);
                Vector3 leftWrist = new Vector3(pose[4].x, pose[4].y, pose[4].z);
                Vector3 rightShoulder = new Vector3(pose[1].x, pose[1].y, pose[1].z);
                Vector3 rightElbow = new Vector3(pose[3].x, pose[3].y, pose[3].z);
                Vector3 rightWrist = new Vector3(pose[5].x, pose[5].y, pose[5].z);
                Vector3 hipCenter = new Vector3(pose[8].x, pose[8].y, pose[8].z);

                float leftElbowAngle = CalculateAngle(leftShoulder, leftElbow, leftWrist);
                float leftShoulderAngle = CalculateAngle(leftElbow, leftShoulder, hipCenter);
                float rightElbowAngle = CalculateAngle(rightShoulder, rightElbow, rightWrist);
                float rightShoulderAngle = CalculateAngle(rightElbow, rightShoulder, hipCenter);

                flattened.Add(Mathf.Cos(leftElbowAngle));
                flattened.Add(Mathf.Sin(leftElbowAngle));
                flattened.Add(Mathf.Cos(leftShoulderAngle));
                flattened.Add(Mathf.Sin(leftShoulderAngle));
                flattened.Add(Mathf.Cos(rightElbowAngle));
                flattened.Add(Mathf.Sin(rightElbowAngle));
                flattened.Add(Mathf.Cos(rightShoulderAngle));
                flattened.Add(Mathf.Sin(rightShoulderAngle));
            }
            else
            {
                flattened.AddRange(new float[8]);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"관절 각도 추가 오류: {e.Message}");
            flattened.AddRange(new float[8]);
        }
    }

    private void AddFingerStates(List<float> flattened, List<SimpleLandmark> leftHand, List<SimpleLandmark> rightHand)
    {
        AddSingleHandFingerStates(flattened, leftHand);
        AddSingleHandFingerStates(flattened, rightHand);
    }

    private void AddSingleHandFingerStates(List<float> flattened, List<SimpleLandmark> hand)
    {
        if (hand != null && hand.Count == 21)
        {
            int[][] fingerIndices = {
                new int[] { 5, 6, 7, 8 },
                new int[] { 9, 10, 11, 12 },
                new int[] { 13, 14, 15, 16 },
                new int[] { 17, 18, 19, 20 }
            };

            foreach (var finger in fingerIndices)
            {
                Vector3 p1 = new Vector3(hand[finger[0]].x, hand[finger[0]].y, hand[finger[0]].z);
                Vector3 p2 = new Vector3(hand[finger[1]].x, hand[finger[1]].y, hand[finger[1]].z);
                Vector3 p3 = new Vector3(hand[finger[2]].x, hand[finger[2]].y, hand[finger[2]].z);

                Vector3 v1 = p2 - p1;
                Vector3 v2 = p3 - p2;

                if (v1.magnitude < 1e-6 || v2.magnitude < 1e-6)
                {
                    flattened.Add(0.0f);
                    continue;
                }

                float cos = Vector3.Dot(v1.normalized, v2.normalized);
                cos = Mathf.Clamp(cos, -1.0f, 1.0f);
                float state = (cos + 1.0f) / 2.0f;
                flattened.Add(state);
            }
        }
        else
        {
            flattened.AddRange(new float[4]);
        }
    }

    private void AddPalmNormals(List<float> flattened, List<SimpleLandmark> leftHand, List<SimpleLandmark> rightHand)
    {
        AddSingleHandPalmNormal(flattened, leftHand);
        AddSingleHandPalmNormal(flattened, rightHand);
    }

    private void AddSingleHandPalmNormal(List<float> flattened, List<SimpleLandmark> hand)
    {
        if (hand != null && hand.Count >= 21)
        {
            Vector3 wrist = new Vector3(hand[0].x, hand[0].y, hand[0].z);
            Vector3 indexMcp = new Vector3(hand[5].x, hand[5].y, hand[5].z);
            Vector3 pinkyMcp = new Vector3(hand[17].x, hand[17].y, hand[17].z);

            Vector3 v1 = indexMcp - wrist;
            Vector3 v2 = pinkyMcp - wrist;
            Vector3 normal = Vector3.Cross(v1, v2).normalized;

            flattened.Add(normal.x);
            flattened.Add(normal.y);
            flattened.Add(normal.z);
        }
        else
        {
            flattened.AddRange(new float[3]);
        }
    }

    private void AddVelocityData(List<float> flattened, SimpleFrame currentFrame, SimpleFrame prevFrame)
    {
        float speed_your = 0.0f;
        float speed_friend = 0.0f;

        if (prevFrame != null)
        {
            var currentCoords = new List<Vector3>();
            var prevCoords = new List<Vector3>();
            if (currentFrame.leftHand != null && prevFrame.leftHand != null) { currentCoords.AddRange(LandmarksToVectors(currentFrame.leftHand)); prevCoords.AddRange(LandmarksToVectors(prevFrame.leftHand)); }
            if (currentFrame.rightHand != null && prevFrame.rightHand != null) { currentCoords.AddRange(LandmarksToVectors(currentFrame.rightHand)); prevCoords.AddRange(LandmarksToVectors(prevFrame.rightHand)); }
            if (currentFrame.pose != null && prevFrame.pose != null) { currentCoords.AddRange(LandmarksToVectors(currentFrame.pose)); prevCoords.AddRange(LandmarksToVectors(prevFrame.pose)); }
            if (currentFrame.faceKey != null && prevFrame.faceKey != null) { currentCoords.AddRange(LandmarksToVectors(currentFrame.faceKey)); prevCoords.AddRange(LandmarksToVectors(prevFrame.faceKey)); }

            if (currentCoords.Count > 0 && currentCoords.Count == prevCoords.Count)
            {
                float totalChange = 0;
                for (int i = 0; i < currentCoords.Count; i++)
                {
                    totalChange += Mathf.Pow(currentCoords[i].x - prevCoords[i].x, 2);
                    totalChange += Mathf.Pow(currentCoords[i].y - prevCoords[i].y, 2);
                    totalChange += Mathf.Pow(currentCoords[i].z - prevCoords[i].z, 2);
                }
                speed_your = Mathf.Sqrt(totalChange) / currentCoords.Count;
            }

            var currentHandCoords = new List<Vector3>();
            var prevHandCoords = new List<Vector3>();
            if (currentFrame.leftHand != null && prevFrame.leftHand != null) { currentHandCoords.AddRange(LandmarksToVectors(currentFrame.leftHand)); prevHandCoords.AddRange(LandmarksToVectors(prevFrame.leftHand)); }
            if (currentFrame.rightHand != null && prevFrame.rightHand != null) { currentHandCoords.AddRange(LandmarksToVectors(currentFrame.rightHand)); prevHandCoords.AddRange(LandmarksToVectors(prevFrame.rightHand)); }

            if (currentHandCoords.Count > 0 && currentHandCoords.Count == prevHandCoords.Count)
            {
                float handChangeSum = 0;
                for (int i = 0; i < currentHandCoords.Count; i++)
                {
                    handChangeSum += Vector3.Distance(currentHandCoords[i], prevHandCoords[i]);
                }
                speed_friend = handChangeSum / currentHandCoords.Count;
            }
        }

        flattened.Add(speed_your);
        flattened.Add(speed_friend);
    }

    private List<Vector3> LandmarksToVectors(List<SimpleLandmark> landmarks)
    {
        return landmarks.Select(lm => new Vector3(lm.x, lm.y, lm.z)).ToList();
    }


    private void AddMaskFlags(List<float> flattened, List<SimpleLandmark> leftHand, List<SimpleLandmark> rightHand, List<SimpleLandmark> pose, List<SimpleLandmark> face)
    {
        flattened.Add(leftHand != null && leftHand.Count >= 21 ? 1f : 0f);
        flattened.Add(rightHand != null && rightHand.Count >= 21 ? 1f : 0f);
        flattened.Add(pose != null && pose.Count >= 9 ? 1f : 0f);
        flattened.Add(face != null && face.Count >= 21 ? 1f : 0f);
    }

    private float CalculateAngle(Vector3 point1, Vector3 vertex, Vector3 point2)
    {
        Vector3 v1 = (point1 - vertex).normalized;
        Vector3 v2 = (point2 - vertex).normalized;
        float dot = Vector3.Dot(v1, v2);
        dot = Mathf.Clamp(dot, -1f, 1f);
        return Mathf.Acos(dot);
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
                Debug.Log($"프레임 시퀀스 개수: {frameSequence.Count}");
                if (frameSequence.Count == 0)
                {
                    Debug.LogError("frameSequence가 비어있음!");
                    yield break;
                }

                sequenceArray = new float[frameSequence.Count][];
                for (int i = 0; i < frameSequence.Count; i++)
                {
                    if (frameSequence[i] != null)
                    {
                        sequenceArray[i] = frameSequence[i].ToArray();
                    }
                    else
                    {
                        Debug.LogError($"프레임 {i}가 null!");
                        sequenceArray[i] = new float[244];
                    }
                }
            }

            var sequenceData = new SequenceData { sequence = sequenceArray };
            json = JsonConvert.SerializeObject(sequenceData);

            Debug.Log($"생성된 JSON 길이: {json.Length}");

            if (saveToTextFile)
            {
                SaveJsonToFile(json);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON 생성 오류: {e.Message}");
            yield break;
        }

        if (!sendToServer)
        {
            Debug.Log("[배틀] 서버 전송 비활성화 - 로컬 저장만 수행");
            yield break;
        }

        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<ServerResponse>(request.downloadHandler.text);
                    Debug.Log($"[서버 응답] 예측: {response.prediction}");

                    if (!gestureHandled)
                    {
                        gestureHandled = true;
                        OnGestureRecognitionComplete?.Invoke(response.prediction);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"서버 응답 파싱 오류: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"서버 전송 실패: {request.error}");
            }
        }
    }

    private void SaveJsonToFile(string json)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"battle_sequence_{timestamp}.json";
            string filePath = Path.Combine(actualSaveDirectory, fileName);
            File.WriteAllText(filePath, json);
            Debug.Log($"JSON 저장 완료: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON 파일 저장 실패: {e.Message}");
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

    void OnDestroy()
    {
        isDestroying = true;
        CancelInvoke();
        StopAllCoroutines();
    }
}