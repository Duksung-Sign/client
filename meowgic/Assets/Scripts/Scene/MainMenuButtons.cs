using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

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

    [Header("화살표 오브젝트")]
    [SerializeField] private RectTransform arrowLeft;
    [SerializeField] private RectTransform arrowRight;
    [SerializeField] private float arrowOffsetX = 200f;

    private void Start()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.AddListener(OnNewGameClicked);
            UpdateButtonTextColor(newGameButton, true);
            AddArrowEvents(newGameButton);
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
            AddArrowEvents(continueButton);
        }

        if (quitGameButton != null)
        {
            quitGameButton.onClick.AddListener(OnQuitGameClicked);
            UpdateButtonTextColor(quitGameButton, true);
            AddArrowEvents(quitGameButton);
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
    private void AddArrowEvents(Button btn)
    {
        EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) => ShowArrows(btn));
        trigger.triggers.Add(entryEnter);

        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) => HideArrows());
        trigger.triggers.Add(entryExit);
    }

    private void ShowArrows(Button btn)
    {
        RectTransform btnRect = btn.GetComponent<RectTransform>();
        Vector2 btnPos = btnRect.anchoredPosition;
        arrowLeft.anchoredPosition = new Vector2(btnPos.x - arrowOffsetX, btnPos.y);
        arrowRight.anchoredPosition = new Vector2(btnPos.x + arrowOffsetX, btnPos.y);
        arrowLeft.gameObject.SetActive(true);
        arrowRight.gameObject.SetActive(true);
    }


    private void HideArrows()
    {
        arrowLeft.gameObject.SetActive(false);
        arrowRight.gameObject.SetActive(false);
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