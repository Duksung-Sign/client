using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;
using System.Collections;

public class CreditController : MonoBehaviour
{
    [Header("필수 설정")]
    [SerializeField] private GameObject scrollViewPrefab;
    [SerializeField] private float scrollDuration = 80f;
    [SerializeField] private Font creditFont;

    [Header("로고 이미지 (옵션)")]
    [SerializeField] private Sprite gameLogoSprite;
    [SerializeField] private Sprite teamLogoSprite;
    [SerializeField] private Sprite schoolLogoSprite;

    [Header("오디오 설정")]
    [SerializeField] private AudioClip creditBGM;
    [SerializeField] private float bgmVolume = 0.5f;

    private GameObject scrollView;
    private RectTransform viewport;
    private RectTransform content;
    private CanvasGroup canvasGroup;
    private Tween scrollTween;
    private AudioSource audioSource;
    private GameObject endingTextObject;  // 엔딩 텍스트 참조

    void Start()
    {
        SetupAudio();
        SetupScrollView();
        CreateAllCredits();
        StartCredits();
    }

    void SetupAudio()
    {
        if (AudioManager.Instance != null && creditBGM != null)
        {
            AudioManager.Instance.CrossfadeBGM(creditBGM); 
        }
    }

    void SetupScrollView()
    {
        Canvas canvas = FindObjectOfType<Canvas>();

        canvasGroup = canvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = 0;

        scrollView = new GameObject("ScrollView");
        scrollView.transform.SetParent(canvas.transform);
        RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = Vector2.zero;
        scrollRect.offsetMax = Vector2.zero;
        scrollRect.localScale = Vector3.one;

        ScrollRect sr = scrollView.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Unrestricted;

        GameObject viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollView.transform);
        viewport = viewportGO.AddComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.localScale = Vector3.one;

