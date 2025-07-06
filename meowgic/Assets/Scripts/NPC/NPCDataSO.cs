using UnityEngine;

/// <summary>
/// CSV 기반 시스템에서 사용하는 간소화된 NPC 데이터
/// 대화는 CSV에서 관리하므로 기본 정보만 포함
/// </summary>
[CreateAssetMenu(fileName = "NPCData", menuName = "Game Data/NPC Data")]
public class NPCDataSO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("NPC의 고유 ID (CSV에서 참조용)")]
    public string npcID = "";

    [Tooltip("표시될 NPC 이름")]
    public string displayName = "NPC";

    [Tooltip("NPC 설명")]
    [TextArea(2, 3)]
    public string description = "";

    [Header("비주얼")]
    [Tooltip("대화창에서 사용할 초상화")]
    public Sprite npcPortrait;

    [Tooltip("게임 월드에서 사용할 스프라이트")]
    public Sprite npcSprite;

    [Header("CSV 연동")]
    [Tooltip("기본 대화 세트 ID (CSV DialogueSets 참조)")]
    public string defaultDialogueSetID = "";

    [Header("게임플레이")]
    [Tooltip("상호작용 가능한 NPC인지")]
    public bool isInteractable = true;

    [Tooltip("NPC 타입 분류")]
    public NPCType npcType = NPCType.Villager;

    [Header("위치 정보")]
    [Tooltip("이 NPC가 속한 지역/맵")]
    public string homeArea = "";

    [Tooltip("기본 스폰 위치 (선택사항)")]
    public Vector3 defaultSpawnPosition = Vector3.zero;

    // 실제 사용할 ID 반환 (비어있으면 파일명 사용)
    public string GetNPCID()
    {
        return string.IsNullOrEmpty(npcID) ? name : npcID;
    }

    // 실제 사용할 이름 반환
    public string GetDisplayName()
    {
        return string.IsNullOrEmpty(displayName) ? name : displayName;
    }

    // 기본 대화 세트 ID 반환 (비어있으면 자동 생성)
    public string GetDefaultDialogueSetID()
    {
        if (!string.IsNullOrEmpty(defaultDialogueSetID))
            return defaultDialogueSetID;

        string id = GetNPCID().ToLower().Replace(" ", "_");
        return $"npc_{id}";
    }

    void OnValidate()
    {
        // 에디터에서 자동으로 ID 생성
        if (string.IsNullOrEmpty(npcID))
        {
            npcID = name.ToLower().Replace(" ", "_");
        }

        // 기본 대화 세트 ID 자동 생성
        if (string.IsNullOrEmpty(defaultDialogueSetID))
        {
            defaultDialogueSetID = GetDefaultDialogueSetID();
        }
    }
}

[System.Serializable]
public enum NPCType
{
    Villager,       // 일반 마을사람
    QuestGiver,     // 퀘스트 제공자
    Guard,          // 경비원
    Child,          // 아이
    Elder,          // 어르신
    Blacksmith,     // 대장장이
    Innkeeper,      // 여관주인
    Farmer,         // 농부
    Priest,         // 성직자
    Mysterious      // 정체불명
}