using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    [Header("아이템 데이터")]
    public ItemDataSO itemData;        // ScriptableObject 참조

    [Header("오버라이드 (필요시)")]
    public bool overrideSprite = false;
    public Sprite customSprite;

    private SpriteRenderer spriteRenderer;
    private bool hasOutline = false;

    void Start()
    {
        if (itemData == null)
        {
            Debug.LogError($"ItemData가 설정되지 않았습니다: {gameObject.name}");
            return;
        }

        SetupSprite();
        AddOutline();
    }

    void SetupSprite()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        // 커스텀 스프라이트가 있으면 우선 사용, 없으면 데이터에서 가져오기
        if (overrideSprite && customSprite != null)
        {
            spriteRenderer.sprite = customSprite;
        }
        else
        {
            spriteRenderer.sprite = itemData.GetWorldSprite();
        }
    }

    void AddOutline()
    {
        if (hasOutline || spriteRenderer == null) return;

        GameObject outlineObj = new GameObject("Outline");
        outlineObj.transform.SetParent(transform);
        outlineObj.transform.localPosition = Vector3.zero;
        outlineObj.transform.localScale = Vector3.one * (1 + itemData.outlineWidth);

        SpriteRenderer outlineRenderer = outlineObj.AddComponent<SpriteRenderer>();
        outlineRenderer.sprite = spriteRenderer.sprite;
        outlineRenderer.color = itemData.GetOutlineColor();
        outlineRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;

        hasOutline = true;
    }

    public virtual void Interact()
    {
        if (itemData == null) return;

        if (itemData.itemType == ItemType.Collectible)
        {
            CollectItem();
        }
        else
        {
            InspectItem();
        }
    }

    protected virtual void CollectItem()
    {
        Debug.Log($"{itemData.displayName} 획득!");

        // 사운드 재생
        if (itemData.pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(itemData.pickupSound, transform.position);
        }

        // TODO: 인벤토리 시스템 연동
        // InventoryManager.Instance.AddItem(itemData, 1);

        // 퀘스트 아이템인 경우 특별 처리
        if (itemData.isQuestItem)
        {
            // QuestManager.Instance.OnQuestItemCollected(itemData.itemID);
            Debug.Log($"퀘스트 아이템 획득: {itemData.displayName}");
        }

        Destroy(gameObject);
    }

    protected virtual void InspectItem()
    {
        Debug.Log($"[{itemData.displayName}] {itemData.itemDescription}");

        // DialogueManager를 통해 대화창에 표시 (쯔꾸루 스타일)
        if (DialogueManager.Instance != null)
        {
            // 플레이어 혼잣말 형식으로 아이템 설명 표시
            string itemMessage = $"{itemData.displayName}이다.\n{itemData.itemDescription}";
            DialogueManager.Instance.ShowItemDescription(itemMessage);
            Debug.Log($"DialogueManager.ShowItemDescription 호출됨: {itemMessage}");
        }
        else
        {
            Debug.LogWarning("DialogueManager.Instance를 찾을 수 없습니다!");
        }
    }

    // 아이템 사용 (인벤토리에서 호출)
    public virtual bool UseItem()
    {
        if (!itemData.CanUse()) return false;

        // 사운드 재생
        if (itemData.useSound != null)
        {
            AudioSource.PlayClipAtPoint(itemData.useSound, transform.position);
        }

        // 효과 적용
        if (itemData.healAmount > 0)
        {
            // PlayerManager.Instance.Heal(itemData.healAmount);
            Debug.Log($"체력 {itemData.healAmount} 회복!");
        }

        if (itemData.manaAmount > 0)
        {
            // PlayerManager.Instance.RestoreMana(itemData.manaAmount);
            Debug.Log($"마나 {itemData.manaAmount} 회복!");
        }

        return itemData.consumeOnUse;  // 소모 여부 반환
    }

    // 에디터용 헬퍼
    void OnValidate()
    {
        if (itemData != null && !overrideSprite)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && itemData.GetWorldSprite() != null)
            {
                sr.sprite = itemData.GetWorldSprite();
            }
        }
    }
}