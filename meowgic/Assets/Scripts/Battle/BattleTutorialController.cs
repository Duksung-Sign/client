using UnityEngine;
using System.Collections;

public class BattleTutorialController : MonoBehaviour
{
    private enum TutorialState { None, Intro, PromptAttack, PromptSkillSelect, PromptGesture, Complete }
    private TutorialState currentState = TutorialState.None;
    private bool isTutorialActive = false;
    private bool isProcessingDialogue = false;

    private const string TUTORIAL_SET_ID = "first_battle_tutorial";

    void Awake()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnBattleEvent += OnBattleEventReceived;
            BattleEventManager.Instance.OnPlayerRequestsAttack += OnAttackButtonClicked;
            BattleEventManager.Instance.OnPlayerSkillSelected += OnSkillSelected;
        }
    }

    void OnDestroy()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnBattleEvent -= OnBattleEventReceived;
            BattleEventManager.Instance.OnPlayerRequestsAttack -= OnAttackButtonClicked;
            BattleEventManager.Instance.OnPlayerSkillSelected -= OnSkillSelected;
        }
    }

    IEnumerator Start()
    {
        yield return new WaitUntil(() => BattleManager.Instance != null);

        if (BattleManager.Instance.IsTutorialMode())
        {
            isTutorialActive = true;
            Debug.Log("[BattleTutorialController] 튜토리얼 모드 활성화!");
            BattleEventManager.Instance.TriggerBattleEvent(BattleEventType.TutorialStart);
        }
        else
        {
            this.enabled = false;
            Debug.Log("[BattleTutorialController] 일반 전투이므로 튜토리얼 컨트롤러 비활성화");
        }
    }

    private void OnAttackButtonClicked()
    {
        if (!isTutorialActive || currentState == TutorialState.Complete) return;

        Debug.Log($"[Tutorial] 공격 버튼 클릭됨, 현재 상태: {currentState}");

        if (currentState == TutorialState.PromptAttack)
        {
            if (DialogueManager.Instance.IsInDialogue()) return;

            // 스킬 선택 상태로 전환
            currentState = TutorialState.PromptSkillSelect;

            // BattleManager의 상태를 스킬 선택 상태로 변경
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.ChangeBattleStatePublic(BattleState.PlayerTurn_SelectingSkill);
            }

            // 003: "모아에서 가장 중요한 건 표정과 앞손 기억해 두는 게 좋다고!"
            ShowTutorialDialogue("battle_tutorial_003");
        }
    }

    private void OnSkillSelected(string skillName)
    {
        if (!isTutorialActive || currentState != TutorialState.PromptSkillSelect) return;

        Debug.Log($"[Tutorial] 스킬 선택됨: {skillName}, 현재 상태: {currentState}");

        // 어떤 스킬이든 선택하면 제스처 단계로 진행
        currentState = TutorialState.PromptGesture;
        StartCoroutine(ShowGestureInstructions());
    }

    private IEnumerator ShowGestureInstructions()
    {
        // 004: "좋아. 잘했어! 이제 야까 확인한 모아 동작을 구사하면 돼."
        ShowTutorialDialogue("battle_tutorial_004");
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        // 007: "준비 됐으면 해볼까?"
        ShowTutorialDialogue("battle_tutorial_007");
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        // 제스처 인식은 BattleManager가 알아서 시작
        // (HandlePlayerSkillSelection에서 StartGestureRecognition 호출됨)
    }

    public void OnBattleEventReceived(BattleEventType receivedEvent)
    {
        if (!isTutorialActive || currentState == TutorialState.Complete) return;

        Debug.Log($"[Tutorial] 이벤트 수신: {receivedEvent}, 현재 상태: {currentState}");

        switch (currentState)
        {
            case TutorialState.None when receivedEvent == BattleEventType.TutorialStart:
                StartCoroutine(TutorialSequence());
                break;

            case TutorialState.PromptGesture when receivedEvent == BattleEventType.PlayerAttackSuccess:
                StartCoroutine(HandleTutorialSuccess());
                break;

            case TutorialState.PromptGesture when receivedEvent == BattleEventType.PlayerAttackFail:
                StartCoroutine(HandleTutorialFail());
                break;
        }
    }

    private IEnumerator TutorialSequence()
    {
        currentState = TutorialState.Intro;
        // 001: "전투가 시작되면 공격 스킬을 고를 수 있어."
        ShowTutorialDialogue("battle_tutorial_001");
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        currentState = TutorialState.PromptAttack;
        // 002: "먼저 공격하기를 누르고 원하는 스킬을 눌러봐."
        ShowTutorialDialogue("battle_tutorial_002");
    }

    private IEnumerator HandleTutorialSuccess()
    {
        ShowTutorialDialogue("battle_tutorial_011");
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        CompleteTutorial();
    }

    private IEnumerator HandleTutorialFail()
    {
        ShowTutorialDialogue("battle_tutorial_009");
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        ShowTutorialDialogue("battle_tutorial_010");
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        currentState = TutorialState.PromptSkillSelect;
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.ChangeBattleStatePublic(BattleState.PlayerTurn_SelectingSkill);
        }
    }

    private void ShowTutorialDialogue(string lineID)
    {
        if (isProcessingDialogue)
        {
            Debug.LogWarning($"[Tutorial] 이미 대화 처리 중. {lineID} 무시");
            return;
        }

        if (DialogueManager.Instance != null)
        {
            isProcessingDialogue = true;
            Debug.Log($"[Tutorial] 대화 시작: {TUTORIAL_SET_ID}/{lineID}");
            DialogueManager.Instance.StartDialogueFromLine(TUTORIAL_SET_ID, lineID);
            StartCoroutine(ResetDialogueFlag());
        }
        else
        {
            Debug.LogError("[BattleTutorialController] DialogueManager.Instance가 없습니다!");
        }
    }

    private IEnumerator ResetDialogueFlag()
    {
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        isProcessingDialogue = false;
    }

    private void CompleteTutorial()
    {
        currentState = TutorialState.Complete;
        isTutorialActive = false;
        Debug.Log("[BattleTutorialController] 튜토리얼 완료! 정상 전투 모드로 전환합니다.");

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.MarkDialogueAsFinished(TUTORIAL_SET_ID);
            GameDataManager.Instance.SaveState();
        }

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.EndTutorialMode();
        }
    }
}