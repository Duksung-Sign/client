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
    private float[] prevFullFeature = null;

    public float[] ProcessAndConvertToVector(
        NormalizedLandmarkList pose,
        NormalizedLandmarkList face,
        NormalizedLandmarkList leftHand,
        NormalizedLandmarkList rightHand)
    {
        SignLanguageData processedData = ProcessHolisticData(pose, face, leftHand, rightHand);
        if (processedData == null)
        {
            return null; // 데이터 가공에 실패하면 null을 반환합니다.
        }

        float[] vector = ConvertToVector(processedData);

        return vector;
    }

    public SignLanguageData ProcessHolisticData(
        NormalizedLandmarkList poseLandmarks,
        NormalizedLandmarkList faceLandmarks,
        NormalizedLandmarkList leftHandLandmarks,
        NormalizedLandmarkList rightHandLandmarks)
    {
        var data = new SignLanguageData();

        data.pose_flag = (poseLandmarks != null && poseLandmarks.Landmark.Count > 0) ? 1 : 0;
        data.face_flag = (faceLandmarks != null && faceLandmarks.Landmark.Count > 0) ? 1 : 0;
        data.lh_flag = (leftHandLandmarks != null && leftHandLandmarks.Landmark.Count > 0) ? 1 : 0;
        data.rh_flag = (rightHandLandmarks != null && rightHandLandmarks.Landmark.Count > 0) ? 1 : 0;

        var poseWorld = new List<Vector3>();
        if (data.pose_flag == 1)
        {
            foreach (var lm in poseLandmarks.Landmark)
            {
                poseWorld.Add(TransformLandmark(lm));
            }
            FillPoseData(data, poseWorld);
        }

        var leftHandWorld = new List<Vector3>();
        if (data.lh_flag == 1)
        {
            foreach (var lm in leftHandLandmarks.Landmark)
            {
                leftHandWorld.Add(TransformLandmark(lm));
            }
            FillHandData(data, leftHandWorld, "left");
        }

        var rightHandWorld = new List<Vector3>();
        if (data.rh_flag == 1)
        {
            foreach (var lm in rightHandLandmarks.Landmark)
            {
                rightHandWorld.Add(TransformLandmark(lm));
            }
            FillHandData(data, rightHandWorld, "right");
        }

        if (data.face_flag == 1)
        {
            var allFaceLandmarks = new List<Vector3>();
            foreach (var lm in faceLandmarks.Landmark)
            {
                allFaceLandmarks.Add(TransformLandmark(lm));
            }
            FillFaceData(data, allFaceLandmarks);
        }

        if (data.pose_flag == 1 && poseWorld.Count >= 25)
        {
            Vector2 lElbow = CalculateJointAngle(poseWorld[11], poseWorld[13], poseWorld[15]);
            data.l_elbow_cos = lElbow.x;
            data.l_elbow_sin = lElbow.y;

            Vector2 rElbow = CalculateJointAngle(poseWorld[12], poseWorld[14], poseWorld[16]);
            data.r_elbow_cos = rElbow.x;
            data.r_elbow_sin = rElbow.y;

            Vector3 hipCenter = (poseWorld[23] + poseWorld[24]) * 0.5f;

            Vector2 lShoulder = CalculateJointAngle(poseWorld[13], poseWorld[11], hipCenter);
            data.l_shoulder_cos = lShoulder.x;
            data.l_shoulder_sin = lShoulder.y;

            Vector2 rShoulder = CalculateJointAngle(poseWorld[14], poseWorld[12], hipCenter);
            data.r_shoulder_cos = rShoulder.x;
            data.r_shoulder_sin = rShoulder.y;

            data.hip_center_x = hipCenter.x;
            data.hip_center_y = hipCenter.y;
            data.hip_center_z = hipCenter.z;
            data.speed_your = CalculateSpeed(hipCenter);
            UpdatePreviousData(hipCenter);
        }

        if (data.lh_flag == 1 && leftHandWorld.Count >= 21)
        {
            Vector3 lNormal = CalculatePalmNormal(leftHandWorld);
            data.left_palm_nx = lNormal.x;
            data.left_palm_ny = lNormal.y;
            data.left_palm_nz = lNormal.z;

            data.left_index_state = GetFingerState(leftHandWorld, 5);
            data.left_middle_state = GetFingerState(leftHandWorld, 9);
            data.left_ring_state = GetFingerState(leftHandWorld, 13);
            data.left_pinky_state = GetFingerState(leftHandWorld, 17);
        }

        if (data.rh_flag == 1 && rightHandWorld.Count >= 21)
        {
            Vector3 rNormal = CalculatePalmNormal(rightHandWorld);
            data.right_palm_nx = rNormal.x;
            data.right_palm_ny = rNormal.y;
            data.right_palm_nz = rNormal.z;

            data.right_index_state = GetFingerState(rightHandWorld, 5);
            data.right_middle_state = GetFingerState(rightHandWorld, 9);
            data.right_ring_state = GetFingerState(rightHandWorld, 13);
            data.right_pinky_state = GetFingerState(rightHandWorld, 17);
        }
        float[] currentFullFeature = ConvertToVector(data);

        // 속도 계산
        if (prevFullFeature != null)
        {
            // speed_your: 좌표 부분(216개)의 차이
            float diffSumSq = 0;
            for (int i = 0; i < 216; i++)
            {
                diffSumSq += (currentFullFeature[i] - prevFullFeature[i]) * (currentFullFeature[i] - prevFullFeature[i]);
            }
            data.speed_your = Mathf.Sqrt(diffSumSq) / 216.0f;

            // speed_friend: 손 좌표 부분(126개)의 차이
            float handDistSum = 0;
            for (int i = 0; i < 42; i++)
            { // 42개의 3D 포인트 (양손)
                int idx = i * 3;
                float dx = currentFullFeature[idx] - prevFullFeature[idx];
                float dy = currentFullFeature[idx + 1] - prevFullFeature[idx + 1];
                float dz = currentFullFeature[idx + 2] - prevFullFeature[idx + 2];
                handDistSum += Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            data.speed_friend = handDistSum / 42.0f;
        }

        // 이전 프레임 데이터 업데이트
        prevFullFeature = currentFullFeature;

        return data;
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
        Vector3 v1 = (p1 - p2).normalized;
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

    private float[] ConvertToVector(SignLanguageData data)
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
}