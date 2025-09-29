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
    public Button helloSkillButton;
    public Button sorrySkillButton;
    public Button thankSkillButton;
    public TextMeshProUGUI statusText;

    private Coroutine statusMessageCoroutine;

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
        helloSkillButton?.onClick.AddListener(() => BattleEventManager.Instance?.SelectPlayerSkill("hello"));
        sorrySkillButton?.onClick.AddListener(() => BattleEventManager.Instance?.SelectPlayerSkill("sorry"));
        thankSkillButton?.onClick.AddListener(() => BattleEventManager.Instance?.SelectPlayerSkill("thank"));
    }

    private void HandleBattleStateChange(BattleState newState)
    {
        ShowMainUI(false);
        ShowSkillPanel(false);
        ShowStatusMessage(null, 0);

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
        attackButton?.gameObject.SetActive(show);
        escapeButton?.gameObject.SetActive(show);
    }

    private void ShowSkillPanel(bool show)
    {
        skillSelectionPanel?.SetActive(show);
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
}