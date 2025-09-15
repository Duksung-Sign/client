using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    public MonsterDataSO currentMonster;
    private int currentPlayerHP;
    private int currentMonsterHP;

    private HolisticJsonSender holisticExtractor;

    private bool isPlayerTurn = true;
    private bool waitingForGesture = false;
    private string selectedSkill = "";
    private bool gestureResponseReceived = false;
    private string battleResult = "";
    private string defeatedMonsterID = "";
    private bool isInitialStabilizationComplete = false;
    private bool battleInitialized = false;

    // 캐릭터 대사들 (다이나믹 버블용)
    private static class CharacterDialogue
    {
        public static string AttackSuccess(string monsterName, int damage)
            => $"성공이야! {monsterName}에게 {damage}의 피해를 입혔어!";

        public const string AttackFail = "인식 실패! 다시 해 보자!";

        public static string MonsterAttack(string monsterName, int damage)
            => $"{monsterName}의 공격! {damage}의 피해를 입었어.";

        public static string EscapeBlocked(string monsterName)
            => $"{monsterName}에게서 벗어날 수 없다.";

        public const string EscapeFail = "탈출에 실패했어!";
    }

    // 시스템 메시지들 (statusText용)
    private static class SystemMessages
    {
        public const string MediaPipeStabilizing = "양 손바닥을 보여주세요.";
        public const string MediaPipeReady = "안정화 완료!";
        public const string WaitingForGesture = "제스처를 인식 중...";
        public const string InitializingBattle = "전투 준비 중...";
    }

    // [수정됨] Awake에서 새로운 이벤트도 구독
    void Awake()
    {
        if (BattleEventManager.Instance != null)
        {
            // 기존 이벤트 구독
            BattleEventManager.Instance.OnPlayerSkillSelected += HandlePlayerSkillSelection;
            BattleEventManager.Instance.OnPlayerAttemptEscape += AttemptEscape;
            BattleEventManager.Instance.OnRequestStartGestureRecognition += StartGestureRecognition;

            // [추가] 새로운 이벤트 구독
            BattleEventManager.Instance.OnPlayerRequestsAttack += HandleAttackRequest;
        }
    }

    // [수정됨] OnDestroy에서 새로운 이벤트도 구독 해제
    void OnDestroy()
    {
        if (BattleEventManager.Instance != null)
        {
            // 기존 이벤트 구독 해제
            BattleEventManager.Instance.OnPlayerSkillSelected -= HandlePlayerSkillSelection;
            BattleEventManager.Instance.OnPlayerAttemptEscape -= AttemptEscape;
            BattleEventManager.Instance.OnRequestStartGestureRecognition -= StartGestureRecognition;

            // [추가] 새로운 이벤트 구독 해제
            BattleEventManager.Instance.OnPlayerRequestsAttack -= HandleAttackRequest;
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
        // UI 컴포넌트 초기화 및 null 체크
        yield return StartCoroutine(InitializeComponents());

        if (!ValidateComponents())
        {
            Debug.LogError("필수 컴포넌트 초기화 실패!");
            yield break;
        }

        // 데이터 로드
        LoadMonsterData();
        LoadPlayerData();

        BattleEventManager.Instance.RequestUpdateStatus(SystemMessages.InitializingBattle);

        // MediaPipe 안정화 대기
        yield return StartCoroutine(MonitorStabilization());

        // 전투 시작
        InitializeBattle();
        battleInitialized = true;
        PlayerTurnStart();
    }

    private IEnumerator InitializeComponents()
    {
        holisticExtractor = FindObjectOfType<HolisticJsonSender>();
        if (holisticExtractor != null)
        {
            holisticExtractor.OnGestureRecognitionComplete += OnGestureRecognized;
        }

        // 컴포넌트들이 Start() 메서드를 실행할 시간을 줌
        yield return new WaitForEndOfFrame();
    }

    private bool ValidateComponents()
    {
        bool valid = true;

        if (holisticExtractor == null)
        {
            Debug.LogWarning("HolisticJsonSender를 찾을 수 없습니다. 제스처 인식이 비활성화됩니다.");
        }

        return valid;
    }

    void LoadMonsterData()
    {
        if (GameManager.Instance?.currentBattleMonster != null)
        {
            currentMonster = GameManager.Instance.currentBattleMonster;
            currentMonsterHP = currentMonster.maxHP;

            Debug.Log($"몬스터 로드 완료: {currentMonster.GetDisplayName()}, HP: {currentMonsterHP}");
        }
        else
        {
            Debug.LogError("전투할 몬스터 데이터가 없습니다!");
        }
    }

    void LoadPlayerData()
    {
        var statsMgr = PlayerStatsManager.Instance;
        if (statsMgr != null)
        {
            currentPlayerHP = statsMgr.GetCurrentHP();
            Debug.Log($"플레이어 HP 로드: {currentPlayerHP}");
        }
        else
        {
            Debug.LogError("PlayerStatsManager를 찾을 수 없습니다!");
            currentPlayerHP = 100; // 기본값
        }
    }

    void InitializeBattle()
    {
        isPlayerTurn = true;
        ResetGestureState();

        BattleEventManager.Instance.RequestUpdateStatus("");
    }

    private IEnumerator MonitorStabilization()
    {
        if (holisticExtractor == null)
        {
            Debug.Log("HolisticJsonSender 없음 - 제스처 인식 건너뜀");
            isInitialStabilizationComplete = true;
            yield break;
        }

        BattleEventManager.Instance.RequestUpdateStatus(SystemMessages.MediaPipeStabilizing);

        // 최대 10초 대기 (무한 대기 방지)
        float maxWaitTime = 10f;
        float waitTime = 0f;

        while (waitTime < maxWaitTime && !holisticExtractor.IsMediaPipeReady())
        {
            waitTime += Time.deltaTime;
            yield return null;
        }

        isInitialStabilizationComplete = true;

        if (waitTime >= maxWaitTime)
        {
            Debug.LogWarning("MediaPipe 안정화 타임아웃 - 제스처 인식 없이 진행");
            BattleEventManager.Instance.RequestUpdateStatus("제스처 인식 비활성화");
        }
        else
        {
            BattleEventManager.Instance.RequestUpdateStatus(SystemMessages.MediaPipeReady);
        }

        yield return new WaitForSeconds(1.5f);
    }

    void PlayerTurnStart()
    {
        if (!battleInitialized) return;

        isPlayerTurn = true;
        ResetGestureState();

        BattleEventManager.Instance.RequestUpdateStatus("");

        // [추가] 플레이어 턴 시작 시 메인 UI 표시
        BattleEventManager.Instance.RequestShowMainUI(true);
        BattleEventManager.Instance.RequestShowSkillPanel(false);

        BattleEventManager.Instance.TriggerBattleEvent(BattleEventType.PlayerTurnStarted);
        Debug.Log("플레이어 턴 시작 - 메인 UI 표시");
    }

    // [추가] 공격 버튼 클릭 처리 - UI 흐름을 BattleManager가 통제
    private void HandleAttackRequest()
    {
        if (!isPlayerTurn || waitingForGesture || !battleInitialized)
        {
            Debug.Log($"공격 요청 무시됨 - 턴: {isPlayerTurn}, 대기: {waitingForGesture}, 초기화: {battleInitialized}");
            return;
        }

        Debug.Log("BattleManager: 공격 요청 접수. UI 변경 지시.");

        // UIManager에게 메인 UI를 숨기고 스킬 패널을 보여주라고 '명령'한다.
        BattleEventManager.Instance.RequestShowMainUI(false);
        BattleEventManager.Instance.RequestShowSkillPanel(true);
    }

    // [수정됨] 스킬 선택 처리 - 스킬 패널 닫기도 BattleManager가 지시
    private void HandlePlayerSkillSelection(string skillName)
    {
        if (!isPlayerTurn || waitingForGesture || !battleInitialized)
        {
            Debug.Log($"스킬 선택 무시됨 - 턴: {isPlayerTurn}, 대기: {waitingForGesture}, 초기화: {battleInitialized}");
            return;
        }

        selectedSkill = skillName.Trim().ToLower();
        Debug.Log($"BattleManager: 스킬 선택({skillName}) 접수. 스킬 패널 닫기 지시.");

        // 스킬이 선택되었으니, UIManager에게 스킬 패널을 닫으라고 '명령'한다.
        BattleEventManager.Instance.RequestShowSkillPanel(false);

        BattleEventManager.Instance.TriggerBattleEvent(BattleEventType.PlayerSkillSelected);
        StartGestureRecognition();
    }

    public void StartGestureRecognition()
    {
        if (!isPlayerTurn || waitingForGesture || !isInitialStabilizationComplete || !battleInitialized)
        {
            Debug.Log($"제스처 인식 시작 실패 - 턴: {isPlayerTurn}, 대기: {waitingForGesture}, 안정화: {isInitialStabilizationComplete}, 초기화: {battleInitialized}");
            return;
        }

        if (holisticExtractor == null)
        {
            Debug.Log("HolisticJsonSender 없음 - 자동 성공 처리");
            ProcessGestureResult(selectedSkill); // 제스처 인식 없이 자동 성공
            return;
        }

        ResetGestureState();
        waitingForGesture = true;

        BattleEventManager.Instance.RequestUpdateStatus(SystemMessages.WaitingForGesture);
        BattleEventManager.Instance.TriggerBattleEvent(BattleEventType.GestureRecognitionStarted);
        Debug.Log("제스처 인식 시작");

        StartCoroutine(WaitForGestureRecognition());
    }

    private void ResetGestureState()
    {
        waitingForGesture = false;
        gestureResponseReceived = false;
    }

    IEnumerator WaitForGestureRecognition()
    {
        if (holisticExtractor != null)
        {
            holisticExtractor.StartGestureCollection();
        }

        float responseWaitTime = 15f;
        while (responseWaitTime > 0 && !gestureResponseReceived)
        {
            responseWaitTime -= Time.deltaTime;

            if (responseWaitTime > 0)
            {
                BattleEventManager.Instance.RequestUpdateStatus($"{SystemMessages.WaitingForGesture} ({responseWaitTime:F0}초)");
            }

            yield return null;
        }

        if (!gestureResponseReceived)
        {
            Debug.Log("제스처 인식 타임아웃");
            ProcessGestureResult("timeout");
        }
    }

    public void OnGestureRecognized(string prediction)
    {
        if (!waitingForGesture || gestureResponseReceived)
        {
            Debug.Log($"제스처 응답 무시됨 - 대기중: {waitingForGesture}, 이미받음: {gestureResponseReceived}");
            return;
        }

        gestureResponseReceived = true;
        string cleanPrediction = prediction?.Trim().ToLower() ?? "none";

        Debug.Log($"제스처 인식됨: {cleanPrediction} (선택된 스킬: {selectedSkill})");
        ProcessGestureResult(cleanPrediction);
    }

    private void ProcessGestureResult(string prediction)
    {
        if (!isPlayerTurn) return;

        waitingForGesture = false;
        BattleEventManager.Instance.RequestUpdateStatus("");

        // 제스처 매칭 확인 (공백 및 대소문자 무시)
        bool isMatch = !string.IsNullOrEmpty(selectedSkill) &&
                      !string.IsNullOrEmpty(prediction) &&
                      selectedSkill.Equals(prediction, System.StringComparison.OrdinalIgnoreCase);

        Debug.Log($"제스처 매칭 결과: {isMatch} (선택: {selectedSkill}, 인식: {prediction})");

        if (isMatch)
        {
            BattleEventManager.Instance.TriggerBattleEvent(BattleEventType.PlayerAttackSuccess);
            ExecutePlayerAttack();
        }
        else
        {
            BattleEventManager.Instance.TriggerBattleEvent(BattleEventType.PlayerAttackFail);
            PlayerAttackFailed();
        }
    }

    void ExecutePlayerAttack()
    {
        var statsMgr = PlayerStatsManager.Instance;
        int damage = statsMgr?.GetAttackPower() ?? 10; // 기본 공격력
        currentMonsterHP = Mathf.Max(0, currentMonsterHP - damage);

        string message = CharacterDialogue.AttackSuccess(currentMonster?.GetDisplayName() ?? "몬스터", damage);
        BattleEventManager.Instance.RequestShowCharacterDialogue(message);

        Debug.Log($"플레이어 공격 성공! 데미지: {damage}, 몬스터 HP: {currentMonsterHP}");

        if (currentMonsterHP <= 0)
        {
            EndBattle(true);
        }
        else
        {
            isPlayerTurn = false;
            StartCoroutine(MonsterTurn());
        }
    }

    void PlayerAttackFailed()
    {
        BattleEventManager.Instance.RequestShowCharacterDialogue(CharacterDialogue.AttackFail);
        Debug.Log("플레이어 공격 실패!");

        isPlayerTurn = false;
        StartCoroutine(MonsterTurn());
    }

    IEnumerator MonsterTurn()
    {
        yield return new WaitForSeconds(2.5f);

        var statsMgr = PlayerStatsManager.Instance;
        int damage = currentMonster?.attackPower ?? 5; // 기본 공격력

        if (statsMgr != null)
        {
            statsMgr.TakeDamage(damage);
            currentPlayerHP = statsMgr.GetCurrentHP();
        }
        else
        {
            currentPlayerHP = Mathf.Max(0, currentPlayerHP - damage);
        }

        string message = CharacterDialogue.MonsterAttack(currentMonster?.GetDisplayName() ?? "몬스터", damage);
        BattleEventManager.Instance.RequestShowCharacterDialogue(message);

        Debug.Log($"몬스터 공격! 데미지: {damage}, 플레이어 HP: {currentPlayerHP}");

        if (currentPlayerHP <= 0)
        {
            EndBattle(false);
        }
        else
        {
            yield return new WaitForSeconds(2.5f);
            PlayerTurnStart(); // 다시 플레이어 턴으로, 메인 UI가 표시됨
        }
    }

    public void AttemptEscape()
    {
        if (!isPlayerTurn || !battleInitialized) return;

        if (currentMonster != null && !currentMonster.canEscape)
        {
            string message = CharacterDialogue.EscapeBlocked(currentMonster.GetDisplayName());
            BattleEventManager.Instance.RequestShowCharacterDialogue(message);
            Debug.Log("도망 불가능한 몬스터");
            return;
        }

        bool escapeSuccess = Random.Range(0f, 1f) > 0.5f;
        if (escapeSuccess)
        {
            Debug.Log("도망 성공!");
            EndBattle(false);
        }
        else
        {
            BattleEventManager.Instance.RequestShowCharacterDialogue(CharacterDialogue.EscapeFail);
            Debug.Log("도망 실패!");
            isPlayerTurn = false;
            StartCoroutine(MonsterTurn());
        }
    }

    void EndBattle(bool playerWon)
    {
        battleInitialized = false; // 전투 종료

        if (playerWon)
        {
            battleResult = "victory";
            Debug.Log("플레이어 승리!");
        }
        else
        {
            Debug.Log("전투 종료");
        }

        StartCoroutine(ReturnToField());
    }

    IEnumerator ReturnToField()
    {
        if (battleResult == "victory" && currentMonster != null)
        {
            GameManager.Instance?.SetMonsterDefeated(currentMonster.name);
        }

        yield return new WaitForSeconds(3f); // 결과 확인 시간 증가

        string returnScene = GameManager.Instance?.GetPreviousScene();

        if (!string.IsNullOrEmpty(returnScene))
        {
            Debug.Log($"이전 씬으로 이동: {returnScene}");
            SceneManager.LoadScene(returnScene);
        }
        else
        {
            Debug.LogError("돌아갈 씬을 찾을 수 없습니다! 게임을 재시작하세요.");
            SceneManager.LoadScene("MainMenu");
        }
    }
}