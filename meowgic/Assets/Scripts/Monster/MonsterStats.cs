using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterStats : MonoBehaviour
{
    [Header("몬스터 기본 정보")]
    public string monsterName = "눈알";
    public Sprite monsterSprite;

    [Header("몬스터 스탯")]
    public int maxHP = 30;
    public int attackPower = 8;
    public int defense = 3;

    [Header("AI 설정")]
    public float patrolRange = 3f;
    public float chaseRange = 5f;
    public float moveSpeed = 2f;

    [Header("전투 설정")]
    public bool canEscape = true;
    public string[] skillNames = { "기본 공격" };
}