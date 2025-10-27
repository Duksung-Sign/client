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
    public string serverEndpoint = "ws"; 

    // --- 내부 상태 변수 ---
    private bool isStreaming = false;
    private int lastProcessedFrame = -1;

    // 백그라운드 스레드(웹소켓)의 결과를 메인 스레드(유니티)에서 안전하게 처리하기 위한 큐
    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    public event Action<string> OnPredictionReceived;

    void Start()
    {
        holisticSolution = FindObjectOfType<HolisticTrackingSolution>();
        if (holisticSolution == null)
        {
            Debug.LogError("[WS Sender] HolisticTrackingSolution을 찾을 수 없습니다!");
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

        if (ws != null) { try { ws.Close(); } catch { } ws = null; }

        ws = new WebSocket(url);
        ws.Log.Level = WebSocketSharp.LogLevel.Warn;
        ws.OnOpen += (sender, e) => Debug.Log("✅ OnOpen: 서버에 연결됨");
        ws.OnMessage += (sender, e) => {
            try
            {
                var resp = JsonConvert.DeserializeObject<ServerResponse>(e.Data);
                mainThreadActions.Enqueue(() => {
                    float top = 0f;
                    if (resp?.probabilities != null && resp.probabilities.Count > 0)
                        top = Mathf.Max(0f, (float)System.Linq.Enumerable.Max(resp.probabilities.Values));
                    Debug.Log($"🔥 예측: {resp?.prediction} (≈{top:0.00})");
                    OnPredictionReceived?.Invoke(resp?.prediction ?? "none");
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"JSON 파싱 실패: {ex.Message}\n{e.Data}");
            }
        };
        ws.OnError += (sender, e) => Debug.LogError($"❌ OnError: {e.Message}");
        ws.OnClose += (sender, e) => Debug.LogWarning($"🔌 OnClose: {e.Code} / {e.Reason}");

        Debug.Log($"[WS] Connecting to {url}");
        try
        {
            ws.Connect();
            Debug.Log($"[WS] ReadyState={ws.ReadyState}, IsAlive={ws.IsAlive}");
            bool pong = ws.Ping();
            Debug.Log($"[WS] Ping => {pong}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WS] Connect() EX: {ex.GetType().Name}: {ex.Message}");
        }
    }


    private void OnLandmarks(
        NormalizedLandmarkList pose,
        NormalizedLandmarkList face,
        NormalizedLandmarkList leftHand,
        NormalizedLandmarkList rightHand)
    {
        if (!isStreaming || ws == null || !ws.IsAlive)
        {
            return;
        }

        if (Time.frameCount == lastProcessedFrame) return;
        lastProcessedFrame = Time.frameCount;

        float[] vector = dataProcessor.ProcessAndConvertToVector(pose, face, leftHand, rightHand);
        if (vector == null) return;

        var payload = new FramePayload { frame = vector };
        string jsonPayload = JsonConvert.SerializeObject(payload);
        ws.Send(jsonPayload);
    }

    public void StartStreaming()
    {
        Debug.Log($"🚀 스트리밍 시작 요청. ws null? {ws == null}, alive? {ws?.IsAlive}");
        if (ws == null || !ws.IsAlive)
        {
            StartCoroutine(WaitAndStartStreaming()); // 연결될 때까지 최대 5초 대기
            return;
        }
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
            Debug.Log("✅ 연결 확인됨. 스트리밍 시작");
            isStreaming = true;
        }
        else
        {
            Debug.LogError("❌ WebSocket 연결 실패(타임아웃) — 서버/포트/SG 확인 필요");
        }
    }

    public void StopStreaming()
    {
        Debug.Log("데이터 스트리밍을 중단합니다.");
        isStreaming = false;
    }

    public bool IsMediaPipeReady()
    {
        if (holisticSolution == null) return false;

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