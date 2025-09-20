using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;
using TMPro;

[System.Serializable]
public class DialogueLine
{
    public string lineID;
    public string dialogueSetID;
    public string speakerName;
    public string text;
    public bool isPlayer;
    public string nextLineID;
    public int order;
    public string customSpeaker;

    public DialogueLine() { }

    public DialogueLine(string lineID, string dialogueSetID, string speakerName, string text,
                       bool isPlayer, string nextLineID, int order, string customSpeaker)
    {
        this.lineID = lineID;
        this.dialogueSetID = dialogueSetID;
        this.speakerName = speakerName;
        this.text = text;
        this.isPlayer = isPlayer;
        this.nextLineID = nextLineID;
        this.order = order;
        this.customSpeaker = customSpeaker;
    }
}

[System.Serializable]
public class DialogueSet
{
    public string dialogueSetID;
    public string npcName;
    public string startLineID;
    public bool isRepeatable;
    public string description;
    public string portraitPath;

    public DialogueSet() { }

    public DialogueSet(string dialogueSetID, string npcName, string startLineID,
                      bool isRepeatable, string description, string portraitPath)
    {
        this.dialogueSetID = dialogueSetID;
        this.npcName = npcName;
        this.startLineID = startLineID;
        this.isRepeatable = isRepeatable;
        this.description = description;
        this.portraitPath = portraitPath;
    }
}

[System.Serializable]
public class DialogueCondition
{
    public string dialogueSetID;
    public string conditionType;
    public string target;
    public string value;
    public string description;

    public DialogueCondition() { }

    public DialogueCondition(string dialogueSetID, string conditionType, string target,
                           string value, string description)
    {
        this.dialogueSetID = dialogueSetID;
        this.conditionType = conditionType;
        this.target = target;
        this.value = value;
        this.description = description;
    }
}

// ===== CSV 파싱 유틸리티 =====
public static class DialogueCSVParser
{
    public static List<DialogueLine> ParseDialogueLines(string csvText)
    {
        var lines = new List<DialogueLine>();

        try
        {
            string[] rows = csvText.Split('\n');

            for (int i = 1; i < rows.Length; i++)
            {
                string row = rows[i].Trim();
                if (string.IsNullOrEmpty(row)) continue;

                string[] values = ParseCSVLine(row);
                if (values.Length >= 7)
                {
                    string lineID = values[0].Trim();

                    // 빈 LineID 건너뛰기
                    if (string.IsNullOrEmpty(lineID))
                    {
                        Debug.LogWarning($"빈 LineID 발견, 건너뜀: 줄 {i}");
                        continue;
                    }

                    var line = new DialogueLine(
                        lineID, // LineID
                        values[1].Trim(), // DialogueSetID
                        values[2].Trim(), // SpeakerName
                        values[3].Trim(), // Text
                        ParseBool(values[4]), // IsPlayer
                        values[5].Trim(), // NextLineID
                        ParseInt(values[6]), // Order
                        values.Length >= 8 ? values[7].Trim() : ""  // CustomSpeaker
                    );
                    lines.Add(line);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"DialogueLines CSV 파싱 실패: {e.Message}");
        }

        return lines;
    }

    public static List<DialogueSet> ParseDialogueSets(string csvText)
    {
        var sets = new List<DialogueSet>();

        try
        {
            string[] rows = csvText.Split('\n');

            for (int i = 1; i < rows.Length; i++)
            {
                string row = rows[i].Trim();
                if (string.IsNullOrEmpty(row)) continue;

                string[] values = ParseCSVLine(row);
                if (values.Length >= 6)
                {
                    var set = new DialogueSet(
                        values[0].Trim(), // DialogueSetID
                        values[1].Trim(), // NPCName
                        values[2].Trim(), // StartLineID
                        ParseBool(values[3]), // IsRepeatable
                        values[4].Trim(), // Description
                        values[5].Trim()  // PortraitPath
                    );
                    sets.Add(set);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"DialogueSets CSV 파싱 실패: {e.Message}");
        }

        return sets;
    }

    public static List<DialogueCondition> ParseDialogueConditions(string csvText)
    {
        var conditions = new List<DialogueCondition>();

        try
        {
            string[] rows = csvText.Split('\n');

            for (int i = 1; i < rows.Length; i++)
            {
                string row = rows[i].Trim();
                if (string.IsNullOrEmpty(row)) continue;

                string[] values = ParseCSVLine(row);
                if (values.Length >= 5)
                {
                    var condition = new DialogueCondition(
                        values[0].Trim(), // DialogueSetID
                        values[1].Trim(), // ConditionType
                        values[2].Trim(), // Target
                        values[3].Trim(), // Value
                        values[4].Trim()  // Description
                    );
                    conditions.Add(condition);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"DialogueConditions CSV 파싱 실패: {e.Message}");
        }

        return conditions;
    }

    private static string[] ParseCSVLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentField);
                currentField = "";
            }
            else
            {
                currentField += c;
            }
        }

