using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using DG.Tweening;

public class FadeManager : MonoBehaviour
{
    public static FadeManager Instance;

    [Header("Fade UI 프리팹")]
    [SerializeField] private GameObject fadeUIPrefab;
    [SerializeField] private float defaultFadeDuration = 1.0f;

    private CanvasGroup fadeCanvasGroup;
    private bool isFading = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (fadeUIPrefab != null)
            {
                GameObject fadeUIObject = Instantiate(fadeUIPrefab);
                fadeCanvasGroup = fadeUIObject.GetComponent<CanvasGroup>();
                DontDestroyOnLoad(fadeUIObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadSceneWithFade(string sceneName, Action onComplete = null)
    {
        if (isFading) return;
        StartCoroutine(FadeAndLoadScene(sceneName, onComplete));
    }

    private IEnumerator FadeAndLoadScene(string sceneName, Action onComplete)
    {
        isFading = true;

        yield return FadeOut(defaultFadeDuration);

        if (GameDataManager.Instance != null &&
            !sceneName.Equals("MainMenu") &&
            !sceneName.Equals("Initializer"))
        {
            GameDataManager.Instance.SetLastSceneName(sceneName);
        }

        yield return SceneManager.LoadSceneAsync(sceneName);

        yield return null;

        onComplete?.Invoke();

        yield return FadeIn(defaultFadeDuration);

        isFading = false;
    }

    public IEnumerator FadeIn(float duration)
    {
        if (fadeCanvasGroup == null) yield break;

        fadeCanvasGroup.blocksRaycasts = false;
        yield return fadeCanvasGroup.DOFade(0f, duration)
            .SetEase(Ease.InOutQuad)
            .SetUpdate(true)
            .WaitForCompletion();
    }

    public IEnumerator FadeOut(float duration)
    {
        if (fadeCanvasGroup == null) yield break;

        fadeCanvasGroup.blocksRaycasts = true;
        yield return fadeCanvasGroup.DOFade(1f, duration)
            .SetEase(Ease.InOutQuad)
            .SetUpdate(true)
            .WaitForCompletion();
    }
}