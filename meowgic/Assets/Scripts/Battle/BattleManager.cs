using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    public BattleState CurrentBattleState { get; private set; }
    private MonsterDataSO currentMonster;
    private string defeatedMonsterID;
    private int currentPlayerHP;
    private int currentMonsterHP;
    private string selectedSkill = "";
    private string selectedGestureType = "";

    private HolisticJsonSender holisticExtractor;
    private bool gestureSuccess = false;
    private float gestureStartTime;

    [SerializeField]
    private float gestureTimeoutSeconds = 10f;

    [SerializeField]
    private float gestureWarmupSeconds = 0.5f;

    [Header("Tutorial Settings")]
    public bool isTutorialMode = false;

    private static class SystemMessages
    {
        public const string MediaPipeStabilizing = "양손바닥 보여줘!";
        public const string MediaPipeReady = "안정화 완료!";
    }

    private static readonly Dictionary<string, string> GestureMapping = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
    {
        {"안녕하세요", "hello"},
        {"안녕", "hello"},
        {"감사합니다", "thx"},
        {"감사", "thx"},
        {"고마워", "thx"},
        {"공부", "study"},
        {"학습", "study"},
        {"좋아", "okay"},
        {"오케이", "okay"},
        {"확인", "okay"},
        {"나", "me"},
        {"저", "me"},
        {"너", "you"},
        {"당신", "you"},
        {"도착", "arrive"},
        {"도착했어", "arrive"},
        {"만나서반가워", "nicetomeet"},
        {"반가워", "nicetomeet"},
        {"처음뵙겠습니다", "nicetomeet"},
        {"없음", "none"},
        {"사랑", "love"},
        {"좋아해", "love"},
        {"사랑해", "love"},
        {"싫어", "hate"},
        {"싫어해", "hate"},
        {"전화", "call"},
        {"통화", "call"},
        {"좋은", "goodnice"},
        {"좋다", "goodnice"},
        {"미안", "sorry"},
        {"미안해", "sorry"},
        {"죄송", "sorry"},
        {"hello", "hello"},
        {"thx", "thx"},
        {"study", "study"},
        {"okay", "okay"},
        {"me", "me"},
        {"you", "you"},
        {"arrive", "arrive"},
        {"nicetomeet", "nicetomeet"},
        {"none", "none"},
        {"love", "love"},
        {"hate", "hate"},
        {"call", "call"},
        {"goodnice", "goodnice"},
        {"sorry", "sorry"}
    };

    private string NormalizeGestureId(string gesture)
    {
        if (string.IsNullOrEmpty(gesture))
            return "none";

        gesture = gesture.Trim();

        if (GestureMapping.TryGetValue(gesture, out string normalized))
            return normalized;

        return gesture.ToLower();
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnPlayerSkillSelected += HandlePlayerSkillSelection;
            BattleEventManager.Instance.OnPlayerRequestsAttack += HandleAttackRequest;
            BattleEventManager.Instance.OnPlayerAttemptEscape += AttemptEscape;
        }
        else
        {
            Debug.LogError("[BattleManager] BattleEventManager.Instance가 없습니다!");
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
            holisticExtractor.OnPredictionReceived -= OnGestureRecognized;
        }
    }

    void Start()
    {
        isTutorialMode = GameDataManager.Instance.IsNextBattleTutorial;

        if (isTutorialMode)
        {
            Debug.Log("[BattleManager] 튜토리얼 전투 시작!");
        }
        else
        {
            Debug.Log("[BattleManager] 일반 전투 시작!");
        }

        StartCoroutine(InitializeBattleSequence());

        if (StoryProgressManager.Instance != null)
        {
            var currentStory = GameDataManager.Instance.CurrentStoryID;

            if (currentStory == "cheddar_attacked")
            {
                GameDataManager.Instance.SetCurrentStoryID("first_real_battle");
                GameDataManager.Instance.SaveState();
                Debug.Log("[BattleManager] 스토리 상태를 first_real_battle로 변경");
            }
        }
    }

    private IEnumerator InitializeBattleSequence()
    {
        ChangeBattleState(BattleState.Initializing);

        holisticExtractor = FindObjectOfType<HolisticJsonSender>();

        if (holisticExtractor != null)
        {
            holisticExtractor.OnPredictionReceived += OnGestureRecognized;
        }
        else
        {
            Debug.LogError("[BattleManager] HolisticJsonSender를 찾지 못했습니다!");
        }

        if (!LoadBattleData())
        {
            Debug.LogError("[BattleManager] 전투 데이터 로드 실패");
            FadeManager.Instance.LoadSceneWithFade("MainMenu");
            yield break;
        }
        LoadPlayerData();

        BattleEventManager.Instance?.NotifyPlayerHPChanged(currentPlayerHP, PlayerStatsManager.Instance.GetMaxHP());
        BattleEventManager.Instance?.NotifyMonsterHPChanged(currentMonsterHP, currentMonster.maxHP);

        yield return StartCoroutine(MonitorStabilization());

        BattleEventManager.Instance.TriggerBattleEvent(BattleEventType.BattleStarted);

        if (!isTutorialMode && !string.IsNullOrEmpty(GameDataManager.Instance.CurrentBattleDialogueID))
        {
            DialogueManager.Instance.StartDialogue(GameDataManager.Instance.CurrentBattleDialogueID);
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        }

        if (!isTutorialMode)
        {
            PlayerTurnStart();
        }
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
        if (CurrentBattleState != BattleState.PlayerTurn_SelectingSkill)
            return;

        selectedSkill = skillName;

        if (SkillDatabase.Instance != null)
        {
            string rawGestureType = SkillDatabase.Instance.GetGestureType(skillName);
            selectedGestureType = NormalizeGestureId(rawGestureType);
        }
        else
        {
            selectedGestureType = NormalizeGestureId(skillName);
        }

        StartGestureRecognition();
    }

    public void SetSelectedSkill(string skillName)
    {
        selectedSkill = skillName;
        selectedGestureType = NormalizeGestureId(skillName);
        Debug.Log($"[BattleManager] 스킬 설정: {skillName} -> {selectedGestureType}");
    }

    public void StartGestureRecognition()
    {
        ChangeBattleState(BattleState.PlayerTurn_Gesture);

        if (holisticExtractor == null)
        {
            Debug.LogWarning("[BattleManager] ⚠️⚠️⚠️ HolisticExtractor가 NULL입니다! 즉시 성공 처리합니다");
            OnGestureRecognized(selectedGestureType);
            return;
        }

        Debug.Log($"[BattleManager] ✅ HolisticExtractor 발견! 제스처 인식 시작: {selectedGestureType}");

        gestureSuccess = false;
        gestureStartTime = Time.time;
        holisticExtractor.StartStreaming();
        StartCoroutine(WaitForGestureResult());
    }

    private IEnumerator WaitForGestureResult()
    {
        yield return new WaitForSeconds(gestureWarmupSeconds);

        gestureSuccess = false;

        float elapsedTime = 0f;

        while (elapsedTime < gestureTimeoutSeconds)
        {
            if (gestureSuccess)
            {
                holisticExtractor.StopStreaming();
                ProcessGestureResult(selectedGestureType);
                yield break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        holisticExtractor.StopStreaming();
        ProcessGestureResult("no_match");
    }

    public void OnGestureRecognized(string prediction)
    {
        float timeSinceStart = Time.time - gestureStartTime;

        if (timeSinceStart < gestureWarmupSeconds)
        {
            Debug.Log($"[BattleManager] ⏱️ Warmup 중 무시: {prediction} (경과: {timeSinceStart:F2}초)");
            return;
        }

        if (CurrentBattleState != BattleState.PlayerTurn_Gesture || gestureSuccess)
            return;

        string normalizedPrediction = NormalizeGestureId(prediction);

        Debug.Log($"[BattleManager] 🎯 제스처 수신: '{prediction}' → '{normalizedPrediction}' (목표: '{selectedGestureType}', 경과: {timeSinceStart:F2}초)");

        if (selectedGestureType.Equals(normalizedPrediction, System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"[BattleManager] ✅✅✅ 제스처 매칭 성공!");
            gestureSuccess = true;
        }
        else
        {
            Debug.Log($"[BattleManager] ❌ 제스처 불일치");
        }
    }

    private void ProcessGestureResult(string prediction)
    {
        ChangeBattleState(BattleState.ActionInProgress);

        bool isMatch = selectedGestureType.Equals(prediction, System.StringComparison.OrdinalIgnoreCase);

        if (isMatch)
            StartCoroutine(ExecutePlayerAttack());
        else
            StartCoroutine(PlayerAttackFailed());
    }

    private IEnumerator ExecutePlayerAttack()
    {
        BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.PlayerAttackSuccess);

        if (isTutorialMode)
        {
            yield break;
        }

        int damage = PlayerStatsManager.Instance.GetAttackPower();
        currentMonsterHP = Mathf.Max(0, currentMonsterHP - damage);

        BattleEventManager.Instance?.NotifyMonsterHPChanged(currentMonsterHP, currentMonster.maxHP);

        DialogueManager.Instance.StartDialogue("battle_attack_success");
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        if (currentMonsterHP <= 0)
        {
            EndBattle(true);
        }
        else
        {
            StartCoroutine(MonsterTurn());
        }
    }

    private IEnumerator PlayerAttackFailed()
    {
        BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.PlayerAttackFail);

        if (!isTutorialMode)
        {
            DialogueManager.Instance.StartDialogue("battle_attack_fail");
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

            StartCoroutine(MonsterTurn());
        }
    }

    private IEnumerator MonsterTurn()
    {
        if (isTutorialMode) yield break;

        ChangeBattleState(BattleState.MonsterTurn);
        BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.MonsterTurnStarted);
        yield return new WaitForSeconds(1f);

        ChangeBattleState(BattleState.ActionInProgress);
        int damage = currentMonster.attackPower;
        PlayerStatsManager.Instance.TakeDamage(damage);
        currentPlayerHP = PlayerStatsManager.Instance.GetCurrentHP();

        BattleEventManager.Instance?.NotifyPlayerHPChanged(currentPlayerHP, PlayerStatsManager.Instance.GetMaxHP());
        BattleEventManager.Instance?.TriggerBattleEvent(BattleEventType.MonsterAttack);

        DialogueManager.Instance.StartDialogue("battle_monster_attack");
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        if (currentPlayerHP <= 0)
        {
            EndBattle(false);
        }
        else
        {
            PlayerTurnStart();
        }
    }

    public void AttemptEscape()
    {
        if (isTutorialMode) return;

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

    public bool IsTutorialMode()
    {
        return isTutorialMode;
    }

    public void EndTutorialMode()
    {
        if (!isTutorialMode)
        {
            Debug.LogWarning("[BattleManager] 이미 정상 전투 모드입니다.");
            return;
        }

        Debug.Log("[BattleManager] 튜토리얼 모드 종료 → 정상 전투 시작");
        isTutorialMode = false;

        GameDataManager.Instance.IsNextBattleTutorial = false;

        int damage = PlayerStatsManager.Instance.GetAttackPower();
        currentMonsterHP = Mathf.Max(0, currentMonsterHP - damage);
        BattleEventManager.Instance?.NotifyMonsterHPChanged(currentMonsterHP, currentMonster.maxHP);

        if (currentMonsterHP <= 0)
        {
            EndBattle(true);
        }
        else
        {
            StartCoroutine(MonsterTurn());
        }
    }

    public void ChangeBattleStatePublic(BattleState newState)
    {
        ChangeBattleState(newState);
    }
}