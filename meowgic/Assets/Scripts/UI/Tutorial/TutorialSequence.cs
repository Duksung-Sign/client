using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TutorialStep
{
    [Tooltip("하이라이트할 UI 오브젝트에 붙어있는 UITag의 태그 이름")]
    public string TargetTag;
    [TextArea]
    public string InstructionText;
}

[CreateAssetMenu(fileName = "NewTutorialSequence", menuName = "Tutorial/Tutorial Sequence")]
public class TutorialSequence : ScriptableObject
{
    public List<TutorialStep> Steps;
}