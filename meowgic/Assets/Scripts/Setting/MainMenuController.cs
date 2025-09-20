using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("Arrows (UI)")]
    public RectTransform leftArrow;   // 왼쪽 화살표(UI)
    public RectTransform rightArrow;  // 오른쪽 화살표(UI)

    [Header("Arrows Movement Settings")]
    [SerializeField] private float stepY;   // ↑/↓ 한 번에 움직일 양
    [SerializeField] private float minY;  // 최솟값(아래쪽 한계)
    [SerializeField] private float maxY;  // 최댓값(위쪽 한계)
    [SerializeField] private bool clampToBounds = true; // 범위 밖 이동 방지

    [Header("Runtime State (읽기용)")]
    [SerializeField] private float currentY;   // 현재 Y 위치(anchoredPosition.y)
    [SerializeField] private int currentIndex; // 현재 인덱스(0부터, step 기준)

    public float CurrentY => currentY;
    public int CurrentIndex => currentIndex;
    public int SlotCount => Mathf.RoundToInt((maxY - minY) / stepY) + 1;


    [Header("Row Indexes")]
    [SerializeField] private int resolutionRowIndex = 0; // 1줄: 해상도
    [SerializeField] private int screenModeRowIndex = 1; // 2줄: 화면모드
    [SerializeField] private int initialRowIndex = 0;    // 시작 줄

    // 확인버튼
    [Header("OK(확인) 위치 설정")]
    [SerializeField] private bool hasOkSlot = true;
    [SerializeField] private RectTransform okSlot; // OK 버튼 RectTransform (권장)
    [SerializeField] private float okY = 0f;       // okSlot을 못 쓰면 Y값 직접 입력
    // OK 상태 여부
    [SerializeField] private bool onOk = false;
    // 경계 비교 오차
    const float EPS = 0.001f;


    // 1) 해상도
    [Header("ResolutionOptions")]
    public Vector2Int[] resolutionOptions = new Vector2Int[]
    {
        new Vector2Int(1920, 1080),
        new Vector2Int(1600,  900),
        new Vector2Int(1280,  720),
    };

    [Header("ResolutionState")]
    [SerializeField] private int selectedIndex = 0;
    public Vector2Int CurrentResolution =>
        (resolutionOptions != null && resolutionOptions.Length > 0)
        ? resolutionOptions[Mathf.Clamp(selectedIndex, 0, resolutionOptions.Length - 1)]
        : new Vector2Int(0, 0);

    public TMP_Text textField;             // 해상도 표시 텍스트(TMP)


    // 2) 화면 모드
    [Header("Screen Mode")]
    public TMP_Text screenModeText;               // "전체화면"/"창모드" 표시
    public string[] screenModeOptions = { "전체화면", "창모드" };
    [SerializeField] private int selectedModeIndex = 0;   // 0: 전체화면, 1: 창모드
    bool SelectedFullscreen => selectedModeIndex == FullscreenOptionIndex();


    [Header("포커스(선택)")]
    public GameObject firstSelected; // 매니저에서 못 넘겨줄 때 대비

    const string KEY_RES_INDEX = "Settings.ResolutionIndex";
    const string KEY_FULLSCREEN = "Settings.Fullscreen";

    private int pendingResIndex;
    private bool pendingFullscreen;

    void Awake()
    {
        // 화살표 위치 동기화 + 시작 줄 강제
        SyncFromLeftArrow();
        SetIndex(Mathf.Clamp(initialRowIndex, 0, SlotCount - 1));

        // 저장값 → 보류값
        LoadFromPrefsToPending();

        // 보류값을 현재 선택으로 반영(텍스트만 갱신)
        selectedIndex = Mathf.Clamp(pendingResIndex, 0, Mathf.Max(0, (resolutionOptions?.Length ?? 1) - 1));
        selectedModeIndex = pendingFullscreen ? FullscreenOptionIndex() : WindowedOptionIndex();
        UpdateUIFromPending();   // 텍스트 갱신(실제 적용 X)

    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(+1); // 위로
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(-1); // 아래로

        if (!onOk)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.A)) HandleLeftRight(+1);
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.D)) HandleLeftRight(-1);
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Space)) OnClickConfirm();
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

    /// <summary>dir: +1=위, -1=아래</summary>
    public void Move(int dir)
    {
        if (leftArrow == null || rightArrow == null) return;

        // 1) OK 상태일 때: 위=최상, 아래=최하로 점프
        if (onOk)
        {
            onOk = false;
            if (dir > 0) SetY(minY);
            else SetY(maxY);
            return;
        }

        // 2) 일반 줄: 경계에서 OK로 점프
        bool atTop = Mathf.Abs(currentY - maxY) <= EPS;
        bool atBottom = Mathf.Abs(currentY - minY) <= EPS;

        if (hasOkSlot)
        {
            if (dir > 0 && atTop) { GoOk(); return; }
            if (dir < 0 && atBottom) { GoOk(); return; }
        }

        // 3) 일반 이동 (경계 밖으로는 가지 않음)
        float newY = currentY + dir * stepY;
        if (clampToBounds) newY = Mathf.Clamp(newY, minY, maxY);
        if (Mathf.Approximately(newY, currentY)) return;

        SetY(newY);
    }

    private void GoOk()
    {
        onOk = true;

        // OK의 목표 Y (RectTransform 있으면 그걸 우선)
        float targetY = okSlot != null ? okSlot.anchoredPosition.y : okY;

        // 화살표 위치 세팅
        var lp = leftArrow.anchoredPosition;
        var rp = rightArrow.anchoredPosition;

        //if (useCustomOkX)
        //{
        //    lp.x = okLeftX;
        //    rp.x = okRightX;
        //}
        lp.y = targetY;
        rp.y = targetY;

        leftArrow.anchoredPosition = lp;
        rightArrow.anchoredPosition = rp;
    }

    /// <summary>절대 Y로 이동</summary>
    public void SetY(float y)
    {
        currentY = clampToBounds ? Mathf.Clamp(y, minY, maxY) : y;
        currentIndex = Mathf.RoundToInt((maxY - currentY) / stepY);

        // 두 화살표를 같은 Y로 이동 (X는 그대로 유지)
        var lp = leftArrow.anchoredPosition;
        var rp = rightArrow.anchoredPosition;
        lp.y = currentY; rp.y = currentY;
        leftArrow.anchoredPosition = lp;
        rightArrow.anchoredPosition = rp;
    }

    public void SetIndex(int index)
    {
        index = Mathf.Clamp(index, 0, SlotCount - 1);
        float y = maxY - index * Mathf.Max(1f, stepY);
        SetY(y);
    }

    /// <summary>왼쪽 화살표 현재 위치에서 상태를 동기화(초기화 용)</summary>
    public void SyncFromLeftArrow()
    {
        if (leftArrow == null) return;
        SetY(leftArrow.anchoredPosition.y);
    }

    // === 현재 줄 옵션 좌/우 순환(텍스트만 변경) ===
    void HandleLeftRight(int dir)
    {
        // 해상도 줄
        if (currentIndex == resolutionRowIndex && resolutionOptions != null && resolutionOptions.Length > 0)
        {
            Debug.Log("해상도 변경");
            selectedIndex = WrapIndex(selectedIndex + (dir > 0 ? 1 : -1), resolutionOptions.Length);
            UpdateResolutionLabel();         // 텍스트만 바뀜
            return;
        }
        // 화면모드 줄
        if (currentIndex == screenModeRowIndex && screenModeOptions != null && screenModeOptions.Length >= 2)
        {
            Debug.Log("화면 모드 변경");
            selectedModeIndex = WrapIndex(selectedModeIndex + (dir > 0 ? 1 : -1), screenModeOptions.Length);
            UpdateScreenModeLabel();         // 텍스트만 바뀜
            return;
        }
    }

    public void ClickLeftArrow() { HandleLeftRight(-1); }
    public void ClickRightArrow() { HandleLeftRight(+1); }

    // === 버튼 ===
    public void OnClickConfirm()
    {
        Debug.Log("확인버튼 클릭");

        pendingResIndex = selectedIndex;
        pendingFullscreen = (selectedModeIndex == FullscreenOptionIndex());

        SavePendingToPrefs();

        ApplyPendingToRuntime();

        CloseSettings();
    }

    public void OnClickCancel()
    {
        // 저장된 값으로 되돌림 + 즉시 적용
        Debug.Log("취소버튼 클릭");
        LoadFromPrefsToPending();

        selectedIndex = pendingResIndex;
        selectedModeIndex = pendingFullscreen ? FullscreenOptionIndex() : WindowedOptionIndex();

        UpdateUIFromPending();
        CloseSettings();
    }

    // === 패널 열릴 때(UI만 저장값으로 동기화) ===
    public void RefreshUIFromPrefsOnly()
    {
        LoadFromPrefsToPending();

        selectedIndex = pendingResIndex;
        selectedModeIndex = pendingFullscreen ? FullscreenOptionIndex() : WindowedOptionIndex();

        UpdateUIFromPending();
    }

    // === 내부: 저장/로드 ===
    void LoadFromPrefsToPending()
    {
        int maxIdx = Mathf.Max(0, (resolutionOptions?.Length ?? 1) - 1);
        pendingResIndex = Mathf.Clamp(PlayerPrefs.GetInt(KEY_RES_INDEX, 0), 0, maxIdx);
        pendingFullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, 0) == 1;
    }

    void SavePendingToPrefs()
    {
        PlayerPrefs.SetInt(KEY_RES_INDEX, pendingResIndex);
        PlayerPrefs.SetInt(KEY_FULLSCREEN, pendingFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }


// === 내부: 적용/동기화 ===
void ApplyPendingToRuntime()
    {
        ApplyResolution(pendingResIndex, pendingFullscreen);
    }

    void UpdateUIFromPending()
    {
        UpdateResolutionLabel();
        UpdateScreenModeLabel();
    }

    // === 실제 적용부 ===
    void ApplyResolution(int index, bool fullscreen)
    {
        if (resolutionOptions == null || resolutionOptions.Length == 0) return;

        index = Mathf.Clamp(index, 0, resolutionOptions.Length - 1);
        var size = resolutionOptions[index];

        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            Debug.LogWarning("[Settings] WebGL에선 런타임 해상도 변경이 제한됩니다.");
            return;
        }

    #if UNITY_STANDALONE_OSX
            var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed; // macOS 안전
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

    void CloseSettings()
    {
        Debug.Log("설정창 닫기");
        var mgr = FindObjectOfType<SettingsManager>();
        if (mgr != null) mgr.Close();
    }


    // === 표시 유틸 ===
    void UpdateResolutionLabel()
    {
        if (textField == null || resolutionOptions == null || resolutionOptions.Length == 0) return;
        var r = CurrentResolution;
        textField.text = $"{r.x}x{r.y}";
    }

    void UpdateScreenModeLabel()
    {
        if (screenModeText == null || screenModeOptions == null || screenModeOptions.Length == 0) return;
        selectedModeIndex = Mathf.Clamp(selectedModeIndex, 0, screenModeOptions.Length - 1);
        screenModeText.text = screenModeOptions[selectedModeIndex];
    }

    // === 도우미 ===
    int WrapIndex(int i, int n) => ((i % n) + n) % n;

    int FullscreenOptionIndex()
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

    int WindowedOptionIndex()
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
 }

