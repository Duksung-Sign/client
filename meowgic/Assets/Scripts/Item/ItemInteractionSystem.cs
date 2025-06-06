using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public enum InteractionState
{
    None,           // 아무것도 안 보임
    ShowPrompt,     // [E] 아이템명 표시  
    ShowDescription // 아이템 설명 표시
}

public class ItemInteractionSystem : MonoBehaviour
{
    [Header("상호작용 설정")]
    public float interactionRange = 2f;
    public LayerMask itemLayerMask = -1;

    [Header("UI 설정")]
    public GameObject promptUI;              // [E] 프롬프트 UI
    public TextMeshProUGUI promptText;       // "[E] 아이템명" 텍스트

    public GameObject descriptionUI;         // 설명 UI 패널
    public TextMeshProUGUI descriptionText;  // 아이템 설명 텍스트

    private InteractionState currentState = InteractionState.None;
    private ItemBase currentNearbyItem;
    private List<ItemBase> nearbyItems = new List<ItemBase>();
    private float descriptionOpenTime;  // 설명 UI가 열린 시간

    void Start()
    {
        SetUIState(InteractionState.None);
    }

    void Update()
    {
        CheckNearbyItems();
        HandleInput();
        CheckDescriptionUIDistance();
    }

    void CheckDescriptionUIDistance()
    {
        if (currentState == InteractionState.ShowDescription && currentNearbyItem != null)
        {
            float distance = Vector3.Distance(transform.position, currentNearbyItem.transform.position);
            if (distance > interactionRange * 2f)
            {
                Debug.Log("아이템에서 멀어져서 설명 UI를 닫습니다.");
                currentNearbyItem = null;
                SetUIState(InteractionState.None);
            }
        }
    }

    void CheckNearbyItems()
    {
        if (currentState == InteractionState.ShowDescription)
        {
            return;
        }

        nearbyItems.Clear();

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, interactionRange, itemLayerMask);

        foreach (Collider2D col in colliders)
        {
            ItemBase item = col.GetComponent<ItemBase>();
            if (item != null)
            {
                nearbyItems.Add(item);
            }
        }

        ItemBase closestItem = GetClosestItem();

        if (closestItem != currentNearbyItem)
        {
            currentNearbyItem = closestItem;
            UpdatePromptUI();
        }
    }

    ItemBase GetClosestItem()
    {
        if (nearbyItems.Count == 0) return null;

        ItemBase closest = nearbyItems[0];
        float closestDistance = Vector3.Distance(transform.position, closest.transform.position);

        for (int i = 1; i < nearbyItems.Count; i++)
        {
            float distance = Vector3.Distance(transform.position, nearbyItems[i].transform.position);
            if (distance < closestDistance)
            {
                closest = nearbyItems[i];
                closestDistance = distance;
            }
        }

        return closest;
    }

    void UpdatePromptUI()
    {
        if (currentNearbyItem != null && currentState == InteractionState.None)
        {
            // 프롬프트 표시
            SetUIState(InteractionState.ShowPrompt);
            if (promptText != null)
            {
                promptText.text = $"[E] {currentNearbyItem.displayName}";
            }
        }
        else if (currentNearbyItem == null && currentState == InteractionState.ShowPrompt)
        {
            // 프롬프트 숨기기
            SetUIState(InteractionState.None);
        }
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (currentState == InteractionState.ShowPrompt && currentNearbyItem != null)
            {
                // E키로 설명 UI 열기
                ShowItemDescription();
            }
        }

        // Enter 키로 설명 UI 닫기 (최소 시간 경과 후에만)
        if (Input.GetKeyDown(KeyCode.Return) && currentState == InteractionState.ShowDescription)
        {
            CloseDescription();
        }
    }

    void ShowItemDescription()
    {
        if (currentNearbyItem != null)
        {
            // 아이템 상호작용 로직 실행
            currentNearbyItem.Interact();

            // Inspectable 아이템인 경우에만 설명 UI 표시
            if (currentNearbyItem.itemType == ItemType.Inspectable)
            {
                SetUIState(InteractionState.ShowDescription);
                descriptionOpenTime = Time.time;  // 열린 시간 기록

                if (descriptionText != null)
                {
                    descriptionText.text = currentNearbyItem.itemDescription;
                }
            }
            else
            {
                // Collectible 아이템은 바로 수집되므로 UI 상태 초기화
                currentNearbyItem = null;
                SetUIState(InteractionState.None);
            }
        }
    }

    void CloseDescription()
    {
        // 플레이어가 아직 아이템 근처에 있는지 확인
        // Inspectable 아이템은 사라지지 않으므로 다시 프롬프트 표시
        if (currentNearbyItem != null && currentNearbyItem.itemType == ItemType.Inspectable)
        {
            SetUIState(InteractionState.ShowPrompt);
            if (promptText != null)
            {
                promptText.text = $"[E] {currentNearbyItem.displayName}";
            }
        }
        else
        {
            SetUIState(InteractionState.None);
        }
    }

    void SetUIState(InteractionState newState)
    {
        currentState = newState;

        // 모든 UI 숨기기
        if (promptUI != null) promptUI.SetActive(false);
        if (descriptionUI != null) descriptionUI.SetActive(false);

        // 현재 상태에 맞는 UI만 표시
        switch (currentState)
        {
            case InteractionState.ShowPrompt:
                if (promptUI != null) promptUI.SetActive(true);
                break;

            case InteractionState.ShowDescription:
                if (descriptionUI != null) descriptionUI.SetActive(true);
                break;

            case InteractionState.None:
            default:
                // 모든 UI 숨김 (이미 위에서 처리됨)
                break;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}