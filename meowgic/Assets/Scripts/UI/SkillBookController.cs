using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

public class SkillBookController : MonoBehaviour
{
    [System.Serializable]
    public class SkillData
    {
        public string skillName;
        [TextArea] public string description;
        public Sprite skillImage;
    }

    [System.Serializable]
    public class SkillSlot
    {
        public TextMeshProUGUI skillNameText;
        public TextMeshProUGUI descriptionText;
        public Image skillImage;
    }

    [Header("UI References")]
    public GameObject skillWindow;
    public CanvasGroup pageGroup;

    [Header("Skill Slots - 왼쪽 페이지")]
    public SkillSlot leftSlot1;
    public SkillSlot leftSlot2;

    [Header("Skill Slots - 오른쪽 페이지")]
    public SkillSlot rightSlot1;
    public SkillSlot rightSlot2;

    [Header("Navigation")]
    public Button leftArrowButton;
    public Button rightArrowButton;
    public GameObject leftArrowObject;
    public GameObject rightArrowObject;

    [Header("Skill Data")]
    public SkillData[] allSkills;

    [Header("Special Pages")]
    public GameObject doodlePage;
    public string doodleDialogueID = "skillbook_doodle_surprise";
    private bool hasSeenDoodlePage = false;

    [Header("Tutorial")]
    public string tutorialOpenDialogueID = "skillbook_tutorial_open";
    public string tutorialNavigationDialogueID = "skillbook_tutorial_navigation";
    public string tutorialReminderDialogueID = "skillbook_tutorial_reminder";
    public string arrowActivationLineID = "skill_open_006";
    public float tutorialTimeoutDuration = 10f;
    public int requiredKPresses = 3;

    private enum TutorialPhase
    {
        NotStarted,
        Opening,
        ArrowEnabled,
        WaitingForClose,
        Completed
    }

    private TutorialPhase currentPhase = TutorialPhase.NotStarted;
    private bool isInTutorial = false;
    private bool hasCompletedTutorial = false;
    private bool canUseNavigation = false;
    private bool isWaitingForKPresses = false;
    private int kPressCount = 0;
    private Coroutine tutorialTimeoutCoroutine;
    private bool closedByReminder = false;

    [Header("Gesture Recognition")]
    public HolisticJsonSender holisticJsonSender;

    [Header("Audio")]
    public AudioClip pageFlipSound;
    public AudioClip doodleSound;
    private AudioSource audioSource;

    private int currentPageIndex = 0;
    private SkillSlot[] allSlots;
    private bool isWindowOpen = false;
    private bool isShowingSpecialPage = false;

