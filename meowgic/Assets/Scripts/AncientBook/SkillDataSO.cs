using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "Game Data/Skill Data")]
public class SkillDataSO : ScriptableObject
{
    [Header("기본 정보")]
    public string skillID = "";
    public string skillName = "";
    [TextArea(3, 5)]
    public string skillDescription = "";

    [Header("비주얼")]
    public Sprite skillImage;

    [Header("해금 조건")]
    public bool isDefaultSkill = false; // 기본 스킬 여부
    public string requiredPageItemID = ""; // 필요한 페이지 아이템 ID

    public string GetSkillID()
    {
        return string.IsNullOrEmpty(skillID) ? name : skillID;
    }

    public string GetSkillName()
    {
        return string.IsNullOrEmpty(skillName) ? name : skillName;
    }
}
