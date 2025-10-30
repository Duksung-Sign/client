using System;
using UnityEngine.SceneManagement;
using UnityEngine;

public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance { get; private set; }
    public event Action<int, int> OnHPChanged;

    // ⭐ PlayerStats 객체 대신 값만 저장
    private int maxHP;
    private int attackPower;
    private int defense;
    private int currentHP;

    private bool isStatsRegistered = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[PlayerStatsManager] 싱글톤 생성 완료");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[PlayerStatsManager] 씬 로드됨: {scene.name}");

        // ⭐ 아직 스탯이 등록되지 않았을 때만 찾기
        if (!isStatsRegistered)
        {
            var playerStats = FindObjectOfType<PlayerStats>();
            if (playerStats != null)
            {
                RegisterStats(playerStats);
            }
            else
            {
                Debug.LogWarning($"[PlayerStatsManager] ⚠️ '{scene.name}' 씬에 PlayerStats가 없음");
            }
        }
        else
        {
            Debug.Log($"[PlayerStatsManager] ✅ 이미 스탯 등록됨, 유지: HP {currentHP}/{maxHP}");
        }
    }

    // ⭐ PlayerStats의 값을 복사해서 저장
    private void RegisterStats(PlayerStats stats)
    {
        maxHP = stats.maxHP;
        attackPower = stats.attackPower;
        defense = stats.defense;
        currentHP = maxHP;

        isStatsRegistered = true;

        Debug.Log($"[PlayerStatsManager] ✅ 스탯 등록 완료: HP {currentHP}/{maxHP}, ATK {attackPower}, DEF {defense}");
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    public int GetCurrentHP() => currentHP;

    public int GetMaxHP()
    {
        if (!isStatsRegistered)
        {
            Debug.LogWarning("[PlayerStatsManager] ⚠️ 스탯이 아직 등록되지 않음!");
            return 0;
        }
        return maxHP;
    }

    public int GetAttackPower()
    {
        if (!isStatsRegistered)
        {
            Debug.LogWarning("[PlayerStatsManager] ⚠️ 스탯이 아직 등록되지 않음!");
            return 0;
        }
        return attackPower;
    }

    public int GetDefense()
    {
        if (!isStatsRegistered)
        {
            Debug.LogWarning("[PlayerStatsManager] ⚠️ 스탯이 아직 등록되지 않음!");
            return 0;
        }
        return defense;
    }

    public void TakeDamage(int rawDamage)
    {
        if (!isStatsRegistered)
        {
            Debug.LogError("[PlayerStatsManager] ❌ 스탯이 등록되지 않아 데미지 처리 불가!");
            return;
        }

        int finalDamage = Mathf.Max(0, rawDamage - defense);
        int previousHP = currentHP;
        currentHP = Mathf.Max(0, currentHP - finalDamage);

        Debug.Log($"[PlayerStatsManager] 💥 데미지: {rawDamage} → {finalDamage} (방어: {defense})");
        Debug.Log($"[PlayerStatsManager] HP: {previousHP} → {currentHP}/{maxHP}");

        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    public void Heal(int amount)
    {
        if (!isStatsRegistered) return;

        int previousHP = currentHP;
        currentHP = Mathf.Min(maxHP, currentHP + amount);

        Debug.Log($"[PlayerStatsManager] ❤️ 회복: {amount}, HP: {previousHP} → {currentHP}/{maxHP}");

        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    // ⭐ HP 수동 설정 (세이브/로드용)
    public void SetHP(int hp)
    {
        if (!isStatsRegistered) return;

        currentHP = Mathf.Clamp(hp, 0, maxHP);
        Debug.Log($"[PlayerStatsManager] HP 설정: {currentHP}/{maxHP}");
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    // ⭐ 풀 회복
    public void RestoreToFull()
    {
        if (!isStatsRegistered) return;

        currentHP = maxHP;
        Debug.Log($"[PlayerStatsManager] 완전 회복! HP: {currentHP}/{maxHP}");
        OnHPChanged?.Invoke(currentHP, maxHP);
    }

    // ⭐ 디버깅용
    [ContextMenu("Print Current Stats")]
    public void PrintCurrentStats()
    {
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Debug.Log($"[PlayerStatsManager] Stats Registered: {isStatsRegistered}");
        Debug.Log($"[PlayerStatsManager] CurrentHP: {currentHP}");
        Debug.Log($"[PlayerStatsManager] MaxHP: {maxHP}");
        Debug.Log($"[PlayerStatsManager] AttackPower: {attackPower}");
        Debug.Log($"[PlayerStatsManager] Defense: {defense}");
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }

    // ⭐ 강제 초기화 (디버깅용)
    [ContextMenu("Force Initialize with Defaults")]
    public void ForceInitialize()
    {
        maxHP = 100;
        attackPower = 10;
        defense = 5;
        currentHP = maxHP;
        isStatsRegistered = true;

        Debug.Log($"[PlayerStatsManager] 강제 초기화 완료: HP {currentHP}/{maxHP}");
        OnHPChanged?.Invoke(currentHP, maxHP);
    }
}