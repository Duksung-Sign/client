using UnityEngine;
using TMPro;
using UnityEngine.Playables;

public class EmergencyDialogueManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject speechBubbleObject;

    private DynamicSpeechBubble speechBubble;

    void Start()
    {
        if (speechBubbleObject != null)
        {
            speechBubbleObject.SetActive(false);

            speechBubble = speechBubbleObject.GetComponent<DynamicSpeechBubble>();
            if (speechBubble == null)
            {
                speechBubble = speechBubbleObject.GetComponentInChildren<DynamicSpeechBubble>();
            }
        }
    }

    public void ShowDialogue(string dialogue, BubbleDirection direction, float speed, bool shouldWaitForInput, PlayableDirector director, Vector2 position, Vector2 tailPosition, bool useCustomTail)
    {
        speechBubbleObject.SetActive(true);

        if (position != Vector2.zero)
        {
            RectTransform rectTransform = speechBubbleObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = position;
            }
        }

        Transform backgroundTransform = speechBubbleObject.transform.Find("Background");
        if (backgroundTransform != null)
        {
            Vector3 bgScale = backgroundTransform.localScale;
            if (direction == BubbleDirection.Left)
            {
                backgroundTransform.localScale = new Vector3(Mathf.Abs(bgScale.x), bgScale.y, bgScale.z);
            }
            else
            {
                backgroundTransform.localScale = new Vector3(-Mathf.Abs(bgScale.x), bgScale.y, bgScale.z);
            }
        }

        Transform tailTransform = speechBubbleObject.transform.Find("Tail");
        if (tailTransform != null)
        {
            Vector3 tailScale = tailTransform.localScale;

            if (direction == BubbleDirection.Left)
            {
                tailTransform.localScale = new Vector3(Mathf.Abs(tailScale.x), tailScale.y, tailScale.z);
            }
            else
            {
                tailTransform.localScale = new Vector3(-Mathf.Abs(tailScale.x), tailScale.y, tailScale.z);
            }

            if (useCustomTail)
            {
                RectTransform tailRect = tailTransform.GetComponent<RectTransform>();
                if (tailRect != null)
                {
                    tailRect.anchoredPosition = tailPosition;
                }
            }
        }

        speechBubbleObject.transform.localScale = Vector3.one;

        if (speechBubble != null)
        {
            speechBubble.typingSpeed = speed;
            speechBubble.SetText(dialogue);
        }
    }

    public void HideDialogue()
    {
        speechBubbleObject.SetActive(false);

        if (speechBubble != null)
        {
            speechBubble.StopTyping();
        }
    }
}