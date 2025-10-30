using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Random = UnityEngine.Random;
using DG.Tweening;
using UnityEngine.SceneManagement;

[System.Serializable]
public class DialogueLine
{
    public string LineID { get; set; }
    public string DialogueSetID { get; set; }
    public string SpeakerName { get; set; }
    public string Text { get; set; }
    public bool IsPlayer { get; set; }
    public string NextLineID { get; set; }
    public int Order { get; set; }
    public string CustomSpeaker { get; set; }
    public string GroupID { get; set; }
}

[System.Serializable]
public class DialogueSet
{
    public string DialogueSetID { get; set; }
    public string NPCName { get; set; }
    public string StartLineID { get; set; }
    public bool IsRepeatable { get; set; }
    public string Description { get; set; }
    public string PortraitPath { get; set; }
}

[System.Serializable]
public class DialogueCondition
{
    public string DialogueSetID { get; set; }
    public string ConditionType { get; set; }
    public string Target { get; set; }
    public string Value { get; set; }
    public string Description { get; set; }
}

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    private GameObject dialoguePanel;
    private TextMeshProUGUI dialogueText;
    private TextMeshProUGUI speakerNameText;
    private ContinueIndicator continueIndicator;
    private RectTransform continueIndicatorRect;
    private Image leftPortrait;
    private CanvasGroup dialogueCanvasGroup;
    public event System.Action<string> OnLineDisplayed;

    [Header("외부 데이터베이스 참조")]
    [SerializeField] private CharacterDatabase characterDatabase;

    [Header("UI 프리팹")]
    [SerializeField] private GameObject dialogueUIPrefab;

    private Canvas panelCanvas;
    private Camera cachedMainCam;

    [Header("CSV 파일 경로")]
    [SerializeField] private string dialogueLinesPath = "DialogueData/DialogueLines";
    [SerializeField] private string dialogueSetsPath = "DialogueData/DialogueSets";
    [SerializeField] private string dialogueConditionsPath = "DialogueData/DialogueConditions";

    [Header("타이핑 효과")]
    public float baseTypingSpeed = 0.05f;
    public float typingSpeedVariance = 0.02f;
    public float punctuationPause = 0.3f;
    public bool skipTypingOnInput = true;

    [Header("타이핑 사운드 설정")]
    public float soundInterval = 0.1f;

    private Dictionary<string, DialogueLine> dialogueLines = new Dictionary<string, DialogueLine>();
    private Dictionary<string, DialogueSet> dialogueSets = new Dictionary<string, DialogueSet>();
    private Dictionary<string, List<DialogueCondition>> dialogueConditions = new Dictionary<string, List<DialogueCondition>>();
    private Dictionary<string, Func<string, string, bool>> conditionCheckers;
    private Dictionary<string, int> dialogueSetUsageCount = new Dictionary<string, int>();

    private string currentDialogueSetID;
    private string currentLineID;
    private bool isInDialogue = false;
    private bool isTyping = false;
    private bool isPaused = false;
    private string fullText = "";
    private Coroutine typingCoroutine;
    private string lastSpeakerName = "";

    private AudioClip currentTypingSound;
    private float soundTimer;

    // DOTween Sequence 관리용 변수 추가
    private Sequence portraitSequence;

    void Update()
    {
        if (isPaused || !isInDialogue) return;

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
        {
            if (isTyping && skipTypingOnInput)
            {
                CompleteTyping();
            }
            else
            {
                if (string.IsNullOrEmpty(dialogueLines[currentLineID].NextLineID))
                {
                    EndDialogue();
                }
                else
                {
                    NextLine();
                }
            }
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (dialogueUIPrefab != null)
            {
                dialoguePanel = Instantiate(dialogueUIPrefab);
                panelCanvas = dialoguePanel.GetComponent<Canvas>();
                DontDestroyOnLoad(dialoguePanel);

                var uiRefs = dialoguePanel.GetComponent<DialogueUIRefs>();
                if (uiRefs != null)
                {
                    dialogueText = uiRefs.dialogueText;
                    speakerNameText = uiRefs.speakerNameText;
                    leftPortrait = uiRefs.leftPortrait;
                    continueIndicator = uiRefs.continueIndicator;
                    dialogueCanvasGroup = uiRefs.dialogueCanvasGroup;
                    if (continueIndicator != null)
                    {
                        continueIndicatorRect = continueIndicator.GetComponent<RectTransform>();
                    }
                }
                else
                {
                    Debug.LogError("[DialogueManager] dialogueUIPrefab에 DialogueUIRefs 컴포넌트가 없습니다!");
                }

                dialoguePanel.SetActive(false);
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (dialogueLines.Count == 0)
        {
            LoadDialogueData();
            InitializeConditionCheckers();
            GameEventManager.Instance.OnGameStateChanged += HandleGameStateChange;
        }
    }

    void OnEnable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnLockedDoorInteracted += HandleLockedDoorInteraction;
        }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnLockedDoorInteracted -= HandleLockedDoorInteraction;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        // 모든 DOTween 애니메이션 정리
        if (leftPortrait != null)
        {
            leftPortrait.DOKill();
        }
        if (dialogueCanvasGroup != null)
        {
            dialogueCanvasGroup.DOKill();
        }
        if (portraitSequence != null && portraitSequence.IsActive())
        {
            portraitSequence.Kill();
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(BindCanvasNextFrame());
    }

    IEnumerator BindCanvasNextFrame()
    {
        yield return null;
        EnsureCanvasBound();
    }

    void EnsureCanvasBound()
    {
        if (!panelCanvas) return;

        bool settingsOpen = SettingsManager.Instance != null &&
                            SettingsManager.Instance.IsOpen;

        if (settingsOpen)
        {
            if (panelCanvas.renderMode != RenderMode.ScreenSpaceCamera)
                panelCanvas.renderMode = RenderMode.ScreenSpaceCamera;

            var main = Camera.main;
            if (main && (cachedMainCam != main || panelCanvas.worldCamera != main))
            {
                panelCanvas.worldCamera = main;
                panelCanvas.sortingOrder = 10000;
                cachedMainCam = main;
            }
        }
        else
        {
            if (panelCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                panelCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }

    public void RefreshCanvasMode()
    {
        EnsureCanvasBound();
    }

    private List<T> LoadData<T>(string path)
    {
        TextAsset csvFile = Resources.Load<TextAsset>(path);
        if (csvFile == null)
        {
            Debug.LogError($"[DialogueManager] CSV 파일을 찾을 수 없습니다: {path}");
            return new List<T>();
        }
        try
        {
            using (var reader = new StringReader(csvFile.text))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Context.Configuration.BadDataFound = context =>
                {
                    Debug.LogWarning($"[CsvHelper] 잘못된 데이터 발견 (행 {csv.Context.Parser.Row}): '{context.RawRecord}'");
                };
                return csv.GetRecords<T>().ToList();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DialogueManager] CSV 파일 파싱 실패 ({path}): {ex.Message}");
            return new List<T>();
        }
    }

    private void InitializeDialogueSystem()
    {
        if (dialoguePanel != null)
        {
            dialogueCanvasGroup = dialoguePanel.GetComponent<CanvasGroup>();
            if (dialogueCanvasGroup == null)
            {
                dialogueCanvasGroup = dialoguePanel.AddComponent<CanvasGroup>();
            }
            if (continueIndicator != null)
            {
                continueIndicatorRect = continueIndicator.GetComponent<RectTransform>();
            }
        }
        LoadDialogueData();
        InitializeConditionCheckers();
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    private void HandleLockedDoorInteraction(string doorID)
    {
        string dialogueID = $"{doorID}_locked";
        StartDialogue(dialogueID);
    }

    private void LoadDialogueData()
    {
        try
        {
            dialogueLines = LoadData<DialogueLine>(dialogueLinesPath).ToDictionary(line => line.LineID);
            dialogueSets = LoadData<DialogueSet>(dialogueSetsPath).ToDictionary(set => set.DialogueSetID);

            dialogueConditions.Clear();
            var conditionsList = LoadData<DialogueCondition>(dialogueConditionsPath);
            foreach (var condition in conditionsList)
            {
                if (!dialogueConditions.ContainsKey(condition.DialogueSetID))
                    dialogueConditions[condition.DialogueSetID] = new List<DialogueCondition>();
                dialogueConditions[condition.DialogueSetID].Add(condition);
            }

            Debug.Log($"[DialogueManager] 데이터 로드 완료. Lines: {dialogueLines.Count}, Sets: {dialogueSets.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DialogueManager] 데이터 로드 실패: {e.Message}");
        }
    }

    private void InitializeConditionCheckers()
    {
        conditionCheckers = new Dictionary<string, Func<string, string, bool>>
        {
            ["item_obtained"] = (target, value) => GameDataManager.Instance.HasItem(target) == (value != "0"),
            ["item_inspected"] = (target, value) => GameDataManager.Instance.HasInspectedItem(target) == (value != "0"),
            ["dialogue_finished"] = (target, value) => GameDataManager.Instance.HasFinishedDialogue(target) == (value != "0")
        };
    }

    public void StartDialogue(string dialogueSetID)
    {
        if (!dialogueSets.ContainsKey(dialogueSetID))
        {
            Debug.LogError($"[DialogueManager] 존재하지 않는 대화 세트: {dialogueSetID}");
            return;
        }

        if (!dialogueSets[dialogueSetID].IsRepeatable && GameDataManager.Instance.HasFinishedDialogue(dialogueSetID))
        {
            Debug.Log($"[DialogueManager] 이미 완료된 대화입니다: {dialogueSetID}");
            return;
        }

        if (!CanStartDialogue(dialogueSetID))
        {
            Debug.Log($"[DialogueManager] 대화 시작 조건 미충족: {dialogueSetID}");
            return;
        }

        lastSpeakerName = "";
        isInDialogue = true;
        currentDialogueSetID = dialogueSetID;
        currentLineID = dialogueSets[dialogueSetID].StartLineID;

        GameEventManager.Instance.TriggerDialogueStarted(dialogueSetID);

        BeginDialogueSequence();
    }

    public void StartRandomDialogueFromSet(string dialogueSetID)
    {
        var linesInSet = dialogueLines.Values.Where(line => line.DialogueSetID == dialogueSetID).ToList();
        if (linesInSet.Count == 0)
        {
            Debug.LogError($"[DialogueManager] '{dialogueSetID}'에 해당하는 대사를 찾을 수 없습니다.");
            return;
        }

        if (!dialogueSets.ContainsKey(dialogueSetID))
        {
            Debug.LogError($"[DialogueManager] 존재하지 않는 대화 세트: {dialogueSetID}");
            return;
        }

        if (isInDialogue)
        {
            Debug.LogWarning($"[DialogueManager] 이미 대화가 진행 중입니다.");
            return;
        }

        var availableGroupIDs = linesInSet
            .Select(line => line.GroupID)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (availableGroupIDs.Count == 0)
        {
            Debug.LogError($"[DialogueManager] '{dialogueSetID}'에 유효한 GroupID가 없습니다.");
            StartDialogue(dialogueSetID);
            return;
        }

        if (!dialogueSetUsageCount.ContainsKey(dialogueSetID))
        {
            dialogueSetUsageCount[dialogueSetID] = 0;
        }

        int usageCount = dialogueSetUsageCount[dialogueSetID];
        string selectedGroupID;

        if (usageCount < availableGroupIDs.Count)
        {
            selectedGroupID = availableGroupIDs[usageCount];
            Debug.Log($"[DialogueManager] 순차 모드: {selectedGroupID} ({usageCount + 1}번째)");
        }
        else
        {
            selectedGroupID = availableGroupIDs[UnityEngine.Random.Range(0, availableGroupIDs.Count)];
            Debug.Log($"[DialogueManager] 랜덤 모드: {selectedGroupID}");
        }

        var startingLine = linesInSet
            .Where(line => line.GroupID == selectedGroupID)
            .OrderBy(line => line.Order)
            .FirstOrDefault();

        if (startingLine == null)
        {
            Debug.LogError($"[DialogueManager] 그룹 '{selectedGroupID}'에서 시작 대사를 찾을 수 없습니다.");
            return;
        }

        dialogueSetUsageCount[dialogueSetID]++;

        lastSpeakerName = "";
        isInDialogue = true;
        currentDialogueSetID = dialogueSetID;
        currentLineID = startingLine.LineID;

        GameEventManager.Instance.TriggerDialogueStarted(dialogueSetID);

        BeginDialogueSequence();
    }

    private bool CanStartDialogue(string dialogueSetID)
    {
        if (!dialogueConditions.ContainsKey(dialogueSetID)) return true;

        foreach (var condition in dialogueConditions[dialogueSetID])
        {
            if (!conditionCheckers.ContainsKey(condition.ConditionType) ||
                !conditionCheckers[condition.ConditionType](condition.Target, condition.Value))
            {
                return false;
            }
        }
        return true;
    }

    private void DisplayCurrentLine()
    {
        var line = dialogueLines[currentLineID];
        fullText = line.Text;

        OnLineDisplayed?.Invoke(currentLineID);

        string speakerName = string.IsNullOrEmpty(line.CustomSpeaker) ? line.SpeakerName : line.CustomSpeaker;
        speakerNameText.text = speakerName;

        // 이전 애니메이션 정리
        if (portraitSequence != null && portraitSequence.IsActive())
        {
            portraitSequence.Kill(complete: true);
        }

        // ⭐ 배틀 씬에서 튜나 초상화는 표시하지 않음
        bool isBattleScene = SceneManager.GetActiveScene().name == "BattleScene";
        bool isTuna = speakerName == "튜나";

        if (isBattleScene && isTuna)
        {
            // 배틀 씬에서 튜나일 때는 초상화 숨기기
            leftPortrait.DOKill(complete: true);
            if (leftPortrait.gameObject.activeSelf)
            {
                leftPortrait.gameObject.SetActive(false);
            }

            // 타이핑 사운드는 가져오기
            if (characterDatabase != null)
            {
                currentTypingSound = characterDatabase.GetTypingSound(speakerName);
            }
        }
        else if (!string.Equals(speakerName, lastSpeakerName))
        {
            // ⭐ 기존 초상화 표시 로직 (배틀 씬의 튜나가 아닐 때)
            if (characterDatabase != null)
            {
                currentTypingSound = characterDatabase.GetTypingSound(speakerName);
                Sprite portrait = characterDatabase.GetPortrait(speakerName);

                // 기존 애니메이션 완전히 정리
                leftPortrait.DOKill(complete: true);

                if (portrait != null)
                {
                    leftPortrait.sprite = portrait;

                    if (!leftPortrait.gameObject.activeSelf)
                    {
                        leftPortrait.gameObject.SetActive(true);
                    }

                    // 알파값을 명시적으로 1로 설정
                    Color c = leftPortrait.color;
                    c.a = 1f;
                    leftPortrait.color = c;

                    // 선택적 페이드인 효과 (필요한 경우)
                    portraitSequence = DOTween.Sequence().SetUpdate(true);
                    portraitSequence.Append(leftPortrait.DOFade(0f, 0f).From());
                    portraitSequence.Append(leftPortrait.DOFade(1f, 0.3f));
                }
                else
                {
                    if (leftPortrait.gameObject.activeSelf)
                    {
                        leftPortrait.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() => {
                            leftPortrait.gameObject.SetActive(false);
                            // 비활성화 후 알파값 리셋
                            Color c = leftPortrait.color;
                            c.a = 1f;
                            leftPortrait.color = c;
                        });
                    }
                }
            }
        }
        else
        {
            // 같은 화자일 때도 알파값 확인
            if (leftPortrait != null && leftPortrait.gameObject.activeSelf)
            {
                Color c = leftPortrait.color;
                if (c.a < 1f)
                {
                    // 투명 상태면 즉시 불투명하게
                    leftPortrait.DOKill(complete: true);
                    c.a = 1f;
                    leftPortrait.color = c;
                }
            }
        }

        lastSpeakerName = speakerName;
        StartTyping(fullText, currentTypingSound);
    }

    private void NextLine()
    {
        currentLineID = dialogueLines[currentLineID].NextLineID;
        DisplayCurrentLine();
    }

    private void EndDialogue()
    {
        isInDialogue = false;
        lastSpeakerName = "";

        if (continueIndicator != null)
        {
            continueIndicator.Hide();
        }

        // 모든 portrait 애니메이션 즉시 완료
        if (portraitSequence != null && portraitSequence.IsActive())
        {
            portraitSequence.Kill(complete: true);
        }
        leftPortrait.DOKill(complete: true);

        dialogueCanvasGroup.DOKill();
        dialogueCanvasGroup.DOFade(0f, 0.3f).SetUpdate(true).OnComplete(() =>
        {
            dialoguePanel.SetActive(false);
            if (leftPortrait != null)
            {
                leftPortrait.sprite = null;
                // 알파값 리셋
                Color c = leftPortrait.color;
                c.a = 1f;
                leftPortrait.color = c;
                leftPortrait.gameObject.SetActive(false);
            }
        });

        // 현재 대화 라인 ID를 저장 (튜토리얼 추적용)
        string endedLineID = currentLineID;

        GameDataManager.Instance.MarkDialogueAsFinished(currentDialogueSetID);
        GameDataManager.Instance.SaveState();

        // 실제 종료된 라인 ID도 함께 전달 (또는 별도 이벤트)
        GameEventManager.Instance.TriggerDialogueFinished(currentDialogueSetID);

        Debug.Log($"[DialogueManager] 대화 종료: {currentDialogueSetID} (마지막 라인: {endedLineID})");
    }

    private void BeginDialogueSequence()
    {
        if (dialogueCanvasGroup == null)
        {
            Debug.LogError("[DialogueManager] 대화창 UI가 준비되지 않았습니다. Prefab이 제대로 연결되었는지 확인하십시오.");
            isInDialogue = false;
            return;
        }

        if (dialogueText != null)
        {
            dialogueText.text = "";
        }

        if (leftPortrait != null)
        {
            leftPortrait.gameObject.SetActive(false);
            // 시작 시 알파값 리셋
            Color c = leftPortrait.color;
            c.a = 1f;
            leftPortrait.color = c;
        }

        dialogueCanvasGroup.alpha = 0f;
        dialoguePanel.SetActive(true);

        DisplayCurrentLine();

        dialogueCanvasGroup.DOKill();
        dialogueCanvasGroup.DOFade(1f, 0.3f).SetUpdate(true);
    }

    public bool IsInDialogue()
    {
        return isInDialogue;
    }

    void StartTyping(string text, AudioClip typingSound)
    {
        if (continueIndicator != null) continueIndicator.Hide();
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        isTyping = true;
        typingCoroutine = StartCoroutine(TypeText(text, typingSound));
    }

    void CompleteTyping()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        isTyping = false;
        dialogueText.text = fullText;

        if (continueIndicator != null)
        {
            continueIndicator.Hide();
            StartCoroutine(PositionIndicatorAtEndOfText());
        }
    }

    private IEnumerator PositionIndicatorAtEndOfText()
    {
        yield return new WaitForEndOfFrame();

        if (dialogueText == null || continueIndicator == null || continueIndicatorRect == null)
        {
            yield break;
        }

        if (dialogueText.text.Length > 0)
        {
            TMP_TextInfo textInfo = dialogueText.textInfo;

            if (textInfo == null || textInfo.characterCount == 0)
            {
                yield break;
            }

            int lastCharacterIndex = textInfo.characterCount - 1;

            while (lastCharacterIndex >= 0 && !textInfo.characterInfo[lastCharacterIndex].isVisible)
            {
                lastCharacterIndex--;
            }

            if (lastCharacterIndex >= 0)
            {
                TMP_CharacterInfo lastCharInfo = textInfo.characterInfo[lastCharacterIndex];
                Vector3 lastCharPosition = lastCharInfo.topRight;
                float yOffset = lastCharInfo.ascender - (lastCharInfo.ascender - lastCharInfo.descender) / 2f;

                continueIndicatorRect.anchoredPosition = new Vector2(lastCharPosition.x + 36f, yOffset);
            }
            else
            {
                continueIndicatorRect.anchoredPosition = Vector2.zero;
            }

            continueIndicator.Show();
        }
        else
        {
            continueIndicatorRect.anchoredPosition = Vector2.zero;
            continueIndicator.Show();
        }
    }

    IEnumerator TypeText(string text, AudioClip typingSound)
    {
        dialogueText.text = "";
        soundTimer = 0f;

        foreach (char letter in text.ToCharArray())
        {
            dialogueText.text += letter;

            if (letter == ',' || letter == '.' || letter == '?' || letter == '!' || letter == '(' || letter == ')' || letter == '…')
            {
                yield return new WaitForSeconds(punctuationPause);
            }
            else
            {
                soundTimer += baseTypingSpeed;

                if (AudioManager.Instance != null && !char.IsWhiteSpace(letter) && typingSound != null && soundTimer >= soundInterval)
                {
                    soundTimer = 0f;
                    AudioManager.Instance.PlayTypingSound(typingSound);
                }

                float currentTypingSpeed = Random.Range(
                    baseTypingSpeed - typingSpeedVariance,
                    baseTypingSpeed + typingSpeedVariance
                );
                yield return new WaitForSeconds(Mathf.Max(currentTypingSpeed, 0.01f));
            }
        }
        CompleteTyping();
    }

    public void ForceEndDialogue()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        // 모든 애니메이션 정리
        if (portraitSequence != null && portraitSequence.IsActive())
        {
            portraitSequence.Kill(complete: true);
        }
        leftPortrait.DOKill(complete: true);
        dialogueCanvasGroup.DOKill(complete: true);

        isTyping = false;
        isInDialogue = false;
        lastSpeakerName = "";

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            // 강제 종료 시 알파값 리셋
            if (leftPortrait != null)
            {
                Color c = leftPortrait.color;
                c.a = 1f;
                leftPortrait.color = c;
                leftPortrait.sprite = null;
                leftPortrait.gameObject.SetActive(false);
            }
        }
        Debug.Log("[DialogueManager] 대화 강제 종료됨.");
    }

    private void HandleGameStateChange(GameState newState)
    {
        isPaused = (newState == GameState.Menu);
        if (isInDialogue)
        {
            if (newState == GameState.Loading)
            {
                ForceEndDialogue();
            }
            else
            {
                dialoguePanel.SetActive(true);
            }
        }
    }

    public void StartDialogueFromLine(string dialogueSetID, string startLineID)
    {
        if (!dialogueSets.ContainsKey(dialogueSetID))
        {
            Debug.LogError($"[DialogueManager] 존재하지 않는 대화 세트: {dialogueSetID}");
            return;
        }

        if (!dialogueLines.ContainsKey(startLineID))
        {
            Debug.LogError($"[DialogueManager] 존재하지 않는 대화 라인: {startLineID}");
            return;
        }

        // 대화 라인이 해당 세트에 속하는지 확인
        if (dialogueLines[startLineID].DialogueSetID != dialogueSetID)
        {
            Debug.LogError($"[DialogueManager] 라인 '{startLineID}'는 세트 '{dialogueSetID}'에 속하지 않습니다.");
            return;
        }

        lastSpeakerName = "";
        isInDialogue = true;
        currentDialogueSetID = dialogueSetID;
        currentLineID = startLineID; // 지정된 라인부터 시작

        // 중요: 여기서 실제 LineID를 이벤트로 전달
        GameEventManager.Instance.TriggerDialogueStarted(startLineID);  // dialogueSetID 대신 startLineID 전달
        BeginDialogueSequence();
    }
}