using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Character Info")]
    public string playerName = "미오";
    public Sprite playerPortrait;

    [Header("Character Stats")]
    public int maxHP = 30;
    public int attackPower = 10;
    public int defense = 5;
    public string[] availableSkills = { "기본 공격 1" };

    public Sprite GetPlayerPortrait()
    {
        return playerPortrait;
    }

    public string GetPlayerName()
    {
        return playerName;
    }
}