        Mask mask = viewportGO.AddComponent<Mask>();
        mask.enabled = false;
        Image viewportImage = viewportGO.AddComponent<Image>();
        viewportImage.enabled = false;

        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewport);
        content = contentGO.AddComponent<RectTransform>();
        content.anchorMin = new Vector2(0.5f, 1f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.localScale = Vector3.one;
        content.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 60f;
        vlg.padding = new RectOffset(0, 0, 400, 600);
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = viewport;
        sr.content = content;
    }

    void CreateAllCredits()
    {
        // 타이틀 섹션
        if (gameLogoSprite != null) CreateImage(gameLogoSprite, 1000, 400);
        CreateText("묘우직(Meowgic)", 90);
        CreateText("2025 졸업작품", 72);
        CreateText("덕성여자대학교 컴퓨터공학전공", 60);
        CreateSpacer(200);

        CreateText("WISCOM 전시회 2025", 66);
        CreateSpacer(300);

        // 팀 섹션
        if (teamLogoSprite != null) CreateImage(teamLogoSprite, 500, 500);
        CreateText("Team 묘우묘우", 84);
        CreateSpacer(250);

        // CORE DEVELOPMENT
        CreateSectionTitle("CORE DEVELOPMENT");
        CreateRole("Project Director / Lead Programmer", "남서하");
        CreateRole("AI & Server Development", "임지원\n\n전하경");
        CreateRole("Game Designer", "나예원");
        CreateRole("Narrative Director", "이재희");
        CreateRole("Art Director", "김경희");
        CreateSpacer(200);

        // PROGRAMMING
        CreateSectionTitle("PROGRAMMING");
        CreateSubtitle("Client Programming");
        CreateText("Game Systems & Core Mechanics", 42);
        CreateText("UI/UX Implementation", 42);
        CreateText("Dialogue System", 42);
        CreateText("Story Progress System", 42);
        CreateText("남서하", 54);
        CreateSpacer(80);

        CreateSubtitle("Server Programming");
        CreateText("Setting System", 42);
        CreateText("임지원", 54);
        CreateText("Python Flask Server", 42);
        CreateText("전하경", 54);
        CreateSpacer(80);

        CreateSubtitle("KSL AI Development");
        CreateText("임지원\n\n전하경", 54);
        CreateSpacer(200);

        // GAME DESIGN & WRITING
        CreateSectionTitle("GAME DESIGN & WRITING");
        CreateRole("Game Design", "나예원");
        CreateRole("Story & Dialogue", "이재희");
        CreateSpacer(200);

        // ART & VISUAL DESIGN
        CreateSectionTitle("ART & VISUAL DESIGN");
        CreateRole("Art Direction", "김경희");
        CreateSubtitle("Visual Assets");
        CreateText("묘어 고서 일러스트레이션", 42);
        CreateText("Team Logo Design", 42);
        CreateText("Game Logo Design", 42);
        CreateText("김주희", 54);
        CreateSpacer(200);

        // QA
        CreateSectionTitle("QUALITY ASSURANCE");
        CreateSubtitle("Lead Testers");
        CreateText("나윤주", 54);
        CreateText("이주영", 54);
        CreateText("이슬", 54);
        CreateSpacer(250);

        // SPECIAL THANKS
        CreateSectionTitle("SPECIAL THANKS");
        CreateRole("지도교수님", "최승훈 교수님");
        CreateRole("Special Support", "최강타 교수님");
        CreateText("모든 플레이테스터분들께 감사드립니다", 48);
        CreateSpacer(250);

        // DEVELOPMENT
        CreateSectionTitle("DEVELOPMENT");
        CreateRole("Development Period", "2025.05 - 2025.10");
        CreateRole("Engine", "Unity 2022.3 LTS");
        CreateRole("Genre", "수어 학습 RPG");
        CreateRole("Platform", "PC (Windows & Mac)");
        CreateSpacer(300);

        // COPYRIGHT
        CreateText("© 2025 Team 묘우묘우", 54);
        CreateText("Duksung Women's University", 48);
        CreateText("Department of Computer Engineering", 48);
        CreateText("Educational Project", 42);
        CreateSpacer(500);

        // ENDING - 중요! 이 텍스트의 참조를 저장
        endingTextObject = CreateText("플레이해 주셔서\n\n감사합니다!", 96);

        if (schoolLogoSprite != null) CreateImage(schoolLogoSprite, 400, 400);

        // 엔딩 이후 여백은 적게 (화면 중앙에 오도록)
        CreateSpacer(Screen.height / 2);
    }

    void CreateSectionTitle(string text)
    {
        CreateText(text, 72);
        CreateSpacer(60);
    }

    void CreateSubtitle(string text)
    {
        CreateText(text, 56);
        CreateSpacer(40);
    }

    void CreateRole(string role, string name)
    {
        CreateText(role, 48);
        CreateText(name, 60);
        CreateSpacer(50);
    }

    GameObject CreateText(string textContent, int fontSize)
    {
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(content);
        textObj.transform.localScale = Vector3.one;

        Text text = textObj.AddComponent<Text>();
        text.text = textContent;
        text.font = creditFont ? creditFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1200, fontSize * 1.5f * (textContent.Split('\n').Length));

        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.9f);
        shadow.effectDistance = new Vector2(4, -4);

        return textObj;
    }

    void CreateImage(Sprite sprite, float width, float height)
    {
        GameObject imgObj = new GameObject("Image");
        imgObj.transform.SetParent(content);
        imgObj.transform.localScale = Vector3.one;

        Image img = imgObj.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;

        RectTransform rect = imgObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
    }

    void CreateSpacer(float height)
    {
        GameObject spacer = new GameObject("Spacer");
        spacer.transform.SetParent(content);
        spacer.transform.localScale = Vector3.one;

        RectTransform rect = spacer.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(10, height);
    }

    void StartCredits()
    {
        StartCoroutine(StartCreditsCoroutine());
    }

    IEnumerator StartCreditsCoroutine()
    {
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        yield return new WaitForEndOfFrame();

        content.anchoredPosition = new Vector2(0, -Screen.height);

        canvasGroup.DOFade(1f, 2f).OnComplete(() =>
        {
            ScrollToEnding();
        });
    }

    void ScrollToEnding()
    {
        StartCoroutine(WaitForLayoutAndScroll());
    }

    IEnumerator WaitForLayoutAndScroll()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        RectTransform endingRect = endingTextObject.GetComponent<RectTransform>();
        float endingTextY = Mathf.Abs(endingRect.anchoredPosition.y);
        float targetPosition = endingTextY - (Screen.height / 2f);

        scrollTween = content
            .DOAnchorPosY(targetPosition, scrollDuration)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                StartCoroutine(EndingSequence());
            });
    }

    IEnumerator EndingSequence()
    {
        yield return new WaitForSeconds(3f);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeOutBGM(2f);
        }

        canvasGroup.DOFade(0f, 2f).OnComplete(() =>
        {
            SceneManager.LoadScene("Initializer");
        });
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0))
        {
            Skip();
        }
    }

    void Skip()
    {
        StopAllCoroutines();

        if (scrollTween != null)
        {
            scrollTween.Kill();
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.FadeOutBGM(0.3f);
        }

        canvasGroup.DOKill();
        canvasGroup.DOFade(0f, 0.5f)
            .OnComplete(() => SceneManager.LoadScene("Initializer"));
    }

    void OnDestroy()
    {
        scrollTween?.Kill();
    }
}