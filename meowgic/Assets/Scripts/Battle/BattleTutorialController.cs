using UnityEngine;
using System.Collections;

public class BattleTutorialController : MonoBehaviour
{
    private enum TutorialState { None, Intro, PromptAttack, PromptGesture, Complete }
    private TutorialState currentState = TutorialState.None;
    private bool isTutorialActive = false;

    void Awake()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnBattleEvent += OnBattleEventReceived;
        }
    }

    void OnDestroy()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnBattleEvent -= OnBattleEventReceived;
        }
    }

    void Start()
    {
        // GameManager에서 현재 전투가 튜토리얼인지 확인
        // string battleType = GameManager.Instance?.GetCurrentBattleType(); 
        // if (battleType == "tutorial")
        // {
        //     isTutorialActive = true;
        //     BattleEventManager.Instance.TriggerBattleEvent(BattleEventType.TutorialStart);
        // }

        // 위 코드는 GameManager에 GetCurrentBattleType()이 있다는 가정 하의 예시입니다.
        // 지금은 임시로 활성화합니다.
        isTutorialActive = true;
        BattleEventManager.Instance.TriggerBattleEvent(BattleEventType.TutorialStart);
    }

    public void OnBattleEventReceived(BattleEventType receivedEvent)
    {
        if (!isTutorialActive || currentState == TutorialState.Complete) return;

        Debug.Log($"튜토리얼 이벤트 수신: {receivedEvent}, 현재 상태: {currentState}");

        switch (currentState)
        {
            case TutorialState.None when receivedEvent == BattleEventType.TutorialStart:
                StartCoroutine(TutorialSequence());
                break;

            case TutorialState.PromptAttack when receivedEvent == BattleEventType.PlayerSkillSelected:
                currentState = TutorialState.PromptGesture;
                ShowTutorialDialogue("battle_tutorial_003"); // "제스처를 취해보세요!"
                break;

            case TutorialState.PromptGesture when receivedEvent == BattleEventType.PlayerAttackSuccess:
                ShowTutorialDialogue("battle_tutorial_004"); // "좋아. 잘했어!"
                CompleteTutorial();
                break;

            case TutorialState.PromptGesture when receivedEvent == BattleEventType.PlayerAttackFail:
                ShowTutorialDialogue("battle_tutorial_009"); // "공격에 실패하면..."
                currentState = TutorialState.PromptAttack;
                break;
        }
    }

    private IEnumerator TutorialSequence()
    {
        currentState = TutorialState.Intro;
        ShowTutorialDialogue("battle_tutorial_001"); // "전투가 시작되면..."

        // 대화가 끝날 때까지 대기
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        currentState = TutorialState.PromptAttack;
        ShowTutorialDialogue("battle_tutorial_002"); // "먼저 공격하기를..."
    }

    // [REFACTORED] 이 함수는 이제 DialogueManager에 직접 명령합니다.
    private void ShowTutorialDialogue(string dialogueID)
    {
        if (DialogueManager.Instance != null)
        {
            // DialogueManager에게 직접 대화 시작을 '명령'합니다.
            // 더 이상 내부 데이터를 가져올 필요가 없습니다.
            DialogueManager.Instance.StartDialogue(dialogueID);
        }
        else
        {
            Debug.LogError("[BattleTutorialController] DialogueManager.Instance가 없습니다!");
        }
    }

    private void CompleteTutorial()
    {
        currentState = TutorialState.Complete;
        isTutorialActive = false;
        Debug.Log("튜토리얼 완료!");
        // 튜토리얼 완료 상태를 GameDataManager에 저장할 수 있습니다.
        // GameDataManager.Instance.SetTutorialCompleted("battle");
    }
}