using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ContinueIndicator : MonoBehaviour
{
    public Image continueIcon;
    public TextMeshProUGUI dialogueText;
    public float blinkSpeed = 1f;
    public Vector2 offsetFromText = new Vector2(10f, 0f);

    private RectTransform iconRect;
    private bool isBlinking = false;

    void Awake()
    {
        if (continueIcon != null)
            iconRect = continueIcon.GetComponent<RectTransform>();
        Hide();
    }

    public void Show()
    {
        if (continueIcon == null) return;

        UpdateIconPosition();
        continueIcon.gameObject.SetActive(true);
        StartCoroutine(DelayedPositionUpdate());

        if (!isBlinking)
            StartCoroutine(BlinkCoroutine());
    }

    IEnumerator DelayedPositionUpdate()
    {
        yield return null; 
        UpdateIconPosition();
    }

    public void Hide()
    {
        if (continueIcon != null)
            continueIcon.gameObject.SetActive(false);
        StopAllCoroutines();
        isBlinking = false;
    }

    void UpdateIconPosition()
    {
        if (dialogueText == null || iconRect == null) return;

        dialogueText.ForceMeshUpdate();
        var textInfo = dialogueText.textInfo;

        if (textInfo.characterCount == 0)
        {
            continueIcon.gameObject.SetActive(false);
            return;
        }

        int lastVisibleCharacterIndex = -1;

        for (int i = textInfo.characterCount - 1; i >= 0; i--)
        {
            if (textInfo.characterInfo[i].isVisible)
            {
                lastVisibleCharacterIndex = i;
                break; 
            }
        }

        if (lastVisibleCharacterIndex == -1)
        {
            continueIcon.gameObject.SetActive(false);
            return;
        }

        TMP_CharacterInfo lastCharInfo = textInfo.characterInfo[lastVisibleCharacterIndex];

        Vector2 iconPosition = lastCharInfo.topRight;

        iconPosition += offsetFromText;
        iconRect.anchoredPosition = iconPosition;
    }

    IEnumerator BlinkCoroutine()
    {
        isBlinking = true;

        while (isBlinking)
        {
            continueIcon.color = new Color(1f, 1f, 1f, 0.3f);
            yield return new WaitForSeconds(1f / blinkSpeed);
            continueIcon.color = new Color(1f, 1f, 1f, 1f);
            yield return new WaitForSeconds(1f / blinkSpeed);
        }
    }
}