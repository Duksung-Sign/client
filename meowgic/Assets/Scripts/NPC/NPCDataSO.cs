using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NPCData", menuName = "Game Data/NPC Data")]
public class NPCDataSO : ScriptableObject
{
    [Header("기본 정보")]
    public string npcName = "NPC";
    public Sprite npcPortrait;
    public Sprite npcSprite;

    [Header("대화 설정")]
    public DialogueDataSO[] dialogues;
    public int currentDialogueIndex = 0;

    /*[Header("퀘스트")]
    public QuestDataSO[] availableQuests;*/
}