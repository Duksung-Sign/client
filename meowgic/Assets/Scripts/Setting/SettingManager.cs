using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;


public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("Assign in Inspector")]
    [SerializeField] private GameObject settingsPrefab;          // ✅ 프리팹(설계도)
    [SerializeField] private bool openBlocked = false;

    private GameObject settingsUIInstance;   // ✅ 런타임 인스턴스
    private SettingsController settingController;
    private MenuController mainMenuController;

    private bool isOpen = false;

    [Header("Scene rules")]
    [SerializeField] private string titleSceneName = "MainMenu";  // 타이틀 씬 이름
    [SerializeField] private bool destroyUIOnTitle = false;      // 타이틀에서 프리팹 인스턴스 완전 파기할지

    private GameState previousState;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 미리 만들어 숨겨두고 싶다면 ↓ 주석 해제
        // CreateUIIfNeeded();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isOpen) Close();
            else if (!openBlocked) Open();   // ★ 차단 중이면 무시
        }
    }

    public void Open()
    {
        if (openBlocked)                    // ★ 방어적 체크(직접 Open 호출 대비)
        {
            Debug.Log("[SettingsManager] Open blocked.");
            return;
        }

        previousState = GameManager.Instance.CurrentState;
        GameEventManager.Instance.TriggerGameStateChanged(GameState.Menu);
        Time.timeScale = 0f;

        CreateUIIfNeeded();
        GuardEventSystem();

        if (settingsUIInstance == null) return;

        mainMenuController?.ShowMainMenu();

        settingsUIInstance.SetActive(true);
        isOpen = true;
    }

    public void Close()
    {
        if (!isOpen || settingsUIInstance == null) return;

        GameEventManager.Instance.TriggerGameStateChanged(previousState);
        Time.timeScale = 1f;

        settingsUIInstance.SetActive(false);
        isOpen = false;
    }

    // 외부에서 켜고 끄기 쉽게 메서드 제공
    public void SetOpenBlocked(bool blocked)
    {
        openBlocked = blocked;
        // 막는 순간 이미 열려 있으면 닫아주기(원치 않으면 이 줄 삭제)
        if (blocked && isOpen) Close();
    }

    // 필요할 때만 프리팹을 인스턴스화
    private void CreateUIIfNeeded()
    {
        if (settingsUIInstance != null) return;

        if (settingsPrefab == null)
        {
            Debug.LogError("[SettingsManager] settingsPrefab이 비어있습니다.");
            return;
        }

        settingsUIInstance = Instantiate(settingsPrefab);
        DontDestroyOnLoad(settingsUIInstance);

        //settingController = settingsUIInstance.GetComponent<SettingsController>();
        settingController = settingsUIInstance.GetComponentInChildren<SettingsController>(true);
        mainMenuController = settingsUIInstance.GetComponentInChildren<MenuController>(true);

        settingsUIInstance.SetActive(false);

        // 🔸 항상 맨 앞에 보이도록: 자체 Canvas + 높은 정렬
        var canvas = settingsUIInstance.GetComponent<Canvas>();
        if (canvas == null) canvas = settingsUIInstance.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; // 가장 앞 레이어
        canvas.overrideSorting = true;                     // 부모 정렬 무시
        canvas.sortingOrder = 9999;                        // 충분히 높은 값

        // UI 클릭 가능하게
        if (settingsUIInstance.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            settingsUIInstance.AddComponent<UnityEngine.UI.GraphicRaycaster>();
    }

    private void GuardEventSystem()
    {
        if (EventSystem.current != null) return;

        Debug.LogWarning("[Settings] EventSystem이 씬에 없습니다. UI 입력이 동작하지 않습니다.");
        // 필요하면 응급 생성:
        var go = new GameObject("EventSystem (Auto)");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>(); // 새 입력 시스템이면 InputSystemUIInputModule
        Debug.Log("이벤트시스템 생성됨");
    }

    // 선택: 닫을 때 UI를 아예 없애고 싶다면 이 메서드 사용
    public void CloseAndDestroy()
    {
        Close();
        if (settingsUIInstance != null)
        {
            Destroy(settingsUIInstance);
            settingsUIInstance = null;
            settingController = null;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded; // 해제
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 타이틀 씬이면: 열기 차단 + 닫기(+원하면 파기)
        if (!string.IsNullOrEmpty(titleSceneName) && scene.name == titleSceneName)
        {
            // 앞으로 열기 금지
            SetOpenBlocked(true);

            // 이미 떠 있으면 닫기/파기
            if (destroyUIOnTitle) CloseAndDestroy();
            else Close();

            return;
        }

        // 타이틀이 아니면 다시 열 수 있게 풀어주기(필요 시)
        SetOpenBlocked(false);
    }
}
