using UnityEngine;

/// <summary>
/// 아이템 데이터 (매핑 시스템과 퀘스트 시스템에서 사용)
/// </summary>
[CreateAssetMenu(fileName = "ItemData", menuName = "Game Data/Item Database")]
public class ItemDataSO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("아이템 고유 ID (매핑 시스템에서 사용)")]
    public string itemID = "";

    [Tooltip("게임에서 표시될 아이템 이름")]
    public string displayName = "";

    [Tooltip("아이템 설명")]
    [TextArea(3, 5)]
    public string itemDescription = "";

    [Header("탐색 관련 (Inspectable 타입용)")]
    [Tooltip("탐색 시 표시할 대화 ID (Inspectable 타입 전용)")]
    public string inspectionDialogueID = "";

    [Header("타입 및 분류")]
    [Tooltip("아이템 타입")]
    public ItemType itemType = ItemType.Collectible;

    [Tooltip("아이템 카테고리")]
    public ItemCategory category = ItemCategory.Misc;

    [Header("퀘스트 관련")]
    [Tooltip("퀘스트 아이템 여부")]
    public bool isQuestItem = false;

    [Tooltip("중요한 아이템 여부 (삭제 불가)")]
    public bool isKeyItem = false;

    [Tooltip("관련 퀘스트 ID들")]
    public string[] relatedQuestIDs;

    [Header("비주얼")]
    [Tooltip("인벤토리용 아이콘")]
    public Sprite icon;

    [Tooltip("월드에서 보이는 스프라이트 (null이면 icon 사용)")]
    public Sprite worldSprite;

    [Header("아웃라인 설정")]
    [Tooltip("아웃라인 색상")]
    public Color outlineColor = Color.yellow;

    [Tooltip("아웃라인 두께")]
    [Range(0f, 0.5f)]
    public float outlineWidth = 0.1f;

    [Header("스택 및 거래")]
    [Tooltip("중첩 가능 여부")]
    public bool stackable = true;

    [Tooltip("최대 중첩 수")]
    [Range(1, 999)]
    public int maxStackSize = 99;

    [Tooltip("버릴 수 있는지")]
    public bool droppable = true;

    [Header("사용 효과")]
    [Tooltip("사용 시 소모 여부")]
    public bool consumeOnUse = false;

    [Tooltip("체력 회복량")]
    [Range(0, 999)]
    public int healAmount = 0;

    [Tooltip("마나 회복량")]
    [Range(0, 999)]
    public int manaAmount = 0;

    [Tooltip("버프 효과 ID")]
    public string buffEffectID = "";

    [Tooltip("사용 시 실행할 액션")]
    public ItemUseAction useAction = ItemUseAction.None;

    [Header("사운드")]
    [Tooltip("획득 사운드 ID")]
    public string pickupSoundID = "";

    [Tooltip("사용 사운드 ID")]
    public string useSoundID = "";

    [Header("특수 효과")]
    [Tooltip("획득 시 이펙트 ID")]
    public string pickupEffectID = "";

    [Tooltip("사용 시 이펙트 ID")]
    public string useEffectID = "";

    // 실제 사용할 아이템 ID 반환
    public string GetItemID()
    {
        return string.IsNullOrEmpty(itemID) ? name : itemID;
    }

    // 실제 표시될 이름 반환
    public string GetDisplayName()
    {
        return string.IsNullOrEmpty(displayName) ? name : displayName;
    }

    // 실제 월드에서 표시할 스프라이트 반환
    public Sprite GetWorldSprite()
    {
        return worldSprite != null ? worldSprite : icon;
    }

    // 아이템 사용 가능 여부 체크
    public bool CanUse()
    {
        return consumeOnUse && (healAmount > 0 || manaAmount > 0 || !string.IsNullOrEmpty(buffEffectID) || useAction != ItemUseAction.None);
    }

    public Color GetOutlineColor()
    {
        if (isQuestItem)
            return Color.cyan;  // 퀘스트 아이템은 하늘색

        return outlineColor;
    }

    // 관련 퀘스트 ID 확인
    public bool IsRelatedToQuest(string questID)
    {
        if (relatedQuestIDs == null) return false;
        return System.Array.Exists(relatedQuestIDs, id => id == questID);
    }

    public string GetFullDescription()
    {
        string fullDesc = itemDescription;

        if (CanUse())
        {
            fullDesc += "\n\n<color=#00ff00>사용 효과:</color>";
            if (healAmount > 0)
                fullDesc += $"\n• 체력 +{healAmount}";
            if (manaAmount > 0)
                fullDesc += $"\n• 마나 +{manaAmount}";
        }

        if (isQuestItem)
        {
            fullDesc += "\n\n<color=#00ffff>중요한 아이템</color>";
        }

        return fullDesc;
    }

    [System.Serializable]
    public enum ItemType
    {
        Collectible,    // 획득 가능 (인벤토리로)
        Inspectable     // 탐색 가능 (스크립트만)
    }

    [System.Serializable]
    public enum ItemCategory
    {
        Misc,           // 기타
        Consumable,     // 소모품
        QuestItem,      // 퀘스트 아이템
        KeyItem,        // 키 아이템
    }

    [System.Serializable]
    public enum ItemUseAction
    {
        None,           // 없음
        Heal,           // 치료
        RestoreMana,    // 마나 회복
        Buff,           // 버프
        Unlock,         // 잠금 해제
    }
}