using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
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

    // 경계 비교 오차
    const float EPS = 0.001f;

    [Header("포커스(선택)")]
    public GameObject firstSelected; // 매니저에서 못 넘겨줄 때 대비


    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;   // MainMenuPanel
    [SerializeField] private GameObject settingsPanel;   // SettingPanel

    [Header("First Selected")]
    [SerializeField] private GameObject firstSelectedMain;     // 메인 메뉴 첫 포커스(예: SettingLabel or CancelButton)
    [SerializeField] private GameObject firstSelectedSettings; // 설정 화면 첫 포커스(예: resolutionLabel)

    [Header("Controllers")]
    [SerializeField] private SettingsController settingsController;

    private bool IsOnSettings => settingsPanel && settingsPanel.activeSelf;

    void Awake()
    {
        // 화살표 위치 동기화 + 시작 줄 강제
        SyncFromLeftArrow();
        SetIndex(Mathf.Clamp(0, 0, SlotCount - 1));

    }

    private void Update()
    {
        if (IsOnSettings) return;

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(+1); // 위로
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(-1); // 아래로

        if (!gameObject.activeInHierarchy) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel && settingsPanel.activeSelf) ShowMainMenu();
            else SettingsManager.Instance?.Close();
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
        {
            if (currentIndex == 0)       // 첫 번째 줄
            {
                ShowSettings();
            }
            else if (currentIndex == 1)  // 두 번째 줄
            {
                OnClickQuitToTitle();
            }
        }

    }

    // === 매니저에서 열릴 때 호출(권장) ===
    public void OnOpen()
    {
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

        // 3) 일반 이동 (경계 밖으로는 가지 않음)
        float newY = currentY + dir * stepY;
        if (newY < minY) newY = maxY;
        if (newY > maxY) newY = minY;
        if (Mathf.Approximately(newY, currentY)) return;

        SetY(newY);
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

        // 저장→UI 동기화해서 최신값 보이기
        settingsController?.RefreshUIFromPrefsOnly();

        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);

        if (settingsController) settingsController.enabled = true;

        Focus(firstSelectedSettings ? firstSelectedSettings : settingsController?.firstSelected);
    }

    // === 버튼: 계속하기 ===
    public void OnClickContinue()
    {
        SettingsManager.Instance?.Close();
    }

    // === 버튼: 타이틀로 ===
    public void OnClickQuitToTitle()
    {
        SettingsManager.Instance?.Close();
        if (!string.IsNullOrEmpty("MainMenu"))
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

    // === 설정 화면의 '뒤로' 버튼 ===
    public void OnClickBack()
    {
        ShowMainMenu();
    }

    private void Focus(GameObject go)
    {
        if (!go || !EventSystem.current) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(go);
    }


    // === 실제 적용부 ===

    public void CloseSettings()
    {
        Debug.Log("설정창 닫기");
        var mgr = FindObjectOfType<SettingsManager>();
        if (mgr != null) mgr.Close();
    }
 }

