using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
using UnityEngine;

public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance;
    public event Action<int, int> OnHPChanged;

    private int maxHP;
    private int attackPower;
    private int defense;

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

    /// <summary>
    /// PlayerStats 컴포넌트가 Awake에서 호출해서 등록
    /// </summary>
    public void RegisterBaseStats(PlayerStats stats)
    {
        maxHP = stats.maxHP;
        attackPower = stats.attackPower;
        defense = stats.defense;

        currentHP = maxHP;
        OnHPChanged?.Invoke(currentHP, maxHP);
        Debug.Log($"[StatsManager] Registered base stats: HP {maxHP}, ATK {attackPower}, DEF {defense}");
    }

    public int GetMaxHP() => maxHP;
    public int GetAttackPower() => attackPower;
    public int GetDefense() => defense;
    public int GetCurrentHP() => currentHP;

    public void TakeDamage(int dmg)
    {
        currentHP = Mathf.Max(0, currentHP - dmg);
        OnHPChanged?.Invoke(currentHP, maxHP);
        Debug.Log($"[StatsManager] Took {dmg} damage. CurrentHP = {currentHP}");
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        OnHPChanged?.Invoke(currentHP, maxHP);
        Debug.Log($"[StatsManager] Healed {amount}. CurrentHP = {currentHP}");
    }

    public void ResetHP()
    {
        currentHP = maxHP;
        OnHPChanged?.Invoke(currentHP, maxHP);
        Debug.Log("[StatsManager] HP reset");
    }
}
