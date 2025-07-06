using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ConditionalDialogue
{
    [Header("조건 설정")]
    public string dialogueSetID;
    public string description;

    [Header("발동 조건")]
    public DialogueConditionType conditionType;
    public string targetValue;
    public int requiredValue = 1;

    public bool CheckConditions()
    {
        switch (conditionType)
        {
            case DialogueConditionType.None:
                return true;
            case DialogueConditionType.StoryReached:
                return StoryProgressManager.Instance != null &&
                       StoryProgressManager.Instance.GetCurrentStoryID() == targetValue;
            case DialogueConditionType.QuestCompleted:
                return PlayerPrefs.GetInt($"Quest_{targetValue}_Completed", 0) == 1;
            case DialogueConditionType.ItemObtained:
                return PlayerPrefs.GetInt($"Item_{targetValue}_Count", 0) >= requiredValue;
            case DialogueConditionType.DialogueFinished:
                return PlayerPrefs.GetInt($"Dialogue_{targetValue}_Finished", 0) == 1;
            default:
                return false;
        }
    }
}

public enum DialogueConditionType
{
    None,
    StoryReached,
    QuestCompleted,
    ItemObtained,
    DialogueFinished
}

public class NPCController : MonoBehaviour
{
    [Header("NPC 기본 정보")]
    public string npcName = "NPC";
    public Sprite npcPortrait;
    public Sprite npcSprite;

    [Header("기본 대화")]
    [Tooltip("기본적으로 사용할 CSV DialogueSet ID")]
    public string defaultDialogueSetID = "";

    [Header("조건별 대화 시스템")]
    [Tooltip("특정 조건에서 다른 대화 실행")]
    public ConditionalDialogue[] conditionalDialogues;

    [Header("스토리 연동")]
    [Tooltip("대화 완료 후 스토리 진행 여부")]
    public bool advanceStoryAfterDialogue = false;
    [Tooltip("대화 완료 시 진행할 특정 스토리 ID (비어두면 자동 진행)")]
    public string specificNextStoryID = "";

    [Header("반복 설정")]
    [Tooltip("대화를 반복할 수 있는지")]
    public bool allowRepeatDialogue = true;

    [Header("Debug")]
    public bool debugMode = false;

    void Awake()
    {
        ValidateSettings();
    }

    void ValidateSettings()
    {
        // 기본 DialogueSetID가 없으면 NPC 이름으로 생성
        if (string.IsNullOrEmpty(defaultDialogueSetID))
        {
            defaultDialogueSetID = $"npc_{npcName.ToLower().Replace(" ", "_")}";
            if (debugMode)
                Debug.Log($"[NPCController] 자동 생성된 DialogueSetID: {defaultDialogueSetID}");
        }

        if (debugMode)
        {
            Debug.Log($"[NPCController] {npcName} 초기화 완료");
            Debug.Log($"- Default DialogueSetID: {defaultDialogueSetID}");
            Debug.Log($"- Conditional Dialogues: {conditionalDialogues?.Length ?? 0}개");
            Debug.Log($"- Story Integration: {advanceStoryAfterDialogue}");
        }
    }

