using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Collectible,    // 획득 가능 (인벤토리로)
    Inspectable     // 탐색 가능 (스크립트만)
}

public class ItemBase : MonoBehaviour
{
    [Header("Basic Info")]
    public string itemID;           // health_potion_small
    public string displayName;      // 빨간 물약
    [TextArea(2, 4)]
    public string itemDescription;  // 먹으면 힘이 날 거 같다

    [Header("Type")]
    public ItemType itemType = ItemType.Collectible;

    [Header("Visual")]
    public Sprite icon;             // 인벤토리용 아이콘
    public Color outlineColor = Color.yellow;
    public float outlineWidth = 0.1f;

    private SpriteRenderer spriteRenderer;
    private bool hasOutline = false;

    void Start()
    {
        AddOutline();
    }

    void AddOutline()
    {
        if (hasOutline) return;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        GameObject outlineObj = new GameObject("Outline");
        outlineObj.transform.SetParent(transform);
        outlineObj.transform.localPosition = Vector3.zero;
        outlineObj.transform.localScale = Vector3.one * (1 + outlineWidth);

        SpriteRenderer outlineRenderer = outlineObj.AddComponent<SpriteRenderer>();
        outlineRenderer.sprite = spriteRenderer.sprite;
        outlineRenderer.color = outlineColor;
        outlineRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;

        hasOutline = true;
    }

    public virtual void Interact()
    {
        if (itemType == ItemType.Collectible)
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
        Debug.Log($"{displayName} 획득!");

        // TODO: 인벤토리 시스템 연동
        // InventoryManager.Instance.AddItem(itemID, 1);
        // 특별한 아이템(고서 등)은 특별 처리
        // if (itemID == "ancient_book") 
        // {
        //     BookManager.Instance.AddBook(this);
        // }

        Destroy(gameObject);
    }

    protected virtual void InspectItem()
    {
        Debug.Log($"[{displayName}] {itemDescription}");

        // UI로 스크립트 표시
        ItemUIManager.Instance.ShowItemDescription(itemDescription);
    }
}
