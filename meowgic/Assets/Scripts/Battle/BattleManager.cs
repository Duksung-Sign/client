using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    [Header("배틀 참가자")]
    public MonsterDataSO currentMonster;

    [Header("현재 배틀 상태")]
    private int currentPlayerHP;
    private int currentMonsterHP;

    [Header("UI 참조")]
    private BattleUIManager uiManager;

    [Header("미디어파이프 참조")]
    private HolisticJsonSender holisticExtractor;

    [Header("배틀 상태")]
    private bool isPlayerTurn = true;
    private bool waitingForGesture = false;
    private string selectedSkill = "";

    // *** 타이밍 이슈 해결을 위한 추가 변수들 ***
    private bool gestureResponseReceived = false;
    private string receivedPrediction = "";
    private Coroutine gestureTimeoutCoroutine;

    // 배틀 결과 추적을 위한 추가 변수들
    private string battleResult = ""; // "victory", "escape", "defeat"
    private string defeatedMonsterID = ""; // 죽인 몬스터의 ID

    // 안정화 관련 UI 안내
    private bool isInitialStabilizationComplete = false;

    void Start()
    {
        uiManager = FindObjectOfType<BattleUIManager>();
        holisticExtractor = FindObjectOfType<HolisticJsonSender>();

        LoadMonsterData();
        LoadPlayerData();
        InitializeBattle();

        StartCoroutine(MonitorStabilization());
    }

    void LoadMonsterData()
    {
        if (GameManager.Instance?.currentBattleMonster != null)
        {
            currentMonster = GameManager.Instance.currentBattleMonster;
            currentMonsterHP = currentMonster.maxHP;

            Debug.Log($"몬스터 로드: {currentMonster.monsterName} (HP: {currentMonsterHP})");

            if (uiManager != null && currentMonster.monsterSprite != null)
            {
                uiManager.SetMonsterImage(currentMonster.monsterSprite);
            }
        }
        else
        {
            Debug.LogError("GameManager에서 몬스터 정보를 가져올 수 없습니다!");
        }
    }

    void LoadPlayerData()
    {
        var statsMgr = PlayerStatsManager.Instance;
        if (statsMgr != null)
        {
            currentPlayerHP = statsMgr.GetCurrentHP();
            Debug.Log($"플레이어 로드: HP {currentPlayerHP}/{statsMgr.GetMaxHP()}, ATK {statsMgr.GetAttackPower()}");
        }
        else
        {
            Debug.LogError("PlayerStatsManager 인스턴스를 찾을 수 없습니다!");
            currentPlayerHP = 30;
        }
    }

    void InitializeBattle()
    {
        isPlayerTurn = true;
        battleResult = "";
        defeatedMonsterID = "";

        // *** 배틀 시작 시 제스처 상태 완전 리셋 ***
        ResetGestureState();

        if (uiManager != null)
        {
            uiManager.ShowMainBattleUI();
        }

        Debug.Log("배틀 초기화 완료 + 제스처 상태 리셋");
    }

    private IEnumerator MonitorStabilization()
    {
        if (holisticExtractor == null)
        {
            yield break;
        }

        int retryCount = 0;
        const int maxRetries = 3; // 최대 3번 재시도

        while (retryCount < maxRetries && !isInitialStabilizationComplete)
        {
            yield return StartCoroutine(AttemptStabilization(retryCount));

            if (!isInitialStabilizationComplete)
            {
                retryCount++;

                if (retryCount < maxRetries)
                {
                    // 재시도 안내
                    if (uiManager != null)
                    {
                        uiManager.ShowResult($"안정화를 재시도하고 있어! ({retryCount + 1}/{maxRetries})\n손을 카메라에 더 가까이 보여줘!");
                    }
                    yield return new WaitForSeconds(2f);
                }
            }
        }

        // 모든 재시도 실패 시 사용자 선택권 제공
        if (!isInitialStabilizationComplete)
        {
            yield return StartCoroutine(HandleStabilizationFailure());
        }
    }

    private IEnumerator AttemptStabilization(int attemptNumber)
    {
        // 시도 번호에 따른 안내 메시지
        string initialMessage = attemptNumber == 0 ?
            "미디어파이프 초기화 중이야!\n3초 동안 양 손바닥을 카메라에 보여줘!" :
            $"안정화를 시도하고 있어! ({attemptNumber + 1}/3)\n손을 카메라에 더 가까이 보여줘!";

        if (uiManager != null)
        {
            uiManager.ShowResult(initialMessage);
        }

        // MediaPipe 안정화 대기
        float waitTime = 0f;
        const float maxWaitTime = 8f; // 최대 8초 대기

        while (waitTime < maxWaitTime)
        {
            // 안정화 상태 체크
            if (holisticExtractor.IsMediaPipeStable() &&
                holisticExtractor.GetConsecutiveRealDataFrames() >= 3)
            {
                isInitialStabilizationComplete = true;

                if (uiManager != null)
                {
                    uiManager.ShowResult("미디어파이프 안정화 완료!\n이제 묘어를 준비하자!");
                }

                // 3초 후 메인 UI로 전환
                yield return new WaitForSeconds(3f);
                if (uiManager != null)
                {
                    uiManager.ShowMainBattleUI();
                }
                yield break;
            }

            // 진행 상황 업데이트 (매 2초마다)
            if (waitTime % 2f < 0.1f && uiManager != null)
            {
                int frames = holisticExtractor.GetConsecutiveRealDataFrames();
                float progress = Mathf.Clamp01(waitTime / maxWaitTime) * 100f;

                string progressMsg = attemptNumber == 0 ?
                    $"양 손바닥을 카메라에 보여줘!\n진행률: {frames}/3 프레임 ({progress:F0}%)" :
                    $"재시도 중이야!\n손가락을 펼쳐서 보여줘! ({progress:F0}%)";

                uiManager.ShowResult(progressMsg);
            }

            waitTime += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator HandleStabilizationFailure()
    {
        if (uiManager != null)
        {
            uiManager.ShowResult("안정화에 실패했어!\n조명은 밝게! 손은 잘 나오게!");
        }

        yield return new WaitForSeconds(4f);

        if (uiManager != null)
        {
            uiManager.ShowResult("5초 후에 다시 시작할게! 손을 준비해줘!");
        }

        // 5초 카운트다운
        for (int i = 5; i >= 1; i--)
        {
            yield return new WaitForSeconds(1f);
            if (uiManager != null)
            {
                uiManager.ShowResult($"{i}초 후 다시 시작한다? 준비됐지?");
            }
        }

        // 재시도
        yield return StartCoroutine(AttemptStabilization(0));

        if (!isInitialStabilizationComplete)
        {
            // 최종 실패 시 더미 데이터로 진행
            if (uiManager != null)
            {
                uiManager.ShowResult("실패하고 말았어.");
                yield return new WaitForSeconds(2f);
            }
            yield return StartCoroutine(ProceedWithDummyData());
        }
    }

    private IEnumerator ProceedWithDummyData()
    {
        if (uiManager != null)
        {
            uiManager.ShowResult("내가 도와줄게.\n인식이 정확하지 않을 수 있어!");
        }

        // HolisticExtractor를 더미 모드로 전환
        if (holisticExtractor != null)
        {
            holisticExtractor.SwitchToDummyData();
        }

        isInitialStabilizationComplete = true;

        yield return new WaitForSeconds(3f);

        if (uiManager != null)
        {
            uiManager.ShowMainBattleUI();
        }
        Debug.Log("더미 데이터 모드로 배틀 시작");
    }

    void ResetBattleUI()
    {
        if (uiManager != null)
        {
            uiManager.ShowMainBattleUI();
        }
    }

    public void UseSkill(string skillName)
    {
        if (!isPlayerTurn || waitingForGesture) return;
        if (!isInitialStabilizationComplete) return; // 안정화 완료 전에는 스킬 사용 불가

        // 상태 리셋
        ResetGestureState();
        selectedSkill = skillName;
        waitingForGesture = true;

        Debug.Log($"스킬 선택: {skillName} (상태 완전 리셋됨)");

        StartCoroutine(SkillCountdownAndGuidance(skillName));
    }

    private IEnumerator SkillCountdownAndGuidance(string skillName)
    {
        string skillDisplayName = GetSkillDisplayName(skillName);

        // 준비 안내
        if (uiManager != null)
        {
            uiManager.ShowResult($"묘어를 사용할 준비를 해줘!");
        }

        yield return new WaitForSeconds(1f);

        // 3, 2, 1 카운트다운
        for (int i = 3; i >= 1; i--)
        {
            if (uiManager != null)
            {
                uiManager.ShowResult($"{i}초 후 시작한다?\n{skillDisplayName}을(를) 준비해!");
            }
            yield return new WaitForSeconds(1f);
        }

        // 스킬 실행 안내
        if (uiManager != null)
        {
            uiManager.ShowResult($"{skillDisplayName}을(를) 보여줘!");
        }

        // 실제 제스처 인식 시작
        StartCoroutine(WaitForGestureRecognition());
    }

    private void ResetGestureState()
    {
        waitingForGesture = false;
        gestureResponseReceived = false;
        receivedPrediction = "";
        Debug.Log("제스처 상태 완전 리셋");
    }

    string GetSkillDisplayName(string skillName)
    {
        switch (skillName)
        {
            case "hello": return "안녕하세요";
            case "sorry": return "죄송합니다";
            case "thank": return "감사합니다";
            default: return skillName;
        }
    }

    IEnumerator WaitForGestureRecognition()
    {
        if (holisticExtractor != null)
        {
            holisticExtractor.StartGestureCollection();

            // 1단계: 프레임 수집 대기 (최대 5초)
            float collectWaitTime = 5f;
            while (collectWaitTime > 0 && holisticExtractor.IsCollectingFrames())
            {
                collectWaitTime -= Time.deltaTime;
                yield return null;
            }

            // 강제 전송이 필요한 경우
            if (collectWaitTime <= 0 && holisticExtractor.GetCurrentFrameCount() > 0)
            {
                Debug.Log("수집 타임아웃 - 강제 전송");
                holisticExtractor.ForceSendCurrentSequence();
            }

            // 2단계: 서버 응답 대기 (더 긴 시간, 10초)
            float responseWaitTime = 10f;
            Debug.Log("서버 응답 대기 시작...");

            while (responseWaitTime > 0 && waitingForGesture && !gestureResponseReceived)
            {
                responseWaitTime -= Time.deltaTime;
                yield return null;
            }

            // 타임아웃 처리
            if (waitingForGesture && !gestureResponseReceived)
            {
                Debug.LogWarning("서버 응답 타임아웃 - none으로 처리");
                ProcessGestureResult("none");
            }
        }
        else
        {
            Debug.LogError("HolisticExtractor를 찾을 수 없음");
            ProcessGestureResult("none");
        }
    }

    public void OnGestureRecognized(string prediction)
    {
        if (!waitingForGesture)
        {
            Debug.Log($"제스처 대기 중이 아님 - 무시: {prediction}");
            return;
        }

        if (gestureResponseReceived)
        {
            Debug.Log($"이미 응답 받음 - 무시: {prediction}");
            return;
        }

        Debug.Log($"수어 인식 결과 수신: {prediction}, 선택한 스킬: {selectedSkill}");

        gestureResponseReceived = true;
        receivedPrediction = prediction;

        ProcessGestureResult(prediction);
    }

    private void ProcessGestureResult(string prediction)
    {
        if (!waitingForGesture) return;

        waitingForGesture = false;
        gestureResponseReceived = true;

        if (prediction == selectedSkill)
        {
            ExecutePlayerAttack();
        }
        else
        {
            PlayerAttackFailed();
        }

        StartCoroutine(DelayedGestureStateReset());
    }

    private IEnumerator DelayedGestureStateReset()
    {
        yield return new WaitForSeconds(1f);
        ResetGestureState();
        Debug.Log("지연된 제스처 상태 리셋 완료");
    }

    void ExecutePlayerAttack()
    {
        var statsMgr = PlayerStatsManager.Instance;
        int damage = Mathf.Max(1, statsMgr.GetAttackPower() - currentMonster.defense);
        currentMonsterHP -= damage;

        string message = $"성공이야!\n{currentMonster.monsterName}에게 {damage}의 피해를 입혔어!";

        if (currentMonsterHP <= 0)
        {
            message += $"\n{currentMonster.monsterName}을(를) 물리쳤다!";

            // 승리 처리
            battleResult = "victory";
            defeatedMonsterID = GetMonsterUniqueID(); // 몬스터 고유 ID 저장

            EndBattle(true);
        }
        else
        {
            message += $"\n{currentMonster.monsterName} HP: {currentMonsterHP}";
            isPlayerTurn = false;
            StartCoroutine(MonsterTurn());
        }

        if (uiManager != null)
        {
            uiManager.ShowResult(message);
        }

        Debug.Log(message);
    }

    void PlayerAttackFailed()
    {
        string message = $"인식 실패야!\n다음에 다시 시도하자!";

        if (uiManager != null)
        {
            uiManager.ShowResult(message);
        }

        isPlayerTurn = false;
        StartCoroutine(MonsterTurn());

        Debug.Log(message);
    }

    IEnumerator MonsterTurn()
    {
        yield return new WaitForSeconds(2f);
        var statsMgr = PlayerStatsManager.Instance;
        int damage = Mathf.Max(1, currentMonster.attackPower - statsMgr.GetDefense());
        statsMgr.TakeDamage(damage);
        currentPlayerHP = statsMgr.GetCurrentHP();

        string message = $"{currentMonster.monsterName}의 공격!\n{damage}의 피해를 받았어!";

        if (currentPlayerHP <= 0)
        {
            message += "\n쓰러지고 말았어.";
            battleResult = "defeat";
            EndBattle(false);
        }
        else
        {
            message += $"\n플레이어 HP: {currentPlayerHP}";
            isPlayerTurn = true;

            if (uiManager != null)
            {
                uiManager.ShowResult(message);
                StartCoroutine(DelayedBattleUIReset());
            }
        }

        Debug.Log(message);
    }

    IEnumerator DelayedBattleUIReset()
    {
        yield return new WaitForSeconds(3f);

        if (isPlayerTurn)
        {
            // *** 플레이어 턴으로 돌아올 때 상태 확실히 리셋 ***
            ResetGestureState();
            ResetBattleUI();
            Debug.Log("배틀 UI 리셋 + 제스처 상태 리셋");
        }
    }

    public void AttemptEscape()
    {
        if (!isPlayerTurn) return;

        if (!currentMonster.canEscape)
        {
            if (uiManager != null)
            {
                uiManager.ShowResult($"{currentMonster.monsterName}에게서 벗어날 수 없다.");
                StartCoroutine(DelayedBattleUIReset());
            }
            return;
        }

        bool escapeSuccess = Random.Range(0f, 1f) > 0.5f;

        if (escapeSuccess)
        {
            battleResult = "escape";
            defeatedMonsterID = GetMonsterUniqueID(); // 도망쳤을 때도 몬스터 ID 저장

            if (uiManager != null)
            {
                uiManager.ShowResult("무사히 도망쳤어!");
            }

            StartCoroutine(ReturnToField());
        }
        else
        {
            if (uiManager != null)
            {
                uiManager.ShowResult("이크, 붙잡혔다!");
            }

            isPlayerTurn = false;
            StartCoroutine(MonsterTurn());
        }
    }

    void EndBattle(bool playerWon)
    {
        if (playerWon)
        {
            // 승리 처리 (경험치, 아이템 등)
            StartCoroutine(ReturnToField());
        }
        else
        {
            // 패배 처리
            StartCoroutine(GameOver());
        }
    }

    public void ContinueBattle()
    {
        if (isPlayerTurn && uiManager != null)
        {
            // *** Continue 버튼 클릭 시에도 상태 리셋 ***
            ResetGestureState();
            uiManager.ShowMainBattleUI();
            Debug.Log("배틀 계속 + 제스처 상태 리셋");
        }
    }

    // 몬스터 고유 ID 가져오기
    string GetMonsterUniqueID()
    {
        // MonsterController에서 PlayerPrefs에 저장한 ID 가져오기
        string monsterID = PlayerPrefs.GetString("CurrentMonsterID", "");

        if (string.IsNullOrEmpty(monsterID) && currentMonster != null)
        {
            // 백업: 몬스터 데이터의 이름으로 ID 생성
            monsterID = $"{currentMonster.monsterName}_backup_{System.DateTime.Now.Ticks}";
            Debug.LogWarning($"몬스터 ID를 찾을 수 없어 백업 ID 생성: {monsterID}");
        }

        return monsterID;
    }

    IEnumerator ReturnToField()
    {
        yield return new WaitForSeconds(2f);

        PlayerPrefs.SetInt("FirstBattleCompleted", 1);
        PlayerPrefs.Save();
        Debug.Log("첫 배틀 완료 상태 저장됨");

        // GameManager에 배틀 결과 전달
        if (GameManager.Instance != null)
        {
            // 배틀 결과에 따라 다른 처리
            switch (battleResult)
            {
                case "victory":
                    // 몬스터를 죽였으므로 해당 몬스터 제거
                    GameManager.Instance.SetMonsterDefeated(defeatedMonsterID);
                    Debug.Log($"몬스터 {defeatedMonsterID} 제거 설정 (승리)");
                    break;

                case "escape":
                    // 도망쳤어도 몬스터 제거 (한 번 만난 몬스터는 사라짐)
                    GameManager.Instance.SetMonsterDefeated(defeatedMonsterID);
                    Debug.Log($"몬스터 {defeatedMonsterID} 제거 설정 (도망)");
                    break;
            }

            // 배틀 상태 정리
            GameManager.Instance.EndBattle();
        }

        // PlayerPrefs 정리
        PlayerPrefs.DeleteKey("CurrentMonsterID");

        // 원래 씬으로 돌아가기
        string previousScene = PlayerPrefs.GetString("PreviousScene", "Maptest");
        Debug.Log($"원래 씬으로 복귀: {previousScene}");

        SceneManager.LoadScene(previousScene);
    }

    IEnumerator GameOver()
    {
        yield return new WaitForSeconds(2f);

        // GameManager에 패배 상태 전달
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EndBattle();
        }

        // PlayerPrefs 정리
        PlayerPrefs.DeleteKey("CurrentMonsterID");

        SceneManager.LoadScene("MainMenu");
    }
}