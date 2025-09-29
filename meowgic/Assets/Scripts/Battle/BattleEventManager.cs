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
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public event Action<string> OnPlayerSkillSelected;
    public event Action OnPlayerAttemptEscape;
    public event Action OnRequestStartGestureRecognition;
    public event Action<string> OnRequestShowTutorialMessage;
    public event Action<string> OnRequestShowCharacterDialogue;
    public event Action<string, float> OnRequestUpdateStatus;
    public event Action<BattleState> OnBattleStateChanged;

    public event Action<bool> OnRequestShowMainUI;
    public event Action<bool> OnRequestShowSkillPanel;
    public event Action OnPlayerRequestsAttack;

    public event Action<BattleEventType> OnBattleEvent;

    public void SelectPlayerSkill(string skillName) => OnPlayerSkillSelected?.Invoke(skillName);
    public void AttemptEscape() => OnPlayerAttemptEscape?.Invoke();
    public void StartGestureRecognition() => OnRequestStartGestureRecognition?.Invoke();
    public void ShowTutorialMessage(string message) => OnRequestShowTutorialMessage?.Invoke(message);
    public void RequestShowCharacterDialogue(string dialogue) => OnRequestShowCharacterDialogue?.Invoke(dialogue);
    public void RequestUpdateStatus(string status, float duration = 2.5f) => OnRequestUpdateStatus?.Invoke(status, duration); 
    public void RequestShowMainUI(bool show) => OnRequestShowMainUI?.Invoke(show);
    public void RequestShowSkillPanel(bool show) => OnRequestShowSkillPanel?.Invoke(show);
    public void PlayerRequestsAttack() => OnPlayerRequestsAttack?.Invoke();

    public void RequestShowTutorialMessage(string message) => OnRequestShowTutorialMessage?.Invoke(message);
    public void RequestStartGestureRecognition() => OnRequestStartGestureRecognition?.Invoke();

    public void TriggerBattleStateChanged(BattleState newState) => OnBattleStateChanged?.Invoke(newState);

    public void TriggerBattleEvent(BattleEventType eventType)
    {
        Debug.Log($"전투 이벤트 발생: {eventType}");
        OnBattleEvent?.Invoke(eventType);
    }
}