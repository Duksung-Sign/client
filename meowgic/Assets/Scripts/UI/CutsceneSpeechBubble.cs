using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CutsceneSpeechBubble : MonoBehaviour
{
    [Header("UI 요소")]
    public Image bubbleBackground;
    public TextMeshProUGUI dialogueText;
    public CanvasGroup canvasGroup;

    private Transform targetCharacter;
    private Vector3 offset;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        canvasGroup.alpha = 0f;
    }

    void LateUpdate()
    {
        if (targetCharacter != null && mainCamera != null)
        {
            Vector3 worldPos = targetCharacter.position + offset;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            transform.position = screenPos;
        }
    }

    public void Initialize(Transform character, Vector3 bubbleOffset, Color bubbleColor)
    {
        targetCharacter = character;
        offset = bubbleOffset;
        bubbleBackground.color = bubbleColor;
    }

    public IEnumerator ShowMessage(string message, float typeSpeed, float displayTime)
    {
        gameObject.SetActive(true);

        // 페이드 인
        yield return StartCoroutine(FadeIn());

        // 타자기 효과
        yield return StartCoroutine(TypewriterEffect(message, typeSpeed));

        // 대기
        yield return new WaitForSeconds(displayTime);

        // 페이드 아웃
        yield return StartCoroutine(FadeOut());

        gameObject.SetActive(false);
    }

    IEnumerator FadeIn()
    {
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = elapsed / duration;
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    IEnumerator TypewriterEffect(string message, float speed)
    {
        dialogueText.text = "";

        for (int i = 0; i <= message.Length; i++)
        {
            dialogueText.text = message.Substring(0, i);
            yield return new WaitForSeconds(speed);
        }
    }

    IEnumerator FadeOut()
    {
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - (elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }

    public void HideMessage()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }
}