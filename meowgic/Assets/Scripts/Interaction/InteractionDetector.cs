using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    public float detectionRange = 2f;
    public LayerMask interactionLayers = -1;

    [Header("UI")]
    public GameObject promptUI;
    public TMPro.TextMeshProUGUI promptText;

    private GameObject currentTarget;
    private List<GameObject> nearbyObjects = new List<GameObject>();

    void Update()
    {
        // 대화 중이 아닐 때만 상호작용 감지
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue())
        {
            // 대화 중에는 프롬프트 UI 숨기기
            if (promptUI != null && promptUI.activeInHierarchy)
            {
                promptUI.SetActive(false);
            }
            return;
        }

        CheckForInteractables();
        HandleInput();
    }

    void CheckForInteractables()
    {
        nearbyObjects.Clear();

        Collider2D[] colliders = Physics2D.OverlapCircleAll(
            transform.position, detectionRange, interactionLayers);

        foreach (Collider2D col in colliders)
        {
            if (col.GetComponent<ItemBase>() != null ||
                col.GetComponent<NPCController>() != null)
            {
                nearbyObjects.Add(col.gameObject);
            }
        }

        GameObject closest = GetClosestObject();

        if (closest != currentTarget)
        {
            currentTarget = closest;
            UpdatePrompt();
        }
    }

    GameObject GetClosestObject()
    {
        if (nearbyObjects.Count == 0) return null;

        GameObject closest = nearbyObjects[0];
        float closestDistance = Vector3.Distance(transform.position, closest.transform.position);

        for (int i = 1; i < nearbyObjects.Count; i++)
        {
            float distance = Vector3.Distance(transform.position, nearbyObjects[i].transform.position);
            if (distance < closestDistance)
            {
                closest = nearbyObjects[i];
                closestDistance = distance;
            }
        }

        return closest;
    }

    void UpdatePrompt()
    {
        if (currentTarget != null)
        {
            if (promptUI != null)
                promptUI.SetActive(true);

            string actionText = GetActionText(currentTarget);
            string objectName = GetObjectName(currentTarget);

            if (promptText != null)
                promptText.text = $"[E] {actionText} {objectName}";
        }
        else
        {
            if (promptUI != null)
                promptUI.SetActive(false);
        }
    }

    string GetActionText(GameObject target)
    {
        ItemBase item = target.GetComponent<ItemBase>();
        if (item != null)
        {
            return item.itemType == ItemType.Collectible ? "획득" : "조사";
        }

        NPCController npc = target.GetComponent<NPCController>();
        if (npc != null)
        {
            return "대화";
        }

        return "상호작용";
    }

    string GetObjectName(GameObject target)
    {
        ItemBase item = target.GetComponent<ItemBase>();
        if (item != null) return item.displayName;

        NPCController npc = target.GetComponent<NPCController>();
        if (npc != null) return npc.npcName;

        return target.name;
    }

    void HandleInput()
    {
        // 대화 중이 아닐 때만 E키 처리
        if (Input.GetKeyDown(KeyCode.E) && currentTarget != null)
        {
            Debug.Log($"상호작용 시작: {currentTarget.name}");

            ItemBase item = currentTarget.GetComponent<ItemBase>();
            if (item != null)
            {
                item.Interact();
                return;
            }

            NPCController npc = currentTarget.GetComponent<NPCController>();
            if (npc != null)
            {
                npc.Interact();
                return;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}