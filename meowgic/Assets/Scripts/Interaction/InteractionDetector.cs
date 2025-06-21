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
    public Canvas promptCanvas; // 프롬프트 UI 캔버스

    [Header("UI References")]
    public Image characterPortraitImage;

    [Header("Player Reference")]
    public PlayerStats playerStats;

    [Header("Prompt Position Settings")]
    public Vector3 promptOffset = new Vector3(0, 1.5f, 0); // 아이템 위쪽으로 얼마나 띄울지

    private GameObject currentTarget;
    private List<GameObject> nearbyObjects = new List<GameObject>();
    private Sprite currentCharacterSprite;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindObjectOfType<Camera>();

        // 프롬프트 캔버스를 월드 스페이스로 설정
        if (promptCanvas != null)
        {
            promptUI.SetActive(false);
            promptCanvas.renderMode = RenderMode.WorldSpace;
            promptCanvas.worldCamera = mainCamera;
        }
    }

    void Update()
    {
        // 대화 중이거나 아이템 설명 중일 때는 상호작용 감지 중단
        bool isInDialogue = DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue();

        if (isInDialogue)
        {
            // 대화 중일 때는 프롬프트 UI 숨기기
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
            if (col.GetComponent<ItemManager>() != null ||
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
                promptText.text = $"[E] {objectName}";

            // 프롬프트 UI를 아이템 위쪽에 위치시키기
            UpdatePromptPosition();

            // NPC의 경우 초상화 미리보기 설정
            UpdateCharacterPortrait();
        }
        else
        {
            if (promptUI != null)
                promptUI.SetActive(false);
        }
    }

    void UpdatePromptPosition()
    {
        if (currentTarget == null || promptCanvas == null) return;

        Vector3 worldPosition = currentTarget.transform.position + promptOffset;
        promptCanvas.transform.position = worldPosition;

        // 카메라를 향하도록 회전 (빌보드 효과)
        if (mainCamera != null)
        {
            Vector3 directionToCamera = mainCamera.transform.position - promptCanvas.transform.position;
            directionToCamera.y = 0; // Y축 회전만 적용

            if (directionToCamera != Vector3.zero)
            {
                promptCanvas.transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }
    }

    void UpdateCharacterPortrait()
    {
        if (characterPortraitImage == null) return;

        NPCController npc = currentTarget?.GetComponent<NPCController>();
        if (npc != null && npc.npcPortrait != null)
        {
            characterPortraitImage.sprite = npc.npcPortrait;
            currentCharacterSprite = npc.npcPortrait;
        }
        else
        {
            Sprite playerPortrait = GetPlayerPortrait();
            characterPortraitImage.sprite = playerPortrait;
            currentCharacterSprite = playerPortrait;
        }
    }

    string GetActionText(GameObject target)
    {
        ItemManager item = target.GetComponent<ItemManager>();
        if (item != null && item.itemData != null)
        {
            return item.itemData.itemType == ItemType.Collectible ? "획득" : "조사";
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
        ItemManager item = target.GetComponent<ItemManager>();
        if (item != null && item.itemData != null)
            return item.itemData.displayName;

        NPCController npc = target.GetComponent<NPCController>();
        if (npc != null)
            return npc.npcName;

        return target.name;
    }

    void HandleInput()
    {
        // 대화 중이 아닐 때만 E키 처리
        if (Input.GetKeyDown(KeyCode.E) && currentTarget != null)
        {
            Debug.Log($"상호작용 시작: {currentTarget.name}");

            // 상호작용 시작할 때 promptUI 숨기기
            if (promptUI != null)
            {
                promptUI.SetActive(false);
            }

            ItemManager item = currentTarget.GetComponent<ItemManager>();
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