using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum TutorialState
{
    None,
    Intro,
    PromptAttack,
    PromptGesture,
    Complete
}

public enum BattleEvent
{
    TutorialStart,
    PlayerTurnStarted,
    PlayerSkillSelected,
    PlayerAttackSuccess,
    PlayerAttackFail
}

public class BattleTutorialController : MonoBehaviour
{
    private TutorialState currentState = TutorialState.None;
    private bool isTutorialActive = false;
    private Coroutine currentTutorialCoroutine;

    // 튜토리얼 대화 ID들
    private readonly string[] tutorialDialogueIDs = {
        "battle_tutorial_001", // "전투가 시작되면..."
        "battle_tutorial_002", // "먼저 공격하기를..."
        "battle_tutorial_003", // "묘어에서 가장 중요한..."
        "battle_tutorial_004", // "좋아. 잘했어!"
        "battle_tutorial_008", // "이런! 말하는 사이에..."
        "battle_tutorial_009", // "공격에 실패하면..."
        "battle_tutorial_010", // "다시 한 번 공격해보자."
        "battle_tutorial_011"  // "정말 잘했어!"
    };

    // 튜토리얼 메시지 백업 (DialogueManager 없을 때)
    private readonly Dictionary<string, string> tutorialMessages =
        new Dictionary<string, string>
    {
        {"battle_tutorial_001", "전투가 시작되었습니다! 공격 버튼을 눌러보세요."},
        {"battle_tutorial_002", "먼저 공격하기 버튼을 클릭하여 스킬을 선택하세요."},
        {"battle_tutorial_003", "이제 선택한 스킬에 맞는 제스처를 취해보세요!"},
        {"battle_tutorial_004", "좋아요! 공격에 성공했습니다!"},
        {"battle_tutorial_008", "이런! 몬스터가 공격해왔네요."},
        {"battle_tutorial_009", "공격에 실패하면 몬스터가 반격합니다."},
        {"battle_tutorial_010", "다시 한 번 공격해보세요."},
        {"battle_tutorial_011", "정말 잘했어요! 튜토리얼 완료!"}
    };

