using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    #region Battle Data & State
    public BattleState CurrentBattleState { get; private set; }
    private MonsterDataSO currentMonster;
    private string defeatedMonsterID;
    private int currentPlayerHP;
    private int currentMonsterHP;
    private string selectedSkill = "";
    #endregion

    #region Gesture Recognition
    private HolisticJsonSender holisticExtractor;
    private bool gestureResponseReceived = false;
    #endregion

    #region Dialogue & Message Constants
    private static class CharacterDialogue
    {
        public static string AttackSuccess(string monsterName, int damage) => $"성공이야! {monsterName}에게 {damage}의 피해를 입혔어!";
        public const string AttackFail = "인식 실패! 다시 해 보자!";
        public static string MonsterAttack(string monsterName, int damage) => $"{monsterName}의 공격! {damage}의 피해를 입었어.";
        public const string EscapeFail = "탈출에 실패했어!";
        public const string EscapeBlocked = "도망칠 수 없다.";
    }

    private static class SystemMessages
    {
        public const string MediaPipeStabilizing = "양 손바닥을 보여주세요.";
        public const string MediaPipeReady = "안정화 완료!";
        public const string WaitingForGesture = "제스처를 인식 중...";
        public const string InitializingBattle = "전투 준비 중...";
    }
    #endregion

    #region Unity Lifecycle & Event Management
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
    #endregion

    #region Battle Initialization
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
            SceneManager.LoadScene("MainMenu");
            yield break;
        }
        LoadPlayerData();

        yield return StartCoroutine(MonitorStabilization());

        BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.BattleStarted);
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

        BattleEventManager.Instance?.RequestUpdateStatus(SystemMessages.MediaPipeStabilizing);
        float waitTime = 0f;
        while (waitTime < 10f && !holisticExtractor.IsMediaPipeReady())
        {
            waitTime += Time.deltaTime;
            yield return null;
        }
        BattleEventManager.Instance?.RequestUpdateStatus(SystemMessages.MediaPipeReady);
        yield return new WaitForSeconds(1.5f);
    }
    #endregion

    #region Turn & Action Management
    private void PlayerTurnStart()
    {
        ChangeBattleState(BattleState.PlayerTurn_AwaitingInput);
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
            OnGestureRecognized(selectedSkill); // 제스처 인식 없이 자동 성공
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
        BattleEventManager.Instance?.RequestShowCharacterDialogue(CharacterDialogue.AttackSuccess(currentMonster.displayName, damage));
        yield return new WaitForSeconds(2.5f);

        if (currentMonsterHP <= 0) EndBattle(true);
        else StartCoroutine(MonsterTurn());
    }

    private IEnumerator PlayerAttackFailed()
    {
        BattleEventManager.Instance?.RequestShowCharacterDialogue(CharacterDialogue.AttackFail);
        yield return new WaitForSeconds(2.5f);
        StartCoroutine(MonsterTurn());
    }

    private IEnumerator MonsterTurn()
    {
        ChangeBattleState(BattleState.MonsterTurn);
        yield return new WaitForSeconds(1f);

        ChangeBattleState(BattleState.ActionInProgress);
        int damage = currentMonster.attackPower;
        PlayerStatsManager.Instance.TakeDamage(damage);
        currentPlayerHP = PlayerStatsManager.Instance.GetCurrentHP();
        BattleEventManager.Instance?.RequestShowCharacterDialogue(CharacterDialogue.MonsterAttack(currentMonster.displayName, damage));
        yield return new WaitForSeconds(2.5f);

        if (currentPlayerHP <= 0) EndBattle(false);
        else PlayerTurnStart();
    }

    public void AttemptEscape()
    {
        if (CurrentBattleState != BattleState.PlayerTurn_AwaitingInput) return;

        ChangeBattleState(BattleState.ActionInProgress);
        bool escapeSuccess = Random.value > 0.5f;

        if (escapeSuccess) EndBattle(false, true);
        else
        {
            BattleEventManager.Instance?.RequestShowCharacterDialogue(CharacterDialogue.EscapeFail);
            StartCoroutine(MonsterTurn());
        }
    }
    #endregion

    #region Battle End & State Change
    private void EndBattle(bool playerWon, bool escaped = false)
    {
        if (escaped)
        {
            ChangeBattleState(BattleState.Escaped);
        }
        else
        {
            ChangeBattleState(playerWon ? BattleState.BattleOver_Victory : BattleState.BattleOver_Defeat);
        }

        if (playerWon && !string.IsNullOrEmpty(defeatedMonsterID))
        {
            GameDataManager.Instance.AddDefeatedMonster(defeatedMonsterID);
        }

        if (playerWon)
        {
            GameEventManager.Instance.TriggerBattleOver(defeatedMonsterID);
        }

        GameDataManager.Instance.SaveState();
        StartCoroutine(ReturnToField());
    }



    private IEnumerator ReturnToField()
    {
        yield return new WaitForSeconds(3f);
        string returnScene = GameDataManager.Instance.PreviousSceneName;
        SceneManager.LoadScene(string.IsNullOrEmpty(returnScene) ? "MainMenu" : returnScene);
    }

    private void ChangeBattleState(BattleState newState)
    {
        if (CurrentBattleState == newState) return;
        CurrentBattleState = newState;
        BattleEventManager.Instance?.TriggerBattleStateChanged(newState);
    }
    #endregion
}