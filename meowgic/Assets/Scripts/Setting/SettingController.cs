using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
public class SettingsController : MonoBehaviour
{
    [Header("Arrow Cursor")]
    [SerializeField] private ArrowCursor arrows; // 공통 네비게이터
    [Header("Rows (Index)")]
    [SerializeField] private int resolutionRowIndex = 0; // 1줄: 해상도
    [SerializeField] private int screenModeRowIndex = 1; // 2줄: 화면모드
    [SerializeField] private int initialRowIndex = 0; // 시작 줄
    [Header("Resolution Options")]
    public Vector2Int[] resolutionOptions = new Vector2Int[]
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1280, 720),
    };
    [SerializeField] private int selectedResIndex = 0; // UI상 표시용(보류 아님)
    public Vector2Int CurrentResolution =>
    (resolutionOptions != null && resolutionOptions.Length > 0)
    ? resolutionOptions[Mathf.Clamp(selectedResIndex, 0, resolutionOptions.Length - 1)]
    : new Vector2Int(0, 0);
    [Header("Screen Mode")]
    public TMP_Text screenModeText; // "전체화면"/"창모드" 표시
    public string[] screenModeOptions = { "전체화면", "창모드" };
    [SerializeField] private int selectedModeIndex = 0; // 0: 전체화면, 1: 창모드

    [Header("Labels")]
    public TMP_Text resolutionText; // 해상도 표시 텍스트(TMP)
    [Header("Focus (선택)")]
    public GameObject firstSelected; // 매니저에서 못 넘겨줄 때 대비
                                     // PlayerPrefs 키
    const string KEY_RES_INDEX = "Settings.ResolutionIndex";
    const string KEY_FULLSCREEN = "Settings.Fullscreen";
    // 저장값을 메모리에 보류
    private int pendingResIndex;
    private bool pendingFullscreen;
    private int FullscreenOptionIndex()
    {
        for (int i = 0; i < screenModeOptions.Length; i++)
        {
            var s = screenModeOptions[i];
            if (string.IsNullOrWhiteSpace(s)) continue;
            s = s.Trim().ToLower();
            if (s.Contains("전체") || s.Contains("full")) return i;
        }
        return 0;
    }
    private int WindowedOptionIndex()
    {
        for (int i = 0; i < screenModeOptions.Length; i++)
        {
            var s = screenModeOptions[i];
            if (string.IsNullOrWhiteSpace(s)) continue;
            s = s.Trim().ToLower();
            if (s.Contains("창") || s.Contains("window")) return i;
        }
        return Mathf.Min(1, Mathf.Max(0, screenModeOptions.Length - 1));
    }
    private bool SelectedFullscreen => selectedModeIndex == FullscreenOptionIndex();
    void Awake()
    {
        // 화살표 위치 동기화 + 시작 줄 강제
        arrows?.SyncFromLeftArrow();
        if (arrows != null)
        {
            int start = Mathf.Clamp(initialRowIndex, 0, arrows.SlotCount - 1);
            arrows.SetIndex(start);
        }
        
        // 저장값 → 보류값
        LoadFromPrefsToPending();
        // 보류값을 UI 선택으로 반영(텍스트만 갱신)
        selectedResIndex = Mathf.Clamp(pendingResIndex, 0, Mathf.Max(0,
        (resolutionOptions?.Length ?? 1) - 1));
        selectedModeIndex = pendingFullscreen ? FullscreenOptionIndex() : WindowedOptionIndex();
        UpdateUIFromSelection();
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) arrows?.Move(+1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            arrows?.Move(-1);
        if (arrows == null) return;
        // 일반 줄에서 좌/우
        if (!arrows.OnOk && !arrows.OnBack)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                HandleLeftRight(+1);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                HandleLeftRight(-1);
        }
        else if (!arrows.OnOk && arrows.OnBack)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) OnClickBack();
        }
        else // OnOk
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                OnClickConfirm();
        }
    }

    // === 매니저에서 열릴 때 호출(권장) ===
    public void OnOpen()
    {
        RefreshUIFromPrefsOnly();
        // 첫 포커스 주기(키보드/패드 네비)
        if (firstSelected != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }
    }

    public void OnClose() { /* 필요 시 추가 */ }

    // === 현재 줄 옵션 좌/우 순환(텍스트만 변경) ===
    private void HandleLeftRight(int dir)
    {
        if (arrows == null) return;
        // 해상도 줄
        if (arrows.CurrentIndex == resolutionRowIndex && resolutionOptions != null &&
        resolutionOptions.Length > 0)
        {
            selectedResIndex = WrapIndex(selectedResIndex + (dir > 0 ? 1 : -1), resolutionOptions.Length);
            UpdateResolutionLabel();
            return;
        }
        // 화면모드 줄
        if (arrows.CurrentIndex == screenModeRowIndex && screenModeOptions != null &&
        screenModeOptions.Length >= 2)
        {
            selectedModeIndex = WrapIndex(selectedModeIndex + (dir > 0 ? 1 : -1),
            screenModeOptions.Length);
            UpdateScreenModeLabel();
            return;
        }
    }

    public void OnClickLeftArrow() => HandleLeftRight(-1);
    public void OnClickRightArrow() => HandleLeftRight(+1);

    // === 버튼 ===
    public void OnClickConfirm()
    {
        Debug.Log("[Settings] 확인 클릭");
        pendingResIndex = selectedResIndex;
        pendingFullscreen = (selectedModeIndex == FullscreenOptionIndex());
        SavePendingToPrefs();
        ApplyPendingToRuntime();
        CloseSettings();
    }

    public void OnClickCancel()
    {
        Debug.Log("[Settings] 취소 클릭");
        LoadFromPrefsToPending();
        selectedResIndex = pendingResIndex;
        selectedModeIndex = pendingFullscreen ? FullscreenOptionIndex() : WindowedOptionIndex();
        UpdateUIFromSelection();
        SettingsManager.Instance?.Close();
    }

