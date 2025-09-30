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
            var faceWorld = new List<Vector3>();
            for (int i = 0; i < Mathf.Min(21, faceLandmarks.Landmark.Count); i++)
            {
                faceWorld.Add(TransformLandmark(faceLandmarks.Landmark[i]));
            }
            FillFaceData(data, faceWorld);
        }

        if (data.pose_flag == 1 && poseWorld.Count >= 25)
        {
            Vector2 lElbow = CalculateJointAngle(poseWorld[11], poseWorld[13], poseWorld[15]);
            data.l_elbow_cos = lElbow.x;
            data.l_elbow_sin = lElbow.y;

            Vector2 rElbow = CalculateJointAngle(poseWorld[12], poseWorld[14], poseWorld[16]);
            data.r_elbow_cos = rElbow.x;
            data.r_elbow_sin = rElbow.y;

            Vector2 lShoulder = CalculateJointAngle(poseWorld[23], poseWorld[11], poseWorld[13]);
            data.l_shoulder_cos = lShoulder.x;
            data.l_shoulder_sin = lShoulder.y;

            Vector2 rShoulder = CalculateJointAngle(poseWorld[24], poseWorld[12], poseWorld[14]);
            data.r_shoulder_cos = rShoulder.x;
            data.r_shoulder_sin = rShoulder.y;

            Vector3 hipCenter = (poseWorld[23] + poseWorld[24]) * 0.5f;
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

    private void FillFaceData(SignLanguageData data, List<Vector3> worldLandmarks)
    {
        for (int i = 0; i < worldLandmarks.Count && i < 21; i++)
        {
            SetFieldValue(data, $"face_{i}_x", worldLandmarks[i].x);
            SetFieldValue(data, $"face_{i}_y", worldLandmarks[i].y);
            SetFieldValue(data, $"face_{i}_z", worldLandmarks[i].z);
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

        Vector3 v1 = handLandmarks[9] - handLandmarks[0];
        Vector3 v2 = handLandmarks[17] - handLandmarks[0];

        return Vector3.Cross(v1, v2).normalized;
    }

    public float GetFingerState(List<Vector3> handLandmarks, int fingerStartIndex)
    {
        if (handLandmarks == null || handLandmarks.Count < fingerStartIndex + 3) return 0;

        Vector3 p1 = handLandmarks[fingerStartIndex];
        Vector3 p2 = handLandmarks[fingerStartIndex + 1];
        Vector3 p3 = handLandmarks[fingerStartIndex + 2];

        Vector2 angleCosSin = CalculateJointAngle(p1, p2, p3);
        float angleDegrees = Mathf.Acos(angleCosSin.x) * Mathf.Rad2Deg;

        return (angleDegrees > 160.0f) ? 1 : 0;
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
}