using UnityEngine;
using UnityEngine.Playables;

public enum BubbleDirection
{
    Left,
    Right
}

public class DialogueClip : PlayableAsset
{
    [Header("Dialogue Content")]
    [TextArea(3, 10)]
    public string dialogueText;

    [Header("Bubble Settings")]
    public BubbleDirection direction;
    public bool waitForInput = true;

    [Header("Timing Settings")]
    [Range(0.01f, 0.2f)]
    public float manualTypingSpeed = 0.05f;

    [Header("Position Settings")]
    public bool useCustomPosition = false;
    public Vector2 bubblePosition = Vector2.zero;

    [Header("Tail Settings")]
    public bool useCustomTailPosition = false;
    public Vector2 tailPosition = Vector2.zero;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<DialogueBehaviour>.Create(graph);
        DialogueBehaviour dialogueBehaviour = playable.GetBehaviour();

        dialogueBehaviour.dialogueText = dialogueText;
        dialogueBehaviour.direction = direction;
        dialogueBehaviour.useCustomPosition = useCustomPosition;
        dialogueBehaviour.bubblePosition = bubblePosition;
        dialogueBehaviour.useCustomTailPosition = useCustomTailPosition; 
        dialogueBehaviour.tailPosition = tailPosition; 

        return playable;
    }
}