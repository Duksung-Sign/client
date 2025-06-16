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

    // 배틀 결과 추적을 위한 추가 변수들
    private string battleResult = ""; // "victory", "escape", "defeat"
    private string defeatedMonsterID = ""; // 죽인 몬스터의 ID

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

        if (uiManager != null)
        {
            uiManager.ShowMainBattleUI();
        }

        Debug.Log("배틀 초기화 완료");
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

        selectedSkill = skillName;
        waitingForGesture = true;

        Debug.Log($"스킬 선택: {skillName}");

        if (uiManager != null)
        {
            uiManager.ShowResult($"{GetSkillDisplayName(skillName)}을 보여주세요!");
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
            holisticExtractor.StartGestureCollection();

            float waitTime = 5f;
            while (waitTime > 0 && holisticExtractor.IsCollectingFrames())
            {
                waitTime -= Time.deltaTime;
                yield return null;
            }

            if (waitTime <= 0 && holisticExtractor.GetCurrentFrameCount() > 0)
            {
                holisticExtractor.ForceSendCurrentSequence();
            }

            float responseWaitTime = 5f;
            while (responseWaitTime > 0 && waitingForGesture)
            {
                responseWaitTime -= Time.deltaTime;
                yield return null;
            }

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
            ExecutePlayerAttack();
        }
        else
        {
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
            ResetBattleUI();
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
            uiManager.ShowMainBattleUI();
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