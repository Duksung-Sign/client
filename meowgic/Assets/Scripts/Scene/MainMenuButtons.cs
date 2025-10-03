using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuButtons : MonoBehaviour
{
    [Header("UI 버튼")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitGameButton;

    [Header("새 게임 설정")]
    [SerializeField] private string startingStoryID = "library_intro";

    [Header("텍스트 색상 설정")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color disabledTextColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    private void Start()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.AddListener(OnNewGameClicked);
            UpdateButtonTextColor(newGameButton, true); 
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (quitGameButton != null)
        {
            quitGameButton.onClick.AddListener(OnQuitGameClicked);
            UpdateButtonTextColor(quitGameButton, true); 
        }

        CheckForSaveData();
    }

    private void CheckForSaveData()
    {
        if (continueButton == null) return;

        bool hasSaveData = PlayerPrefs.HasKey("CurrentStoryID");
        continueButton.interactable = hasSaveData;
        UpdateButtonTextColor(continueButton, hasSaveData);
    }

    private void UpdateButtonTextColor(Button button, bool isEnabled)
    {
        TextMeshProUGUI tmpText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (tmpText != null)
        {
            tmpText.color = isEnabled ? normalTextColor : disabledTextColor;
            return;
        }

        Text uiText = button.GetComponentInChildren<Text>();
        if (uiText != null)
        {
            uiText.color = isEnabled ? normalTextColor : disabledTextColor;
        }
    }

    public void OnNewGameClicked()
    {
        Debug.Log("[MainMenu] 새 게임 시작을 요청합니다.");
        StoryProgressManager.Instance.StartNewGame(startingStoryID);
    }

    public void OnContinueClicked()
    {
        Debug.Log("[MainMenu] 이어하기를 요청합니다.");
        StoryProgressManager.Instance.ContinueStory();
    }

    public void OnQuitGameClicked()
    {
        Debug.Log("[MainMenu] 게임 상태 저장 후 종료를 요청합니다.");
        GameDataManager.Instance.SaveState();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}