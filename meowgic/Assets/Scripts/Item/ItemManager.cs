using UnityEngine;
using System.Collections;

public class ItemManager : MonoBehaviour, IInteractable
{
    [Header("아이템 데이터")]
    public ItemDataSO itemData;

    [Header("효과 설정")]
    public GameObject idleEffect;

    [Header("사운드 설정")]
    [SerializeField] private AudioClip collectSound; 

    private AudioSource audioSource;
    private bool isInspectionDialogueRunning = false;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

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

    IEnumerator Start()
    {
        while (GameDataManager.Instance == null ||
               DialogueManager.Instance == null ||
               GameEventManager.Instance == null)
        {
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);

        if (itemData == null)
        {
            Debug.LogError($"ItemData가 설정되지 않았습니다: {gameObject.name}");
            Destroy(gameObject);
            yield break;
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
        PlayCollectSound();

        GameDataManager.Instance.AddItem(itemData.itemID);
        GameDataManager.Instance.SaveState();

        GameEventManager.Instance.TriggerItemCollected(itemData.itemID);

        Debug.Log($"[ItemManager] {itemData.displayName} 획득.");

        if (collectSound != null)
        {
            Destroy(gameObject, collectSound.length);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void PlayCollectSound()
    {
        if (collectSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(collectSound);
        }
        else if (AudioManager.Instance != null && collectSound != null)
        {
            AudioManager.Instance.PlaySFX(collectSound, 0.15f);
        }
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

        Debug.Log($"[ItemManager] '{itemData.displayName}' 조사를 완료했습니다.");

        Destroy(gameObject);
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