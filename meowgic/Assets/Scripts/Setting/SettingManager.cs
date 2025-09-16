using UnityEngine;
using UnityEngine.EventSystems;

public class SettingsManager : MonoBehaviour
{
    public GameObject settingsUI;    // Canvas 루트
    public GameObject firstSelected;

    public SettingsController controller;

    bool isOpen = false;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // 1)첫 로딩 시 비활성화 처리
        settingsUI.SetActive(false);  // ESC 전까지 완전 비활성(렌더X, 클릭X)

        // (선택) 설정값만 로드해서 적용하고 싶으면 여기서 호출
        // GetComponent<SettingsController>()?.LoadAndApplyAll();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Toggle();
    }

    public void Toggle()
    {
        isOpen = !isOpen;
        if (isOpen) ShowUI();
        else settingsUI.SetActive(false);
        PauseOrResume(isOpen);
    }

    void ShowUI()
    {
        // ★ 열릴 때 UI를 저장된 값으로 맞춰둔다
        if (controller != null) controller.RefreshUIFromPrefsOnly();
        
        settingsUI.SetActive(true);

        //canvasGroup.alpha = 1f;
        //canvasGroup.interactable = true;
        //canvasGroup.blocksRaycasts = true;
        EventSystem.current?.SetSelectedGameObject(firstSelected);
    }

    void PauseOrResume(bool pause)
    {
        Time.timeScale = pause ? 0f : 1f;
        AudioListener.pause = pause;
        Cursor.visible = pause;
        Cursor.lockState = pause ? CursorLockMode.None : CursorLockMode.Locked;
    }

//#if ENABLE_INPUT_SYSTEM
//    using UnityEngine.InputSystem;
//#endif

//    void PauseOrResume(bool pause)
//    {
//        if (pause)
//        {
//            // === 일시정지 ===
//            Time.timeScale = 0f;
//            AudioListener.pause = true;

//            Cursor.visible = true;
//            Cursor.lockState = CursorLockMode.None;

//            // ★ InputSystem 액션맵 전환
//#if ENABLE_INPUT_SYSTEM
//                var pi = FindObjectOfType<PlayerInput>();
//                if (pi) pi.SwitchCurrentActionMap("UI");   // UI용 액션맵으로 전환
//#endif
//        }
//        else
//        {
//            // === 재개 ===
//            Time.timeScale = 1f;
//            AudioListener.pause = false;

//            Cursor.visible = false;
//            Cursor.lockState = CursorLockMode.Locked;

//            // ★ InputSystem 액션맵 전환 복귀
//#if ENABLE_INPUT_SYSTEM
//                var pi = FindObjectOfType<PlayerInput>();
//                if (pi) pi.SwitchCurrentActionMap("Gameplay"); // 다시 게임용 액션맵으로 전환
//#endif
//        }
//    }
}
