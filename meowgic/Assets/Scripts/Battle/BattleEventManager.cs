using UnityEngine;
using System;

public class BattleEventManager : MonoBehaviour
{
    public static BattleEventManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 기존 이벤트들
    public event Action<string> OnPlayerSkillSelected;
    public event Action OnPlayerAttemptEscape;
    public event Action OnRequestStartGestureRecognition;
    public event Action<string> OnRequestShowTutorialMessage;
    public event Action<string> OnRequestShowCharacterDialogue;
    public event Action<string> OnRequestUpdateStatus;

    // 새로 추가된 UI 제어 이벤트들
    public event Action<bool> OnRequestShowMainUI;
    public event Action<bool> OnRequestShowSkillPanel;
    public event Action OnPlayerRequestsAttack;

    // [추가] BattleTutorialController를 위한 이벤트들
    public event Action<BattleEventType> OnBattleEvent;

    // 기존 메서드들
    public void SelectPlayerSkill(string skillName) => OnPlayerSkillSelected?.Invoke(skillName);
    public void AttemptEscape() => OnPlayerAttemptEscape?.Invoke();
    public void StartGestureRecognition() => OnRequestStartGestureRecognition?.Invoke();
    public void ShowTutorialMessage(string message) => OnRequestShowTutorialMessage?.Invoke(message);
    public void RequestShowCharacterDialogue(string dialogue) => OnRequestShowCharacterDialogue?.Invoke(dialogue);
    public void RequestUpdateStatus(string status) => OnRequestUpdateStatus?.Invoke(status);

    // 새로 추가된 UI 제어 메서드들
    public void RequestShowMainUI(bool show) => OnRequestShowMainUI?.Invoke(show);
    public void RequestShowSkillPanel(bool show) => OnRequestShowSkillPanel?.Invoke(show);
    public void PlayerRequestsAttack() => OnPlayerRequestsAttack?.Invoke();

    // [추가] BattleTutorialController를 위한 메서드들
    public void RequestShowTutorialMessage(string message) => OnRequestShowTutorialMessage?.Invoke(message);
    public void RequestStartGestureRecognition() => OnRequestStartGestureRecognition?.Invoke();

    public void TriggerBattleEvent(BattleEventType eventType)
    {
        Debug.Log($"전투 이벤트 발생: {eventType}");
        // [추가] 이벤트를 구독자들에게 전파
        OnBattleEvent?.Invoke(eventType);
    }
}

public enum BattleEventType
{
    PlayerTurnStarted,
    PlayerSkillSelected,
    GestureRecognitionStarted,
    PlayerAttackSuccess,
    PlayerAttackFail,
    TutorialStart  // [추가] BattleTutorialController에서 사용하는 이벤트 타입
}