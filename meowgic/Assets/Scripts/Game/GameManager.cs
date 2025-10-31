using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using DG.Tweening;
using Mediapipe.Unity.Sample.Holistic;

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

    // ⭐ 씬 전환 중복 방지 플래그
    private bool isTransitioning = false;

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
        // ⭐ 이미 전환 중이거나 이미 전투 상태면 무시
        if (isTransitioning || CurrentState == GameState.Battle)
        {
            Debug.LogWarning("[GameManager] 이미 전투 전환 중이거나 전투 중입니다. 요청 무시.");
            return;
        }

        Debug.Log($"[GameManager] 전투 시작 요청 수신: {monsterData.displayName}, 도망가기: {canEscape}");
        GameDataManager.Instance.SetCurrentBattleMonster(monsterData, uniqueMonsterID, battleDialogueID, canEscape);
        GameDataManager.Instance.SetPreviousScene(SceneManager.GetActiveScene().name);
        ChangeState(GameState.Battle);

        StartCoroutine(EnterBattleScene("BattleScene"));
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
        // ⭐ 이미 전환 중이면 무시
        if (isTransitioning)
        {
            Debug.LogWarning("[GameManager] 이미 씬 전환 중입니다. 요청 무시.");
            return;
        }

        if (sceneName == "BattleScene")
        {
            StartCoroutine(EnterBattleScene(sceneName));
        }
        else if (SceneManager.GetActiveScene().name == "BattleScene")
        {
            StartCoroutine(ExitBattleScene(sceneName, null));
        }
        else
        {
            FadeManager.Instance.LoadSceneWithFade(sceneName);
        }
    }

    public void ChangeScene(string sceneName, Action onSceneLoaded)
    {
        // ⭐ 이미 전환 중이면 무시
        if (isTransitioning)
        {
            Debug.LogWarning("[GameManager] 이미 씬 전환 중입니다. 요청 무시.");
            return;
        }

        if (sceneName == "BattleScene")
        {
            StartCoroutine(EnterBattleScene(sceneName, onSceneLoaded));
        }
        else if (SceneManager.GetActiveScene().name == "BattleScene")
        {
            StartCoroutine(ExitBattleScene(sceneName, onSceneLoaded));
        }
        else
        {
            FadeManager.Instance.LoadSceneWithFade(sceneName, onSceneLoaded);
        }
    }

    private IEnumerator EnterBattleScene(string battleScene, Action onSceneLoaded = null)
    {
        // ⭐ 이미 전환 중이면 중복 실행 방지
        if (isTransitioning)
        {
            Debug.LogWarning("[GameManager] EnterBattleScene 중복 실행 방지");
            yield break;
        }

        isTransitioning = true;
        Debug.Log("[GameManager] BattleScene 진입 준비 중...");

        // DOTween 정리
        DOTween.KillAll();

        // 기존 Mediapipe 정리
        var existingHolistic = FindObjectOfType<HolisticTrackingSolution>();
        if (existingHolistic != null)
        {
            try
            {
                Debug.Log("[GameManager] 기존 Mediapipe 정리 중...");
                existingHolistic.Stop();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameManager] Mediapipe 정리 중 오류 (무시됨): {e.Message}");
            }
            yield return new WaitForSeconds(0.3f);
        }

        // 리소스 언로드 및 가비지 컬렉션
        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();

        yield return new WaitForSeconds(0.2f);

        Debug.Log("[GameManager] BattleScene 로드 시작");

        // 씬 로드
        if (onSceneLoaded != null)
        {
            FadeManager.Instance.LoadSceneWithFade(battleScene, () => {
                isTransitioning = false;  // ⭐ 전환 완료
                onSceneLoaded?.Invoke();
            });
        }
        else
        {
            FadeManager.Instance.LoadSceneWithFade(battleScene, () => {
                isTransitioning = false;  // ⭐ 전환 완료
            });
        }
    }

    private IEnumerator ExitBattleScene(string nextScene, Action onSceneLoaded)
    {
        // ⭐ 이미 전환 중이면 중복 실행 방지
        if (isTransitioning)
        {
            Debug.LogWarning("[GameManager] ExitBattleScene 중복 실행 방지");
            yield break;
        }

        isTransitioning = true;
        Debug.Log("[GameManager] BattleScene 퇴장 준비 중...");

        var holistic = FindObjectOfType<HolisticTrackingSolution>();
        if (holistic != null)
        {
            try
            {
                Debug.Log("[GameManager] Mediapipe 정리 중...");
                holistic.Stop();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameManager] Mediapipe 정리 중 오류 (무시됨): {e.Message}");
            }

            yield return new WaitForSeconds(0.3f);
        }

        DOTween.KillAll();

        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();

        yield return new WaitForSeconds(0.2f);

        Debug.Log($"[GameManager] {nextScene} 로드 시작");

        if (onSceneLoaded != null)
        {
            FadeManager.Instance.LoadSceneWithFade(nextScene, () => {
                isTransitioning = false;  // ⭐ 전환 완료
                onSceneLoaded?.Invoke();
            });
        }
        else
        {
            FadeManager.Instance.LoadSceneWithFade(nextScene, () => {
                isTransitioning = false;  // ⭐ 전환 완료
            });
        }
    }

    public bool CanPlayerMove()
    {
        return CurrentState == GameState.Field;
    }
}