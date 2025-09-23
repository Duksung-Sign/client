using UnityEngine;
using UnityEngine.Playables;

public class DialogueBehaviour : PlayableBehaviour
{
    [Header("Content")]
    public string dialogueText;
    public BubbleDirection direction;

    [Header("Typing")]
    public float typingSpeed = 0.05f;

    [Header("Position")]
    public bool useCustomPosition;
    public Vector2 bubblePosition;

    [Header("Tail Position")] 
    public bool useCustomTailPosition;
    public Vector2 tailPosition;

    private bool isClipPlayed = false;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        EmergencyDialogueManager dialogueManager = playerData as EmergencyDialogueManager;

        if (dialogueManager != null && !isClipPlayed && info.weight > 0)
        {
            PlayableDirector director = playable.GetGraph().GetResolver() as PlayableDirector;
            Vector2 position = useCustomPosition ? bubblePosition : Vector2.zero;
            Vector2 tailPos = useCustomTailPosition ? tailPosition : Vector2.zero;

            dialogueManager.ShowDialogue(dialogueText, direction, typingSpeed, false, director, position, tailPos, useCustomTailPosition);
            isClipPlayed = true;
        }

        if (isClipPlayed && info.weight == 0 && dialogueManager != null)
        {
            dialogueManager.HideDialogue();
        }
    }
}