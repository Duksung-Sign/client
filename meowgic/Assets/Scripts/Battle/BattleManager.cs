using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    [Header("배틀 참가자")]
    public MonsterDataSO currentMonster;

    [Header("현재 배틀 상태")]
    private int currentPlayerHP;        // 배틀 중 변화하는 HP만 별도 관리
    private int currentMonsterHP;       // 배틀 중 변화하는 HP만 별도 관리

    [Header("UI 참조")]
    private BattleUIManager uiManager;

    [Header("미디어파이프 참조")]
    private HolisticJsonSender holisticExtractor;

    [Header("배틀 상태")]
    private bool isPlayerTurn = true;
    private bool waitingForGesture = false;
    private string selectedSkill = "";

    void Start()
    {
        uiManager = FindObjectOfType<BattleUIManager>();
        holisticExtractor = FindObjectOfType<HolisticJsonSender>();

        LoadMonsterData();
        LoadPlayerData();
        InitializeBattle();
    }

    void LoadMonsterData()
    {
        if (GameManager.Instance?.currentBattleMonster != null)
        {
            currentMonster = GameManager.Instance.currentBattleMonster;
            currentMonsterHP = currentMonster.maxHP;  // HP만 별도 저장

            Debug.Log($"몬스터 로드: {currentMonster.monsterName} (HP: {currentMonsterHP})");

            // UI 설정
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

        if (uiManager != null)
        {
            uiManager.ShowMainBattleUI(); // 간단하게 메인 UI 표시
        }

        Debug.Log("배틀 초기화 완료");
    }

    // UI 초기화를 위한 새로운 메서드
    void ResetBattleUI()
    {
        if (uiManager != null)
        {
            uiManager.ShowMainBattleUI(); // 메인 배틀 UI로 돌아가기
        }
    }

    public void UseSkill(string skillName)
    {
        if (!isPlayerTurn || waitingForGesture) return;

        selectedSkill = skillName;
        waitingForGesture = true;

        Debug.Log($"스킬 선택: {skillName}");

        if (uiManager != null)
        {
            uiManager.ShowResult($"{GetSkillDisplayName(skillName)}를 보여주세요!");
        }

        StartCoroutine(WaitForGestureRecognition());
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
            // 미디어파이프에서 30프레임 수집하도록 신호
            holisticExtractor.StartGestureCollection();

            // 30프레임 수집 대기 (약 3초, 10fps 기준)
            float waitTime = 5f;
            while (waitTime > 0 && holisticExtractor.IsCollectingFrames())
            {
                waitTime -= Time.deltaTime;
                yield return null;
            }

            // 30프레임이 모이면 자동으로 서버로 전송되고
            // ProcessServerResponse에서 OnGestureRecognized가 호출됨
            // 만약 시간이 초과되면 현재까지 수집된 데이터로 강제 전송
            if (waitTime <= 0 && holisticExtractor.GetCurrentFrameCount() > 0)
            {
                holisticExtractor.ForceSendCurrentSequence();
            }

            // 서버 응답 대기 (최대 5초)
            float responseWaitTime = 5f;
            while (responseWaitTime > 0 && waitingForGesture)
            {
                responseWaitTime -= Time.deltaTime;
                yield return null;
            }

            // 응답이 없으면 실패 처리
            if (waitingForGesture)
            {
                OnGestureRecognized("none");
            }
        }
    }

    public void OnGestureRecognized(string prediction)
    {
        waitingForGesture = false;

        Debug.Log($"수어 인식 결과: {prediction}, 선택한 스킬: {selectedSkill}");

        if (prediction == selectedSkill)
        {
            // 공격 성공
            ExecutePlayerAttack();
        }
        else
        {
            // 공격 실패
            PlayerAttackFailed();
        }
    }

    void ExecutePlayerAttack()
    {
        var statsMgr = PlayerStatsManager.Instance;
        int damage = Mathf.Max(1, statsMgr.GetAttackPower() - currentMonster.defense);
        currentMonsterHP -= damage;

        string message = $"{GetSkillDisplayName(selectedSkill)} 성공!\n{currentMonster.monsterName}에게 {damage}의 피해!";

        if (currentMonsterHP <= 0)
        {
            message += $"\n{currentMonster.monsterName}을(를) 물리쳤다!";
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
        string message = $"수어 인식 실패!\n올바른 {GetSkillDisplayName(selectedSkill)} 수어를 하지 못했습니다.";

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

        string message = $"{currentMonster.monsterName}의 공격!\n플레이어가 {damage}의 피해를 받았습니다.";

        if (currentPlayerHP <= 0)
        {
            message += "\n플레이어가 쓰러졌습니다...";
            EndBattle(false);
        }
        else
        {
            message += $"\n플레이어 HP: {currentPlayerHP}";
            isPlayerTurn = true;

            // 메시지를 보여준 후 잠시 기다렸다가 UI 초기화
            if (uiManager != null)
            {
                uiManager.ShowResult(message);
                StartCoroutine(DelayedBattleUIReset());
            }
        }

        Debug.Log(message);
    }

    // 메시지를 보여준 후 잠시 기다렸다가 UI를 초기화하는 코루틴
    IEnumerator DelayedBattleUIReset()
    {
        yield return new WaitForSeconds(3f); // 3초 기다린 후 (메시지를 읽을 시간)

        if (isPlayerTurn) // 아직 플레이어 턴이면
        {
            ResetBattleUI(); // UI 초기화
        }
    }

    public void AttemptEscape()
    {
        if (!isPlayerTurn) return;

        // currentMonster에서 직접 참조
        if (!currentMonster.canEscape)
        {
            if (uiManager != null)
            {
                uiManager.ShowResult($"{currentMonster.monsterName}에게서 벗어날 수 없다.");
                StartCoroutine(DelayedBattleUIReset()); // 메시지 후 UI 초기화
            }
            return;
        }

        // 50% 확률로 도망 성공
        bool escapeSuccess = Random.Range(0f, 1f) > 0.5f;

        if (escapeSuccess)
        {
            if (uiManager != null)
            {
                uiManager.ShowResult("무사히 도망쳤다!");
            }

            StartCoroutine(ReturnToField());
        }
        else
        {
            if (uiManager != null)
            {
                uiManager.ShowResult("도망칠 수 없었다!");
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
            uiManager.ShowMainBattleUI(); // 메인 배틀 UI로 돌아가기
        }
    }

    IEnumerator ReturnToField()
    {
        yield return new WaitForSeconds(2f);

        // 원래 씬으로 돌아가기
        string previousScene = PlayerPrefs.GetString("PreviousScene", "MainScene");
        SceneManager.LoadScene(previousScene);
    }

    IEnumerator GameOver()
    {
        yield return new WaitForSeconds(2f);

        SceneManager.LoadScene("MainMenu");
    }
}