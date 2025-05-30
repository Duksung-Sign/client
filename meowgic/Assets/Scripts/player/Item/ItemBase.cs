using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Collectible,
    Inspectable
}

public class ItemBase : MonoBehaviour
{
    [Header("Basic Info")]
    public string itemID;
    public string displayName;
    [TextArea(2, 4)]
    public string itemDescription;

    [Header("Type")]
    public ItemType itemType = ItemType.Collectible;

    [Header("Visual Effects")]
    public Color outlineColor = Color.yellow;
    public float outlineWidth = 0.1f;

    private SpriteRenderer spriteRenderer;
    private Material outlineMaterial;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        AddOutline();
    }

    void AddOutline()
    {
        // Outline용 자식 오브젝트 생성
        GameObject outlineObj = new GameObject("Outline");
        outlineObj.transform.SetParent(transform);
        outlineObj.transform.localPosition = Vector3.zero;
        outlineObj.transform.localScale = Vector3.one * (1 + outlineWidth);

        // SpriteRenderer 복사
        SpriteRenderer outlineRenderer = outlineObj.AddComponent<SpriteRenderer>();
        outlineRenderer.sprite = spriteRenderer.sprite;
        outlineRenderer.color = outlineColor;
        outlineRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
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
        Destroy(gameObject);
    }

    protected virtual void InspectItem()
    {
        Debug.Log($"[{displayName}] {itemDescription}");
    }
}
