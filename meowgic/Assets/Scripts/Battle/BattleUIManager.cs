using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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

    [Header("채널 1: 캐릭터 대사 UI")]
    public DynamicSpeechBubble canSpeechBubble;
    [Header("채널 2: 튜토리얼 UI")]
    public DynamicSpeechBubble tutorSpeechBubble;
    [Header("채널 3: 시스템 상태 UI")]
    public TextMeshProUGUI statusText;

    private Coroutine hideDialogueCoroutine;

    void Awake()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnBattleStateChanged += HandleBattleStateChange;

            BattleEventManager.Instance.OnRequestShowCharacterDialogue += ShowCharacterDialogue;
            BattleEventManager.Instance.OnRequestUpdateStatus += UpdateStatusText;
        }
    }

    void OnDestroy()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnBattleStateChanged -= HandleBattleStateChange;
            BattleEventManager.Instance.OnRequestShowCharacterDialogue -= ShowCharacterDialogue;
            BattleEventManager.Instance.OnRequestUpdateStatus -= UpdateStatusText;
        }
    }

    void Start()
    {
        InitializeUIState();
        SetupButtonEvents();
    }

    private void InitializeUIState()
    {
        canSpeechBubble?.gameObject.SetActive(false);
        tutorSpeechBubble?.gameObject.SetActive(false);
        statusText?.gameObject.SetActive(false);
        skillSelectionPanel?.SetActive(false);
    }

    private void SetupButtonEvents()
    {
        attackButton?.onClick.AddListener(OnAttackButtonClicked);
        escapeButton?.onClick.AddListener(OnEscapeButtonClicked);
        helloSkillButton?.onClick.AddListener(() => OnSkillButtonClicked("hello"));
        sorrySkillButton?.onClick.AddListener(() => OnSkillButtonClicked("sorry"));
        thankSkillButton?.onClick.AddListener(() => OnSkillButtonClicked("thank"));
    }

    private void HandleBattleStateChange(BattleState newState)
    {
        ShowMainUI(false);
        ShowSkillPanel(false);
        UpdateStatusText(null); 

        switch (newState)
        {
            case BattleState.Initializing:
                UpdateStatusText("전투 준비 중...");
                break;

            case BattleState.PlayerTurn_AwaitingInput:
                ShowMainUI(true);
                break;

            case BattleState.PlayerTurn_SelectingSkill:
                ShowSkillPanel(true);
                break;

            case BattleState.PlayerTurn_Gesture:
                UpdateStatusText("제스처를 인식 중...");
                break;

            case BattleState.ActionInProgress:
            case BattleState.MonsterTurn:
                break;

            case BattleState.BattleOver_Victory:
                ShowCharacterDialogue("승리했어!");
                break;

            case BattleState.BattleOver_Defeat:
                ShowCharacterDialogue("패배했어...");
                break;

            case BattleState.Escaped:
                ShowCharacterDialogue("무사히 도망쳤다!");
                break;
        }
    }

    void OnAttackButtonClicked()
    {
        BattleEventManager.Instance?.PlayerRequestsAttack();
    }

    void OnEscapeButtonClicked()
    {
        BattleEventManager.Instance?.AttemptEscape();
    }

    void OnSkillButtonClicked(string skillName)
    {
        BattleEventManager.Instance?.SelectPlayerSkill(skillName);
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

    private void ShowCharacterDialogue(string dialogue)
    {
        if (string.IsNullOrEmpty(dialogue) || canSpeechBubble == null) return;
        if (hideDialogueCoroutine != null) StopCoroutine(hideDialogueCoroutine);

        canSpeechBubble.gameObject.SetActive(true);
        canSpeechBubble.SetText(dialogue);
        hideDialogueCoroutine = StartCoroutine(HideDialogueAfterDelay(2.5f));
    }

    private void UpdateStatusText(string message)
    {
        if (statusText == null) return;
        if (string.IsNullOrEmpty(message))
        {
            statusText.gameObject.SetActive(false);
        }
        else
        {
            statusText.text = message;
            statusText.gameObject.SetActive(true);
        }
    }

    private IEnumerator HideDialogueAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (canSpeechBubble != null)
        {
            canSpeechBubble.gameObject.SetActive(false);
        }
        hideDialogueCoroutine = null;
    }
}