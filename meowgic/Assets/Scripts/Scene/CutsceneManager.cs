using UnityEngine;
using System.Collections;
using DG.Tweening;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance;

    private bool isPlayingCutscene = false;
    public bool IsPlayingCutscene() => isPlayingCutscene;

    [Header("튜나 설정")]
    public GameObject tunaPrefab;

    [Header("볼륨 프로필")]
    public VolumeProfile libraryAfterProfile;

    [Header("일러스트 슬라이드쇼 설정")]
    [Tooltip("일러스트 이미지들이 있는 Resources 폴더 경로 (예: Illustrations/Library)")]
    public string illustrationFolderPath = "Illustrations/Library";
    [Tooltip("일러스트 페이드 인/아웃 시간")]
    public float illustrationFadeDuration = 1f;
    [Tooltip("슬라이드쇼 BGM")]
    public AudioClip slideshowBGM;

    [Header("시네마틱 효과")]
    private GameObject topBar, bottomBar;
    private bool cinematicBarsActive = false;

    private GameObject tunaInstance;
    private GameObject illustrationCanvas;
    private bool waitingForInput = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameEventManager.Instance.OnGameStateChanged += HandleGameStateChange;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // 일러스트 슬라이드쇼 중 입력 감지
        if (waitingForInput)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                waitingForInput = false;
            }
        }
    }

    public void PlayCutscene(string cutsceneID)
    {
        if (isPlayingCutscene) return;
        StartCoroutine(CutsceneCoroutine(cutsceneID));
    }

    IEnumerator CutsceneCoroutine(string cutsceneID)
    {
        isPlayingCutscene = true;
        GameManager.Instance.ChangeState(GameState.Cutscene);

        if (cutsceneID == "library_cutscene")
        {
            yield return StartCoroutine(LibraryCutsceneWithIllustrations());
        }
        else if (cutsceneID == "cheddar_appears")
        {
            yield return StartCoroutine(CheddarAppearsCutscene());
        }
        else if (cutsceneID == "outside_village" || cutsceneID == "outside_cutscene")
        {
            yield return StartCoroutine(OutsideShockCutscene());
        }
        else if (cutsceneID == "cheddar_attack")
        {
            yield return StartCoroutine(CheddarAttackCutscene());
        }
        else if (cutsceneID == "after_battle")
        {
            yield return StartCoroutine(AfterBattleCutscene());
        }

        GameManager.Instance.ChangeState(GameState.Field);
        isPlayingCutscene = false;
    }

    /// <summary>
    /// 라이브러리 컷씬 - 일러스트 7장을 슬라이드쇼로 보여주고 library_intro 대화 시작
    /// </summary>
    IEnumerator LibraryCutsceneWithIllustrations()
    {
        Debug.Log("[CutsceneManager] 라이브러리 일러스트 컷씬 시작");

        // 아련한 브금 재생
        if (slideshowBGM != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.CrossfadeBGM(slideshowBGM);
        }

        // 일러스트 로드
        Sprite[] illustrations = LoadIllustrations();
        if (illustrations.Length == 0)
        {
            Debug.LogWarning($"[CutsceneManager] {illustrationFolderPath}에서 일러스트를 찾을 수 없습니다! 컷씬을 건너뜁니다.");
            // 일러스트가 없어도 컷씬을 종료하고 StoryNode가 자동 진행하도록 함
            yield break;
        }

        // 일러스트 캔버스 생성
        CreateIllustrationCanvas();

        // 각 일러스트를 순서대로 표시
        for (int i = 0; i < illustrations.Length; i++)
        {
            Debug.Log($"[CutsceneManager] 일러스트 {i + 1}/{illustrations.Length} 표시");
            yield return StartCoroutine(ShowIllustration(illustrations[i]));
        }

        // 일러스트 캔버스 제거
        DestroyIllustrationCanvas();

        // 슬라이드쇼 완료 - StoryNode 시스템이 자동으로 library_intro로 진행
        Debug.Log("[CutsceneManager] 라이브러리 일러스트 컷씬 종료");
    }

    /// <summary>
    /// Resources 폴더에서 일러스트 이미지들 로드
    /// </summary>
    Sprite[] LoadIllustrations()
    {
        // Resources 폴더에서 모든 스프라이트 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>(illustrationFolderPath);

        if (sprites.Length == 0)
        {
            Debug.LogWarning($"[CutsceneManager] {illustrationFolderPath}에 일러스트가 없습니다.");
        }
        else
        {
            Debug.Log($"[CutsceneManager] {sprites.Length}개의 일러스트를 로드했습니다.");
        }

        return sprites;
    }

    /// <summary>
    /// 전체화면 일러스트 캔버스 생성 (검정 배경)
    /// </summary>
    void CreateIllustrationCanvas()
    {
        // 캔버스 생성
        illustrationCanvas = new GameObject("IllustrationCanvas");
        Canvas canvas = illustrationCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 최상위 레이어

        UnityEngine.UI.CanvasScaler canvasScaler = illustrationCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);

        illustrationCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // 검정 배경 패널 생성
        GameObject bgPanel = new GameObject("BackgroundPanel");
        bgPanel.transform.SetParent(illustrationCanvas.transform, false);

        UnityEngine.UI.Image bgImage = bgPanel.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = Color.black;

        RectTransform bgRect = bgPanel.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;

        // 일러스트 이미지 홀더 생성
        GameObject imageHolder = new GameObject("ImageHolder");
        imageHolder.transform.SetParent(illustrationCanvas.transform, false);

        UnityEngine.UI.Image image = imageHolder.AddComponent<UnityEngine.UI.Image>();
        image.preserveAspect = true; // 비율 유지

        RectTransform imageRect = imageHolder.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.sizeDelta = Vector2.zero;
        imageRect.anchoredPosition = Vector2.zero;

        // CanvasGroup 추가 (페이드 효과용)
        CanvasGroup canvasGroup = imageHolder.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        Debug.Log("[CutsceneManager] 일러스트 캔버스 생성 완료");
    }

    /// <summary>
    /// 일러스트 캔버스 제거
    /// </summary>
    void DestroyIllustrationCanvas()
    {
        if (illustrationCanvas != null)
        {
            Destroy(illustrationCanvas);
            illustrationCanvas = null;
            Debug.Log("[CutsceneManager] 일러스트 캔버스 제거");
        }
    }

    /// <summary>
    /// 개별 일러스트를 페이드 인/아웃으로 표시하고 입력 대기
    /// </summary>
    IEnumerator ShowIllustration(Sprite illustration)
    {
        if (illustrationCanvas == null)
        {
            Debug.LogError("[CutsceneManager] 일러스트 캔버스가 없습니다!");
            yield break;
        }

        // 이미지 홀더 찾기
        Transform imageHolderTransform = illustrationCanvas.transform.Find("ImageHolder");
        if (imageHolderTransform == null)
        {
            Debug.LogError("[CutsceneManager] ImageHolder를 찾을 수 없습니다!");
            yield break;
        }

        UnityEngine.UI.Image image = imageHolderTransform.GetComponent<UnityEngine.UI.Image>();
        CanvasGroup canvasGroup = imageHolderTransform.GetComponent<CanvasGroup>();

        if (image == null || canvasGroup == null)
        {
            Debug.LogError("[CutsceneManager] Image 또는 CanvasGroup을 찾을 수 없습니다!");
            yield break;
        }

        // 이미지 설정
        image.sprite = illustration;

        // 페이드 인
        canvasGroup.alpha = 0f;
        yield return canvasGroup.DOFade(1f, illustrationFadeDuration)
            .SetEase(Ease.InOutQuad)
            .WaitForCompletion();

        // 입력 대기
        waitingForInput = true;
        Debug.Log("[CutsceneManager] 입력 대기 중... (클릭 또는 스페이스/엔터)");

        yield return new WaitUntil(() => !waitingForInput);

        Debug.Log("[CutsceneManager] 입력 감지됨");

        // 페이드 아웃
        yield return canvasGroup.DOFade(0f, illustrationFadeDuration)
            .SetEase(Ease.InOutQuad)
            .WaitForCompletion();

        // 짧은 딜레이
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>
    /// outside_shock 대화와 함께 진행되는 컷씬 (마을 밖 충격적인 장면)
    /// </summary>
    IEnumerator OutsideShockCutscene()
    {
        Debug.Log("[CutsceneManager] Outside Shock 컷씬 시작");

        // 시네마틱 바 표시
        yield return StartCoroutine(ShowCinematicBars(0.5f));

        CameraController cameraController = FindObjectOfType<CameraController>();
        Transform originalTarget = null;

        if (cameraController != null)
        {
            originalTarget = cameraController.target;
        }

        // 불길한 BGM 변경
        if (AudioManager.Instance != null)
        {
            AudioClip ominousBGM = Resources.Load<AudioClip>("Audio/ominous_bgm");
            if (ominousBGM != null)
            {
                AudioManager.Instance.CrossfadeBGM(ominousBGM);
            }
        }

        // 1. 몬스터들을 먼저 보여주기 (영화적 연출) → 마지막에 미오에게 카메라 복귀
        yield return StartCoroutine(ShowMonstersInVillage(cameraController));

        // 2. 체다 등장 (대화 시작 전)
        yield return StartCoroutine(CheddarEmergencyEntrance());

        // 3. outside_shock 대화 시작 (미오와 튜나와 체다의 대화)
        if (DialogueManager.Instance != null && !DialogueManager.Instance.IsInDialogue())
        {
            DialogueManager.Instance.StartDialogue("outside_shock");

            // 대화 완료 대기
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        }

        // 4. 카메라 원위치
        if (cameraController != null && originalTarget != null)
        {
            cameraController.target = originalTarget;
            cameraController.ResetZoom(1f);
        }

        // 시네마틱 바 제거
        yield return StartCoroutine(HideCinematicBars(0.5f));

        Debug.Log("[CutsceneManager] Outside Shock 컷씬 종료");
    }

    /// <summary>
    /// 몬스터들을 영화적으로 보여주는 서브 컷씬
    /// </summary>
    IEnumerator ShowMonstersInVillage(CameraController cameraController)
    {
        // 전체 샷 - 마을 전경
        if (cameraController != null)
        {
            Debug.Log("[CutsceneManager] 절망적인 마을 전경");
            cameraController.ZoomTo(8f, 2.5f);

            // 화면 어둡게
            Volume globalVolume = FindObjectOfType<Volume>();
            if (globalVolume != null)
            {
                ColorAdjustments colorAdjustments;
                if (globalVolume.profile.TryGet(out colorAdjustments))
                {
                    DOTween.To(() => colorAdjustments.postExposure.value,
                        x => colorAdjustments.postExposure.value = x,
                        -0.5f, 2f);
                }
            }

            yield return new WaitForSeconds(2f);
        }

        // 몬스터 찾기
        GameObject[] monsters = GameObject.FindGameObjectsWithTag("Monster");
        Debug.Log($"[CutsceneManager] {monsters.Length}마리의 몬스터 발견!");

        if (monsters.Length > 0)
        {
            // 번개 효과
            yield return StartCoroutine(LightningEffect());

            // 몬스터 클로즈업 (빠른 컷)
            for (int i = 0; i < Mathf.Min(monsters.Length, 3); i++)
            {
                if (monsters[i] == null) continue;

                if (cameraController != null)
                {
                    // 빠른 암전 전환
                    yield return StartCoroutine(QuickFade(0.1f));

                    cameraController.target = monsters[i].transform;
                    cameraController.ZoomTo(2f, 0.1f);

                    yield return StartCoroutine(QuickFadeIn(0.2f));

                    // 몬스터 으르렁
                    if (AudioManager.Instance != null && i == 0)
                    {
                        AudioClip growl = Resources.Load<AudioClip>("Sounds/monster_growl");
                        if (growl != null) AudioManager.Instance.PlaySFX(growl, 0.5f);
                    }

                    yield return new WaitForSeconds(0.8f);
                }
            }
        }

        // ✨ 추가: 마지막 몬스터 쇼트 후 미오에게 카메라 복귀
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && cameraController != null)
        {
            Debug.Log("[CutsceneManager] 미오에게 카메라 복귀");

            // 빠른 페이드 전환
            yield return StartCoroutine(QuickFade(0.15f));

            // 미오를 중간 샷으로 (체다 등장을 볼 수 있게)
            cameraController.target = player.transform;
            cameraController.ZoomTo(3f, 0.2f); // 적당한 거리

            yield return StartCoroutine(QuickFadeIn(0.2f));

            // 미오의 두려운 표정 (애니메이션이 있다면)
            Animator playerAnimator = player.GetComponent<Animator>();
            if (playerAnimator != null)
            {
                // 예: playerAnimator.SetTrigger("Scared");
            }

            yield return new WaitForSeconds(0.8f); // 미오의 반응 시간
        }
    }

    /// <summary>
    /// 체다가 급하게 등장하는 애니메이션 (기존 코드 스타일 참고)
    /// </summary>
    IEnumerator CheddarEmergencyEntrance()
    {
        Debug.Log("[CutsceneManager] 체다 긴급 등장!");

        GameObject cheddar = GameObject.Find("Cheddar");
        if (cheddar == null)
        {
            // 체다 생성
            GameObject cheddarPrefab = Resources.Load<GameObject>("Prefabs/Cheddar");
            if (cheddarPrefab != null)
            {
                cheddar = Instantiate(cheddarPrefab);
                cheddar.name = "Cheddar";
            }
            else
            {
                Debug.LogError("[CutsceneManager] Cheddar 프리팹을 찾을 수 없습니다!");
                yield break;
            }
        }

        // 문 위치 설정 (맵에 맞게 조정 필요)
        Vector3 doorPosition = new Vector3(-4.52f, 26.565f, 0f);
        Vector3 targetPosition = new Vector3(-4.52f, 24.3f, 0f);

        cheddar.transform.position = doorPosition;
        cheddar.SetActive(false);

        // 문 열리는 소리
        if (AudioManager.Instance != null)
        {
            AudioClip doorSound = Resources.Load<AudioClip>("Sounds/door_slam");
            if (doorSound != null)
            {
                AudioManager.Instance.PlaySFX(doorSound, 0.7f);
            }
        }

        yield return new WaitForSeconds(0.2f);

        // 체다 등장
        cheddar.SetActive(true);

        // FootstepSound 활성화
        FootstepSound cheddarFootstep = cheddar.GetComponent<FootstepSound>();
        if (cheddarFootstep != null)
        {
            cheddarFootstep.EnableFootsteps();
        }

        // 이동 방향 계산
        Vector3 direction = (targetPosition - doorPosition).normalized;

        Animator animator = cheddar.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("isWalk", true);

            // 방향 설정 (CheddarAppearsCutscene 참고)
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                animator.SetFloat("xDir", Mathf.Sign(direction.x));
                animator.SetFloat("yDir", 0f);
            }
            else
            {
                animator.SetFloat("xDir", 0f);
                animator.SetFloat("yDir", Mathf.Sign(direction.y));
            }
        }

        // 목표 위치로 빠르게 이동 (급하게 뛰어오는 느낌)
        float distance = Vector3.Distance(doorPosition, targetPosition);
        float duration = distance / 2f; // 빠르게 이동

        yield return cheddar.transform.DOMove(targetPosition, duration)
            .SetEase(Ease.OutQuad)
            .WaitForCompletion();

        // FootstepSound 비활성화
        if (cheddarFootstep != null)
        {
            cheddarFootstep.DisableFootsteps();
        }

        // 걷기 애니메이션 정지
        if (animator != null)
        {
            animator.SetBool("isWalk", false);
        }

        Debug.Log("[CutsceneManager] 체다 등장 완료");
    }

    /// <summary>
    /// 체다가 몬스터에게 공격당하는 컷씬 - 수정 버전
    /// </summary>
    IEnumerator CheddarAttackCutscene()
    {
        Debug.Log("[CutsceneManager] 체다 공격 컷씬 시작");

        // 시네마틱 바
        yield return StartCoroutine(ShowCinematicBars(0.3f));

        GameObject cheddar = GameObject.Find("Cheddar");
        if (cheddar == null)
        {
            Debug.LogWarning("[CutsceneManager] Cheddar를 찾을 수 없습니다! 생성을 시도합니다.");

            GameObject cheddarPrefab = Resources.Load<GameObject>("Prefabs/Cheddar");
            if (cheddarPrefab != null)
            {
                cheddar = Instantiate(cheddarPrefab);
                cheddar.name = "Cheddar";
                cheddar.transform.position = new Vector3(0, 0, 0);
            }

            if (cheddar == null)
            {
                Debug.LogError("[CutsceneManager] Cheddar 생성 실패!");
                yield return StartCoroutine(HideCinematicBars(0.3f));
                yield break;
            }
        }

        CameraController cameraController = FindObjectOfType<CameraController>();
        Transform originalTarget = null;

        if (cameraController != null)
        {
            originalTarget = cameraController.target;
            cameraController.target = cheddar.transform;
            cameraController.ZoomTo(2f, 0.5f);
            yield return new WaitForSeconds(0.5f);
        }

        // 몬스터 찾기
        GameObject monster = FindClosestMonster(cheddar.transform.position);

        // 몬스터가 없으면 임시 생성
        bool isTempMonster = false;
        if (monster == null)
        {
            Debug.LogWarning("[CutsceneManager] 몬스터를 찾을 수 없어 임시 몬스터를 생성합니다.");
            monster = new GameObject("TempMonster");
            monster.transform.position = cheddar.transform.position + new Vector3(-3f, 0f, 0f);
            SpriteRenderer monsterSr = monster.AddComponent<SpriteRenderer>();
            isTempMonster = true;
        }

        // 몬스터의 AI 및 물리 비활성화 (밀림 방지)
        MonoBehaviour[] monsterAIComponents = monster.GetComponents<MonoBehaviour>();
        Rigidbody2D monsterRb = monster.GetComponent<Rigidbody2D>();
        Collider2D monsterCollider = monster.GetComponent<Collider2D>();

        // AI 컴포넌트 비활성화 (일반적인 AI 컴포넌트 이름들)
        foreach (var component in monsterAIComponents)
        {
            if (component.GetType().Name.Contains("AI") ||
                component.GetType().Name.Contains("Enemy") ||
                component.GetType().Name.Contains("Monster"))
            {
                component.enabled = false;
            }
        }

        // 물리 및 콜리전 비활성화
        if (monsterRb != null) monsterRb.simulated = false;
        if (monsterCollider != null) monsterCollider.enabled = false;

        // 몬스터 접근 (위협적으로)
        if (monster != null)
        {
            Debug.Log("[CutsceneManager] 몬스터 접근 시작");

            // 불길한 소리
            if (AudioManager.Instance != null)
            {
                AudioClip ominous = Resources.Load<AudioClip>("Sounds/ominous_approach");
                if (ominous != null) AudioManager.Instance.PlaySFX(ominous, 0.4f);
            }

            Vector3 attackPos = cheddar.transform.position + new Vector3(-1.5f, 0f, 0f);

            // 천천히 다가오기
            yield return monster.transform.DOMove(attackPos, 2f)
                .SetEase(Ease.InOutSine)
                .WaitForCompletion();

            yield return new WaitForSeconds(0.3f);
        }

        // 공격 순간 - 시간 정지 효과
        Time.timeScale = 0.2f;
        yield return new WaitForSecondsRealtime(0.3f);
        Time.timeScale = 1f;

        // "아아악!!" 대사 시작
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue("cheddar_attack_dialogue");
        }

        // 화면 번쩍임
        yield return StartCoroutine(ScreenFlash());

        // 강력한 카메라 흔들림
        if (cameraController != null)
        {
            cameraController.ShakeCamera(0.5f, 0.8f);
        }

        // 타격 효과음
        if (AudioManager.Instance != null)
        {
            AudioClip hit = Resources.Load<AudioClip>("Sounds/heavy_impact");
            if (hit != null) AudioManager.Instance.PlaySFX(hit, 0.9f);
        }

        // 체다 쓰러짐 - "cheddar_hurt" 애니메이션 사용
        Animator cheddarAnimator = cheddar.GetComponent<Animator>();
        if (cheddarAnimator != null)
        {
            cheddarAnimator.SetTrigger("cheddar_hurt");
        }
        else
        {
            // 애니메이터가 없을 경우 대체 애니메이션
            cheddar.transform.DORotate(new Vector3(0, 0, -90), 0.5f);
            cheddar.transform.DOMoveY(cheddar.transform.position.y - 0.5f, 0.5f);
        }

        yield return new WaitForSeconds(1.5f);

        // 몬스터 정지
        if (monster != null && !isTempMonster)
        {
            Animator monsterAnim = monster.GetComponent<Animator>();
            if (monsterAnim != null)
            {
                monsterAnim.SetBool("isIdle", true);
            }
        }

        // 대화 완료 대기
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue())
        {
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        }

        // 카메라 줌아웃
        if (cameraController != null)
        {
            cameraController.ZoomTo(4f, 1f);
            yield return new WaitForSeconds(1f);
        }

        // 시네마틱 바 제거
        yield return StartCoroutine(HideCinematicBars(0.3f));

        // 카메라 원위치
        if (cameraController != null && originalTarget != null)
        {
            cameraController.target = originalTarget;
            cameraController.ResetZoom(0.8f);
        }

        // 몬스터 AI 및 물리 재활성화
        if (!isTempMonster && monster != null)
        {
            foreach (var component in monsterAIComponents)
            {
                if (component.GetType().Name.Contains("AI") ||
                    component.GetType().Name.Contains("Enemy") ||
                    component.GetType().Name.Contains("Monster"))
                {
                    component.enabled = true;
                }
            }
            if (monsterRb != null) monsterRb.simulated = true;
            if (monsterCollider != null) monsterCollider.enabled = true;
        }

        // 임시 몬스터 제거
        if (isTempMonster && monster != null)
        {
            Destroy(monster);
        }

        Debug.Log("[CutsceneManager] 체다 공격 컷씬 종료");
    }

    /// <summary>
    /// 가장 가까운 몬스터 찾기 헬퍼 메서드
    /// </summary>
    GameObject FindClosestMonster(Vector3 position)
    {
        GameObject closestMonster = null;
        float minDistance = float.MaxValue;

        // TutorialMonster 태그 먼저 확인
        try
        {
            GameObject[] tutorialMonsters = GameObject.FindGameObjectsWithTag("TutorialMonster");
            if (tutorialMonsters.Length > 0)
            {
                foreach (GameObject m in tutorialMonsters)
                {
                    if (m != null)
                    {
                        float distance = Vector3.Distance(m.transform.position, position);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestMonster = m;
                        }
                    }
                }
                Debug.Log($"[CutsceneManager] TutorialMonster 태그에서 가장 가까운 몬스터 사용: {closestMonster?.name}");
                return closestMonster;
            }
        }
        catch (UnityException e)
        {
            Debug.LogWarning($"[CutsceneManager] TutorialMonster 태그 찾기 실패: {e.Message}");
        }

        // Monster 태그 확인
        GameObject[] monsters = GameObject.FindGameObjectsWithTag("Monster");
        if (monsters.Length > 0)
        {
            foreach (GameObject m in monsters)
            {
                if (m != null)
                {
                    float distance = Vector3.Distance(m.transform.position, position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestMonster = m;
                    }
                }
            }
            Debug.Log($"[CutsceneManager] Monster 태그에서 가장 가까운 몬스터 사용: {closestMonster?.name}");
        }

        return closestMonster;
    }

    // 기존 CheddarAppearsCutscene 메서드는 그대로 유지
    IEnumerator CheddarAppearsCutscene()
    {
        SceneContext sceneContext = FindObjectOfType<SceneContext>();
        if (sceneContext != null)
        {
            MapAudioProfile afterProfile = Resources.Load<MapAudioProfile>("Audio/Audio_Library_After");
            if (afterProfile != null)
            {
                sceneContext.audioProfile = afterProfile;
                AudioManager.Instance.CrossfadeBGM(afterProfile.backgroundMusic);
            }
        }

        Volume globalVolume = FindObjectOfType<Volume>();
        if (globalVolume != null && libraryAfterProfile != null)
        {
            globalVolume.profile = libraryAfterProfile;
            Debug.Log("[Cutscene] Volume Profile 변경 완료");
        }

        yield return new WaitForSeconds(0.5f);

        SpawnTuna();

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue("mio_feels_something");
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        }

        yield return new WaitForSeconds(0.5f);

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue("cheddar_shout_mio");
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        }

        yield return new WaitForSeconds(0.3f);

        if (AudioManager.Instance != null)
        {
            AudioClip doorOpenSound = Resources.Load<AudioClip>("Sounds/door_close");
            if (doorOpenSound != null)
            {
                AudioManager.Instance.PlaySFX(doorOpenSound, 0.3f);
            }
        }

        yield return new WaitForSeconds(0.3f);

        GameObject cheddar = GameObject.Find("Cheddar");
        if (cheddar != null)
        {
            FootstepSound cheddarFootstep = cheddar.GetComponent<FootstepSound>();

            Vector3[] waypoints = new Vector3[]
            {
                new Vector3(0.148f, -3.94f, 0f),
                new Vector3(0.148f, -2.56f, 0f),
                new Vector3(3.29f, -2.56f, 0f),
                new Vector3(3.29f, -0.21f, 0f),
                new Vector3(6.99f, -0.21f, 0f),
                new Vector3(6.99f, 0.2f, 0f),
            };

            cheddar.transform.position = waypoints[0];
            cheddar.SetActive(true);

            Animator animator = cheddar.GetComponent<Animator>();

            CameraController cameraController = FindObjectOfType<CameraController>();
            Transform originalTarget = null;

            if (cameraController != null)
            {
                originalTarget = cameraController.target;
                cameraController.target = cheddar.transform;
                cameraController.ZoomTo(1.5f, 0.5f);
            }

            yield return new WaitForSeconds(0.5f);

            if (cheddarFootstep != null)
            {
                cheddarFootstep.EnableFootsteps();
            }

            for (int i = 1; i < waypoints.Length; i++)
            {
                Vector3 currentPos = waypoints[i - 1];
                Vector3 nextPos = waypoints[i];
                Vector3 direction = (nextPos - currentPos).normalized;

                if (animator != null)
                {
                    animator.SetBool("isWalk", true);

                    if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                    {
                        animator.SetFloat("xDir", Mathf.Sign(direction.x));
                        animator.SetFloat("yDir", 0f);
                    }
                    else
                    {
                        animator.SetFloat("xDir", 0f);
                        animator.SetFloat("yDir", Mathf.Sign(direction.y));
                    }
                }

                float distance = Vector3.Distance(currentPos, nextPos);
                float duration = distance / 2f;

                yield return cheddar.transform.DOMove(nextPos, duration)
                    .SetEase(Ease.Linear)
                    .WaitForCompletion();

                if (i < waypoints.Length - 1)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }

            if (cheddarFootstep != null)
            {
                cheddarFootstep.DisableFootsteps();
            }

            if (animator != null)
            {
                animator.SetBool("isWalk", false);
            }

            if (cameraController != null && originalTarget != null)
            {
                cameraController.target = originalTarget;
                cameraController.ResetZoom(0.8f);
                yield return new WaitForSeconds(0.8f);
            }
        }
        else
        {
            Debug.LogError("[Cutscene] Cheddar를 찾을 수 없습니다!");
        }

        yield return new WaitForSeconds(0.3f);

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue("cheddar_conversation");
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        }

        yield return new WaitForSeconds(0.5f);

        DespawnTuna();

        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// 기존 OutsideVillageCutscene은 더 이상 사용하지 않음 (OutsideShockCutscene으로 대체)
    /// </summary>
    IEnumerator OutsideVillageCutscene()
    {
        yield return StartCoroutine(OutsideShockCutscene());
    }

    /// <summary>
    /// 전투 후 체다와 재회하는 컷씬
    /// </summary>
    IEnumerator AfterBattleCutscene()
    {
        Debug.Log("[CutsceneManager] 전투 후 컷씬 시작");

        // 씬 오브젝트 초기화 대기
        yield return new WaitForSeconds(0.5f);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        GameObject cheddar = GameObject.Find("Cheddar");

        // ✅ 체다가 쓰러진 위치 (공격당한 바로 그 위치)
        Vector3 cheddarFallenPosition = new Vector3(-4.52f, 24.3f, 0f);

        // Cheddar가 없으면 생성
        if (cheddar == null)
        {
            Debug.Log("[CutsceneManager] Cheddar를 찾을 수 없어 생성합니다.");
            
            GameObject cheddarPrefab = Resources.Load<GameObject>("Prefabs/Cheddar");
            if (cheddarPrefab != null)
            {
                cheddar = Instantiate(cheddarPrefab, cheddarFallenPosition, Quaternion.identity);
                cheddar.name = "Cheddar";
                
                // ✅ 애니메이터 즉시 비활성화 (애니메이션 재생 방지)
                Animator cheddarAnimator = cheddar.GetComponent<Animator>();
                if (cheddarAnimator != null)
                {
                    cheddarAnimator.enabled = false;
                }
                
                // ✅ 회전으로 쓰러진 상태 표현 (애니메이션 X)
                cheddar.transform.rotation = Quaternion.Euler(0, 0, -90);
            }
            else
            {
                Debug.LogError("[CutsceneManager] Cheddar 프리팹을 찾을 수 없습니다!");
            }
        }

        if (player == null || cheddar == null)
        {
            Debug.LogWarning("[CutsceneManager] Player 또는 Cheddar를 여전히 찾을 수 없습니다!");
            yield break;
        }

        // 플레이어를 체다 옆으로 이동
        Vector3 targetPos = cheddar.transform.position + new Vector3(-1.5f, 0f, 0f);

        Animator playerAnimator = player.GetComponent<Animator>();
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isWalk", true);
        }

        Debug.Log("[CutsceneManager] 플레이어가 체다에게 이동");
        yield return player.transform.DOMove(targetPos, 1.5f).SetEase(Ease.Linear).WaitForCompletion();

        if (playerAnimator != null)
        {
            playerAnimator.SetBool("isWalk", false);
        }

        yield return new WaitForSeconds(0.5f);

        // ✅ 체다 일어서기
        Animator cheddarAnimator2 = cheddar.GetComponent<Animator>();
        if (cheddarAnimator2 != null)
        {
            // 애니메이터 활성화
            cheddarAnimator2.enabled = true;
            
            Debug.Log("[CutsceneManager] 체다 일어서기 애니메이션");
            cheddarAnimator2.SetTrigger("StandUp");
            yield return new WaitForSeconds(1f);
        }
        else
        {
            // 애니메이터가 없으면 회전으로 일어나기
            cheddar.transform.DORotate(Vector3.zero, 0.5f);
            yield return new WaitForSeconds(0.5f);
        }

        // 전투 후 대화 시작
        if (DialogueManager.Instance != null)
        {
            Debug.Log("[CutsceneManager] 전투 후 대화 시작");
            DialogueManager.Instance.StartDialogue("after_battle_dialogue");
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        }

        Debug.Log("[CutsceneManager] 전투 후 컷씬 종료");
    }

    #region 시네마틱 효과 메서드들

    IEnumerator ShowCinematicBars(float duration)
    {
        if (cinematicBarsActive) yield break;

        Canvas canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[CutsceneManager] Canvas를 찾을 수 없습니다.");
            yield break;
        }

        topBar = new GameObject("TopCinematicBar");
        topBar.transform.SetParent(canvas.transform, false);
        UnityEngine.UI.Image topImage = topBar.AddComponent<UnityEngine.UI.Image>();
        topImage.color = Color.black;

        RectTransform topRT = topBar.GetComponent<RectTransform>();
        topRT.anchorMin = new Vector2(0, 1);
        topRT.anchorMax = new Vector2(1, 1);
        topRT.pivot = new Vector2(0.5f, 1);
        topRT.sizeDelta = new Vector2(0, 100);
        topRT.anchoredPosition = new Vector2(0, 100);

        bottomBar = new GameObject("BottomCinematicBar");
        bottomBar.transform.SetParent(canvas.transform, false);
        UnityEngine.UI.Image bottomImage = bottomBar.AddComponent<UnityEngine.UI.Image>();
        bottomImage.color = Color.black;

        RectTransform bottomRT = bottomBar.GetComponent<RectTransform>();
        bottomRT.anchorMin = new Vector2(0, 0);
        bottomRT.anchorMax = new Vector2(1, 0);
        bottomRT.pivot = new Vector2(0.5f, 0);
        bottomRT.sizeDelta = new Vector2(0, 100);
        bottomRT.anchoredPosition = new Vector2(0, -100);

        topRT.DOAnchorPosY(0, duration).SetEase(Ease.OutQuad);
        bottomRT.DOAnchorPosY(0, duration).SetEase(Ease.OutQuad);

        cinematicBarsActive = true;
        yield return new WaitForSeconds(duration);
    }

    IEnumerator HideCinematicBars(float duration)
    {
        if (!cinematicBarsActive) yield break;

        if (topBar != null)
        {
            RectTransform topRT = topBar.GetComponent<RectTransform>();
            topRT.DOAnchorPosY(100, duration).SetEase(Ease.InQuad)
                .OnComplete(() => Destroy(topBar));
        }

        if (bottomBar != null)
        {
            RectTransform bottomRT = bottomBar.GetComponent<RectTransform>();
            bottomRT.DOAnchorPosY(-100, duration).SetEase(Ease.InQuad)
                .OnComplete(() => Destroy(bottomBar));
        }

        cinematicBarsActive = false;
        yield return new WaitForSeconds(duration);
    }

    IEnumerator ScreenFlash()
    {
        Canvas canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[CutsceneManager] Canvas를 찾을 수 없어 화면 번쩍임을 건너뜁니다.");
            yield break;
        }

        GameObject flash = new GameObject("FlashPanel");
        flash.transform.SetParent(canvas.transform, false);

        UnityEngine.UI.Image flashImage = flash.AddComponent<UnityEngine.UI.Image>();
        flashImage.color = Color.white;

        RectTransform rt = flash.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        flashImage.DOFade(0, 0.3f).OnComplete(() => Destroy(flash));

        yield return new WaitForSeconds(0.3f);
    }

    IEnumerator LightningEffect()
    {
        if (AudioManager.Instance != null)
        {
            AudioClip thunder = Resources.Load<AudioClip>("Sounds/thunder");
            if (thunder != null) AudioManager.Instance.PlaySFX(thunder, 0.6f);
        }

        for (int i = 0; i < 2; i++)
        {
            yield return StartCoroutine(ScreenFlash());
            yield return new WaitForSeconds(0.1f);
        }
    }

    IEnumerator QuickFade(float duration)
    {
        if (FadeManager.Instance != null)
        {
            yield return FadeManager.Instance.FadeOut(duration);
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }
    }

    IEnumerator QuickFadeIn(float duration)
    {
        if (FadeManager.Instance != null)
        {
            yield return FadeManager.Instance.FadeIn(duration);
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }
    }

    #endregion

    private void SpawnTuna()
    {
        if (tunaPrefab == null)
        {
            Debug.LogWarning("[CutsceneManager] 튜나 프리팹이 설정되지 않았습니다.");
            return;
        }

        GameObject spawnPoint = GameObject.Find("TunaSpawnPoint");
        if (spawnPoint == null)
        {
            Debug.LogError("[CutsceneManager] TunaSpawnPoint를 찾을 수 없습니다!");
            return;
        }

        Vector3 spawnPos = spawnPoint.transform.position;
        tunaInstance = Instantiate(tunaPrefab, spawnPos, Quaternion.identity);

        tunaInstance.transform.localScale = Vector3.zero;
        tunaInstance.transform.DOScale(0.5f, 0.5f)
            .SetEase(Ease.OutBack);

        tunaInstance.transform.DOMoveY(spawnPos.y + 0.2f, 0.3f)
            .SetLoops(2, LoopType.Yoyo);

        Debug.Log("[CutsceneManager] 튜나 등장!");
    }

    private void DespawnTuna()
    {
        if (tunaInstance == null) return;

        tunaInstance.transform.DOScale(0f, 0.5f)
            .SetEase(Ease.InBack)
            .OnComplete(() => {
                Destroy(tunaInstance);
                tunaInstance = null;
                Debug.Log("[CutsceneManager] 튜나 퇴장!");
            });
    }

    private void HandleGameStateChange(GameState newState)
    {
        if (newState == GameState.Loading && isPlayingCutscene)
        {
            StopAllCoroutines();
            isPlayingCutscene = false;

            if (tunaInstance != null)
            {
                Destroy(tunaInstance);
                tunaInstance = null;
            }
            Debug.Log("[CutsceneManager] 씬 로딩으로 인해 모든 컷씬을 중단합니다.");
        }
    }

    void OnDestroy()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnGameStateChanged -= HandleGameStateChange;
        }
    }
}