        result.Add(currentField);
        return result.ToArray();
    }

    private static bool ParseBool(string value)
    {
        return value.Trim().ToUpper() == "TRUE";
    }

    private static int ParseInt(string value)
    {
        if (int.TryParse(value.Trim(), out int result))
            return result;
        return 0;
    }
}

// ===== 새로운 CSV 기반 DialogueManager =====
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI speakerNameText;

    [Header("Character Portraits")]
    public Image leftPortrait;  // 플레이어
    public Image rightPortrait; // NPC

    [Header("CSV 파일 경로")]
    [SerializeField] private string dialogueLinesPath = "DialogueData/DialogueLines";
    [SerializeField] private string dialogueSetsPath = "DialogueData/DialogueSets";
    [SerializeField] private string dialogueConditionsPath = "DialogueData/DialogueConditions";

    [Header("기본 초상화")]
    public Sprite defaultPlayerPortrait;
    public Sprite defaultNPCPortrait;

    [Header("디버그")]
    public bool debugMode = true;
    public bool logDialogueEvents = true;

    // 타이핑 효과, 추후 DialogueManager에서 DialogueUIManager로 분리 필요
    [Header("타이핑 효과")]
    public float typingSpeed = 0.05f; 
    public bool skipTypingOnInput = true;

    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private string fullText = "";

    // 대화 데이터
    private Dictionary<string, DialogueLine> dialogueLines = new Dictionary<string, DialogueLine>();
    private Dictionary<string, DialogueSet> dialogueSets = new Dictionary<string, DialogueSet>();
    private Dictionary<string, List<DialogueCondition>> dialogueConditions = new Dictionary<string, List<DialogueCondition>>();

    // 현재 대화 상태
    private string currentDialogueSetID;
    private string currentLineID;
    private bool isInDialogue = false;
    private bool isLastLine = false;
    private bool isShowingItemDescription = false;
    private bool blockInput = false;

    private bool advanceLocked = false;

    // 조건 체크 함수들
    private Dictionary<string, Func<string, string, bool>> conditionCheckers;

    // 초상화 캐시
    private Dictionary<string, Sprite> portraitCache = new Dictionary<string, Sprite>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeDialogueSystem();
        }
        else
        {
            Destroy(gameObject);
        }

        if (dialoguePanel == null)
            dialoguePanel = GameObject.Find("talkPanel");

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    void Start()
    {
        if (dialoguePanel == null)
        {
            GameObject foundTalkPanel = GameObject.Find("talkPanel");
            if (foundTalkPanel != null)
            {
                dialoguePanel = foundTalkPanel;
                Debug.Log("talkPanel을 자동으로 찾아서 연결했습니다!");
            }
            else
            {
                Debug.LogError("talkPanel을 찾을 수 없습니다!");
            }
        }
    }

    void Update()
    {
        if (blockInput)
        {
            blockInput = false;
            return;
        }

        if (advanceLocked) return;

        if (isInDialogue && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            if (isTyping && skipTypingOnInput)
            {
                CompleteTyping();
                return;
            }

            if (isLastLine)
            {
                EndDialogue();
            }
            else
            {
                NextLine();
            }
        }

        if (isShowingItemDescription &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            if (isTyping && skipTypingOnInput)
            {
                CompleteTyping();
                return;
            }

            HideItemDescription();
        }
    }

    void InitializeDialogueSystem()
    {
        InitializeConditionCheckers();
        LoadDialogueDataFromCSV();

        if (debugMode)
        {
            Debug.Log($"[DialogueManager] 시스템 초기화 완료");
            Debug.Log($"[DialogueManager] 대화 라인: {dialogueLines.Count}개");
            Debug.Log($"[DialogueManager] 대화 세트: {dialogueSets.Count}개");
        }
    }

    public void setAdvanceLocked(bool locked)
    {
        advanceLocked = locked;
        Debug.Log("대화창 일시정지");
    }

    // ===== CSV 데이터 로드 =====
    void LoadDialogueDataFromCSV()
    {
        LoadDialogueLines();
        LoadDialogueSets();
        LoadDialogueConditions();
    }

    void LoadDialogueLines()
    {
        try
        {
            TextAsset csvFile = Resources.Load<TextAsset>(dialogueLinesPath);
            if (csvFile == null)
            {
                Debug.LogError($"[DialogueManager] CSV 파일을 찾을 수 없습니다: {dialogueLinesPath}");
                return;
            }

            var lines = DialogueCSVParser.ParseDialogueLines(csvFile.text);
            dialogueLines.Clear();

            foreach (var line in lines)
            {
                if (dialogueLines.ContainsKey(line.lineID))
                {
                    Debug.LogError($"[DialogueManager] 중복된 LineID 발견: {line.lineID}");
                }
                else
                {
                    Debug.Log($"[DialogueManager] 라인 추가: {line.lineID}");
                }
                dialogueLines[line.lineID] = line;
            }

            if (debugMode)
                Debug.Log($"[DialogueManager] DialogueLines 로드 완료: {lines.Count}개");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DialogueManager] DialogueLines 로드 실패: {e.Message}");
        }
    }

    void LoadDialogueSets()
    {
        try
        {
            TextAsset csvFile = Resources.Load<TextAsset>(dialogueSetsPath);
            if (csvFile == null)
            {
                Debug.LogError($"[DialogueManager] CSV 파일을 찾을 수 없습니다: {dialogueSetsPath}");
                return;
            }

            var sets = DialogueCSVParser.ParseDialogueSets(csvFile.text);
            dialogueSets.Clear();

            foreach (var set in sets)
            {
                dialogueSets[set.dialogueSetID] = set;
            }

            if (debugMode)
                Debug.Log($"[DialogueManager] DialogueSets 로드 완료: {sets.Count}개");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DialogueManager] DialogueSets 로드 실패: {e.Message}");
        }
    }

    void LoadDialogueConditions()
    {
        try
        {
            TextAsset csvFile = Resources.Load<TextAsset>(dialogueConditionsPath);
            if (csvFile == null)
            {
                Debug.LogWarning($"[DialogueManager] CSV 파일을 찾을 수 없습니다: {dialogueConditionsPath} (선택사항)");
                return;
            }

            var conditions = DialogueCSVParser.ParseDialogueConditions(csvFile.text);
            dialogueConditions.Clear();

            foreach (var condition in conditions)
            {
                if (!dialogueConditions.ContainsKey(condition.dialogueSetID))
                    dialogueConditions[condition.dialogueSetID] = new List<DialogueCondition>();

                dialogueConditions[condition.dialogueSetID].Add(condition);
            }

            if (debugMode)
                Debug.Log($"[DialogueManager] DialogueConditions 로드 완료: {conditions.Count}개");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DialogueManager] DialogueConditions 로드 실패: {e.Message}");
        }
    }

    // ===== 조건 체크 시스템 =====
    void InitializeConditionCheckers()
    {
        conditionCheckers = new Dictionary<string, Func<string, string, bool>>()
        {
            ["none"] = (target, value) => true,
            ["story_reached"] = CheckStoryReached,
            ["quest_completed"] = CheckQuestCompleted,
            ["item_obtained"] = CheckItemObtained,
            ["item_inspected"] = CheckItemInspected,
            ["dialogue_finished"] = CheckDialogueFinished
        };
    }

    bool CheckStoryReached(string target, string value)
    {
        if (StoryProgressManager.Instance != null)
        {
            string currentStory = StoryProgressManager.Instance.GetCurrentStoryID();
            return currentStory == target;
        }
        return false;
    }

    bool CheckQuestCompleted(string target, string value)
    {
        return PlayerPrefs.GetInt($"Quest_{target}_Completed", 0) == 1;
    }

    bool CheckItemObtained(string target, string value)
    {
        int requiredAmount = string.IsNullOrEmpty(value) ? 1 : int.Parse(value);
        int currentAmount = PlayerPrefs.GetInt($"Item_{target}_Count", 0);
        return currentAmount >= requiredAmount;
    }

    bool CheckItemInspected(string target, string value)
    {
        int requiredAmount = string.IsNullOrEmpty(value) ? 1 : int.Parse(value);
        int inspectedCount = PlayerPrefs.GetInt($"Item_{target}_Inspected", 0);
        return inspectedCount >= requiredAmount;
    }

    bool CheckDialogueFinished(string target, string value)
    {
        return PlayerPrefs.GetInt($"Dialogue_{target}_Finished", 0) == 1;
    }

    public void StartDialogue(string dialogueSetID)
    {
        if (!dialogueSets.ContainsKey(dialogueSetID))
        {
            Debug.LogError($"[DialogueManager] 존재하지 않는 대화 세트: {dialogueSetID}");
            return;
        }

        if (!CanStartDialogue(dialogueSetID))
        {
            Debug.Log($"[DialogueManager] 대화 시작 조건 미충족: {dialogueSetID}");
            return;
        }

        var dialogueSet = dialogueSets[dialogueSetID];
        currentDialogueSetID = dialogueSetID;
        currentLineID = dialogueSet.startLineID;
        isInDialogue = true;
        isShowingItemDescription = false;

        blockInput = true;

        if (logDialogueEvents)
            Debug.Log($"[DialogueManager] 대화 시작: {dialogueSetID} - {dialogueSet.description}");

        var playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.SetCanMove(false);
        }

        if (dialoguePanel != null)
        {
            StartCoroutine(DelayedActivatePanel());
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(true);
        }

        DisplayCurrentLine();
    }

    IEnumerator DelayedActivatePanel()
    {
        yield return null;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }
    }

    public string GetDialogueText(string lineID)
    {
        if (dialogueLines.ContainsKey(lineID))
        {
            return dialogueLines[lineID].text;
        }

        return null;
    }

    public void StartDialogue(NPCDataSO npcData, int dialogueID)
    {
        Debug.LogWarning("[DialogueManager] 구 방식 호출됨. CSV 기반 StartDialogue(string) 사용을 권장합니다.");

        string dialogueSetID = $"legacy_dialogue_{dialogueID}";
        StartDialogue(dialogueSetID);
    }

    bool CanStartDialogue(string dialogueSetID)
    {
        if (!dialogueConditions.ContainsKey(dialogueSetID))
            return true;

        var conditions = dialogueConditions[dialogueSetID];
        foreach (var condition in conditions)
        {
            if (!CheckDialogueCondition(condition))
                return false;
        }

        return true;
    }

    bool CheckDialogueCondition(DialogueCondition condition)
    {
        if (!conditionCheckers.ContainsKey(condition.conditionType))
        {
            Debug.LogWarning($"[DialogueManager] 알 수 없는 조건 타입: {condition.conditionType}");
            return true;
        }

        return conditionCheckers[condition.conditionType](condition.target, condition.value);
    }

    void DisplayCurrentLine()
    {
        Debug.Log($"[DialogueManager] DisplayCurrentLine 호출: currentLineID = {currentLineID}");

        if (string.IsNullOrEmpty(currentLineID) || !dialogueLines.ContainsKey(currentLineID))
        {
            Debug.LogError($"[DialogueManager] 라인을 찾을 수 없음: {currentLineID}, Dictionary 크기: {dialogueLines.Count}");
            EndDialogue();
            return;
        }

        var currentLine = dialogueLines[currentLineID];

        if (logDialogueEvents)
            Debug.Log($"[DialogueManager] 대화 라인 표시: {currentLineID} - {currentLine.speakerName}");

        fullText = currentLine.text;
        StartTyping(fullText);

        SetCurrentSpeaker(currentLine);
        isLastLine = string.IsNullOrEmpty(currentLine.nextLineID);
    }


    void SetCurrentSpeaker(DialogueLine line)
    {
        string speakerName = string.IsNullOrEmpty(line.customSpeaker) ? line.speakerName : line.customSpeaker;

        if (speakerNameText != null)
            speakerNameText.text = speakerName;

        // 초상화 설정
        UpdatePortraits(line);
    }

    void UpdatePortraits(DialogueLine line)
    {
        if (leftPortrait != null && rightPortrait != null)
        {
            Sprite playerPortrait = GetPlayerPortrait();
            Sprite npcPortrait = GetNPCPortrait(line.speakerName);

            // 플레이어 초상화 (왼쪽)
            leftPortrait.sprite = playerPortrait;
            SetPortraitAlpha(leftPortrait, line.isPlayer ? 1f : 0.5f);

            // NPC 초상화 (오른쪽)  
            rightPortrait.sprite = npcPortrait;
            SetPortraitAlpha(rightPortrait, !line.isPlayer ? 1f : 0.5f);
        }
    }

    void SetPortraitAlpha(Image portrait, float alpha)
    {
        if (portrait != null)
        {
            Color color = portrait.color;
            color.a = alpha;
            portrait.color = color;
        }
    }

    Sprite GetPlayerPortrait()
    {
        var playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null && playerStats.playerPortrait != null)
            return playerStats.playerPortrait;

        return defaultPlayerPortrait;
    }

    Sprite GetNPCPortrait(string speakerName)
    {
        // 캐시에서 찾기
        if (portraitCache.ContainsKey(speakerName))
            return portraitCache[speakerName];

        // Resources에서 로드 시도
        Sprite portrait = Resources.Load<Sprite>($"Portraits/{speakerName}");
        if (portrait != null)
        {
            portraitCache[speakerName] = portrait;
            return portrait;
        }

        return defaultNPCPortrait;
    }

    public void NextLine()
    {
        Debug.Log($"[DialogueManager] NextLine 호출됨! currentLineID: {currentLineID}");

        if (string.IsNullOrEmpty(currentLineID) || !dialogueLines.ContainsKey(currentLineID))
        {
            Debug.LogError($"[DialogueManager] 라인 없음: {currentLineID}");
            EndDialogue();
            return;
        }

        var currentLine = dialogueLines[currentLineID];
        currentLineID = currentLine.nextLineID;
        DisplayCurrentLine();
    }

    // 현재 탐색 중인 아이템 추적
    private ItemManager currentInspectingItem = null;
    private bool isInspectionFirstTime = false;

    // 현재 탐색 중인 아이템 설정 (ItemManager에서 호출)
    public void SetCurrentInspectingItem(ItemManager item, bool isFirstTime)
    {
        currentInspectingItem = item;
        isInspectionFirstTime = isFirstTime;
    }

    public void EndDialogue()
    {
        StartCoroutine(EndDialogueSequence());
    }

    private IEnumerator EndDialogueSequence()
    {
        if (!isInDialogue) yield break;

        string endingDialogueID = currentDialogueSetID;
        isInDialogue = false;

        if (!string.IsNullOrEmpty(endingDialogueID))
        {
            PlayerPrefs.SetInt($"Dialogue_{endingDialogueID}_Finished", 1);
            PlayerPrefs.Save();
            if (logDialogueEvents) Debug.Log($"[DialogueManager] 대화 완료 처리: {endingDialogueID}");

            if (currentInspectingItem != null)
            {
                currentInspectingItem.OnInspectionDialogueCompleted();
                currentInspectingItem = null;
            }
        }

        yield return new WaitForSeconds(0.1f);

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        currentDialogueSetID = "";
        currentLineID = "";
        isLastLine = false;

        var playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.SetCanMove(true);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(false);
        }
    }

    public void ShowItemDescription(string description)
    {
        Debug.Log($"ShowItemDescription 호출됨: {description}");

        if (dialoguePanel == null)
        {
            GameObject foundTalkPanel = GameObject.Find("talkPanel");
            if (foundTalkPanel != null)
            {
                dialoguePanel = foundTalkPanel;
                Debug.Log("talkPanel을 다시 찾아서 연결했습니다!");
            }
            else
            {
                Debug.LogError("talkPanel을 찾을 수 없습니다!");
                return;
            }
        }

        isShowingItemDescription = true;
        isInDialogue = false;

        var playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.SetCanMove(false);
        }

        dialoguePanel.SetActive(true);

        fullText = description;
        StartTyping(fullText);

        var stats = FindObjectOfType<PlayerStats>();
        string name = stats != null && !string.IsNullOrEmpty(stats.playerName)
                      ? stats.playerName
                      : "플레이어";

        if (speakerNameText != null)
            speakerNameText.text = name;

        if (leftPortrait != null)
        {
            leftPortrait.sprite = GetPlayerPortrait();
            SetPortraitAlpha(leftPortrait, 1f);
        }

        if (rightPortrait != null)
        {
            SetPortraitAlpha(rightPortrait, 0f);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(true);
        }
    }

    public void HideItemDescription()
    {
        Debug.Log("HideItemDescription 호출됨");

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        isShowingItemDescription = false;

        var playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.SetCanMove(true);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(false);
        }
    }

    public void SetCharacterPortraits(Sprite playerPortrait, Sprite npcPortrait)
    {
        if (leftPortrait != null)
        {
            leftPortrait.sprite = playerPortrait;
        }

        if (rightPortrait != null)
        {
            rightPortrait.sprite = npcPortrait;
        }

        if (debugMode)
            Debug.Log($"[DialogueManager] 초상화 설정 완료 - Player: {(playerPortrait != null ? playerPortrait.name : "NULL")}, NPC: {(npcPortrait != null ? npcPortrait.name : "NULL")}");
    }

    public bool IsInDialogue()
    {
        return isInDialogue || isShowingItemDescription;
    }

    /* 
     * 타이핑 효과 메서드
     */

    void StartTyping(string text)
    {
        // 기존 타이핑 코루틴이 있다면 중지
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        isTyping = true;
        typingCoroutine = StartCoroutine(TypeText(text));
    }

    void CompleteTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;

        if (dialogueText != null)
        {
            dialogueText.text = fullText;
        }
    }

    IEnumerator TypeText(string text)
    {
        if (dialogueText == null)
        {
            isTyping = false;
            yield break;
        }

        dialogueText.text = "";

        for (int i = 0; i <= text.Length; i++)
        {
            dialogueText.text = text.Substring(0, i);
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        typingCoroutine = null;
    }

    [ContextMenu("Reload Dialogue Data")]
    public void ReloadDialogueData()
    {
        LoadDialogueDataFromCSV();
        Debug.Log("[DialogueManager] 대화 데이터 재로드 완료");
    }

    [ContextMenu("List All Dialogue Sets")]
    public void ListAllDialogueSets()
    {
        Debug.Log("=== 전체 대화 세트 목록 ===");
        foreach (var kvp in dialogueSets)
        {
            Debug.Log($"{kvp.Key}: {kvp.Value.description}");
        }
    }
}