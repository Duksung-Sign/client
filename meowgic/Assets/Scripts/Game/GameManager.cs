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
    public GameObject currentBattleMonsterObject;
    [HideInInspector]
    public string currentBattleDialogueID;

    // 게임 상태들
    private bool isInDialogue = false;
    private bool isInMenu = false;
    private bool isPaused = false;

    // 처친 몬스터들 관리
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

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
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

    public string GetCurrentBattleDialogueID()
    {
        return currentBattleDialogueID;
    }

    /// <summary>
    /// 몬스터 자동 할당, 필드에서 부딪히면 그 몬스터가 저장
    /// </summary>
    /// <param name="monster">몬스터 데이터</param>
    public void SetBattleMonster(MonsterDataSO monster)
    {
        currentBattleMonster = monster;
        // 수정: monsterName -> GetDisplayName()
        Debug.Log($"배틀 몬스터 설정: {monster.GetDisplayName()}");
    }

    /// <summary>
    /// 배틀 시작 (필드 몬스터용)
    /// MonsterController가 호출합니다.
    /// </summary>
    public void StartBattle(MonsterDataSO monster, GameObject monsterObject)
    {
        currentBattleMonster = monster;
        currentBattleMonsterObject = monsterObject;
        currentBattleDialogueID = null; // 필드 전투는 튜토리얼 대사가 없음

        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
        SceneManager.LoadScene("BattleScene");
    }

    /// <summary>
    /// 몬스터 데이터와 대화 ID로 전투 시작 (이벤트/컷씬용)
    /// CutsceneManager가 호출합니다.
    /// </summary>
    public void StartBattle(MonsterDataSO monster, string battleDialogueID = null)
    {
        if (monster == null)
        {
            Debug.LogError("전투를 시작할 몬스터 데이터가 없습니다!");
            return;
        }

        currentBattleMonster = monster;
        currentBattleDialogueID = battleDialogueID; // 전달받은 대화 ID 저장
        currentBattleMonsterObject = null; // 이벤트 전투는 필드 오브젝트가 없음

        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
        SceneManager.LoadScene("BattleScene");
    }

    /// <summary>
    /// 몬스터가 처했음을 기록하고 오브젝트 제거
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
    /// 몬스터가 이미 처졌는지 확인
    /// </summary>
    /// <param name="monsterID">몬스터 고유 ID</param>
    /// <returns>처졌으면 true</returns>
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

    /// <summary>
    /// 몬스터 데이터만으로 전투 시작 (이벤트용)
    /// </summary>
    /// <param name="monster">전투할 몬스터의 데이터</param>
    public void StartBattle(MonsterDataSO monster)
    {
        if (monster == null)
        {
            Debug.LogError("전투를 시작할 몬스터 데이터가 없습니다!");
            return;
        }

        currentBattleMonster = monster;
        currentBattleMonsterObject = null; // 이벤트 전투이므로 필드 오브젝트는 없음

        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);

        Debug.Log($"이벤트 배틀 시작: {monster.GetDisplayName()}");
        SceneManager.LoadScene("BattleScene");
    }

    /// <summary>
    /// 안전한 이전 씬 반환 (저장된 씬이 유효한지 검증)
    /// </summary>
    public string GetPreviousScene()
    {
        string previousScene = PlayerPrefs.GetString("PreviousScene", "");

        if (string.IsNullOrEmpty(previousScene))
        {
            Debug.LogError("이전 씬 정보가 저장되지 않았습니다!");
            return null;
        }

        // 씬이 실제로 존재하는지 확인 (선택사항)
        if (IsSceneInBuildSettings(previousScene))
        {
            return previousScene;
        }
        else
        {
            Debug.LogError($"씬 '{previousScene}'이 빌드 설정에 없습니다!");
            return null;
        }
    }

    /// <summary>
    /// 씬이 빌드 설정에 있는지 확인
    /// </summary>
    private bool IsSceneInBuildSettings(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string buildSceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (buildSceneName == sceneName)
                return true;
        }
        return false;
    }

    // 상태 확인 메서드들
    public bool IsInDialogue() => isInDialogue;
    public bool IsInMenu() => isInMenu;
    public bool IsPaused() => isPaused;
    public bool CanPlayerMove() => !isInDialogue && !isInMenu && !isPaused;

    // 디버그용: 처친 몬스터 목록 확인
    public void LogDefeatedMonsters()
    {
        Debug.Log($"처친 몬스터 수: {defeatedMonsters.Count}");
        foreach (string monsterId in defeatedMonsters)
        {
            Debug.Log($"- {monsterId}");
        }
    }

    // 게임 리셋 시 처친 몬스터 목록 초기화 (개발용)
    [ContextMenu("Reset Defeated Monsters")]
    public void ResetDefeatedMonsters()
    {
        defeatedMonsters.Clear();
        Debug.Log("처친 몬스터 목록이 초기화되었습니다.");
    }
}