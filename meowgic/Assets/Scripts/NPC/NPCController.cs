using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCController : MonoBehaviour
{
    [Header("NPC Info")]
    public string npcName;          // NPC 이름
    public int dialogueID;          // 대화 ID

    [Header("Visual")]
    public Sprite npcPortrait;      // NPC 초상화 (선택사항)

    public virtual void Interact()
    {
        Debug.Log($"{npcName}와 대화 시작");

        // 대화 시스템 호출
        DialogueManager.Instance.StartDialogue(dialogueID);
    }
}

