using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    [TextArea(2, 4)]
    public string text;           // 대사 내용
    public string speakerName;    // 화자 이름 (NPC명 또는 "플레이어")
    public Sprite characterSprite; // 캐릭터 이미지 (선택사항)
}

[System.Serializable]
public class DialogueSet
{
    public int dialogueID;                    // 대화 ID
    public string npcName;                    // NPC 이름
    public List<DialogueLine> dialogueLines; // 대화 라인들
    public bool isRepeatable = true;          // 반복 가능한 대화인지
}

[CreateAssetMenu(fileName = "DialogueDatabase", menuName = "Game Data/Dialogue Database")]
public class DialogueDatabase : ScriptableObject
{
    [Header("NPC 대화 데이터")]
    public List<DialogueSet> dialogueSets = new List<DialogueSet>();

    // ID로 대화 세트 찾기
    public DialogueSet GetDialogueSet(int dialogueID)
    {
        return dialogueSets.Find(set => set.dialogueID == dialogueID);
    }

    // 특정 대화의 특정 라인 가져오기
    public DialogueLine GetDialogueLine(int dialogueID, int lineIndex)
    {
        DialogueSet dialogueSet = GetDialogueSet(dialogueID);
        if (dialogueSet != null && lineIndex < dialogueSet.dialogueLines.Count)
        {
            return dialogueSet.dialogueLines[lineIndex];
        }
        return null;
    }

    // 대화 라인 수 가져오기
    public int GetDialogueLineCount(int dialogueID)
    {
        DialogueSet dialogueSet = GetDialogueSet(dialogueID);
        return dialogueSet?.dialogueLines.Count ?? 0;
    }

    // 대화 완료 여부 확인
    public bool IsDialogueFinished(int dialogueID, int currentIndex)
    {
        return currentIndex >= GetDialogueLineCount(dialogueID);
    }
}