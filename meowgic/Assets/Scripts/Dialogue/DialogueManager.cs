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

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI speakerNameText;
    public ContinueIndicator continueIndicator;
    public Image leftPortrait;
    private CanvasGroup dialogueCanvasGroup; 

    [Header("외부 데이터베이스 참조")] 
    [SerializeField] private CharacterDatabase characterDatabase;

    [Header("CSV 파일 경로")]
    [SerializeField] private string dialogueLinesPath = "DialogueData/DialogueLines";
    [SerializeField] private string dialogueSetsPath = "DialogueData/DialogueSets";
    [SerializeField] private string dialogueConditionsPath = "DialogueData/DialogueConditions";

    [Header("타이핑 효과")]
    public float typingSpeed = 0.05f;
    public bool skipTypingOnInput = true;

    private Dictionary<string, DialogueLine> dialogueLines = new Dictionary<string, DialogueLine>();
    private Dictionary<string, DialogueSet> dialogueSets = new Dictionary<string, DialogueSet>();
    private Dictionary<string, List<DialogueCondition>> dialogueConditions = new Dictionary<string, List<DialogueCondition>>();
    private Dictionary<string, Func<string, string, bool>> conditionCheckers;
    private Dictionary<string, int> dialogueSetUsageCount = new Dictionary<string, int>();

    private string currentDialogueSetID;
    private string currentLineID;
    private bool isInDialogue = false;
    private bool isTyping = false;
    private string fullText = "";
    private Coroutine typingCoroutine;
    private string lastSpeakerName = "";

    void Update()
    {
        if (!isInDialogue) return;

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
            InitializeDialogueSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnLockedDoorInteracted += HandleLockedDoorInteraction;
        }
    }

    void OnDisable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnLockedDoorInteracted -= HandleLockedDoorInteraction;
        }
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
        
        dialoguePanel.SetActive(true);
        dialogueCanvasGroup.DOKill(); 
        dialogueCanvasGroup.alpha = 0f; 
        dialogueCanvasGroup.DOFade(1f, 0.3f); 

        DisplayCurrentLine();
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

        isInDialogue = true;
        currentDialogueSetID = dialogueSetID;
        currentLineID = startingLine.LineID;

        GameEventManager.Instance.TriggerDialogueStarted(dialogueSetID);

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        DisplayCurrentLine();
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
        StartTyping(fullText);

        string speakerName = string.IsNullOrEmpty(line.CustomSpeaker) ? line.SpeakerName : line.CustomSpeaker;
        speakerNameText.text = speakerName;

        if (!string.Equals(speakerName, lastSpeakerName))
        {
            if (characterDatabase != null)
            {
                Sprite portrait = characterDatabase.GetPortrait(speakerName);
                
                leftPortrait.DOKill();

                if (portrait != null) 
                {
                    leftPortrait.sprite = portrait;

                    if (!leftPortrait.gameObject.activeSelf)
                    {
                        leftPortrait.gameObject.SetActive(true);
                        Color c = leftPortrait.color; c.a = 0; leftPortrait.color = c;
                        leftPortrait.DOFade(1f, 0.3f);
                    }
                }
                else 
                {
                    if (leftPortrait.gameObject.activeSelf)
                    {
                        leftPortrait.DOFade(0f, 0.3f).OnComplete(() => {
                            leftPortrait.gameObject.SetActive(false);
                        });
                    }
                }
            }
        }

        lastSpeakerName = speakerName;
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
        
        dialogueCanvasGroup.DOKill();
        dialogueCanvasGroup.DOFade(0f, 0.3f).OnComplete(() => {
            dialoguePanel.SetActive(false);
        });

        GameDataManager.Instance.MarkDialogueAsFinished(currentDialogueSetID);
        GameDataManager.Instance.SaveState(); 
        GameEventManager.Instance.TriggerDialogueFinished(currentDialogueSetID);

        Debug.Log($"[DialogueManager] 대화 종료: {currentDialogueSetID}");
    }

    public bool IsInDialogue()
    {
        return isInDialogue;
    }

    void StartTyping(string text)
    {
        if (continueIndicator != null) continueIndicator.Hide();
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        isTyping = true;
        typingCoroutine = StartCoroutine(TypeText(text));
    }

    void CompleteTyping()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        isTyping = false;
        dialogueText.text = fullText;
        if (continueIndicator != null) continueIndicator.Show();
    }

    IEnumerator TypeText(string text)
    {
        dialogueText.text = "";
        foreach (char letter in text.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }
        CompleteTyping();
    }
}