using UnityEngine;
using UnityEngine.UI;

public class MainMenuButtons : MonoBehaviour
{
    [Header("UI 버튼")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitGameButton; 

    [Header("새 게임 설정")]
    [SerializeField] private string startingStoryID = "library_intro";

    private void Start()
    {
        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGameClicked);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);

        if (quitGameButton != null) 
            quitGameButton.onClick.AddListener(OnQuitGameClicked);

        CheckForSaveData();
    }

    private void CheckForSaveData()
    {
        if (continueButton == null) return;

        if (PlayerPrefs.HasKey("CurrentStoryID"))
        {
            continueButton.interactable = true;
        }
        else
        {
            continueButton.interactable = false;
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