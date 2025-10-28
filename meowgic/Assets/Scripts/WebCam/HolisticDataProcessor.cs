using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Collections;
using System.Linq;
using UnityEngine.Networking;
using Mediapipe.Unity.Sample.Holistic;
using Mediapipe.Unity;
using Mediapipe;
using System.IO;
using System.Globalization;

public class HolisticDataProcessor : MonoBehaviour
{
    public Vector3 scale = new Vector3(10, 10, 10);
    public Vector3 offset = Vector3.zero;
    private Vector3? prevHipCenter = null;
    private float lastTimestamp;

    private static readonly List<int> UNIFIED_FACE_IDX = new List<int> {
    1, 33, 263, 61, 0, 13, 14, 17, 70, 107, 336, 296,
    133, 362, 2, 98, 327, 152, 234, 454, 291
};
    //private List<float> prevFullFeature = null;
    private float[] prevCore216 = null;

    [Header("Optional: Arduino IMU")]
    public SerialSensorReader sensorReader;  // 인스펙터에서 할당
    private const int EXTRA_IMU_DIM = 32;     // L(acc3+gyro3)+R(acc3+gyro3)
    private float[] prevFeature244 = null; // 타입을 float[] 로 변경
    private const int FEATURE_DIM_244 = 244;
    private const int FEATURE_DIM_FINAL = 276;

    private float[] scalerMeans = new float[276]; // 276개 평균값 배열
    private float[] scalerScales = new float[276]; // 276개 표준편차값 배열
    private bool scalerLoaded = false;

    void Awake()
    {
        LoadScalerValues(); // 스케일러 파라미터 로드
    }

