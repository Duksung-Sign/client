using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
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

    [Header("씬 전환 효과")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;
    private bool isChangingScene = false;

    public GameState CurrentState { get; private set; }

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

    private void HandleBattleStartRequested(MonsterDataSO monsterData, string uniqueMonsterID, string battleDialogueID)
    {
        if (CurrentState == GameState.Battle) return;

        Debug.Log($"[GameManager] 전투 시작 요청 수신: {monsterData.displayName}");

        GameDataManager.Instance.SetCurrentBattleMonster(monsterData, uniqueMonsterID, battleDialogueID);
        GameDataManager.Instance.SetPreviousScene(SceneManager.GetActiveScene().name);

        ChangeState(GameState.Battle);
        ChangeScene("BattleScene");
    }

    private void HandleDialogueStarted(string eventType, string dialogueId)
    {
        ChangeState(GameState.Dialogue);
    }

    private void HandleDialogueFinished(string eventType, string dialogueId)
    {
        ChangeState(GameState.Field);
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
        if (isChangingScene) return;

        StartCoroutine(FadeAndLoadScene(sceneName));
    }

    private IEnumerator FadeAndLoadScene(string sceneName)
    {
        isChangingScene = true;

        ChangeState(GameState.Loading); 
        
        fadeCanvasGroup.blocksRaycasts = true; 
        yield return fadeCanvasGroup.DOFade(1f, fadeDuration).WaitForCompletion();

        yield return SceneManager.LoadSceneAsync(sceneName);

        yield return fadeCanvasGroup.DOFade(0f, fadeDuration).WaitForCompletion();
        fadeCanvasGroup.blocksRaycasts = false; 

        ChangeState(GameState.Field); 
        
        isChangingScene = false;
    }
}