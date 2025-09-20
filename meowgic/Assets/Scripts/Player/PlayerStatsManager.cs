using System;
using UnityEngine.SceneManagement;
using UnityEngine;

public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance { get; private set; }

    public event Action<int, int> OnHPChanged;

    private PlayerStats baseStats;
    private int currentHP;

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
        var playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            if (baseStats == null)
            {
                RegisterNewStats(playerStats);
            }
        }
    }

    private void RegisterNewStats(PlayerStats stats)
    {
        baseStats = stats;
        currentHP = baseStats.maxHP;
        OnHPChanged?.Invoke(currentHP, baseStats.maxHP);
        Debug.Log($"[StatsManager] PlayerStats 등록: HP {currentHP}/{baseStats.maxHP}");
    }

    public int GetCurrentHP() => currentHP;
    public int GetMaxHP() => baseStats != null ? baseStats.maxHP : 0;
    public int GetAttackPower() => baseStats != null ? baseStats.attackPower : 0;
    public int GetDefense() => baseStats != null ? baseStats.defense : 0;

    public void TakeDamage(int rawDamage)
    {
        if (baseStats == null) return;

        int finalDamage = Mathf.Max(0, rawDamage - baseStats.defense);
        currentHP = Mathf.Max(0, currentHP - finalDamage);
        OnHPChanged?.Invoke(currentHP, baseStats.maxHP);
    }
}