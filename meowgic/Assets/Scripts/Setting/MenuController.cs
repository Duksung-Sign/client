using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
public class MenuController : MonoBehaviour
{
    [Header("Arrow Cursor")]
    [SerializeField] private ArrowCursor arrows; // 공통 네비게이터 (wrap 활성 권장)
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel; // MainMenuPanel
    [SerializeField] private GameObject settingsPanel; // SettingsPanel
    [Header("First Selected")]
    [SerializeField] private GameObject firstSelectedMain; // 메인 메뉴 첫 포커스(예: SettingLabel or CancelButton)
    [SerializeField] private GameObject firstSelectedSettings; // 설정 화면 첫 포커스(예: resolutionLabel)
    [Header("Controllers")]
    [SerializeField] private SettingsController settingsController;
    private bool IsOnSettings => settingsPanel && settingsPanel.activeSelf;

    private string titleSceneName = "MainMenu";

    void Awake()
    {
        arrows?.SyncFromLeftArrow();
        if (arrows != null) arrows.SetIndex(0);
    }
    void Update()
    {
        if (IsOnSettings) return; // 설정 화면에서는 여기 입력 막기
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) arrows?.Move(+1);

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            arrows?.Move(-1);
        if (!gameObject.activeInHierarchy) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel && settingsPanel.activeSelf) ShowMainMenu();
            else SettingsManager.Instance?.Close();
        }
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log($"[Menu] Space pressed at index={arrows?.CurrentIndex}");
            if (arrows == null) return; 
            if (arrows.CurrentIndex == 0) // 첫 번째 줄: 설정
            {
                ShowSettings();
            }
            else if (arrows.CurrentIndex == 1) // 두 번째 줄: 타이틀로
            {
                OnClickQuitToTitle();
            }
            else if (arrows.CurrentIndex == 2) // 세 번쨰 줄: 게임으로 돌아감
            {
                OnClickBack();
            }
        }
    }

    // === 매니저에서 열릴 때 호출(권장) ===
    public void OnOpen()
    {
        // 첫 포커스 주기(키보드/패드 네비)
        if (firstSelectedMain != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelectedMain);
        }
    }
    public void OnClose() { /* 필요 시 추가 */ }
    private void ClearSelection()
    {
        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(null);
    }

    // === 외부(매니저)에서 호출: 열리면 메인 먼저 보이기 ===
    public void ShowMainMenu()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (settingsController) settingsController.enabled = false;
        ClearSelection();
        Focus(firstSelectedMain);
    }

    // === 버튼: 설정 진입 ===
    public void ShowSettings()
    {
        ClearSelection();
        settingsController?.RefreshUIFromPrefsOnly();
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
        if (settingsController) settingsController.enabled = true;
        Focus(firstSelectedSettings ? firstSelectedSettings : settingsController?.firstSelected);
    }

    // === 버튼: 타이틀로 ===
    public void OnClickQuitToTitle()
    {
        SettingsManager.Instance?.Close();
        if (!string.IsNullOrEmpty(titleSceneName))
        {
            if(GameDataManager.Instance != null)
            {
                GameDataManager.Instance.SaveState();
                GameEventManager.Instance.TriggerGameStateChanged(GameState.Loading);
                FadeManager.Instance.LoadSceneWithFade("MainMenu");
            }
        }
    }

    // === 설정 화면의 '뒤로' 버튼 ===
    public void OnClickBack()
    {
        SettingsManager.Instance?.Close();
    }

    private void Focus(GameObject go)
    {
        if (!go || !EventSystem.current) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(go);
    }
    public void HoverToSameParent(RectTransform target)
    {
        arrows?.FocusSibling(target);
    }
}