    public float[] ProcessAndConvertToVector(
        NormalizedLandmarkList pose,
        NormalizedLandmarkList face,
        NormalizedLandmarkList leftHand,
        NormalizedLandmarkList rightHand)
    {
        // 1. Process base landmark data (angles, normals, finger states etc., but NOT speed yet)
        SignLanguageData processedData = ProcessHolisticDataBase(pose, face, leftHand, rightHand);
        if (processedData == null)
        {
            Debug.LogError("[HolisticProcessor] Failed to process base landmark data.");
            return new float[FEATURE_DIM_FINAL]; // Return zero vector
        }

        // 2. Convert base data to initial 244 feature list (speed values are temporary/0)
        List<float> vector244List = ConvertToVectorBase(processedData);
        // Ensure 244 dimensions
        if (vector244List.Count != FEATURE_DIM_244)
        {
            Debug.LogError($"[HolisticProcessor] ConvertToVectorBase dimension error! Expected: {FEATURE_DIM_244}, Actual: {vector244List.Count}. Fixing...");
            while (vector244List.Count < FEATURE_DIM_244) vector244List.Add(0f);
            while (vector244List.Count > FEATURE_DIM_244) vector244List.RemoveAt(vector244List.Count - 1);
        }
        float[] currentFeature244 = vector244List.ToArray();

        // 3. Calculate final speeds using current and previous NON-SCALED 244 vectors
        //    ▶ 파이썬과 동일: 앞 216 (손126 + 포즈27 + 얼굴63)만으로 속도 계산
        //    ※ ConvertToVectorBase의 레이아웃이 아래 인덱스와 일치해야 합니다.
        const int IDX_HANDS = 0; const int LEN_HANDS = 126;  // LH(63)+RH(63)
        const int IDX_POSE27 = 126; const int LEN_POSE27 = 27;  // 포즈 27 (hip 제외)
        const int IDX_FACE = 153; const int LEN_FACE = 63;   // 얼굴 63
        const int CORE_LEN = LEN_HANDS + LEN_POSE27 + LEN_FACE;   // 216

        const int IDX_SPEED_ALL = 238;  // speed-all
        const int IDX_SPEED_HAND = 239;  // speed-hand

        // core216 구성
        float[] core216 = new float[CORE_LEN];
        Array.Copy(currentFeature244, IDX_HANDS, core216, 0, LEN_HANDS);
        Array.Copy(currentFeature244, IDX_POSE27, core216, LEN_HANDS, LEN_POSE27);
        Array.Copy(currentFeature244, IDX_FACE, core216, LEN_HANDS + LEN_POSE27, LEN_FACE);

        // ▶ speed_all: core216의 L2 차 / 216
        // ▶ speed_hand: 손 126D(= 42점*3)의 프레임별 이동량 평균
        float speed_all = 0f, speed_hand = 0f;
        if (prevCore216 != null && prevCore216.Length == CORE_LEN)
        {
            // 전체 속도
            double sum2 = 0;
            for (int i = 0; i < CORE_LEN; i++)
            {
                double d = core216[i] - prevCore216[i];
                sum2 += d * d;
            }
            speed_all = (float)(Math.Sqrt(sum2) / CORE_LEN);

            // 손 평균 속도 (벡터 크기 평균; 42개 점)
            double handSum = 0;
            for (int i = 0; i < LEN_HANDS; i += 3)
            {
                double dx = currentFeature244[IDX_HANDS + i] - prevCore216[i];
                double dy = currentFeature244[IDX_HANDS + i + 1] - prevCore216[i + 1];
                double dz = currentFeature244[IDX_HANDS + i + 2] - prevCore216[i + 2];
                handSum += Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            speed_hand = (float)(handSum / 42.0);
        }

        // 4. Update speed values in the current 244 vector
        currentFeature244[IDX_SPEED_ALL] = speed_all;
        currentFeature244[IDX_SPEED_HAND] = speed_hand;

        // 5. Store current NON-SCALED "core216" only for the next frame
        prevCore216 = core216; // (clone 불필요: 새 배열이라 참조 저장해도 안전)

        // 6. Combine MediaPipe features (244) with IMU features (32, Right→Left)
        //    ▶ sensorReader가 TryGetLatest32_RL(out ...) 제공해야 함
        float[] imu32;
        if (sensorReader != null && sensorReader.GetLatest32(out imu32))
        {
            // ok
        }
        else
        {
            imu32 = new float[EXTRA_IMU_DIM]; // 준비 전이면 0으로
        }


        // 7. Final dimension check (should be 276)
        float[] finalVectorArray = new float[FEATURE_DIM_FINAL]; // 276
        Buffer.BlockCopy(currentFeature244, 0, finalVectorArray, 0, FEATURE_DIM_244 * sizeof(float));
        Buffer.BlockCopy(imu32, 0, finalVectorArray, FEATURE_DIM_244 * sizeof(float), EXTRA_IMU_DIM * sizeof(float));

        // 8. (스케일러는 서버에서 적용; 여기선 생벡터 반환)

        // 8. Apply StandardScaler
        //if (scalerLoaded)
        //{
        //    ApplyScaling(finalVectorArray);
        //}
        //else
        //{
        //    Debug.LogWarning("[Scaler] Scaler values not loaded. Skipping scaling.");
        //}

        return finalVectorArray;
    }

    public SignLanguageData ProcessHolisticDataBase(
            NormalizedLandmarkList poseLandmarks,
            NormalizedLandmarkList faceLandmarks,
            NormalizedLandmarkList leftHandLandmarks,
            NormalizedLandmarkList rightHandLandmarks)
    {
        var data = new SignLanguageData();

        // --- Flags ---
        data.pose_flag = (poseLandmarks?.Landmark != null && poseLandmarks.Landmark.Count > 0) ? 1 : 0;
        data.face_flag = (faceLandmarks?.Landmark != null && faceLandmarks.Landmark.Count > 0) ? 1 : 0;
        data.lh_flag = (leftHandLandmarks?.Landmark != null && leftHandLandmarks.Landmark.Count > 0) ? 1 : 0;
        data.rh_flag = (rightHandLandmarks?.Landmark != null && rightHandLandmarks.Landmark.Count > 0) ? 1 : 0;

        // --- Process Landmarks ---
        var poseWorld = new List<Vector3>();
        if (data.pose_flag == 1) { foreach (var lm in poseLandmarks.Landmark) poseWorld.Add(TransformLandmark(lm)); FillPoseData(data, poseWorld); }
        var leftHandWorld = new List<Vector3>();
        if (data.lh_flag == 1) { foreach (var lm in leftHandLandmarks.Landmark) leftHandWorld.Add(TransformLandmark(lm)); FillHandData(data, leftHandWorld, "left"); }
        var rightHandWorld = new List<Vector3>();
        if (data.rh_flag == 1) { foreach (var lm in rightHandLandmarks.Landmark) rightHandWorld.Add(TransformLandmark(lm)); FillHandData(data, rightHandWorld, "right"); }
        if (data.face_flag == 1)
        {
            var allFaceLandmarks = new List<Vector3>();
            foreach (var lm in faceLandmarks.Landmark) allFaceLandmarks.Add(TransformLandmark(lm));
            FillFaceData(data, allFaceLandmarks); // Uses UNIFIED_FACE_IDX inside
        }


        // --- Calculate Angles, Normals, Finger States ---
        if (data.pose_flag == 1 && poseWorld.Count >= 25) // Ensure enough landmarks
        {
            Vector3 hipCenter = (poseWorld[23] + poseWorld[24]) * 0.5f;
            Vector2 lElbow = CalculateJointAngle(poseWorld[11], poseWorld[13], poseWorld[15]);
            data.l_elbow_cos = lElbow.x; data.l_elbow_sin = lElbow.y;
            Vector2 rElbow = CalculateJointAngle(poseWorld[12], poseWorld[14], poseWorld[16]);
            data.r_elbow_cos = rElbow.x; data.r_elbow_sin = rElbow.y;
            Vector2 lShoulder = CalculateJointAngle(poseWorld[13], poseWorld[11], hipCenter);
            data.l_shoulder_cos = lShoulder.x; data.l_shoulder_sin = lShoulder.y;
            Vector2 rShoulder = CalculateJointAngle(poseWorld[14], poseWorld[12], hipCenter);
            data.r_shoulder_cos = rShoulder.x; data.r_shoulder_sin = rShoulder.y;
            data.hip_center_x = hipCenter.x; data.hip_center_y = hipCenter.y; data.hip_center_z = hipCenter.z;

            // Remove legacy hip speed calculation here
            // data.speed_your = CalculateSpeed(hipCenter);
            // UpdatePreviousData(hipCenter);
        }
        if (data.lh_flag == 1 && leftHandWorld.Count >= 21)
        {
            Vector3 lNormal = CalculatePalmNormal(leftHandWorld);
            data.left_palm_nx = lNormal.x; data.left_palm_ny = lNormal.y; data.left_palm_nz = lNormal.z;
            data.left_index_state = GetFingerState(leftHandWorld, 5);
            data.left_middle_state = GetFingerState(leftHandWorld, 9);
            data.left_ring_state = GetFingerState(leftHandWorld, 13);
            data.left_pinky_state = GetFingerState(leftHandWorld, 17);
        }
        if (data.rh_flag == 1 && rightHandWorld.Count >= 21)
        {
            Vector3 rNormal = CalculatePalmNormal(rightHandWorld);
            data.right_palm_nx = rNormal.x; data.right_palm_ny = rNormal.y; data.right_palm_nz = rNormal.z;
            data.right_index_state = GetFingerState(rightHandWorld, 5);
            data.right_middle_state = GetFingerState(rightHandWorld, 9);
            data.right_ring_state = GetFingerState(rightHandWorld, 13);
            data.right_pinky_state = GetFingerState(rightHandWorld, 17);
        }

        // Speed calculation is moved to ProcessAndConvertToVector
        return data;
    }


    // New function to calculate speeds based on Python logic
    private void CalculateSpeeds(float[] currentFeature244, ref float speed_your_final, ref float speed_friend_final)
    {
        // Initialize speeds to 0
        speed_your_final = 0f;
        speed_friend_final = 0f;

        // Only calculate if previous frame data exists and has the correct dimension
        if (prevFeature244 == null || prevFeature244.Length != FEATURE_DIM_244)
        {
            return; // Cannot calculate speed on the first frame or if dimensions mismatch
        }

        // speed_your: L2 norm diff over first 216 coords (72 landmarks * 3 coords), normalized
        float diffSumSqYour = 0;
        int coordDim = 216;
        for (int i = 0; i < coordDim; i++)
        {
            // Check bounds just in case, though ideally lengths are always 244
            if (i >= currentFeature244.Length || i >= prevFeature244.Length) break;
            float diff = currentFeature244[i] - prevFeature244[i];
            diffSumSqYour += diff * diff;
        }
        // Python: float(np.linalg.norm(current_all - prev_all) / len(current_all))
        // np.linalg.norm is Sqrt(SumOfSquares). Division by len(current_all) which is 216.
        if (coordDim > 0) // Avoid division by zero
        {
            speed_your_final = Mathf.Sqrt(diffSumSqYour) / coordDim;
        }


        // speed_friend: Mean Euclidean distance diff over 42 hand points (126 coords)
        float handDistSum = 0;
        int handCoordDim = 126; // 42 landmarks * 3 coords
        int numHandPoints = 42;
        for (int i = 0; i < numHandPoints; i++)
        {
            int idx = i * 3;
            // Check array bounds before accessing
            if (idx + 2 >= handCoordDim || idx + 2 >= currentFeature244.Length || idx + 2 >= prevFeature244.Length)
            {
                Debug.LogWarning($"[CalculateSpeeds] Index out of bounds for speed_friend at point {i}");
                continue; // Skip this point
            }
            float dx = currentFeature244[idx] - prevFeature244[idx];
            float dy = currentFeature244[idx + 1] - prevFeature244[idx + 1];
            float dz = currentFeature244[idx + 2] - prevFeature244[idx + 2];
            handDistSum += Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        if (numHandPoints > 0) // Avoid division by zero
        {
            // Python: float(np.linalg.norm(current_hands - prev_hands, axis=1).mean())
            // This calculates distance for each point, then averages. Matches the loop above.
            speed_friend_final = handDistSum / numHandPoints;
        }
    }


    // Renamed: Converts base SignLanguageData to the base 244 feature list
    // !!! Feature order MUST match Python CSV header's first 244 columns !!!
    private List<float> ConvertToVectorBase(SignLanguageData data)
    {
        List<float> vector = new List<float>(FEATURE_DIM_244);

        // === Verify this EXACT order against Python CSV ===
        // 1. Left Hand Coordinates (Indices 0-62)
        for (int i = 0; i < 21; i++) { vector.Add(GetFieldValue(data, $"left_hand_{i}_x")); vector.Add(GetFieldValue(data, $"left_hand_{i}_y")); vector.Add(GetFieldValue(data, $"left_hand_{i}_z")); }
        // 2. Right Hand Coordinates (Indices 63-125)
        for (int i = 0; i < 21; i++) { vector.Add(GetFieldValue(data, $"right_hand_{i}_x")); vector.Add(GetFieldValue(data, $"right_hand_{i}_y")); vector.Add(GetFieldValue(data, $"right_hand_{i}_z")); }
        // 3. Pose Coordinates (Indices 126-152) - CHECK ORDER!
        string[] poseJoints = { "l_shoulder", "r_shoulder", "l_elbow", "r_elbow", "l_wrist", "r_wrist", "l_hip", "r_hip", "hip_center" };
        foreach (var joint in poseJoints) { vector.Add(GetFieldValue(data, $"{joint}_x")); vector.Add(GetFieldValue(data, $"{joint}_y")); vector.Add(GetFieldValue(data, $"{joint}_z")); }
        // 4. Face Coordinates (Indices 153-215)
        for (int i = 0; i < 21; i++) { vector.Add(GetFieldValue(data, $"face_{i}_x")); vector.Add(GetFieldValue(data, $"face_{i}_y")); vector.Add(GetFieldValue(data, $"face_{i}_z")); }
        // 5. Angle Features (Indices 216-223) - CHECK ORDER!
        vector.Add(data.l_elbow_cos); vector.Add(data.l_elbow_sin);
        vector.Add(data.r_elbow_cos); vector.Add(data.r_elbow_sin);      
        vector.Add(data.l_shoulder_cos); vector.Add(data.l_shoulder_sin); 
        vector.Add(data.r_shoulder_cos); vector.Add(data.r_shoulder_sin);
        // 6. Finger States (Indices 224-231) - CHECK ORDER!
        vector.Add(data.left_index_state); vector.Add(data.left_middle_state); vector.Add(data.left_ring_state); vector.Add(data.left_pinky_state);
        vector.Add(data.right_index_state); vector.Add(data.right_middle_state); vector.Add(data.right_ring_state); vector.Add(data.right_pinky_state);
        // 7. Palm Normals (Indices 232-237) - CHECK ORDER!
        vector.Add(data.left_palm_nx); vector.Add(data.left_palm_ny); vector.Add(data.left_palm_nz);
        vector.Add(data.right_palm_nx); vector.Add(data.right_palm_ny); vector.Add(data.right_palm_nz);
        // 8. Speed Features (Indices 238-239) - Add placeholders
        vector.Add(0f); // Placeholder for speed_your (index 238)
        vector.Add(0f); // Placeholder for speed_friend (index 239)
        // 9. Flags (Indices 240-243) - CHECK ORDER!
        vector.Add(data.lh_flag); vector.Add(data.rh_flag); vector.Add(data.pose_flag); vector.Add(data.face_flag); // Verify: Is this order correct vs Python?

        return vector;
    }

    private void FillHandData(SignLanguageData data, List<Vector3> worldLandmarks, string prefix)
    {
        for (int i = 0; i < worldLandmarks.Count && i < 21; i++)
        {
            SetFieldValue(data, $"{prefix}_hand_{i}_x", worldLandmarks[i].x);
            SetFieldValue(data, $"{prefix}_hand_{i}_y", worldLandmarks[i].y);
            SetFieldValue(data, $"{prefix}_hand_{i}_z", worldLandmarks[i].z);
        }
    }

    private void FillPoseData(SignLanguageData data, List<Vector3> worldLandmarks)
    {
        var mapping = new Dictionary<string, int>
        {
            {"l_shoulder", 11}, {"r_shoulder", 12}, {"l_elbow", 13}, {"r_elbow", 14},
            {"l_wrist", 15}, {"r_wrist", 16}, {"l_hip", 23}, {"r_hip", 24}
        };

        foreach (var pair in mapping)
        {
            if (pair.Value < worldLandmarks.Count)
            {
                SetFieldValue(data, $"{pair.Key}_x", worldLandmarks[pair.Value].x);
                SetFieldValue(data, $"{pair.Key}_y", worldLandmarks[pair.Value].y);
                SetFieldValue(data, $"{pair.Key}_z", worldLandmarks[pair.Value].z);
            }
        }
    }

    private void FillFaceData(SignLanguageData data, List<Vector3> allFaceLandmarks)
    {
        var faceWorld = new List<Vector3>();
        // UNIFIED_FACE_IDX를 사용해서 필요한 랜드마크만 순서대로 추출
        foreach (int idx in UNIFIED_FACE_IDX)
        {
            if (idx < allFaceLandmarks.Count)
            {
                faceWorld.Add(allFaceLandmarks[idx]);
            }
            else
            {
                faceWorld.Add(Vector3.zero); // 인덱스가 범위를 벗어날 경우 대비
            }
        }

        // 추출된 21개의 랜드마크를 데이터에 채우기
        for (int i = 0; i < faceWorld.Count; i++)
        {
            SetFieldValue(data, $"face_{i}_x", faceWorld[i].x);
            SetFieldValue(data, $"face_{i}_y", faceWorld[i].y);
            SetFieldValue(data, $"face_{i}_z", faceWorld[i].z);
        }
    }

    private void SetFieldValue(object obj, string fieldName, object value)
    {
        FieldInfo field = obj.GetType().GetField(fieldName);
        if (field != null)
        {
            field.SetValue(obj, value);
        }
    }

    public Vector3 TransformLandmark(NormalizedLandmark landmark)
    {
        return new Vector3(
            landmark.X,
            landmark.Y,
            landmark.Z
        );
    }

    public Vector2 CalculateJointAngle(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 v1 = (p2 - p1).normalized;
        Vector3 v2 = (p3 - p2).normalized;

        float cosAngle = Vector3.Dot(v1, v2);
        float angleRad = Mathf.Acos(Mathf.Clamp(cosAngle, -1.0f, 1.0f));
        float sinAngle = Mathf.Sin(angleRad);

        return new Vector2(cosAngle, sinAngle);
    }

    public Vector3 CalculatePalmNormal(List<Vector3> handLandmarks)
    {
        if (handLandmarks == null || handLandmarks.Count < 18) return Vector3.zero;

        // Python과 동일하게 wrist(0), index_mcp(5), pinky_mcp(17) 사용
        Vector3 v1 = handLandmarks[5] - handLandmarks[0];
        Vector3 v2 = handLandmarks[17] - handLandmarks[0];

        return Vector3.Cross(v1, v2).normalized;
    }

    public float GetFingerState(List<Vector3> handLandmarks, int fingerStartIndex)
    {
        if (handLandmarks == null || handLandmarks.Count < fingerStartIndex + 3) return 0f;

        // Python과 동일하게 MCP, PIP, DIP, TIP 인덱스 사용
        Vector3 p0 = handLandmarks[fingerStartIndex];     // e.g., Index MCP
        Vector3 p1 = handLandmarks[fingerStartIndex + 1]; // e.g., Index PIP
        Vector3 p2 = handLandmarks[fingerStartIndex + 2]; // e.g., Index DIP

        Vector3 v1 = p1 - p0;
        Vector3 v2 = p2 - p1;

        // v1, v2의 크기가 0에 가까우면 0을 반환하여 오류 방지
        if (v1.magnitude < 1e-6 || v2.magnitude < 1e-6) return 0.0f;

        float cosAngle = Vector3.Dot(v1.normalized, v2.normalized);

        // Python의 (cos + 1.0) / 2.0 와 동일한 로직
        return (cosAngle + 1.0f) / 2.0f;
    }

    public float CalculateSpeed(Vector3 currentPos)
    {
        if (prevHipCenter == null) return 0.0f;

        float deltaTime = Time.time - lastTimestamp;
        if (deltaTime == 0) return 0.0f;

        Vector3 displacement = currentPos - prevHipCenter.Value;
        return displacement.magnitude / deltaTime;
    }

    public void UpdatePreviousData(Vector3 currentHipCenter)
    {
        prevHipCenter = currentHipCenter;
        lastTimestamp = Time.time;
    }

    private List<float> ConvertToVector(SignLanguageData data)
    {
        List<float> vector = new List<float>();

        for (int i = 0; i < 21; i++)
        {
            vector.Add(GetFieldValue(data, $"left_hand_{i}_x"));
            vector.Add(GetFieldValue(data, $"left_hand_{i}_y"));
            vector.Add(GetFieldValue(data, $"left_hand_{i}_z"));
        }

        for (int i = 0; i < 21; i++)
        {
            vector.Add(GetFieldValue(data, $"right_hand_{i}_x"));
            vector.Add(GetFieldValue(data, $"right_hand_{i}_y"));
            vector.Add(GetFieldValue(data, $"right_hand_{i}_z"));
        }

        string[] poseJoints = { "l_shoulder", "r_shoulder", "l_elbow", "r_elbow",
                               "l_wrist", "r_wrist", "l_hip", "r_hip", "hip_center" };
        foreach (var joint in poseJoints)
        {
            vector.Add(GetFieldValue(data, $"{joint}_x"));
            vector.Add(GetFieldValue(data, $"{joint}_y"));
            vector.Add(GetFieldValue(data, $"{joint}_z"));
        }

        for (int i = 0; i < 21; i++)
        {
            vector.Add(GetFieldValue(data, $"face_{i}_x"));
            vector.Add(GetFieldValue(data, $"face_{i}_y"));
            vector.Add(GetFieldValue(data, $"face_{i}_z"));
        }

        vector.Add(data.l_elbow_cos);
        vector.Add(data.l_elbow_sin);
        vector.Add(data.l_shoulder_cos);
        vector.Add(data.l_shoulder_sin);
        vector.Add(data.r_elbow_cos);
        vector.Add(data.r_elbow_sin);
        vector.Add(data.r_shoulder_cos);
        vector.Add(data.r_shoulder_sin);

        vector.Add(data.left_index_state);
        vector.Add(data.left_middle_state);
        vector.Add(data.left_ring_state);
        vector.Add(data.left_pinky_state);
        vector.Add(data.right_index_state);
        vector.Add(data.right_middle_state);
        vector.Add(data.right_ring_state);
        vector.Add(data.right_pinky_state);

        vector.Add(data.left_palm_nx);
        vector.Add(data.left_palm_ny);
        vector.Add(data.left_palm_nz);
        vector.Add(data.right_palm_nx);
        vector.Add(data.right_palm_ny);
        vector.Add(data.right_palm_nz);

        vector.Add(data.speed_your);
        vector.Add(data.speed_friend);

        vector.Add(data.lh_flag);
        vector.Add(data.rh_flag);
        vector.Add(data.pose_flag);
        vector.Add(data.face_flag);

        if (vector.Count != 244)
        {
            Debug.LogWarning($"Vector dimension mismatch! Expected: 244, Actual: {vector.Count}");
            while (vector.Count < 244) vector.Add(0f);
        }

        return vector;
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

    void LoadScalerValues()
    {
        try
        {
            // 방법 1: Resources 폴더에서 텍스트 파일 로드
            var meansText = Resources.Load<TextAsset>("Setting/scaler_means_276");
            var scalesText = Resources.Load<TextAsset>("Setting/scaler_scales_276");

            if (meansText == null || scalesText == null)
            {
                Debug.LogError("[Scaler] 스케일러 값 파일을 Resources 폴더에서 찾을 수 없습니다.");
                // 방법 2 시도 또는 에러 처리
                // return;
            }
            else
            {
                string[] meanLines = meansText.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string[] scaleLines = scalesText.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                if (meanLines.Length != 276 || scaleLines.Length != 276)
                {
                    Debug.LogError($"[Scaler] 스케일러 값 파일의 라인 수가 276이 아닙니다! Means:{meanLines.Length}, Scales:{scaleLines.Length}");
                    return;
                }

                for (int i = 0; i < 276; i++)
                {
                    scalerMeans[i] = float.Parse(meanLines[i], CultureInfo.InvariantCulture);
                    scalerScales[i] = float.Parse(scaleLines[i], CultureInfo.InvariantCulture);
                    // 표준편차가 0에 가까운 경우 나누기 오류 방지 (Python StandardScaler와 유사하게 처리)
                    if (Mathf.Abs(scalerScales[i]) < 1e-8f)
                    {
                        scalerScales[i] = 1.0f; // 0으로 나누는 대신 1로 설정
                    }
                }
                scalerLoaded = true;
                Debug.Log("[Scaler] 스케일러 값 로드 완료.");
                return; // 성공했으므로 여기서 종료
            }   

        }
        catch (Exception e)
        {
            Debug.LogError($"[Scaler] 스케일러 값 로드 중 오류: {e.Message}");
            scalerLoaded = false;
        }
    }
    private void ApplyScaling(float[] data)
    {
        if (data == null || data.Length != 276 || !scalerLoaded) { Debug.Log("스케일러 미적용"); return; }

        for (int i = 0; i < 276; i++)
        {
            // 표준화 공식: (값 - 평균) / 표준편차
            data[i] = (data[i] - scalerMeans[i]) / scalerScales[i];
        }
    }
}