public void OnClickBack()
    {
        var menu = FindObjectOfType<MenuController>();
        if (menu) menu.ShowMainMenu();
    }

    // === 패널 열릴 때(UI만 저장값으로 동기화) ===
    public void RefreshUIFromPrefsOnly()
    {
        LoadFromPrefsToPending();
        selectedResIndex = pendingResIndex;
        selectedModeIndex = pendingFullscreen ? FullscreenOptionIndex() : WindowedOptionIndex();
        UpdateUIFromSelection();
    }

    // === 내부: 저장/로드 ===
    private void LoadFromPrefsToPending()
    {
        int maxIdx = Mathf.Max(0, (resolutionOptions?.Length ?? 1) - 1);
        pendingResIndex = Mathf.Clamp(PlayerPrefs.GetInt(KEY_RES_INDEX, 0), 0, maxIdx);
        pendingFullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, 0) == 1;
    }

    private void SavePendingToPrefs()
    {
        PlayerPrefs.SetInt(KEY_RES_INDEX, pendingResIndex);
        PlayerPrefs.SetInt(KEY_FULLSCREEN, pendingFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    // === 내부: 적용/동기화 ===
    private void ApplyPendingToRuntime()
    {
        ApplyResolution(pendingResIndex, pendingFullscreen);
    }

    private void UpdateUIFromSelection()
    {
        UpdateResolutionLabel();
        UpdateScreenModeLabel();
    }

    // === 실제 적용부 ===
    private void ApplyResolution(int index, bool fullscreen)
    {
        if (resolutionOptions == null || resolutionOptions.Length == 0) return;
        index = Mathf.Clamp(index, 0, resolutionOptions.Length - 1);
        var size = resolutionOptions[index];
        
    if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            Debug.LogWarning("[Settings] WebGL에선 런타임 해상도 변경이 제한됩니다.");
            return;
        }
        // 먼저 모드 설정 → 그 다음 해상도 적용 (플랫폼별 안전 순서)
#if UNITY_STANDALONE_OSX
var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed; //
macOS 안전
#else
        var mode = fullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;
#endif
        Screen.fullScreenMode = mode;
#if UNITY_2022_2_OR_NEWER
        var rr = Screen.currentResolution.refreshRateRatio;
        Screen.SetResolution(size.x, size.y, mode, rr);
#else
var hz = Screen.currentResolution.refreshRate;
Screen.SetResolution(size.x, size.y, mode, hz);
#endif
        Debug.Log($"[Settings] Applied: {size.x}x{size.y}, Fullscreen={fullscreen}, Mode={mode}");
    }

    private void CloseSettings()
    {
        var mgr = FindObjectOfType<SettingsManager>();
        if (mgr != null) mgr.Close();
    }

    // === 표시 유틸 ===
    private void UpdateResolutionLabel()
    {
        if (resolutionText == null || resolutionOptions == null || resolutionOptions.Length == 0) return;
        var r = CurrentResolution;
        resolutionText.text = $"{r.x}x{r.y}";
    }

    private void UpdateScreenModeLabel()
    {
        if (screenModeText == null || screenModeOptions == null || screenModeOptions.Length == 0)
            return;
        selectedModeIndex = Mathf.Clamp(selectedModeIndex, 0, screenModeOptions.Length - 1);
        screenModeText.text = screenModeOptions[selectedModeIndex];
    }
    // === 도우미 ===
    private int WrapIndex(int i, int n) => ((i % n) + n) % n;

    // 마우스 호버 → 동일 부모 내에서 화살표 이동
    public void HoverToSameParent(RectTransform target)
    {
        arrows?.FocusSibling(target);
    }
}