using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Collectible,    // 획득 가능 (인벤토리로)
    Inspectable     // 탐색 가능 (스크립트만)
}

[CreateAssetMenu(fileName = "ItemData", menuName = "Game Data/Item Database")]
public class ItemDataSO : ScriptableObject
{
    [Header("기본 정보")]
    public string itemID;           // health_potion_small
    public string displayName;      // 빨간 물약
    [TextArea(2, 4)]
    public string itemDescription;  // 먹으면 좋은 일이 일어날 수도?

    [Header("타입")]
    public ItemType itemType = ItemType.Collectible;
    public bool isQuestItem = false;    // 퀘스트 아이템 여부

    [Header("시각적 요소")]
    public Sprite icon;             // 인벤토리용 아이콘
    public Sprite worldSprite;      // 월드에서 보이는 스프라이트 (null이면 icon 사용)

    [Header("아웃라인 설정")]
    public Color outlineColor = Color.yellow;
    public float outlineWidth = 0.1f;

    [Header("아이템 속성")]
    public bool stackable = true;       // 중첩 가능 여부
    public int maxStackSize = 99;       // 최대 중첩 수
    public int sellPrice = 10;          // 판매 가격
    public int buyPrice = 20;           // 구매 가격

    [Header("사용 효과 (포션 등)")]
    public bool consumeOnUse = false;   // 사용 시 소모 여부
    public int healAmount = 0;          // 체력 회복량
    public int manaAmount = 0;          // 마나 회복량

    [Header("특수 설정")]
    public bool droppable = true;       // 버릴 수 있는지
    public bool tradeable = true;       // 거래 가능한지

    [Header("사운드")]
    public AudioClip pickupSound;
    public AudioClip useSound;

    // 실제 월드에서 표시할 스프라이트 반환
    public Sprite GetWorldSprite()
    {
        return worldSprite != null ? worldSprite : icon;
    }

    // 아이템 사용 가능 여부 체크
    public bool CanUse()
    {
        return consumeOnUse && (healAmount > 0 || manaAmount > 0);
    }

    // 퀘스트 아이템인 경우 특별한 색상 반환
    public Color GetOutlineColor()
    {
        if (isQuestItem)
            return Color.cyan;  // 퀘스트 아이템은 하늘색

        return outlineColor;
    }

    // 아이템 설명에 효과 정보 추가
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
}