using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using DG.Tweening;

public class SceneLoader : MonoBehaviour
{
    [Header("스플래시 UI 요소")]
    [SerializeField] private Image companyLogoImage;
    [SerializeField] private Image autosaveIconImage;

    [Header("애니메이션 시간 설정")]
    [SerializeField] private float fadeInDuration = 1.5f;
    [SerializeField] private float holdDuration = 2.0f;
    [SerializeField] private float fadeOutDuration = 1.5f;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        StartCoroutine(SplashScreenSequence());
    }

    private IEnumerator SplashScreenSequence()
    {
        if (companyLogoImage != null)
        {
            companyLogoImage.color = new Color(1, 1, 1, 0);
            companyLogoImage.transform.localScale = Vector3.one * 0.9f;

            companyLogoImage.DOFade(1f, fadeInDuration);
            yield return companyLogoImage.transform.DOScale(1f, fadeInDuration)
                .SetEase(Ease.InOutQuad) 
                .WaitForCompletion();

            yield return new WaitForSeconds(holdDuration);

            yield return companyLogoImage.DOFade(0f, fadeOutDuration).WaitForCompletion();
        }

        yield return new WaitForSeconds(0.5f);

        if (autosaveIconImage != null)
        {
            autosaveIconImage.color = new Color(1, 1, 1, 0);
            autosaveIconImage.transform.localScale = Vector3.one * 0.9f;

            autosaveIconImage.DOFade(1f, fadeInDuration);
            yield return autosaveIconImage.transform.DOScale(1f, fadeInDuration)
                .SetEase(Ease.InOutQuad) 
                .WaitForCompletion();

            yield return new WaitForSeconds(holdDuration);
            yield return autosaveIconImage.DOFade(0f, fadeOutDuration).WaitForCompletion();
        }

        FadeManager.Instance.LoadSceneWithFade("MainMenu");

        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }
}