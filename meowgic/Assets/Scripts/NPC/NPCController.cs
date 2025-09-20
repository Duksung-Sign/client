using UnityEngine;
using System.Linq;

[System.Serializable]
public class ConditionalDialogue
{
    public string dialogueSetID;
    public DialogueConditionType conditionType;
    public string targetValue;

    public bool CheckConditions()
    {
        switch (conditionType)
        {
            case DialogueConditionType.StoryReached:
                return GameDataManager.Instance.CurrentStoryID == targetValue;
            case DialogueConditionType.ItemObtained:
                return GameDataManager.Instance.HasItem(targetValue);
            case DialogueConditionType.DialogueFinished:
                return GameDataManager.Instance.HasFinishedDialogue(targetValue);
            default:
                return true;
        }
    }
}

public enum DialogueConditionType
{
    None,
    StoryReached,
    ItemObtained,
    DialogueFinished
}


public class NPCController : MonoBehaviour, IInteractable
{
    [Header("NPC 정보")]
    public string npcName = "NPC";
    public Sprite npcPortrait;

    [Header("대화 설정")]
    public string defaultDialogueSetID = "";
    public ConditionalDialogue[] conditionalDialogues;
    public bool allowRepeatDialogue = true;

    public void Interact()
    {
        string dialogueID = SelectBestDialogue();
        if (!string.IsNullOrEmpty(dialogueID))
        {
            DialogueManager.Instance.StartDialogue(dialogueID);
        }
        else
        {
            Debug.LogWarning($"[NPCController] {npcName}: 사용 가능한 대화가 없습니다.");
        }
    }

    public string GetInteractionPromptText()
    {
        return $"{npcName}에게 말 걸기";
    }

    private string SelectBestDialogue()
    {
        foreach (var cd in conditionalDialogues.OrderBy(c => c.conditionType != DialogueConditionType.None)) // None 조건을 나중에 체크
        {
            if (cd.CheckConditions())
            {
                if (!allowRepeatDialogue && GameDataManager.Instance.HasFinishedDialogue(cd.dialogueSetID))
                {
                    continue; 
                }
                return cd.dialogueSetID;
            }
        }

        if (!string.IsNullOrEmpty(defaultDialogueSetID))
        {
            if (!allowRepeatDialogue && GameDataManager.Instance.HasFinishedDialogue(defaultDialogueSetID))
            {
                return null; 
            }
            return defaultDialogueSetID;
        }

        return null;
    }

    public bool IsCurrentlyInteractable()
    {
        return true;
    }
}