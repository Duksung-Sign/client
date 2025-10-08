using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillBookUIRefs : MonoBehaviour
{
    public CanvasGroup pageGroup;

    [Header("Skill Slots - 왼쪽 페이지")]
    public SkillBookController.SkillSlot leftSlot1;
    public SkillBookController.SkillSlot leftSlot2;

    [Header("Skill Slots - 오른쪽 페이지")]
    public SkillBookController.SkillSlot rightSlot1;
    public SkillBookController.SkillSlot rightSlot2;

    [Header("Navigation")]
    public Button leftArrowButton;
    public Button rightArrowButton;
    public GameObject leftArrowObject;
    public GameObject rightArrowObject;

    [Header("Special Pages")]
    public GameObject doodlePage;
}