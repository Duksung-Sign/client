using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    [Header("NPC 데이터")]
    public NPCDataSO npcData;

    [Header("기본 정보")]
    public string npcName;
    public Sprite npcPortrait;
    public Sprite npcSprite;

    void Awake()
    {
        if (npcData != null)
        {
            npcName = npcData.npcName;
            npcPortrait = npcData.npcPortrait;
            npcSprite = npcData.npcSprite;
        }
    }

    private int currentDialogueIndex = 0;

    public virtual void Interact()
    {
        if (npcData == null)
        {
            Debug.LogError("NPCData가 할당되지 않았습니다!");
            return;
        }

        Debug.Log($"{npcData.npcName}와 대화 시작");

        if (npcData.dialogues != null && npcData.dialogues.Length > 0)
        {
            int dialogueID = Mathf.Clamp(currentDialogueIndex, 0, npcData.dialogues.Length - 1);

            DialogueManager.Instance.StartDialogue(npcData, dialogueID);

            if (currentDialogueIndex < npcData.dialogues.Length - 1)
            {
                currentDialogueIndex++;
            }
        }
    }
}