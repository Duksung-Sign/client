using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    #region Battle Data & State
    private MonsterDataSO currentMonster;
    private string defeatedMonsterID;
    private int currentPlayerHP;
    private int currentMonsterHP;
    private bool isPlayerTurn = true;
    private string selectedSkill = "";
    private bool battleInitialized = false;
    private string battleResult = "";
    #endregion

    #region Gesture Recognition
    private HolisticJsonSender holisticExtractor;
    private bool waitingForGesture = false;
    private bool gestureResponseReceived = false;
    private bool isInitialStabilizationComplete = false;
    #endregion

    #region Dialogue & Message Constants
    private static class CharacterDialogue
    {
        public static string AttackSuccess(string monsterName, int damage) => $"성공이야! {monsterName}에게 {damage}의 피해를 입혔어!";
        public const string AttackFail = "인식 실패! 다시 해 보자!";
        public static string MonsterAttack(string monsterName, int damage) => $"{monsterName}의 공격! {damage}의 피해를 입었어.";
        public const string EscapeFail = "탈출에 실패했어!";
        public static string EscapeBlocked(string monsterName) => $"{monsterName}는 도망칠 수 없는 상대야!";
        public const string Victory = "승리했어!";
        public const string Defeat = "패배했어...";
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
        BattleEventManager.Instance?.RequestUpdateStatus(SystemMessages.InitializingBattle);

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

        InitializeBattle();
        PlayerTurnStart();
    }

    private bool LoadBattleData()
    {
        if (GameDataManager.Instance == null)
        {
            Debug.LogError("[BattleManager] GameDataManager가 없습니다!");
            return false;
        }

        currentMonster = GameDataManager.Instance.CurrentBattleMonster;
        defeatedMonsterID = GameDataManager.Instance.CurrentBattleMonsterID;

        if (currentMonster == null)
        {
            Debug.LogError("[BattleManager] 전투할 몬스터 데이터가 GameDataManager에 없습니다!");
            return false;
        }

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
        if (holisticExtractor == null)
        {
            isInitialStabilizationComplete = true;
            yield break;
        }
        BattleEventManager.Instance?.RequestUpdateStatus(SystemMessages.MediaPipeStabilizing);
        float waitTime = 0f;
        while (waitTime < 10f && !holisticExtractor.IsMediaPipeReady())
        {
            waitTime += Time.deltaTime;
            yield return null;
        }
        isInitialStabilizationComplete = true;
        BattleEventManager.Instance?.RequestUpdateStatus(SystemMessages.MediaPipeReady);
        yield return new WaitForSeconds(1.5f);
    }

    private void InitializeBattle()
    {
        battleInitialized = true;
        isPlayerTurn = true;
        BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.BattleStarted);
    }
    #endregion

    #region Turn & Action Management
    private void PlayerTurnStart()
    {
        isPlayerTurn = true;
        BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.PlayerTurnStarted);
    }

    private void HandleAttackRequest()
    {
        if (!isPlayerTurn || !battleInitialized) return;
        BattleEventManager.Instance?.RequestShowSkillPanel(true);
    }

    private void HandlePlayerSkillSelection(string skillName)
    {
        if (!isPlayerTurn || !battleInitialized) return;
        selectedSkill = skillName;
        BattleEventManager.Instance?.RequestShowSkillPanel(false);
        StartGestureRecognition();
    }

    private void StartGestureRecognition()
    {
        if (!isPlayerTurn || !isInitialStabilizationComplete) return;

        if (holisticExtractor == null)
        {
            ProcessGestureResult(selectedSkill); // 제스처 인식 없이 자동 성공
            return;
        }

        waitingForGesture = true;
        gestureResponseReceived = false;
        BattleEventManager.Instance?.RequestUpdateStatus(SystemMessages.WaitingForGesture);
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
            ProcessGestureResult("timeout");
        }
    }

    public void OnGestureRecognized(string prediction)
    {
        if (!waitingForGesture || gestureResponseReceived) return;
        gestureResponseReceived = true;
        ProcessGestureResult(prediction);
    }

    private void ProcessGestureResult(string prediction)
    {
        waitingForGesture = false;
        bool isMatch = selectedSkill.Equals(prediction, System.StringComparison.OrdinalIgnoreCase);
        if (isMatch) ExecutePlayerAttack();
        else PlayerAttackFailed();
    }

    private void ExecutePlayerAttack()
    {
        int damage = PlayerStatsManager.Instance.GetAttackPower();
        currentMonsterHP = Mathf.Max(0, currentMonsterHP - damage);
        BattleEventManager.Instance?.RequestShowCharacterDialogue(CharacterDialogue.AttackSuccess(currentMonster.displayName, damage));

        if (currentMonsterHP <= 0) EndBattle(true);
        else StartCoroutine(MonsterTurn());
    }

    private void PlayerAttackFailed()
    {
        BattleEventManager.Instance?.RequestShowCharacterDialogue(CharacterDialogue.AttackFail);
        StartCoroutine(MonsterTurn());
    }

    private IEnumerator MonsterTurn()
    {
        isPlayerTurn = false;
        yield return new WaitForSeconds(2.5f);

        int damage = currentMonster.attackPower;
        PlayerStatsManager.Instance.TakeDamage(damage);
        currentPlayerHP = PlayerStatsManager.Instance.GetCurrentHP();
        BattleEventManager.Instance?.RequestShowCharacterDialogue(CharacterDialogue.MonsterAttack(currentMonster.displayName, damage));

        if (currentPlayerHP <= 0) EndBattle(false);
        else PlayerTurnStart();
    }

    public void AttemptEscape()
    {
        if (!isPlayerTurn || !battleInitialized) return;
        bool escapeSuccess = Random.value > 0.5f;
        if (escapeSuccess) EndBattle(false, true);
        else
        {
            BattleEventManager.Instance?.RequestShowCharacterDialogue(CharacterDialogue.EscapeFail);
            StartCoroutine(MonsterTurn());
        }
    }
    #endregion

    #region Battle End
    private void EndBattle(bool playerWon, bool escaped = false)
    {
        battleInitialized = false;
        if (playerWon)
        {
            if (!string.IsNullOrEmpty(defeatedMonsterID))
            {
                GameDataManager.Instance.AddDefeatedMonster(defeatedMonsterID);
            }
        }

        GameDataManager.Instance.SaveState();
        StartCoroutine(ReturnToField());
    }

    private IEnumerator ReturnToField()
    {
        yield return new WaitForSeconds(3f);
        string returnScene = GameDataManager.Instance.PreviousSceneName;
        if (!string.IsNullOrEmpty(returnScene))
        {
            SceneManager.LoadScene(returnScene);
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
    #endregion
}