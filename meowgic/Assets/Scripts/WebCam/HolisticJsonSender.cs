using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json;
using WebSocketSharp;
using Mediapipe.Unity;
using Mediapipe;
using Mediapipe.Unity.Sample.Holistic;
using System.Collections;

public class HolisticJsonSender : MonoBehaviour
{
    [Serializable]
    public class FramePayload
    {
        public float[] frame;
    }

    [Serializable]
    public class ServerResponse
    {
        public string prediction;
        public Dictionary<string, float> probabilities;
    }

    public HolisticDataProcessor dataProcessor;
    private HolisticTrackingSolution holisticSolution;
    private WebSocket ws;

    [Header("Server Settings")]
    public string serverIP = "54.180.108.68";
    public int serverPort = 8000;
    public string serverEndpoint = "ws/276";

    private bool isStreaming = false;
    private int lastProcessedFrame = -1;
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    private readonly object predictionLock = new object();
    private string latestPrediction = null;

    public event Action<string> OnPredictionReceived;

    void Start()
    {
        holisticSolution = FindObjectOfType<HolisticTrackingSolution>();
        if (holisticSolution == null)
        {
            Debug.LogError("[HolisticJsonSender] HolisticTrackingSolution을 찾을 수 없습니다!");
            enabled = false;
            return;
        }

        holisticSolution.OnHolisticLandmarksOutput.AddListener(OnLandmarks);
        ConnectToServer();
    }

    void Update()
    {
        while (mainThreadActions.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    private void ConnectToServer()
    {
        string ep = serverEndpoint.StartsWith("/") ? serverEndpoint : "/" + serverEndpoint;
        string url = $"ws://{serverIP}:{serverPort}{ep}";

        if (ws != null)
        {
            try { ws.Close(); }
            catch { }
            ws = null;
        }

        ws = new WebSocket(url);
        ws.Log.Level = WebSocketSharp.LogLevel.Warn;

        ws.OnOpen += (sender, e) =>
        {
            Debug.Log("✅ 서버 연결 성공");
        };

        ws.OnMessage += (sender, e) =>
        {
            try
            {
                var resp = JsonConvert.DeserializeObject<ServerResponse>(e.Data);
                mainThreadActions.Enqueue(() =>
                {
                    lock (predictionLock)
                    {
                        latestPrediction = resp?.prediction ?? "none";
                    }
                    OnPredictionReceived?.Invoke(resp?.prediction ?? "none");
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"JSON 파싱 실패: {ex.Message}");
            }
        };

        ws.OnError += (sender, e) => Debug.LogError($"WebSocket 에러: {e.Message}");
        ws.OnClose += (sender, e) => Debug.LogWarning($"WebSocket 연결 종료: {e.Code}");

        try
        {
            ws.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket 연결 실패: {ex.Message}");
        }
    }

    private void OnLandmarks(
        NormalizedLandmarkList pose,
        NormalizedLandmarkList face,
        NormalizedLandmarkList leftHand,
        NormalizedLandmarkList rightHand)
    {
        if (!isStreaming || ws == null || !ws.IsAlive)
            return;

        if (Time.frameCount == lastProcessedFrame)
            return;

        lastProcessedFrame = Time.frameCount;

        float[] vector = dataProcessor.ProcessAndConvertToVector(pose, face, leftHand, rightHand);
        if (vector == null)
            return;

        var payload = new FramePayload { frame = vector };
        string jsonPayload = JsonConvert.SerializeObject(payload);
        ws.Send(jsonPayload);
    }

    public void ClearPredictions()
    {
        lock (predictionLock)
        {
            latestPrediction = null;
        }
    }

    public void StartStreaming()
    {
        if (ws == null || !ws.IsAlive)
        {
            StartCoroutine(WaitAndStartStreaming());
            return;
        }

        ClearPredictions();
        isStreaming = true;
    }

    private IEnumerator WaitAndStartStreaming()
    {
        float timeout = 5f;
        while (timeout > 0f && (ws == null || !ws.IsAlive))
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (ws != null && ws.IsAlive)
        {
            ClearPredictions();
            isStreaming = true;
        }
        else
        {
            Debug.LogError("WebSocket 연결 실패 - 서버 상태를 확인하세요");
        }
    }

    public void StopStreaming()
    {
        isStreaming = false;
    }

    public bool IsMediaPipeReady()
    {
        if (holisticSolution == null)
            return false;

        bool isConnected = ws != null && ws.IsAlive;
        bool hasHand = holisticSolution.CurrentLeftHandLandmarks?.Landmark.Count > 0 ||
                       holisticSolution.CurrentRightHandLandmarks?.Landmark.Count > 0;

        return isConnected && hasHand;
    }

    public bool IsCollectingGesture()
    {
        return isStreaming;
    }

    void OnDestroy()
    {
        if (holisticSolution != null)
        {
            holisticSolution.OnHolisticLandmarksOutput.RemoveListener(OnLandmarks);
        }
        if (ws != null && ws.IsAlive)
        {
            ws.Close();
        }
    }
}