    void OnEnable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnDialogueStarted += HandleDialogueStarted;
            GameEventManager.Instance.OnDialogueFinished += HandleDialogueFinished;
            GameEventManager.Instance.OnKeyEvent += HandleKeyEvent;
        }

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnLineDisplayed += HandleLineDisplayed;
        }
    }

    void OnDisable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnDialogueStarted -= HandleDialogueStarted;
            GameEventManager.Instance.OnDialogueFinished -= HandleDialogueFinished;
            GameEventManager.Instance.OnKeyEvent -= HandleKeyEvent;
        }

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnLineDisplayed -= HandleLineDisplayed;
        }
    }

    void Start()
    {
        allSlots = new SkillSlot[] { leftSlot1, leftSlot2, rightSlot1, rightSlot2 };

        leftArrowButton.onClick.AddListener(PreviousPage);
        rightArrowButton.onClick.AddListener(NextPage);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        skillWindow.SetActive(false);
        isWindowOpen = false;

        if (doodlePage != null)
        {
            doodlePage.SetActive(false);
        }

        if (GameDataManager.Instance != null)
        {
            hasSeenDoodlePage = PlayerPrefs.GetInt("HasSeenDoodlePage", 0) == 1;
            hasCompletedTutorial = PlayerPrefs.GetInt("HasCompletedSkillBookTutorial", 0) == 1;

            if (GameDataManager.Instance.CurrentStoryID == "wait_for_skill_tutorial")
            {
                PlayerPrefs.DeleteKey("HasSeenDoodlePage");
                PlayerPrefs.DeleteKey("HasCompletedSkillBookTutorial");
                hasSeenDoodlePage = false;
                hasCompletedTutorial = false;
                currentPhase = TutorialPhase.NotStarted;
                canUseNavigation = false;
                Debug.Log("[SkillBookController] wait_for_skill_tutorial 스토리 - 튜토리얼 초기화");
            }
            else if (!hasCompletedTutorial)
            {
                PlayerPrefs.DeleteKey("HasSeenDoodlePage");
                currentPhase = TutorialPhase.NotStarted;
                canUseNavigation = false;
                Debug.Log("[SkillBookController] 튜토리얼 미완료 - 초기화");
            }

            if (GameDataManager.Instance.CurrentStoryID == "library_intro")
            {
                PlayerPrefs.DeleteKey("HasSeenDoodlePage");
                PlayerPrefs.DeleteKey("HasCompletedSkillBookTutorial");
                hasSeenDoodlePage = false;
                hasCompletedTutorial = false;
            }
        }
    }

    void Update()
    {
        bool isGestureRecognizing = IsGestureRecognizing();

        if (GameManager.Instance.CurrentState == GameState.Dialogue ||
            GameManager.Instance.CurrentState == GameState.Cutscene ||
            isGestureRecognizing)
        {
            if (isWindowOpen && !isInTutorial)
            {
                CloseWindowWithoutStateChange();
            }
            return;
        }

        if (currentPhase == TutorialPhase.WaitingForClose && isWaitingForKPresses)
        {
            if (Input.GetKeyDown(KeyCode.K))
            {
                kPressCount++;
                Debug.Log($"[SkillBookController] K키 카운트: {kPressCount}/{requiredKPresses}");

                if (kPressCount >= requiredKPresses)
                {
                    isWaitingForKPresses = false;
                    kPressCount = 0;

                    if (DialogueManager.Instance != null)
                    {
                        DialogueManager.Instance.StartDialogue(tutorialNavigationDialogueID);
                    }
                }
            }
            return;
        }

        if (hasCompletedTutorial && !isInTutorial)
        {
            if (Input.GetKeyDown(KeyCode.K))
            {
                ToggleWindow();
            }
        }
    }

    private void HandleKeyEvent(string eventType, string keyString)
    {
        if (eventType == "KEY_PRESSED" && keyString == "K" && !hasCompletedTutorial)
        {
            StartSkillBookTutorial();
        }
    }

    public void StartSkillBookTutorial()
    {
        isInTutorial = true;
        currentPhase = TutorialPhase.Opening;
        canUseNavigation = false;
        OpenWindow();

        SetNavigationEnabled(false);

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(tutorialOpenDialogueID);
        }
    }

    private void HandleLineDisplayed(string lineID)
    {
        if (lineID == arrowActivationLineID && currentPhase == TutorialPhase.Opening)
        {
            if (rightArrowObject != null)
            {
                rightArrowObject.SetActive(true);
            }

            leftArrowButton.interactable = false;
            rightArrowButton.interactable = false;

            StartArrowHighlight();
            Debug.Log("[SkillBookController] 화살표 시각적 활성화 (아직 사용 불가)");
        }
    }

    private void SetNavigationEnabled(bool enabled)
    {
        leftArrowButton.interactable = enabled;
        rightArrowButton.interactable = enabled;

        if (leftArrowObject != null)
        {
            leftArrowObject.SetActive(enabled);
        }
        if (rightArrowObject != null)
        {
            rightArrowObject.SetActive(enabled);
        }
    }

    private bool IsGestureRecognizing()
    {
        if (holisticJsonSender == null) return false;
        return holisticJsonSender.IsCollectingGesture();
    }

    private void HandleDialogueStarted(string eventType, string dialogueId)
    {
        if (isWindowOpen && !isInTutorial)
        {
            CloseWindowWithoutStateChange();
        }
    }

    private void HandleDialogueFinished(string eventType, string dialogueId)
    {
        if (dialogueId == tutorialOpenDialogueID && isInTutorial)
        {
            currentPhase = TutorialPhase.ArrowEnabled;
            canUseNavigation = true;
            UpdateArrowButtons();
            Debug.Log("[SkillBookController] 화살표 사용 가능!");
        }
        else if (dialogueId == tutorialNavigationDialogueID && isInTutorial)
        {
            StopArrowHighlight();
            CompleteTutorial();

            if (tutorialTimeoutCoroutine != null)
            {
                StopCoroutine(tutorialTimeoutCoroutine);
            }
            tutorialTimeoutCoroutine = StartCoroutine(TutorialReminderTimeout());
        }
        else if (dialogueId == tutorialReminderDialogueID)
        {
            if (isWindowOpen)
            {
                closedByReminder = true;
                CloseWindow();
            }

            if (tutorialTimeoutCoroutine != null)
            {
                StopCoroutine(tutorialTimeoutCoroutine);
                tutorialTimeoutCoroutine = null;
            }

            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue("library_tuna_tutorial");
            }
        }
        else if (dialogueId == doodleDialogueID && isShowingSpecialPage)
        {
            HideSpecialPage();
        }
    }

    private void CompleteTutorial()
    {
        isInTutorial = false;
        currentPhase = TutorialPhase.Completed;
        hasCompletedTutorial = true;
        PlayerPrefs.SetInt("HasCompletedSkillBookTutorial", 1);
        PlayerPrefs.Save();
        GameEventManager.Instance.TriggerCustomEvent("SKILLBOOK_TUTORIAL_COMPLETED");
    }

    private IEnumerator TutorialReminderTimeout()
    {
        yield return new WaitForSeconds(tutorialTimeoutDuration);

        if (isWindowOpen && hasCompletedTutorial)
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(tutorialReminderDialogueID);
            }
        }

        tutorialTimeoutCoroutine = null;
    }

    private void StartArrowHighlight()
    {
        if (rightArrowObject != null)
        {
            rightArrowObject.transform.DOScale(1.2f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }
    }

    private void StopArrowHighlight()
    {
        if (rightArrowObject != null)
        {
            rightArrowObject.transform.DOKill();
            rightArrowObject.transform.localScale = Vector3.one;
        }

        if (leftArrowObject != null)
        {
            leftArrowObject.transform.DOKill();
            leftArrowObject.transform.localScale = Vector3.one;
        }
    }

    void ToggleWindow()
    {
        if (isWindowOpen)
        {
            CloseWindow();
        }
        else
        {
            OpenWindow();
        }
    }

    void OpenWindow()
    {
        skillWindow.SetActive(true);
        isWindowOpen = true;

        if (GameManager.Instance.CurrentState != GameState.Battle)
        {
            GameManager.Instance.ChangeState(GameState.Menu);
        }

        UpdatePage();
    }

    void CloseWindow()
    {
        if (tutorialTimeoutCoroutine != null)
        {
            StopCoroutine(tutorialTimeoutCoroutine);
            tutorialTimeoutCoroutine = null;
        }

        StopArrowHighlight();

        skillWindow.SetActive(false);
        isWindowOpen = false;

        if (GameManager.Instance.CurrentState == GameState.Menu)
        {
            GameManager.Instance.ChangeState(GameState.Field);
        }

        if (hasCompletedTutorial &&
            GameDataManager.Instance != null &&
            GameDataManager.Instance.CurrentStoryID == "wait_for_skill_tutorial")
        {
            Debug.Log("[SkillBookController] 튜토리얼 완료 후 스킬북 닫음 - SKILLBOOK_CLOSED 이벤트 발생");
            GameEventManager.Instance.TriggerSkillBookClosed();
        }

        closedByReminder = false;
    }

    void CloseWindowWithoutStateChange()
    {
        skillWindow.SetActive(false);
        isWindowOpen = false;
        isShowingSpecialPage = false;

        if (doodlePage != null)
        {
            doodlePage.SetActive(false);
        }
    }

    void NextPage()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue())
        {
            return;
        }

        if (isShowingSpecialPage || !canUseNavigation) return;

        int maxPage = (allSkills.Length - 1) / 4;

        if (currentPageIndex < maxPage)
        {
            PlayPageFlipSound();

            if (currentPageIndex == 0 && !hasSeenDoodlePage)
            {
                ShowSpecialPage();
            }
            else
            {
                currentPageIndex++;
                UpdatePageContent();
            }

            if (currentPhase == TutorialPhase.ArrowEnabled)
            {
                OnArrowUsedDuringTutorial();
            }
        }
    }

    void PreviousPage()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsInDialogue())
        {
            return;
        }

        if (isShowingSpecialPage || !canUseNavigation) return;

        if (currentPageIndex > 0)
        {
            PlayPageFlipSound();
            currentPageIndex--;
            UpdatePageContent();

            if (currentPhase == TutorialPhase.ArrowEnabled)
            {
                OnArrowUsedDuringTutorial();
            }
        }
    }

    private void OnArrowUsedDuringTutorial()
    {
        currentPhase = TutorialPhase.WaitingForClose;
        StopArrowHighlight();

        isWaitingForKPresses = true;
        kPressCount = 0;
        Debug.Log("[SkillBookController] 이제 K키를 3번 눌러서 닫아보세요!");
    }

    void ShowSpecialPage()
    {
        if (doodlePage == null)
        {
            Debug.LogError("[SkillBookController] doodlePage가 null입니다!");
            return;
        }

        isShowingSpecialPage = true;

        pageGroup.gameObject.SetActive(false);
        doodlePage.SetActive(true);

        if (leftArrowObject != null) leftArrowObject.SetActive(false);
        if (rightArrowObject != null) rightArrowObject.SetActive(false);

        if (GameDataManager.Instance != null &&
            GameDataManager.Instance.HasFinishedDialogue(doodleDialogueID))
        {
            Debug.Log("[SkillBookController] 두들 대화를 이미 봤으므로 바로 다음 페이지로");
            HideSpecialPage();
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PauseBGM();

            if (doodleSound != null)
            {
                AudioManager.Instance.PlaySFX(doodleSound, 0.5f);
            }
        }

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(doodleDialogueID);
        }
    }

    void HideSpecialPage()
    {
        if (doodlePage == null) return;

        isShowingSpecialPage = false;
        doodlePage.SetActive(false);
        pageGroup.gameObject.SetActive(true);

        currentPageIndex = 1;
        UpdatePageContent();

        hasSeenDoodlePage = true;
        PlayerPrefs.SetInt("HasSeenDoodlePage", 1);
        PlayerPrefs.Save();
        Debug.Log("[SkillBookController] 두들 페이지 확인 완료 - 저장됨");

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ResumeBGM();
        }
    }

    void PlayPageFlipSound()
    {
        if (pageFlipSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(pageFlipSound, 0.3f);
        }
    }

    void UpdatePageContent()
    {
        int startIndex = currentPageIndex * 4;

        for (int i = 0; i < 4; i++)
        {
            int skillIndex = startIndex + i;

            if (skillIndex < allSkills.Length)
            {
                allSlots[i].skillNameText.text = allSkills[skillIndex].skillName;
                allSlots[i].descriptionText.text = allSkills[skillIndex].description;
                allSlots[i].skillImage.sprite = allSkills[skillIndex].skillImage;
                allSlots[i].skillImage.enabled = true;

                allSlots[i].skillImage.transform.parent.gameObject.SetActive(true);
            }
            else
            {
                allSlots[i].skillNameText.text = "";
                allSlots[i].descriptionText.text = "";
                allSlots[i].skillImage.enabled = false;

                allSlots[i].skillImage.transform.parent.gameObject.SetActive(false);
            }
        }

        UpdateArrowButtons();
    }

    void UpdatePage()
    {
        UpdatePageContent();
    }

    void UpdateArrowButtons()
    {
        if (isInTutorial && !canUseNavigation)
        {
            SetNavigationEnabled(false);
            return;
        }

        int maxPage = (allSkills.Length - 1) / 4;

        leftArrowButton.interactable = (currentPageIndex > 0);
        rightArrowButton.interactable = (currentPageIndex < maxPage);

        if (leftArrowObject != null)
        {
            leftArrowObject.SetActive(currentPageIndex > 0);
        }

        if (rightArrowObject != null)
        {
            rightArrowObject.SetActive(currentPageIndex < maxPage);
        }
    }
}