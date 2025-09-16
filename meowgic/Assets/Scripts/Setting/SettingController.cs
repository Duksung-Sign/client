using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using System.Linq;
using TMPro; 

public class SettingsController : MonoBehaviour
{
    [Header("오디오")]
    public AudioMixer masterMixer;
    const string MIXER_MASTER = "MasterVolume";

    [Header("디스플레이")]
    public int defaultResolutionIndex = 0;
    public bool defaultFullscreen = false;

    [Header("기타 기본값")]
    public float defaultMasterDb = -5f;

    [Header("UI Widgets")]
    public Slider masterVolumeSlider;
    public Toggle fullscreenToggle;
    public TMP_Dropdown resolutionDropdown; // 0:1920x1080, 1:1600x900, 2:1280x720

    const string KEY_RES_INDEX = "Settings.ResolutionIndex";
    const string KEY_FULLSCREEN = "Settings.Fullscreen";
    const string KEY_MASTER_DB  = "Settings.MasterDb";

    private float pendingMasterDb;
    private int   pendingResIndex;
    private bool  pendingFullscreen;

    // 드롭다운 인덱스 → 실제 해상도 매핑 (원하는 옵션으로 수정 가능)
    static readonly Vector2Int[] RES_OPTS = new[]
    {
        new Vector2Int(1920,1080),
        new Vector2Int(1600, 900),
        new Vector2Int(1280, 720),
    };

    void Awake()
    {
        LoadFromPrefsToPending();
        ApplyPendingToRuntime();
        UpdateUIFromPending();
    }

    // === UI 이벤트(즉시 저장 금지) ===
    public void OnChangeMasterVolume(float db)
    {
        pendingMasterDb = db;
        ApplyMasterVolume(db);
    }

    public void OnChangeFullscreen(bool full)
    {
        pendingFullscreen = full;
        ApplyResolution(pendingResIndex, pendingFullscreen);
    }

    public void OnChangeResolution(int resIndex)
    {
        pendingResIndex = Mathf.Clamp(resIndex, 0, RES_OPTS.Length-1);
        ApplyResolution(pendingResIndex, pendingFullscreen);
    }

    // === 버튼 ===
    public void OnClickConfirm()
    {
        SavePendingToPrefs();
        CloseSettings();
    }

    public void OnClickCancel()
    {
        // 저장된 값으로 되돌림 + 즉시 적용
        LoadFromPrefsToPending();
        ApplyPendingToRuntime();
        UpdateUIFromPending();   // UI도 복구
        CloseSettings();
    }

    // === 패널이 열릴 때(UI를 현재 저장값으로 동기화) ===
    public void RefreshUIFromPrefsOnly()
    {
        // PlayerPrefs 값을 읽어서 UI만 동기화.
        // (런타임 적용은 패널을 열 때마다 다시 할 필요는 없음)
        LoadFromPrefsToPending();
        UpdateUIFromPending();
    }

    // === 내부: 저장/로드 ===
    void LoadFromPrefsToPending()
    {
        pendingResIndex   = PlayerPrefs.GetInt(KEY_RES_INDEX, defaultResolutionIndex);
        pendingResIndex   = Mathf.Clamp(pendingResIndex, 0, RES_OPTS.Length-1);
        pendingFullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, defaultFullscreen ? 1 : 0) == 1;
        pendingMasterDb   = PlayerPrefs.GetFloat(KEY_MASTER_DB, defaultMasterDb);
    }

    void SavePendingToPrefs()
    {
        PlayerPrefs.SetInt(KEY_RES_INDEX, pendingResIndex);
        PlayerPrefs.SetInt(KEY_FULLSCREEN, pendingFullscreen ? 1 : 0);
        PlayerPrefs.SetFloat(KEY_MASTER_DB, pendingMasterDb);
        PlayerPrefs.Save();
    }

    // === 내부: 적용/동기화 ===
    void ApplyPendingToRuntime()
    {
        ApplyMasterVolume(pendingMasterDb);
        ApplyResolution(pendingResIndex, pendingFullscreen);
    }

    void UpdateUIFromPending()
    {
        if (masterVolumeSlider) masterVolumeSlider.SetValueWithoutNotify(pendingMasterDb);
        if (fullscreenToggle)   fullscreenToggle.SetIsOnWithoutNotify(pendingFullscreen);
        if (resolutionDropdown) resolutionDropdown.SetValueWithoutNotify(pendingResIndex);
    }

    // === 실제 적용부 ===
    void ApplyMasterVolume(float db)
    {
        if (masterMixer != null)
            masterMixer.SetFloat(MIXER_MASTER, db);
        else
            AudioListener.volume = Mathf.Clamp01(Mathf.Pow(10f, db / 20f));
    }

    void ApplyResolution(int index, bool fullscreen)
    {
        index = Mathf.Clamp(index, 0, RES_OPTS.Length-1);
        var size = RES_OPTS[index];

        // ⚠️ 주의: 에디터나 WebGL에서는 Screen.SetResolution이 제한됨
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            Debug.LogWarning("[Settings] WebGL에서는 런타임 해상도 변경이 제한됩니다. (Canvas/CSS 크기 조정 필요)");
            return;
        }

        // FullScreenMode를 명시하여 확실히 적용
        var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.fullScreenMode = mode;
        Screen.SetResolution(size.x, size.y, mode);

        // 디버그 로그 (적용 확인)
        Debug.Log($"[Settings] Resolution Applied: {size.x}x{size.y}, Fullscreen={fullscreen}, Mode={mode}");
    }

    void CloseSettings()
    {
        var mgr = FindObjectOfType<SettingsManager>();
        if (mgr != null) mgr.Toggle();
    }

    // (참고) 현재 런타임 해상도와 가장 가까운 옵션의 인덱스를 찾고 싶을 때
    int FindClosestResIndexFromRuntime()
    {
        var w = Screen.width;
        var h = Screen.height;
        int best = 0;
        int bestScore = int.MaxValue;
        for (int i = 0; i < RES_OPTS.Length; i++)
        {
            int dx = RES_OPTS[i].x - w;
            int dy = RES_OPTS[i].y - h;
            int score = dx*dx + dy*dy;
            if (score < bestScore) { bestScore = score; best = i; }
        }
        return best;
    }
}
