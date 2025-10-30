using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using DG.Tweening;

public enum GameState
{
    Field,
    Dialogue,
    Battle,
    Cutscene,
    Menu,
    Loading
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public GameState CurrentState { get; private set; }
    public bool IsFullyInitialized { get; private set; } = false;
    private GameState stateBeforeDialogue = GameState.Field;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(InitializeGameManager());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    IEnumerator InitializeGameManager()
    {
        yield return new WaitUntil(() =>
            GameDataManager.Instance != null &&
            GameEventManager.Instance != null);

        yield return new WaitForSeconds(0.5f);

        IsFullyInitialized = true;
        Debug.Log("[GameManager] 모든 시스템 초기화 완료");
    }

    void OnEnable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnBattleStartRequested += HandleBattleStartRequested;
            GameEventManager.Instance.OnDialogueStarted += HandleDialogueStarted;
            GameEventManager.Instance.OnDialogueFinished += HandleDialogueFinished;
        }
    }

    void OnDisable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnBattleStartRequested -= HandleBattleStartRequested;
            GameEventManager.Instance.OnDialogueStarted -= HandleDialogueStarted;
            GameEventManager.Instance.OnDialogueFinished -= HandleDialogueFinished;
        }
    }

    private void HandleBattleStartRequested(MonsterDataSO monsterData, string uniqueMonsterID, string battleDialogueID, bool canEscape)
    {
        if (CurrentState == GameState.Battle) return;
        Debug.Log($"[GameManager] 전투 시작 요청 수신: {monsterData.displayName}, 도망가기: {canEscape}");
        GameDataManager.Instance.SetCurrentBattleMonster(monsterData, uniqueMonsterID, battleDialogueID, canEscape);
        GameDataManager.Instance.SetPreviousScene(SceneManager.GetActiveScene().name);
        ChangeState(GameState.Battle);
        ChangeScene("BattleScene");
    }

    private void HandleDialogueStarted(string eventType, string dialogueId)
    {
        if (CurrentState != GameState.Dialogue)
        {
            stateBeforeDialogue = CurrentState;
        }
        ChangeState(GameState.Dialogue);
    }

    private void HandleDialogueFinished(string eventType, string dialogueId)
    {
        ChangeState(stateBeforeDialogue);
    }

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        Debug.Log($"[GameManager] 게임 상태 변경 -> {newState}");
        GameEventManager.Instance.TriggerGameStateChanged(newState);
    }

    public void ChangeScene(string sceneName)
    {
        FadeManager.Instance.LoadSceneWithFade(sceneName);
    }

    public void ChangeScene(string sceneName, Action onSceneLoaded)
    {
        FadeManager.Instance.LoadSceneWithFade(sceneName, onSceneLoaded);
    }

    public bool CanPlayerMove()
    {
        return CurrentState == GameState.Field;
    }
}