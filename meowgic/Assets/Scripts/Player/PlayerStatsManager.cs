using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
using UnityEngine;

public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance;
    public event Action<int, int> OnHPChanged;

    [Header("기본 스탯 (백업용)")]
    public int defaultMaxHP = 30;
    public int defaultAttackPower = 10;
    public int defaultDefense = 5;

    private int maxHP;
    private int attackPower;
    private int defense;
    private int currentHP;

    private bool isInitialized = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 기본값으로 초기화
            InitializeWithDefaults();

            Debug.Log("[StatsManager] 싱글톤 생성 및 기본값 초기화");
        }
        else
        {
            Debug.Log("[StatsManager] 중복 인스턴스 제거");
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Scene이 로드될 때마다 PlayerStats를 다시 찾아서 등록
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 현재 씬에서 PlayerStats 찾기
        FindAndRegisterPlayerStats();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[StatsManager] 씬 로드됨: {scene.name}");

        // 새 씬에서 PlayerStats 찾기
        StartCoroutine(DelayedPlayerStatsSearch());
    }

    IEnumerator DelayedPlayerStatsSearch()
    {
        // 씬 로드 후 약간 기다림
        yield return new WaitForSeconds(0.1f);
        FindAndRegisterPlayerStats();
    }

    void FindAndRegisterPlayerStats()
    {
        PlayerStats playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            Debug.Log("[StatsManager] PlayerStats 발견, 재등록 시도");
            RegisterBaseStats(playerStats);
        }
        else
        {
            Debug.LogWarning("[StatsManager] PlayerStats를 찾을 수 없습니다. 기본값 사용");
            if (!isInitialized)
            {
                InitializeWithDefaults();
            }
        }
    }

    void InitializeWithDefaults()
    {
        maxHP = defaultMaxHP;
        attackPower = defaultAttackPower;
        defense = defaultDefense;
        currentHP = maxHP;
        isInitialized = true;

        OnHPChanged?.Invoke(currentHP, maxHP);
        Debug.Log($"[StatsManager] 기본값으로 초기화: HP {maxHP}, ATK {attackPower}, DEF {defense}");
    }

    public void RegisterBaseStats(PlayerStats stats)
    {
        if (stats == null)
        {
            Debug.LogWarning("[StatsManager] null PlayerStats 등록 시도");
            return;
        }

        // 이미 초기화되어 있고 HP가 0이 아니라면 기존 값 유지
        if (isInitialized && currentHP > 0)
        {
            Debug.Log($"[StatsManager] 이미 초기화됨, 기존 HP 유지: {currentHP}/{maxHP}");
            OnHPChanged?.Invoke(currentHP, maxHP);
            return;
        }

        // 새로운 스탯으로 설정
        maxHP = stats.maxHP;
        attackPower = stats.attackPower;
        defense = stats.defense;
        currentHP = maxHP;
        isInitialized = true;

        OnHPChanged?.Invoke(currentHP, maxHP);
        Debug.Log($"[StatsManager] PlayerStats 등록: HP {maxHP}, ATK {attackPower}, DEF {defense}");
    }

    // 강제 초기화 메서드 (디버깅용)
    public void ForceInitialize()
    {
        InitializeWithDefaults();
        Debug.Log("[StatsManager] 강제 초기화 완료");
    }

    // 스탯 유효성 검사
    bool ValidateStats()
    {
        if (maxHP <= 0 || attackPower <= 0 || defense < 0)
        {
            Debug.LogError($"[StatsManager] 잘못된 스탯 발견: HP {maxHP}, ATK {attackPower}, DEF {defense}");
            InitializeWithDefaults();
            return false;
        }
        return true;
    }

    public int GetMaxHP()
    {
        ValidateStats();
        return maxHP;
    }

    public int GetAttackPower()
    {
        ValidateStats();
        return attackPower;
    }

    public int GetDefense()
    {
        ValidateStats();
        return defense;
    }

    public int GetCurrentHP()
    {
        ValidateStats();
        return currentHP;
    }

    public void TakeDamage(int dmg)
    {
        ValidateStats();

        int oldHP = currentHP;
        currentHP = Mathf.Max(0, currentHP - dmg);

        OnHPChanged?.Invoke(currentHP, maxHP);
        Debug.Log($"[StatsManager] 피해 {dmg} 받음. {oldHP} -> {currentHP}");
    }

    public void Heal(int amount)
    {
        ValidateStats();

        int oldHP = currentHP;
        currentHP = Mathf.Min(maxHP, currentHP + amount);

        OnHPChanged?.Invoke(currentHP, maxHP);
        Debug.Log($"[StatsManager] 힐링 {amount}. {oldHP} -> {currentHP}");
    }

    public void ResetHP()
    {
        ValidateStats();

        currentHP = maxHP;
        OnHPChanged?.Invoke(currentHP, maxHP);
        Debug.Log($"[StatsManager] HP 리셋: {currentHP}/{maxHP}");
    }

    // 배틀 시작 전 스탯 확인
    public bool IsReadyForBattle()
    {
        bool ready = isInitialized && maxHP > 0 && currentHP > 0 && attackPower > 0;

        if (!ready)
        {
            Debug.LogWarning($"[StatsManager] 배틀 준비 안됨: initialized={isInitialized}, maxHP={maxHP}, currentHP={currentHP}, attack={attackPower}");

            // 강제 초기화 시도
            InitializeWithDefaults();
            ready = isInitialized && maxHP > 0 && currentHP > 0 && attackPower > 0;
        }

        return ready;
    }

    // 디버깅용 현재 상태 출력
    [ContextMenu("Debug Current Stats")]
    public void DebugCurrentStats()
    {
        Debug.Log($"=== PlayerStatsManager 상태 ===");
        Debug.Log($"초기화됨: {isInitialized}");
        Debug.Log($"HP: {currentHP}/{maxHP}");
        Debug.Log($"공격력: {attackPower}");
        Debug.Log($"방어력: {defense}");
        Debug.Log($"배틀 준비: {IsReadyForBattle()}");
    }
}