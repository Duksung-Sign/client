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

    [Header("퀘스트 아이템 디버깅")]
    public bool debugQuestItem = true;

    // 아이템 고유 ID (수집 상태 추적용)
    [Header("아이템 고유 식별자")]
    [Tooltip("비어있으면 itemData.itemID를 사용합니다")]
    public string uniqueItemID = "";

    private SpriteRenderer spriteRenderer;
    private bool hasOutline = false;

    private static bool firstRunChecked = false;
    private const string kFirstRunKey = "HasLaunchedBefore";

    void Awake()
    {
        // 최초 실행 여부 한 번만 체크
        if (!firstRunChecked)
        {
            if (!PlayerPrefs.HasKey(kFirstRunKey))
            {
                // 첫 실행이니까, 이전에 저장된 모든 수집 상태를 지워준다.
                // (만약 다른 PlayerPrefs를 같이 쓰고 있다면, 
                //  DeleteAll 대신 Item_Collected_ 로 시작하는 키만 지우는 로직을 작성해도 OK)
                PlayerPrefs.DeleteAll();
                PlayerPrefs.SetInt(kFirstRunKey, 1);
                PlayerPrefs.Save();
                Debug.Log("[ItemManager] 첫 실행 - 수집 상태 초기화");
            }
            firstRunChecked = true;
        }
    }

    void Start()
    {
        if (itemData == null)
        {
            Debug.LogError($"ItemData가 설정되지 않았습니다: {gameObject.name}");
            return;
        }

        // 아이템 고유 ID 설정
        SetupUniqueID();

        // 이미 수집된 아이템인지 확인
        if (IsItemAlreadyCollected())
        {
            Debug.Log($"아이템 {GetItemUniqueID()}는 이미 수집되어 제거됩니다.");
            Destroy(gameObject);
            return;
        }

        SetupSprite();
        AddOutline();

        // 퀘스트 아이템 디버깅 정보
        if (debugQuestItem && itemData.isQuestItem)
        {
            Debug.Log($"퀘스트 아이템 초기화: {itemData.displayName} (ID: {itemData.itemID})");
        }
    }

    void SetupUniqueID()
    {
        // 고유 ID가 설정되어 있지 않으면 itemData의 ID 사용
        if (string.IsNullOrEmpty(uniqueItemID))
        {
            uniqueItemID = itemData.itemID;
        }

        // 그래도 비어있으면 GameObject 이름 + 위치로 생성
        if (string.IsNullOrEmpty(uniqueItemID))
        {
            Vector3 pos = transform.position;
            uniqueItemID = $"{gameObject.name}_{pos.x:F2}_{pos.y:F2}";
            Debug.LogWarning($"아이템 고유 ID가 없어서 자동 생성: {uniqueItemID}");
        }
    }

    string GetItemUniqueID()
    {
        return uniqueItemID;
    }

    bool IsItemAlreadyCollected()
    {
        string key = $"Item_Collected_{GetItemUniqueID()}";
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    void SaveItemCollected()
    {
        string key = $"Item_Collected_{GetItemUniqueID()}";
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        Debug.Log($"아이템 수집 상태 저장: {GetItemUniqueID()}");
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
        if (itemData == null)
        {
            Debug.LogError("ItemData가 null입니다!");
            return;
        }

        Debug.Log($"아이템 상호작용: {itemData.displayName} (타입: {itemData.itemType}, 퀘스트아이템: {itemData.isQuestItem})");

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

        // 아이템 수집 상태 저장 (중요!)
        SaveItemCollected();

        // 퀘스트 아이템인지 먼저 확인
        if (itemData.isQuestItem)
        {
            Debug.Log($"=== 퀘스트 아이템 처리 시작: {itemData.displayName} (ID: {itemData.itemID}) ===");

            // QuestItemHandler가 있는지 먼저 확인
            QuestItemHandler questHandler = FindObjectOfType<QuestItemHandler>();
            if (questHandler == null)
            {
                Debug.LogError("QuestItemHandler를 찾을 수 없습니다! 퀘스트 이벤트가 실행되지 않습니다.");
                // QuestItemHandler가 없어도 일반 아이템 처리는 계속
                ProcessNormalItem();
                return;
            }

            // QuestItemHandler의 대상 아이템인지 확인
            bool isTargetItem = CheckIfTargetQuestItem(questHandler);
            if (!isTargetItem)
            {
                Debug.Log($"이 아이템은 QuestItemHandler의 대상이 아닙니다: {itemData.itemID}");
                ProcessNormalItem();
                return;
            }

            Debug.Log($"QuestItemHandler 대상 아이템 확인됨: {itemData.itemID}");

            // 퀘스트 아이템 전용 처리 시작
            ProcessQuestItem(questHandler);
        }
        else
        {
            // 일반 아이템 처리
            ProcessNormalItem();
        }
    }

    // QuestItemHandler의 대상 아이템인지 확인
    bool CheckIfTargetQuestItem(QuestItemHandler questHandler)
    {
        if (questHandler.targetQuestItemIDs == null || questHandler.targetQuestItemIDs.Length == 0)
        {
            Debug.LogWarning("QuestItemHandler의 targetQuestItemIDs가 설정되지 않았습니다!");
            return false;
        }

        foreach (string targetID in questHandler.targetQuestItemIDs)
        {
            if (itemData.itemID == targetID)
            {
                return true;
            }
        }
        return false;
    }

    // 퀘스트 아이템 전용 처리
    void ProcessQuestItem(QuestItemHandler questHandler)
    {
        Debug.Log("=== 퀘스트 아이템 시퀀스 시작 ===");

        // 1단계: 아이템 획득 메시지 표시 (DialogueManager 사용)
        if (DialogueManager.Instance != null)
        {
            string itemMessage = $"{itemData.displayName}이다. {itemData.itemDescription}";
            Debug.Log($"아이템 설명 표시: {itemMessage}");

            DialogueManager.Instance.ShowItemDescription(itemMessage);

            // 2단계: 아이템 설명이 닫힌 후 퀘스트 이벤트 실행
            StartCoroutine(WaitForItemDescriptionThenCallQuest(questHandler));
        }
        else
        {
            Debug.LogError("DialogueManager.Instance가 null입니다!");
            // DialogueManager가 없어도 퀘스트 이벤트는 실행
            CallQuestItemHandler(questHandler);
        }
    }

    // 일반 아이템 처리
    void ProcessNormalItem()
    {
        Debug.Log($"{itemData.displayName} 획득! (일반 아이템)");

        // 사운드 재생
        if (itemData.pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(itemData.pickupSound, transform.position);
        }

        // 아이템 오브젝트 제거
        Destroy(gameObject);
    }

    // 아이템 설명이 닫힐 때까지 기다린 후 QuestItemHandler 호출
    IEnumerator WaitForItemDescriptionThenCallQuest(QuestItemHandler questHandler)
    {
        Debug.Log("아이템 설명 다이얼로그가 닫힐 때까지 대기 중...");

        // 아이템 설명 다이얼로그가 닫힐 때까지 대기
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        Debug.Log("아이템 설명 다이얼로그 종료됨. 퀘스트 이벤트 실행 시작.");

        // 약간 대기 (부드러운 전환을 위해)
        yield return new WaitForSeconds(0.2f);

        // 3단계: QuestItemHandler 호출
        CallQuestItemHandler(questHandler);
    }

    // QuestItemHandler 호출 및 아이템 제거
    void CallQuestItemHandler(QuestItemHandler questHandler)
    {
        Debug.Log($"=== QuestItemHandler.OnSpecificQuestItemObtained 호출: {itemData.itemID} ===");

        // 사운드 재생 (퀘스트 아이템 획득)
        if (itemData.pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(itemData.pickupSound, transform.position);
        }

        // 퀘스트 이벤트 실행
        questHandler.OnSpecificQuestItemObtained(itemData.itemID, transform.position);

        // 아이템 오브젝트 제거
        Debug.Log($"아이템 오브젝트 제거: {gameObject.name}");
        Destroy(gameObject);
    }

    protected virtual void InspectItem()
    {
        Debug.Log($"[{itemData.displayName}] {itemData.itemDescription}");

        // DialogueManager를 통해 대화창에 표시 (탐색 스타일)
        if (DialogueManager.Instance != null)
        {
            // 플레이어 혼잣말로 아이템 설명 표시
            string itemMessage = $"{itemData.displayName}이다. {itemData.itemDescription}";
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

    // 개발자용: 아이템 수집 상태 리셋
    [ContextMenu("Reset Item Collection")]
    public void ResetItemCollection()
    {
        string key = $"Item_Collected_{GetItemUniqueID()}";
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        Debug.Log($"아이템 수집 상태 리셋: {GetItemUniqueID()}");
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