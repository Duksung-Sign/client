using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public PlayerController Player
    {
        get;
        private set;
    }

    [Header("배틀 관련")]
    [HideInInspector]
    public MonsterDataSO currentBattleMonster;
    [HideInInspector]
    public GameObject currentBattleMonsterObject; // 추가: 몬스터 오브젝트 참조

    // 게임 상태들
    private bool isInDialogue = false;
    private bool isInMenu = false;
    private bool isPaused = false;

    // 죽은 몬스터들 관리
    private HashSet<string> defeatedMonsters = new HashSet<string>();

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

    public void RegisterPlayer(PlayerController pc)
    {
        Player = pc;
    }

    // 대화 상태 제어
    public void SetDialogueState(bool inDialogue)
    {
        isInDialogue = inDialogue;
        UpdatePlayerMovement();

        Debug.Log($"대화 상태: {(inDialogue ? "시작" : "종료")}");
    }

    // 메뉴 상태 제어 (나중에 인벤토리 등에서 사용)
    public void SetMenuState(bool inMenu)
    {
        isInMenu = inMenu;
        UpdatePlayerMovement();
    }

    // 일시정지 상태 제어
    public void SetPauseState(bool paused)
    {
        isPaused = paused;
        UpdatePlayerMovement();

        // 일시정지 시 시간도 멈춤
        Time.timeScale = paused ? 0f : 1f;
    }

    // 플레이어 이동 가능 여부 업데이트
    void UpdatePlayerMovement()
    {
        if (Player != null)
        {
            // 대화 중이거나 메뉴 중이거나 일시정지 중이면 이동 불가
            bool canMove = !isInDialogue && !isInMenu && !isPaused;
            Player.SetCanMove(canMove);
        }
    }

    /// <summary>
    /// 몬스터 자동 할당, 필드에서 부딪히면 그 몬스터가 저장
    /// </summary>
    /// <param name="monster">몬스터 데이터</param>
    public void SetBattleMonster(MonsterDataSO monster)
    {
        currentBattleMonster = monster;
        Debug.Log($"배틀 몬스터 설정: {monster.monsterName}");
    }

    /// <summary>
    /// 배틀 시작 (몬스터 오브젝트도 함께 저장)
    /// </summary>
    /// <param name="monster">몬스터 데이터</param>
    /// <param name="monsterObject">몬스터 게임오브젝트</param>
    public void StartBattle(MonsterDataSO monster, GameObject monsterObject)
    {
        currentBattleMonster = monster;
        currentBattleMonsterObject = monsterObject;

        // 현재 씬 저장
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);

        Debug.Log($"배틀 시작: {monster.monsterName}");
        SceneManager.LoadScene("BattleScene");
    }

    /// <summary>
    /// 몬스터가 죽었음을 기록하고 오브젝트 제거
    /// </summary>
    /// <param name="monsterID">몬스터 고유 ID</param>
    public void SetMonsterDefeated(string monsterID)
    {
        if (!string.IsNullOrEmpty(monsterID))
        {
            defeatedMonsters.Add(monsterID);
            Debug.Log($"몬스터 {monsterID}가 제거 목록에 추가됨");

            // 실제 몬스터 오브젝트 제거
            if (currentBattleMonsterObject != null)
            {
                Debug.Log($"몬스터 오브젝트 {currentBattleMonsterObject.name} 제거됨");
                Destroy(currentBattleMonsterObject);
                currentBattleMonsterObject = null;
            }
        }
    }

    /// <summary>
    /// 몬스터가 이미 죽었는지 확인
    /// </summary>
    /// <param name="monsterID">몬스터 고유 ID</param>
    /// <returns>죽었으면 true</returns>
    public bool IsMonsterDefeated(string monsterID)
    {
        return defeatedMonsters.Contains(monsterID);
    }

    /// <summary>
    /// 배틀 종료 시 정리
    /// </summary>
    public void EndBattle()
    {
        currentBattleMonster = null;
        if (currentBattleMonsterObject == null)
        {
            Debug.Log("몬스터 오브젝트가 이미 제거됨");
        }
        currentBattleMonsterObject = null;

        Debug.Log("배틀 상태 정리 완료");
    }

    public void ClearBattleMonster()
    {
        currentBattleMonster = null;
        currentBattleMonsterObject = null;
    }

    // 상태 확인 메서드들
    public bool IsInDialogue() => isInDialogue;
    public bool IsInMenu() => isInMenu;
    public bool IsPaused() => isPaused;
    public bool CanPlayerMove() => !isInDialogue && !isInMenu && !isPaused;

    // 디버깅용: 죽은 몬스터 목록 확인
    public void LogDefeatedMonsters()
    {
        Debug.Log($"죽은 몬스터 수: {defeatedMonsters.Count}");
        foreach (string monsterId in defeatedMonsters)
        {
            Debug.Log($"- {monsterId}");
        }
    }

    // 게임 리셋 시 죽은 몬스터 목록 초기화 (개발용)
    [ContextMenu("Reset Defeated Monsters")]
    public void ResetDefeatedMonsters()
    {
        defeatedMonsters.Clear();
        Debug.Log("죽은 몬스터 목록이 초기화되었습니다.");
    }
}