using TMPro;
using UnityEngine;
using System.Collections;

public class DynamicSpeechBubble : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI messageText;
    public RectTransform background;

    [Header("Settings")]
    public float fixedHeight = 60f;
    public float padding = 20f; 

    [Header("Size Limits")]
    public float minWidth = 100f;
    public float maxWidth = 200f;

    [Header("Typing Effect")]
    public float typingSpeed = 0.05f;
    public bool skipTypingOnSameText = true;

    private string lastText = "";
    private Coroutine typingCoroutine;

    void Start()
    {
        if (messageText != null)
        {
            messageText.enableWordWrapping = false;
            messageText.autoSizeTextContainer = false;
            messageText.overflowMode = TextOverflowModes.Overflow;
        }
    }

    public void SetText(string text)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        PreCalculateSize(text);
        typingCoroutine = StartCoroutine(TypeText(text));
    }

    public void SetTextInstant(string text)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        messageText.SetText(text);
        lastText = text;
        UpdateSize();
    }

    private void PreCalculateSize(string fullText)
    {
        messageText.SetText(fullText);
        messageText.ForceMeshUpdate();

        float textWidth = messageText.preferredWidth;
        float newWidth = Mathf.Clamp(textWidth + padding, minWidth, maxWidth);

        background.sizeDelta = new Vector2(newWidth, fixedHeight);

        messageText.SetText("");
    }

    private IEnumerator TypeText(string fullText)
    {
        messageText.SetText("");

        for (int i = 0; i <= fullText.Length; i++)
        {
            messageText.SetText(fullText.Substring(0, i));
            yield return new WaitForSeconds(typingSpeed);
        }

        typingCoroutine = null;
    }

    private void UpdateSize()
    {
        messageText.ForceMeshUpdate();

        float textWidth = messageText.preferredWidth;
        float newWidth = Mathf.Clamp(textWidth + padding, minWidth, maxWidth);

        background.sizeDelta = new Vector2(newWidth, fixedHeight);
    }

    public void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
    }

    public bool IsTyping()
    {
        return typingCoroutine != null;
    }
}