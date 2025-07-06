using UnityEngine;

/// <summary>
/// 몬스터 기본 데이터 (배틀 시스템과 매핑 시스템에서 사용)
/// </summary>
[CreateAssetMenu(fileName = "MonsterData", menuName = "Game Data/Monster Database")]
public class MonsterDataSO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("몬스터 고유 ID (매핑 시스템에서 사용)")]
    public string monsterID = "";

    [Tooltip("표시될 몬스터 이름")]
    public string displayName = "몬스터";

    [Tooltip("몬스터 설명")]
    [TextArea(2, 3)]
    public string description = "";

    [Header("비주얼")]
    [Tooltip("배틀에서 사용할 몬스터 스프라이트")]
    public Sprite monsterSprite;

    [Tooltip("월드에서 사용할 아이콘 (미니맵 등)")]
    public Sprite monsterIcon;

    [Header("배틀 스탯")]
    [Tooltip("최대 체력")]
    [Range(1, 1000)]
    public int maxHP = 30;

    [Tooltip("공격력")]
    [Range(1, 100)]
    public int attackPower = 8;

    [Tooltip("방어력")]
    [Range(0, 50)]
    public int defense = 3;

    [Header("AI 행동")]
    [Tooltip("순찰 범위")]
    [Range(1f, 20f)]
    public float patrolRange = 3f;

    [Tooltip("추격 범위")]
    [Range(1f, 15f)]
    public float chaseRange = 5f;

    [Tooltip("이동 속도")]
    [Range(0.5f, 10f)]
    public float moveSpeed = 2f;

    [Header("배틀 시스템")]
    [Tooltip("도망칠 수 있는 몬스터인지")]
    public bool canEscape = true;

    [Tooltip("배틀에서 사용할 스킬들")]
    public string[] availableSkills = { "기본 공격" };

    [Header("보상 및 드롭")]
    [Tooltip("처치 시 경험치")]
    [Range(1, 1000)]
    public int expReward = 10;

    [Tooltip("처치 시 골드")]
    [Range(0, 999)]
    public int goldReward = 5;

    [Tooltip("드롭 가능한 아이템 ID들 (매핑 시스템에서 참조)")]
    public DropItem[] dropItems;

    [Header("특성")]
    [Tooltip("몬스터 타입")]
    public MonsterType monsterType = MonsterType.Normal;

    [Tooltip("몬스터 등급")]
    public MonsterRank monsterRank = MonsterRank.Common;

    [Tooltip("약점 속성")]
    public ElementType weakness = ElementType.None;

    [Tooltip("저항 속성")]
    public ElementType resistance = ElementType.None;

    [Header("사운드")]
    [Tooltip("등장 사운드 ID")]
    public string appearSoundID = "";

    [Tooltip("공격 사운드 ID")]
    public string attackSoundID = "";

    [Tooltip("처치 사운드 ID")]
    public string defeatSoundID = "";

    // 실제 사용할 몬스터 ID 반환
    public string GetMonsterID()
    {
        return string.IsNullOrEmpty(monsterID) ? name : monsterID;
    }

    // 실제 표시될 이름 반환
    public string GetDisplayName()
    {
        return string.IsNullOrEmpty(displayName) ? name : displayName;
    }

    // 총 드롭 확률 계산
    public float GetTotalDropRate()
    {
        float total = 0f;
        foreach (var drop in dropItems)
        {
            total += drop.dropRate;
        }
        return total;
    }

    // 드롭 아이템 추첨
    public string[] RollDropItems()
    {
        var droppedItems = new System.Collections.Generic.List<string>();

        foreach (var drop in dropItems)
        {
            float roll = Random.Range(0f, 100f);
            if (roll <= drop.dropRate)
            {
                droppedItems.Add(drop.itemID);
            }
        }

        return droppedItems.ToArray();
    }

    void OnValidate()
    {
        // 에디터에서 자동으로 ID 생성
        if (string.IsNullOrEmpty(monsterID))
        {
            monsterID = name.ToLower().Replace(" ", "_");
        }

        // 스탯 밸런스 체크
        if (attackPower > maxHP * 0.5f)
        {
            Debug.LogWarning($"[{name}] 공격력이 체력 대비 너무 높을 수 있습니다.");
        }

        // 범위 체크
        if (chaseRange <= patrolRange)
        {
            Debug.LogWarning($"[{name}] 추격 범위가 순찰 범위보다 작거나 같습니다.");
        }
    }
}

[System.Serializable]
public class DropItem
{
    [Tooltip("드롭할 아이템 ID")]
    public string itemID = "";

    [Tooltip("드롭 확률 (%)")]
    [Range(0f, 100f)]
    public float dropRate = 10f;

    [Tooltip("최소 드롭 개수")]
    [Range(1, 99)]
    public int minAmount = 1;

    [Tooltip("최대 드롭 개수")]
    [Range(1, 99)]
    public int maxAmount = 1;

    public int GetRandomAmount()
    {
        return Random.Range(minAmount, maxAmount + 1);
    }
}

[System.Serializable]
public enum MonsterType
{
    Normal,     // 일반
    Beast,      // 야수
    Undead,     // 언데드
    Dragon,     // 드래곤
    Demon,      // 악마
    Spirit,     // 정령
    Construct,  // 구조물
    Plant,      // 식물
    Slime,      // 슬라임
    Humanoid    // 인간형
}

[System.Serializable]
public enum MonsterRank
{
    Common,     // 일반
    Elite,      // 정예
    Boss,       // 보스
    MiniBoss,   // 미니보스
    Legendary   // 전설
}

[System.Serializable]
public enum ElementType
{
    None,       // 없음
    Fire,       // 화염
    Water,      // 물
    Earth,      // 대지
    Air,        // 바람
    Lightning,  // 번개
    Ice,        // 얼음
    Dark,       // 어둠
    Light,      // 빛
    Poison,     // 독
    Psychic     // 정신
}