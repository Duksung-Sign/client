using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;

public class BattleUIManager : MonoBehaviour
{
    [Header("UI 요소")]
    public Image monsterImage;
    public Button attackButton;
    public Button escapeButton;
    public GameObject skillSelectionPanel;
    public Button[] attackSkillButtons; 
    public Button cancelSkillButton; 
    public Button prevButton;
    public Button nextButton;
    public TextMeshProUGUI statusText;

    private Coroutine statusMessageCoroutine;

    string[] skillNames = {
        "안녕하세요", "반갑습니다", "감사합니다", "괜찮아요",
        "사랑한다", "연락해주세요", "도착하다", "당신",
        "나", "싫다", "좋다", "죄송합니다"
    };

    int currentPage = 0;

    void Awake()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnBattleStateChanged += HandleBattleStateChange;
            BattleEventManager.Instance.OnRequestUpdateStatus += ShowStatusMessage;
        }

    }

    void OnDestroy()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnBattleStateChanged -= HandleBattleStateChange;
            BattleEventManager.Instance.OnRequestUpdateStatus -= ShowStatusMessage;
        }
    }

    void Start()
    {
        InitializeUIState();
        SetupButtonEvents();
    }

    private void InitializeUIState()
    {
        statusText?.gameObject.SetActive(false);
        skillSelectionPanel?.SetActive(false);
    }

    private void SetupButtonEvents()
    {
        attackButton?.onClick.AddListener(() => BattleEventManager.Instance?.PlayerRequestsAttack());
        escapeButton?.onClick.AddListener(() => BattleEventManager.Instance?.AttemptEscape());

        prevButton?.onClick.AddListener(() =>
        {
            if (currentPage > 0) { currentPage--; UpdateSkillPage(); }
        });
        nextButton?.onClick.AddListener(() =>
        {
            if (currentPage < 3) { currentPage++; UpdateSkillPage(); }
        });

        cancelSkillButton?.onClick.AddListener(() =>
        {
            Debug.Log("Cancel button clicked");
            skillSelectionPanel.SetActive(false);  
            ShowMainUI(true);                     
        });
    }

    private void HandleBattleStateChange(BattleState newState)
    {
        ShowMainUI(false);
        ShowSkillPanel(false);
        ShowStatusMessage(null, 0);
        Debug.Log("HandleBattleStateChange: " + newState);
        switch (newState)
        {
            case BattleState.Initializing:
                ShowStatusMessage("미오, 어떤 걸 할까?", 0); 
                break;

            case BattleState.PlayerTurn_AwaitingInput:
                ShowMainUI(true);
                break;

            case BattleState.PlayerTurn_SelectingSkill:
                ShowSkillPanel(true);
                break;

            case BattleState.PlayerTurn_Gesture:
                ShowStatusMessage("미오! 묘어를 분석 중이야!", 0);
                break;

            case BattleState.ActionInProgress:
            case BattleState.MonsterTurn:
                break;

            case BattleState.BattleOver_Victory:
                ShowStatusMessage("냐학! 승리했어!", 0);
                break;

            case BattleState.BattleOver_Defeat:
                ShowStatusMessage("이런, 패배했어.", 0);
                break;

            case BattleState.Escaped:
                ShowStatusMessage("냐냐냥! 도망치자!", 0);
                break;
        }
    }

    private void ShowMainUI(bool show)
    {
        Debug.Log("ShowMainUI called with: " + show); 
        attackButton?.gameObject.SetActive(show);
        escapeButton?.gameObject.SetActive(show);
    }

    private void ShowSkillPanel(bool show)
    {
        skillSelectionPanel?.SetActive(show);
        if (show)
        {
            currentPage = 0;
            UpdateSkillPage();
        }
    }

    private void ShowStatusMessage(string message, float duration = 2.5f)
    {
        if (statusText == null) return;
        if (statusMessageCoroutine != null)
        {
            StopCoroutine(statusMessageCoroutine);
            statusText.DOKill();
        }

        if (string.IsNullOrEmpty(message))
        {
            statusText.gameObject.SetActive(false);
        }
        else
        {
            statusMessageCoroutine = StartCoroutine(ShowMessageCoroutine(message, duration));
        }
    }

    private IEnumerator ShowMessageCoroutine(string message, float duration)
    {
        statusText.text = message;
        statusText.gameObject.SetActive(true);
        statusText.alpha = 0;

        yield return statusText.DOFade(1f, 0.2f).WaitForCompletion();

        if (duration > 0)
        {
            yield return new WaitForSeconds(duration);

            yield return statusText.DOFade(0f, 0.2f).WaitForCompletion();

            statusText.gameObject.SetActive(false);
        }
    }

    void UpdateSkillPage()
    {
        int startSkillIdx = currentPage * 3;
        for (int i = 0; i < attackSkillButtons.Length; i++)
        {
            int skillIdx = startSkillIdx + i;
            if (skillIdx < skillNames.Length)
            {
                attackSkillButtons[i].gameObject.SetActive(true);
                attackSkillButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = skillNames[skillIdx];
                attackSkillButtons[i].onClick.RemoveAllListeners();
                int capturedIdx = skillIdx;
                attackSkillButtons[i].onClick.AddListener(() => SelectSkill(capturedIdx));
            }
            else
            {
                attackSkillButtons[i].gameObject.SetActive(false);
            }
        }
        prevButton.interactable = (currentPage > 0);
        nextButton.interactable = (currentPage < (skillNames.Length / 3 - 1));

        Debug.Log("Updating skill page: " + currentPage);
        for (int i = 0; i < attackSkillButtons.Length; i++)
        {
            Debug.Log($"Button {i} text component: {attackSkillButtons[i].GetComponentInChildren<TextMeshProUGUI>()}");
        }
    }

    void SelectSkill(int idx)
    {
        skillSelectionPanel.SetActive(false);
        BattleEventManager.Instance.SelectPlayerSkill(skillNames[idx]);
    }
}