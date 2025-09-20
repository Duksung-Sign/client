using System;
using UnityEngine;

public class GameEventManager : MonoBehaviour
{
    public static GameEventManager Instance { get; private set; }

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

    public event Action<GameState> OnGameStateChanged;
    public event Action<string> OnStoryAdvanced;
    public event Action<string> OnLockedDoorInteracted;
    public event Action<MonsterDataSO, string, string> OnBattleStartRequested;

    public event Action<string, string> OnItemCollected;
    public event Action<string, string> OnItemInspected;
    public event Action<string, string> OnDialogueStarted;
    public event Action<string, string> OnDialogueFinished;
    public event Action<string, string> OnKeyEvent;
    public event Action<string, string> OnBattleOver;


    public void TriggerGameStateChanged(GameState newState) => OnGameStateChanged?.Invoke(newState);
    public void TriggerStoryAdvanced(string storyID) => OnStoryAdvanced?.Invoke(storyID);
    public void TriggerLockedDoorInteracted(string doorID) => OnLockedDoorInteracted?.Invoke(doorID);
    public void TriggerBattleStartRequested(MonsterDataSO monsterData, string uniqueMonsterID, string battleDialogueID = null)
        => OnBattleStartRequested?.Invoke(monsterData, uniqueMonsterID, battleDialogueID);

    public void TriggerItemCollected(string itemID)
        => OnItemCollected?.Invoke("ITEM_COLLECTED", itemID);

    public void TriggerItemInspected(string itemID)
        => OnItemInspected?.Invoke("ITEM_INSPECTED", itemID);

    public void TriggerDialogueStarted(string dialogueID)
        => OnDialogueStarted?.Invoke("DIALOGUE_STARTED", dialogueID);

    public void TriggerDialogueFinished(string dialogueID)
        => OnDialogueFinished?.Invoke("DIALOGUE_FINISHED", dialogueID);

    public void TriggerKeyEvent(string type, string id) => OnKeyEvent?.Invoke(type, id);

    public void TriggerBattleOver(string battleID)
    => OnBattleOver?.Invoke("BATTLE_OVER", battleID);
}