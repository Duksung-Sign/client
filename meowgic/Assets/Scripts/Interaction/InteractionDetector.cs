using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InteractionDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    public float detectionRange = 2f;
    public LayerMask interactionLayers = -1;

    [Header("UI")]
    public GameObject promptUI;
    public TextMeshProUGUI promptText;

    [Header("UI References")]
    public Image characterPortraitImage; // UI에서 초상화를 표시할 Image 컴포넌트

    [Header("Player Reference")]
    public PlayerStats playerStats;      // 플레이어 스탯 참조 (초상화 정보 포함)

    private GameObject currentTarget;
    private List<GameObject> nearbyObjects = new List<GameObject>();
    private Sprite currentCharacterSprite;

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

            // NPC인 경우 초상화 미리보기 설정
            UpdateCharacterPortrait();
        }
        else
        {
            if (promptUI != null)
                promptUI.SetActive(false);
        }
    }

    void UpdateCharacterPortrait()
    {
        if (characterPortraitImage == null) return;

        NPCController npc = currentTarget?.GetComponent<NPCController>();
        if (npc != null && npc.npcPortrait != null)
        {
            // NPC 초상화로 설정
            characterPortraitImage.sprite = npc.npcPortrait;
            currentCharacterSprite = npc.npcPortrait;
        }
        else
        {
            // 플레이어 초상화로 설정
            Sprite playerPortrait = GetPlayerPortrait();
            characterPortraitImage.sprite = playerPortrait;
            currentCharacterSprite = playerPortrait;
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
                // NPC와 대화 시작하면서 DialogueManager에 초상화 정보 전달
                SetDialoguePortraits(npc);
                npc.Interact();
                return;
            }
        }
    }

    // DialogueManager에 대화 참여자들의 초상화 설정
    void SetDialoguePortraits(NPCController npc)
    {
        if (DialogueManager.Instance != null)
        {
            Sprite playerPortrait = GetPlayerPortrait();
            // DialogueManager에 플레이어와 NPC 초상화 전달
            DialogueManager.Instance.SetCharacterPortraits(playerPortrait, npc.npcPortrait);
        }
    }

    // 플레이어 초상화를 가져오는 메서드
    Sprite GetPlayerPortrait()
    {
        if (playerStats != null && playerStats.playerPortrait != null)
        {
            return playerStats.playerPortrait;
        }
        Debug.LogWarning("플레이어 초상화를 찾을 수 없습니다!");
        return null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}