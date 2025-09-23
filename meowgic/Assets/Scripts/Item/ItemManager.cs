using UnityEngine;
using System.Collections;

public class ItemManager : MonoBehaviour, IInteractable
{
    [Header("아이템 데이터")]
    public ItemDataSO itemData;

    [Header("효과 설정")]
    public GameObject idleEffect;

    private bool isInspectionDialogueRunning = false;

    void OnEnable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnDialogueFinished += HandleDialogueFinished;
        }
    }

    void OnDisable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnDialogueFinished -= HandleDialogueFinished;
        }
    }

    void Start()
    {
        if (itemData == null)
        {
            Debug.LogError($"ItemData가 설정되지 않았습니다: {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        if (itemData.itemType == ItemDataSO.ItemType.Collectible)
        {
            if (GameDataManager.Instance.HasItem(itemData.itemID))
            {
                Destroy(gameObject);
            }
        }
        else if (itemData.itemType == ItemDataSO.ItemType.Inspectable)
        {
            if (GameDataManager.Instance.HasInspectedItem(itemData.itemID))
            {
                if (idleEffect != null)
                {
                    idleEffect.SetActive(false);
                }
            }
        }
    }

    public void Interact()
    {
        if (itemData.itemType == ItemDataSO.ItemType.Inspectable)
        {
            Inspect();
        }
        else
        {
            Collect();
        }
    }

    private void Collect()
    {
        GameDataManager.Instance.AddItem(itemData.itemID);
        GameDataManager.Instance.SaveState(); 

        GameEventManager.Instance.TriggerItemCollected(itemData.itemID);

        Debug.Log($"[ItemManager] {itemData.displayName} 획득.");
        Destroy(gameObject);
    }

    private void Inspect()
    {
        if (isInspectionDialogueRunning) return;

        if (!string.IsNullOrEmpty(itemData.inspectionDialogueID))
        {
            isInspectionDialogueRunning = true;
            DialogueManager.Instance.StartDialogue(itemData.inspectionDialogueID);
        }
        else
        {
            Debug.LogWarning($"[ItemManager] 조사용 대화 ID가 지정되지 않았습니다: {itemData.displayName}");
        }
    }

    private void HandleDialogueFinished(string eventType, string finishedDialogueID)
    {
        if (!isInspectionDialogueRunning || finishedDialogueID != itemData.inspectionDialogueID)
        {
            return;
        }

        isInspectionDialogueRunning = false;
        Debug.Log($"[ItemManager] '{itemData.displayName}'의 조사 대화({finishedDialogueID})가 종료된 것을 확인했습니다.");

        bool wasFirstInspection = !GameDataManager.Instance.HasInspectedItem(itemData.itemID);

        if (wasFirstInspection)
        {
            CompleteInspection();
        }
    }

    private void CompleteInspection()
    {
        GameDataManager.Instance.MarkItemAsInspected(itemData.itemID);
        GameDataManager.Instance.SaveState();

        GameEventManager.Instance.TriggerItemInspected(itemData.itemID);

        if (idleEffect != null)
        {
            idleEffect.SetActive(false);
        }

        Debug.Log($"[ItemManager] '{itemData.displayName}' 조사를 완료했습니다.");
    }
    
    public string GetInteractionPromptText()
    {
        if (itemData != null)
        {
            return itemData.GetDisplayName();
        }
        return "알 수 없는 아이템";
    }

    public bool IsCurrentlyInteractable()
    {
        if (itemData.itemType == ItemDataSO.ItemType.Collectible)
        {
            return true;
        }

        if (itemData.itemType == ItemDataSO.ItemType.Inspectable)
        {
            bool hasBeenInspected = GameDataManager.Instance.HasInspectedItem(itemData.itemID);
            if (hasBeenInspected)
            {
                return false;
            }
            return true;
        }

        return false;
    }
}