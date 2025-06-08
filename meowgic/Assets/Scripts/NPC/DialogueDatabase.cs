using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    [Header("대사 내용")]
    [TextArea(2, 4)]
    public string text;           // 대사 내용

    [Header("화자 설정")]
    [Tooltip("체크하면 플레이어(미오) 대사, 체크 안하면 NPC 대사")]
    public bool isPlayer = false; // 플레이어가 말하는지 여부 (false = NPC)

    [Header("고급 설정 (선택사항)")]
    [Tooltip("비워두면 자동: 플레이어='미오', NPC=DialogueSet 이름 사용")]
    public string customSpeakerName = ""; // 커스텀 화자 이름 (제3자 등)
}

[System.Serializable]
public class DialogueSet
{
    [Header("대화 정보")]
    public int dialogueID;                    // 대화 ID
    public string npcName;                    // NPC 이름

    [Header("대화 내용")]
    public List<DialogueLine> dialogueLines; // 대화 라인들

    [Header("설정")]
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
        if (dialogueSet != null)
            return dialogueSet.dialogueLines.Count;
        return 0;
    }

    // 대화 완료 여부 확인
    public bool IsDialogueFinished(int dialogueID, int currentIndex)
    {
        return currentIndex >= GetDialogueLineCount(dialogueID);
    }

    // DialogueManager에서 사용할 메서드들 추가
    public string GetSpeakerName(int dialogueID, int lineIndex)
    {
        DialogueLine line = GetDialogueLine(dialogueID, lineIndex);
        if (line != null)
        {
            // 1순위: customSpeakerName이 설정되어 있으면 그것 사용
            if (!string.IsNullOrEmpty(line.customSpeakerName))
            {
                return line.customSpeakerName;
            }

            // 2순위: isPlayer에 따라 자동 결정
            if (line.isPlayer)
            {
                return "미오"; // 플레이어 이름 고정
            }
            else
            {
                // NPC일 경우 DialogueSet의 npcName 사용
                DialogueSet dialogueSet = GetDialogueSet(dialogueID);
                return dialogueSet != null ? dialogueSet.npcName : "NPC";
            }
        }
        return "";
    }

    public bool IsPlayerSpeaking(int dialogueID, int lineIndex)
    {
        DialogueLine line = GetDialogueLine(dialogueID, lineIndex);
        if (line != null)
            return line.isPlayer;
        return false;
    }

    public string GetDialogueText(int dialogueID, int lineIndex)
    {
        DialogueLine line = GetDialogueLine(dialogueID, lineIndex);
        if (line != null)
            return line.text;
        return "";
    }

    // NPC 이름 가져오기 (DialogueSet에서)
    public string GetNPCName(int dialogueID)
    {
        DialogueSet dialogueSet = GetDialogueSet(dialogueID);
        if (dialogueSet != null)
            return dialogueSet.npcName;
        return "";
    }

    // 대화 반복 가능 여부 확인
    public bool IsRepeatable(int dialogueID)
    {
        DialogueSet dialogueSet = GetDialogueSet(dialogueID);
        if (dialogueSet != null)
            return dialogueSet.isRepeatable;
        return true;
    }
}