    // [REFACTORED] Awake에서 이벤트 구독
    void Awake()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnBattleEvent += OnBattleEventReceived;
        }
    }

    // [REFACTORED] OnDestroy에서 구독 해제
    void OnDestroy()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnBattleEvent -= OnBattleEventReceived;
        }

        if (currentTutorialCoroutine != null)
        {
            StopCoroutine(currentTutorialCoroutine);
        }
    }

    void Start()
    {
        StartCoroutine(InitializeTutorial());
    }

    private IEnumerator InitializeTutorial()
    {
        // 컴포넌트 초기화 대기
        yield return new WaitForEndOfFrame();

        // [REFACTORED] 다른 매니저들을 직접 찾지 않음
        // 튜토리얼 여부 확인
        string dialogueID = GameManager.Instance?.GetCurrentBattleDialogueID();
        if (dialogueID == "battle_tutorial")
        {
            isTutorialActive = true;
            Debug.Log("튜토리얼 모드 활성화");
            // [REFACTORED] BattleManager에 직접 이벤트를 보내는 대신 방송국에 방송
            BattleEventManager.Instance.TriggerBattleEvent(BattleEventType.TutorialStart);
        }
        else
        {
            currentState = TutorialState.Complete;
            Debug.Log("일반 전투 모드");
        }
    }

    // [REFACTORED] 이제 이 메서드는 오직 이벤트에 의해 호출됨
    public void OnBattleEventReceived(BattleEventType receivedEvent)
    {
        if (!isTutorialActive || currentState == TutorialState.Complete)
        {
            return;
        }

        Debug.Log($"튜토리얼 이벤트 수신: {receivedEvent}, 현재 상태: {currentState}");

        switch (currentState)
        {
            case TutorialState.None:
                HandleNoneState(receivedEvent);
                break;

            case TutorialState.PromptAttack:
                HandlePromptAttackState(receivedEvent);
                break;

            case TutorialState.PromptGesture:
                HandlePromptGestureState(receivedEvent);
                break;
        }
    }

    private void HandleNoneState(BattleEventType receivedEvent)
    {
        if (receivedEvent == BattleEventType.TutorialStart)
        {
            ShowTutorialDialogue("battle_tutorial_001");
            currentState = TutorialState.PromptAttack;
            Debug.Log("튜토리얼 시작됨");
        }
    }

    private void HandlePromptAttackState(BattleEventType receivedEvent)
    {
        if (receivedEvent == BattleEventType.PlayerTurnStarted)
        {
            ShowTutorialDialogue("battle_tutorial_002");
        }
        else if (receivedEvent == BattleEventType.PlayerSkillSelected)
        {
            // [REFACTORED] UIManager를 직접 제어하지 않음 (필요 시 빈 메시지를 보내 숨길 수 있음)
            BattleEventManager.Instance.RequestShowTutorialMessage("");
            currentState = TutorialState.PromptGesture;
            Debug.Log("제스처 단계로 전환");

            // 이전 코루틴 정리
            if (currentTutorialCoroutine != null)
            {
                StopCoroutine(currentTutorialCoroutine);
            }

            currentTutorialCoroutine = StartCoroutine(MonitorGestureProgress());
        }
    }

    private void HandlePromptGestureState(BattleEventType receivedEvent)
    {
        if (receivedEvent == BattleEventType.PlayerAttackSuccess)
        {
            // 코루틴 정리
            if (currentTutorialCoroutine != null)
            {
                StopCoroutine(currentTutorialCoroutine);
                currentTutorialCoroutine = null;
            }

            // [REFACTORED] UIManager를 직접 호출하는 대신 방송국에 요청
            BattleEventManager.Instance.RequestUpdateStatus("");
            ShowTutorialDialogue("battle_tutorial_004"); // "좋아. 잘했어!"
            currentState = TutorialState.Complete;
            isTutorialActive = false;
            Debug.Log("튜토리얼 완료!");
        }
        else if (receivedEvent == BattleEventType.PlayerAttackFail)
        {
            // 코루틴 정리
            if (currentTutorialCoroutine != null)
            {
                StopCoroutine(currentTutorialCoroutine);
                currentTutorialCoroutine = null;
            }

            // [REFACTORED] UIManager를 직접 호출하는 대신 방송국에 요청
            BattleEventManager.Instance.RequestUpdateStatus("");
            ShowTutorialDialogue("battle_tutorial_009"); // "공격에 실패하면..."
            currentState = TutorialState.PromptAttack;
            Debug.Log("공격 실패 - 다시 공격 단계로");
        }
    }

    private void ShowTutorialDialogue(string dialogueID)
    {
        string message = null;

        if (DialogueManager.Instance != null)
        {
            message = DialogueManager.Instance.GetDialogueText(dialogueID);
        }

        if (string.IsNullOrEmpty(message) && tutorialMessages.TryGetValue(dialogueID, out string backupMessage))
        {
            message = backupMessage;
        }

        if (string.IsNullOrEmpty(message))
        {
            message = $"튜토리얼 단계: {dialogueID}";
            Debug.LogWarning($"튜토리얼 메시지를 찾을 수 없음: {dialogueID}");
        }

        BattleEventManager.Instance.RequestShowTutorialMessage(message);
        Debug.Log($"튜토리얼 메시지 표시: {message}");
    }

    private IEnumerator MonitorGestureProgress()
    {
        ShowTutorialDialogue("battle_tutorial_003");
        yield return new WaitForSeconds(2.5f);

        // [REFACTORED] BattleManager에 직접 호출하는 대신 이벤트로 요청
        BattleEventManager.Instance.RequestStartGestureRecognition();
        Debug.Log("제스처 인식 시작 요청됨");

        // HolisticJsonSender 모니터링은 해당 컴포넌트가 이벤트를 방송하도록 변경되어야 함
        // 현재는 간단히 대기 시간만 설정
        yield return new WaitForSeconds(5f);
        Debug.Log("제스처 모니터링 완료");
    }

    // 외부에서 튜토리얼 강제 완료
    public void CompleteTutorial()
    {
        if (currentTutorialCoroutine != null)
        {
            StopCoroutine(currentTutorialCoroutine);
            currentTutorialCoroutine = null;
        }

        currentState = TutorialState.Complete;
        isTutorialActive = false;
        // [REFACTORED] UIManager를 직접 호출하는 대신 방송국에 요청
        BattleEventManager.Instance.RequestShowTutorialMessage("");
        Debug.Log("튜토리얼 강제 완료");
    }

    // 디버그용
    public void SkipToNextStep()
    {
        switch (currentState)
        {
            case TutorialState.PromptAttack:
                OnBattleEventReceived(BattleEventType.PlayerSkillSelected);
                break;
            case TutorialState.PromptGesture:
                OnBattleEventReceived(BattleEventType.PlayerAttackSuccess);
                break;
        }
    }
}