    public virtual void Interact()
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogError($"[NPCController] {npcName}: DialogueManager.Instance가 null입니다!");
            return;
        }

        // 사용할 대화 선택
        string selectedDialogueSetID = SelectBestDialogue();

        if (string.IsNullOrEmpty(selectedDialogueSetID))
        {
            Debug.LogError($"[NPCController] {npcName}: 사용할 수 있는 대화가 없습니다!");
            return;
        }

        StartDialogue(selectedDialogueSetID);
    }

    string SelectBestDialogue()
    {
        // 1. 조건부 대화 우선 체크 (배열 순서대로)
        if (conditionalDialogues != null)
        {
            foreach (var conditionalDialogue in conditionalDialogues)
            {
                if (conditionalDialogue.CheckConditions())
                {
                    // 이미 완료된 대화인지 체크 (반복 불가인 경우)
                    if (!allowRepeatDialogue)
                    {
                        bool isFinished = PlayerPrefs.GetInt($"Dialogue_{conditionalDialogue.dialogueSetID}_Finished", 0) == 1;
                        if (isFinished)
                        {
                            if (debugMode)
                                Debug.Log($"[NPCController] 조건부 대화 이미 완료됨: {conditionalDialogue.dialogueSetID}");
                            continue;
                        }
                    }

                    if (debugMode)
                        Debug.Log($"[NPCController] 조건부 대화 선택: {conditionalDialogue.dialogueSetID} - {conditionalDialogue.description}");
                    return conditionalDialogue.dialogueSetID;
                }
            }
        }

        // 2. 기본 대화
        if (!string.IsNullOrEmpty(defaultDialogueSetID))
        {
            // 반복 불가인 경우 완료 여부 체크
            if (!allowRepeatDialogue)
            {
                bool isFinished = PlayerPrefs.GetInt($"Dialogue_{defaultDialogueSetID}_Finished", 0) == 1;
                if (isFinished)
                {
                    if (debugMode)
                        Debug.Log($"[NPCController] 기본 대화 이미 완료됨: {defaultDialogueSetID}");
                    return null;
                }
            }

            if (debugMode)
                Debug.Log($"[NPCController] 기본 대화 선택: {defaultDialogueSetID}");
            return defaultDialogueSetID;
        }

        return null;
    }

    void StartDialogue(string dialogueSetID)
    {
        if (debugMode)
            Debug.Log($"[NPCController] {npcName} 대화 시작: {dialogueSetID}");

        // 초상화 설정
        SetupPortraits();

        // 대화 시작
        DialogueManager.Instance.StartDialogue(dialogueSetID);

        // 대화 완료 후 처리
        if (advanceStoryAfterDialogue)
        {
            StartCoroutine(WaitForDialogueAndAdvanceStory(dialogueSetID));
        }
    }

    void SetupPortraits()
    {
        if (DialogueManager.Instance == null) return;

        // 플레이어 초상화 가져오기
        Sprite playerPortrait = GetPlayerPortrait();

        // DialogueManager에 초상화 설정
        DialogueManager.Instance.SetCharacterPortraits(playerPortrait, npcPortrait);

        if (debugMode)
        {
            Debug.Log($"[NPCController] 초상화 설정 완료");
            Debug.Log($"- Player: {(playerPortrait != null ? playerPortrait.name : "NULL")}");
            Debug.Log($"- NPC: {(npcPortrait != null ? npcPortrait.name : "NULL")}");
        }
    }

    Sprite GetPlayerPortrait()
    {
        var playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null && playerStats.playerPortrait != null)
        {
            return playerStats.playerPortrait;
        }

        if (debugMode)
            Debug.LogWarning("[NPCController] 플레이어 초상화를 찾을 수 없습니다!");

        return null;
    }

    IEnumerator WaitForDialogueAndAdvanceStory(string dialogueSetID)
    {
        // 대화 완료까지 대기
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        if (debugMode)
            Debug.Log($"[NPCController] 대화 완료, 스토리 진행 시도: {npcName}");

        // 스토리 진행
        if (StoryProgressManager.Instance != null)
        {
            if (!string.IsNullOrEmpty(specificNextStoryID))
            {
                // 특정 스토리로 진행
                StoryProgressManager.Instance.TryAdvanceStory(specificNextStoryID);
            }
            else
            {
                // 자동 진행
                StoryProgressManager.Instance.TryAdvanceToNextStory();
            }
        }
    }

    // === 공개 메서드들 ===
    public string GetCurrentDialogueSetID()
    {
        return SelectBestDialogue();
    }

    public bool HasAvailableDialogue()
    {
        return !string.IsNullOrEmpty(SelectBestDialogue());
    }

    public void SetNPCInfo(string name, Sprite portrait, Sprite sprite)
    {
        npcName = name;
        npcPortrait = portrait;
        npcSprite = sprite;

        if (debugMode)
            Debug.Log($"[NPCController] NPC 정보 업데이트: {name}");
    }

    // === 디버그 메서드들 ===
    [ContextMenu("Test Interact")]
    public void TestInteract()
    {
        Interact();
    }

    [ContextMenu("Show Available Dialogue")]
    public void ShowAvailableDialogue()
    {
        string dialogueSetID = SelectBestDialogue();
        Debug.Log($"[NPCController] {npcName} 사용 가능한 대화: {dialogueSetID ?? "없음"}");

        if (conditionalDialogues != null)
        {
            Debug.Log("=== 조건부 대화 상태 ===");
            foreach (var conditionalDialogue in conditionalDialogues)
            {
                bool meets = conditionalDialogue.CheckConditions();
                Debug.Log($"- {conditionalDialogue.dialogueSetID}: {meets} ({conditionalDialogue.description})");
            }
        }
    }

    [ContextMenu("Reset All Dialogues")]
    public void ResetAllDialogues()
    {
        // 기본 대화 리셋
        PlayerPrefs.DeleteKey($"Dialogue_{defaultDialogueSetID}_Finished");

        // 조건부 대화들 리셋
        if (conditionalDialogues != null)
        {
            foreach (var conditionalDialogue in conditionalDialogues)
            {
                PlayerPrefs.DeleteKey($"Dialogue_{conditionalDialogue.dialogueSetID}_Finished");
            }
        }

        PlayerPrefs.Save();
        Debug.Log($"[NPCController] {npcName} 모든 대화 리셋 완료");
    }
}