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

    [Header("디버그")]
    public bool debugItemSystem = true;

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
                // 첫 실행이므로, 이전에 저장된 모든 아이템 상태를 지웁니다.
                PlayerPrefs.DeleteAll();
                PlayerPrefs.SetInt(kFirstRunKey, 1);
                PlayerPrefs.Save();
                Debug.Log("[ItemManager] 첫 실행 - 아이템 상태 초기화");
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

        // 이미 수집된 아이템인지 확인 (중복 방지)
        if (IsItemAlreadyObtained())
        {
            if (debugItemSystem)
                Debug.Log($"아이템 {GetItemID()}는 이미 획득되어 제거됩니다.");
            Destroy(gameObject);
            return;
        }

        SetupSprite();
        AddOutline();

        // 디버그 정보
        if (debugItemSystem)
        {
            Debug.Log($"아이템 초기화: {GetItemDisplayName()} (ID: {GetItemID()}, 퀘스트: {itemData.isQuestItem})");
        }
    }

    // 간단한 헬퍼 메서드들
    string GetItemID()
    {
        return string.IsNullOrEmpty(itemData.itemID) ? itemData.name : itemData.itemID;
    }

    string GetItemDisplayName()
    {
        return string.IsNullOrEmpty(itemData.displayName) ? itemData.name : itemData.displayName;
    }

    Sprite GetItemWorldSprite()
    {
        return itemData.worldSprite != null ? itemData.worldSprite : itemData.icon;
    }

    Color GetItemOutlineColor()
    {
        if (itemData.isQuestItem)
            return Color.cyan;  // 퀘스트 아이템은 하늘색
        return itemData.outlineColor;
    }

    /// <summary>
    /// 이미 획득한 아이템인지 확인 (CSV 스토리 시스템 기준)
    /// </summary>
    bool IsItemAlreadyObtained()
    {
        string itemID = GetItemID();
        string countKey = $"Item_{itemID}_Count";
        int currentCount = PlayerPrefs.GetInt(countKey, 0);

        // 퀘스트 아이템은 1개만 존재해야 함
        if (itemData.isQuestItem)
        {
            return currentCount > 0;
        }

        // 일반 아이템은 스택 가능 여부에 따라
        if (itemData.stackable)
        {
            return currentCount >= itemData.maxStackSize;
        }
        else
        {
            return currentCount > 0;
        }
    }

    /// <summary>
    /// 아이템 획득 처리 - CSV 스토리 시스템과 완전 호환
    /// </summary>
    void SaveItemObtained()
    {
        string itemID = GetItemID();
        string countKey = $"Item_{itemID}_Count";
        int currentCount = PlayerPrefs.GetInt(countKey, 0);
        int newCount = currentCount + 1;

        PlayerPrefs.SetInt(countKey, newCount);
        PlayerPrefs.Save();

        if (debugItemSystem)
        {
            Debug.Log($"아이템 획득 저장: {countKey} = {newCount}");
        }

        // 스토리 시스템에 즉시 알림
        NotifyStorySystemItemObtained(itemID, newCount);
    }

    /// <summary>
    /// 스토리 시스템에 아이템 획득 알림
    /// </summary>
    void NotifyStorySystemItemObtained(string itemID, int newCount)
    {
        if (StoryProgressManager.Instance != null)
        {
            // 현재 스토리에서 이 아이템 획득을 기다리고 있는지 즉시 체크
            StoryProgressManager.Instance.TryAdvanceToNextStory();

            if (StoryProgressManager.Instance != null)
            {
                StoryProgressManager.Instance.OnItemObtained(itemID);
            }
        }
    }

    void SetupSprite()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        if (overrideSprite && customSprite != null)
        {
            spriteRenderer.sprite = customSprite;
        }
        else
        {
            spriteRenderer.sprite = GetItemWorldSprite();
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
        outlineRenderer.color = GetItemOutlineColor();
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

        if (debugItemSystem)
        {
            Debug.Log($"아이템 상호작용: {GetItemDisplayName()} (퀘스트: {itemData.isQuestItem})");
        }

        CollectItem();
    }

    protected virtual void CollectItem()
    {
        if (debugItemSystem)
        {
            Debug.Log($"{GetItemDisplayName()} 획득!");
        }

        // 아이템 획득 상태 저장 (CSV 스토리 시스템과 완전 호환)
        SaveItemObtained();

        // 퀘스트 아이템 vs 일반 아이템 처리
        if (itemData.isQuestItem)
        {
            ProcessQuestItem();
        }
        else
        {
            ProcessNormalItem();
        }
    }

    /// <summary>
    /// 퀘스트 아이템 처리
    /// </summary>
    void ProcessQuestItem()
    {
        if (debugItemSystem)
        {
            Debug.Log($"=== 퀘스트 아이템 처리: {GetItemDisplayName()} (ID: {GetItemID()}) ===");
        }

        // QuestItemHandler 찾기
        QuestItemHandler questHandler = FindObjectOfType<QuestItemHandler>();
        if (questHandler == null)
        {
            Debug.LogError("QuestItemHandler를 찾을 수 없습니다! 일반 아이템으로 처리합니다.");
            ProcessNormalItem();
            return;
        }

        // QuestItemHandler의 대상 아이템인지 확인
        if (!IsTargetQuestItem(questHandler))
        {
            if (debugItemSystem)
            {
                Debug.Log($"QuestItemHandler 대상이 아닙니다: {GetItemID()}");
            }
            ProcessNormalItem();
            return;
        }

        // 퀘스트 아이템 시퀀스 시작
        StartQuestItemSequence(questHandler);
    }

    /// <summary>
    /// QuestItemHandler의 대상 아이템인지 확인
    /// </summary>
    bool IsTargetQuestItem(QuestItemHandler questHandler)
    {
        string itemID = GetItemID();

        // 1. 특정 아이템 ID 체크
        if (!string.IsNullOrEmpty(questHandler.specificQuestItemID))
        {
            return itemID == questHandler.specificQuestItemID;
        }

        // 2. 퀘스트 아이템 태그 체크
        if (questHandler.useQuestItemTag)
        {
            return itemData.isQuestItem;
        }

        // 3. 대상 아이템 배열 체크
        if (questHandler.targetQuestItemIDs != null)
        {
            foreach (string targetID in questHandler.targetQuestItemIDs)
            {
                if (itemID == targetID)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 퀘스트 아이템 시퀀스 시작
    /// </summary>
    void StartQuestItemSequence(QuestItemHandler questHandler)
    {
        // 1단계: 아이템 설명 표시
        if (DialogueManager.Instance != null)
        {
            string itemMessage = $"{GetItemDisplayName()}이다. {itemData.itemDescription}";

            if (debugItemSystem)
            {
                Debug.Log($"아이템 설명 표시: {itemMessage}");
            }

            DialogueManager.Instance.ShowItemDescription(itemMessage);
            StartCoroutine(WaitForItemDescriptionThenCallQuest(questHandler));
        }
        else
        {
            Debug.LogError("DialogueManager.Instance가 null입니다!");
            CallQuestItemHandler(questHandler);
        }
    }

    /// <summary>
    /// 일반 아이템 처리
    /// </summary>
    void ProcessNormalItem()
    {
        if (debugItemSystem)
        {
            Debug.Log($"{GetItemDisplayName()} 획득! (일반 아이템)");
        }

        // 사운드 재생
        PlayPickupSound();

        // 아이템 오브젝트 제거
        Destroy(gameObject);
    }

    void PlayPickupSound()
    {
        if (!string.IsNullOrEmpty(itemData.pickupSoundID))
        {
            Debug.Log($"[사운드] 아이템 획득: {itemData.pickupSoundID}");
            // TODO: AudioManager 연동
        }
    }

    /// <summary>
    /// 아이템 설명 종료 후 퀘스트 이벤트 실행
    /// </summary>
    IEnumerator WaitForItemDescriptionThenCallQuest(QuestItemHandler questHandler)
    {
        if (debugItemSystem)
        {
            Debug.Log("아이템 설명 종료 대기 중...");
        }

        // 아이템 설명 대화가 끝날 때까지 대기
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        if (debugItemSystem)
        {
            Debug.Log("아이템 설명 종료됨. 퀘스트 이벤트 실행.");
        }

        yield return new WaitForSeconds(0.2f); // 부드러운 전환

        CallQuestItemHandler(questHandler);
    }

    /// <summary>
    /// QuestItemHandler 호출 및 오브젝트 제거
    /// </summary>
    void CallQuestItemHandler(QuestItemHandler questHandler)
    {
        if (debugItemSystem)
        {
            Debug.Log($"=== QuestItemHandler 호출: {GetItemID()} ===");
        }

        // 사운드 재생
        PlayPickupSound();

        // 퀘스트 이벤트 실행
        questHandler.OnSpecificQuestItemObtained(GetItemID(), transform.position);

        // 오브젝트 제거
        Destroy(gameObject);
    }

    // 아이템 사용 (나중에 인벤토리 시스템에서 호출)
    public virtual bool UseItem()
    {
        if (!itemData.CanUse()) return false;

        if (debugItemSystem)
        {
            Debug.Log($"아이템 사용: {GetItemDisplayName()}");
        }

        // 사용 사운드
        if (!string.IsNullOrEmpty(itemData.useSoundID))
        {
            Debug.Log($"[사운드] 아이템 사용: {itemData.useSoundID}");
        }

        // 효과 적용
        if (itemData.healAmount > 0)
        {
            Debug.Log($"체력 {itemData.healAmount} 회복!");
            // TODO: PlayerStatsManager 연동
        }

        if (itemData.manaAmount > 0)
        {
            Debug.Log($"마나 {itemData.manaAmount} 회복!");
        }

        return itemData.consumeOnUse;
    }

    /// <summary>
    /// 특정 아이템의 현재 개수 조회 (정적 메서드)
    /// </summary>
    public static int GetItemCount(string itemID)
    {
        return PlayerPrefs.GetInt($"Item_{itemID}_Count", 0);
    }

    /// <summary>
    /// 특정 아이템이 획득되었는지 확인 (정적 메서드)
    /// </summary>
    public static bool HasItem(string itemID)
    {
        return GetItemCount(itemID) > 0;
    }

    // 개발자용 디버그 메서드들
    [ContextMenu("Reset This Item")]
    public void ResetThisItem()
    {
        string itemID = GetItemID();
        string countKey = $"Item_{itemID}_Count";
        PlayerPrefs.DeleteKey(countKey);
        PlayerPrefs.Save();
        Debug.Log($"아이템 리셋: {itemID}");
    }

    [ContextMenu("Show Item Info")]
    public void ShowItemInfo()
    {
        string itemID = GetItemID();
        int count = GetItemCount(itemID);
        Debug.Log($"=== 아이템 정보 ===");
        Debug.Log($"ID: {itemID}");
        Debug.Log($"이름: {GetItemDisplayName()}");
        Debug.Log($"현재 개수: {count}");
        Debug.Log($"퀘스트 아이템: {itemData.isQuestItem}");
        Debug.Log($"스택 가능: {itemData.stackable} (최대: {itemData.maxStackSize})");
    }
}