using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening; // DOTween을 사용한 부드러운 깜빡임 예시

[RequireComponent(typeof(Image))]
public class ContinueIndicator : MonoBehaviour
{
    private Image indicatorImage;
    private Coroutine runningCoroutine; 

    void Awake()
    {
        indicatorImage = GetComponent<Image>();
        gameObject.SetActive(false); 
    }

    public void Show()
    {
        gameObject.SetActive(true);
        
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
        }
        runningCoroutine = StartCoroutine(BlinkCoroutine());
    }

    public void Hide()
    {
        if (runningCoroutine != null)
        {
            StopCoroutine(runningCoroutine);
            runningCoroutine = null; 
        }
        gameObject.SetActive(false);
    }

    private IEnumerator BlinkCoroutine()
    {
        while (true)
        {
            yield return indicatorImage.DOFade(0.2f, 0.5f).WaitForCompletion();
            yield return indicatorImage.DOFade(1f, 0.5f).WaitForCompletion();
        }
    }
}