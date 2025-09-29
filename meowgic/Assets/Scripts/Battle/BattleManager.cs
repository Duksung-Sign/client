using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    public BattleState CurrentBattleState { get; private set; }
    private MonsterDataSO currentMonster;
    private string defeatedMonsterID;
    private int currentPlayerHP;
    private int currentMonsterHP;
    private string selectedSkill = "";

    private HolisticJsonSender holisticExtractor;
    private bool gestureResponseReceived = false;

    private static class SystemMessages
    {
        public const string MediaPipeStabilizing = "양 손바닥을 보여주세요.";
        public const string MediaPipeReady = "안정화 완료!";
    }


    void Awake()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnPlayerSkillSelected += HandlePlayerSkillSelection;
            BattleEventManager.Instance.OnPlayerRequestsAttack += HandleAttackRequest;
            BattleEventManager.Instance.OnPlayerAttemptEscape += AttemptEscape;
        }
    }

    void OnDestroy()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnPlayerSkillSelected -= HandlePlayerSkillSelection;
            BattleEventManager.Instance.OnPlayerRequestsAttack -= HandleAttackRequest;
            BattleEventManager.Instance.OnPlayerAttemptEscape -= AttemptEscape;
        }
        if (holisticExtractor != null)
        {
            holisticExtractor.OnGestureRecognitionComplete -= OnGestureRecognized;
        }
    }

    void Start()
    {
        StartCoroutine(InitializeBattleSequence());
    }

    private IEnumerator InitializeBattleSequence()
    {
        ChangeBattleState(BattleState.Initializing);

        holisticExtractor = FindObjectOfType<HolisticJsonSender>();
        if (holisticExtractor != null)
        {
            holisticExtractor.OnGestureRecognitionComplete += OnGestureRecognized;
        }

        if (!LoadBattleData())
        {
            FadeManager.Instance.LoadSceneWithFade("MainMenu");
            yield break;
        }
        LoadPlayerData();

        yield return StartCoroutine(MonitorStabilization());

        BattleEventManager.Instance.TriggerBattleEvent(BattleEventType.BattleStarted);

        if (!string.IsNullOrEmpty(GameDataManager.Instance.CurrentBattleDialogueID))
        {
            DialogueManager.Instance.StartDialogue(GameDataManager.Instance.CurrentBattleDialogueID);
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        }

        PlayerTurnStart();
    }

    private bool LoadBattleData()
    {
        if (GameDataManager.Instance == null) return false;
        currentMonster = GameDataManager.Instance.CurrentBattleMonster;
        defeatedMonsterID = GameDataManager.Instance.CurrentBattleMonsterID;
        if (currentMonster == null) return false;
        currentMonsterHP = currentMonster.maxHP;
        return true;
    }

    private void LoadPlayerData()
    {
        if (PlayerStatsManager.Instance != null)
        {
            currentPlayerHP = PlayerStatsManager.Instance.GetCurrentHP();
        }
    }

    private IEnumerator MonitorStabilization()
    {
        if (holisticExtractor == null) yield break;

        BattleEventManager.Instance?.RequestUpdateStatus(SystemMessages.MediaPipeStabilizing, 0);
        float waitTime = 0f;
        while (waitTime < 10f && !holisticExtractor.IsMediaPipeReady())
        {
            waitTime += Time.deltaTime;
            yield return null;
        }
        BattleEventManager.Instance?.RequestUpdateStatus(SystemMessages.MediaPipeReady, 1.5f);
        yield return new WaitForSeconds(1.5f);
    }

    private void PlayerTurnStart()
    {
        ChangeBattleState(BattleState.PlayerTurn_AwaitingInput);
        BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.PlayerTurnStarted);
    }

    private void HandleAttackRequest()
    {
        if (CurrentBattleState != BattleState.PlayerTurn_AwaitingInput) return;
        ChangeBattleState(BattleState.PlayerTurn_SelectingSkill);
    }

    private void HandlePlayerSkillSelection(string skillName)
    {
        if (CurrentBattleState != BattleState.PlayerTurn_SelectingSkill) return;
        selectedSkill = skillName;
        StartGestureRecognition();
    }

    private void StartGestureRecognition()
    {
        ChangeBattleState(BattleState.PlayerTurn_Gesture);

        if (holisticExtractor == null)
        {
            OnGestureRecognized(selectedSkill); 
            return;
        }

        gestureResponseReceived = false;
        holisticExtractor.StartGestureCollection();
        StartCoroutine(WaitForGestureRecognition());
    }

    private IEnumerator WaitForGestureRecognition()
    {
        float responseWaitTime = 15f;
        while (responseWaitTime > 0 && !gestureResponseReceived)
        {
            responseWaitTime -= Time.deltaTime;
            yield return null;
        }
        if (!gestureResponseReceived)
        {
            OnGestureRecognized("timeout");
        }
    }

    public void OnGestureRecognized(string prediction)
    {
        if (CurrentBattleState != BattleState.PlayerTurn_Gesture || gestureResponseReceived) return;
        gestureResponseReceived = true;
        ProcessGestureResult(prediction);
    }

    private void ProcessGestureResult(string prediction)
    {
        ChangeBattleState(BattleState.ActionInProgress);
        bool isMatch = selectedSkill.Equals(prediction, System.StringComparison.OrdinalIgnoreCase);

        if (isMatch) StartCoroutine(ExecutePlayerAttack());
        else StartCoroutine(PlayerAttackFailed());
    }

    private IEnumerator ExecutePlayerAttack()
    {
        int damage = PlayerStatsManager.Instance.GetAttackPower();
        currentMonsterHP = Mathf.Max(0, currentMonsterHP - damage);

        BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.PlayerAttackSuccess);

        DialogueManager.Instance.StartDialogue("battle_attack_success");
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        if (currentMonsterHP <= 0) EndBattle(true);
        else StartCoroutine(MonsterTurn());
    }

    private IEnumerator PlayerAttackFailed()
    {
        BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.PlayerAttackFail);

        DialogueManager.Instance.StartDialogue("battle_attack_fail"); 
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        StartCoroutine(MonsterTurn());
    }

    private IEnumerator MonsterTurn()
    {
        ChangeBattleState(BattleState.MonsterTurn);
        BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.MonsterTurnStarted);
        yield return new WaitForSeconds(1f);

        ChangeBattleState(BattleState.ActionInProgress);
        int damage = currentMonster.attackPower;
        PlayerStatsManager.Instance.TakeDamage(damage);
        currentPlayerHP = PlayerStatsManager.Instance.GetCurrentHP();

        DialogueManager.Instance.StartDialogue("battle_monster_attack"); 
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        if (currentPlayerHP <= 0) EndBattle(false);
        else PlayerTurnStart();
    }

    public void AttemptEscape()
    {
        if (CurrentBattleState != BattleState.PlayerTurn_AwaitingInput) return;

        ChangeBattleState(BattleState.ActionInProgress);
        bool escapeSuccess = Random.value > 0.5f;

        if (escapeSuccess)
        {
            EndBattle(false, true);
        }
        else
        {
            StartCoroutine(EscapeFailedSequence());
        }
    }

    private IEnumerator EscapeFailedSequence()
    {
        DialogueManager.Instance.StartDialogue("battle_escape_fail"); 
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        StartCoroutine(MonsterTurn());
    }

    private void EndBattle(bool playerWon, bool escaped = false)
    {
        if (escaped)
        {
            ChangeBattleState(BattleState.Escaped);
            BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.BattleEscaped);
        }
        else
        {
            BattleState endState = playerWon ? BattleState.BattleOver_Victory : BattleState.BattleOver_Defeat;
            ChangeBattleState(endState);
            BattleEventManager.Instance?.TriggerBattleEvent(playerWon ? BattleEventType.BattleVictory : BattleEventType.BattleDefeat);
        }

        if (playerWon && !string.IsNullOrEmpty(defeatedMonsterID))
        {
            GameDataManager.Instance.AddDefeatedMonster(defeatedMonsterID);
            GameEventManager.Instance.TriggerBattleOver(defeatedMonsterID);
        }

        GameDataManager.Instance.SaveState();
        StartCoroutine(ReturnToField(playerWon, escaped));
    }

    private IEnumerator ReturnToField(bool playerWon, bool escaped)
    {
        string dialogueID = escaped ? "battle_escape_success" : (playerWon ? "battle_victory" : "battle_defeat");
        DialogueManager.Instance.StartDialogue(dialogueID);
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        string returnScene = GameDataManager.Instance.PreviousSceneName;
        FadeManager.Instance.LoadSceneWithFade(string.IsNullOrEmpty(returnScene) ? "MainMenu" : returnScene);
    }

    private void ChangeBattleState(BattleState newState)
    {
        if (CurrentBattleState == newState) return;
        CurrentBattleState = newState;
        BattleEventManager.Instance?.TriggerBattleStateChanged(newState);
    }
}