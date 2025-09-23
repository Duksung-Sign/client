using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using UnityEngine.SceneManagement;
using CsvHelper.Configuration;

#region Data Classes
[System.Serializable]
public class StoryNodes
{
    public string StoryID { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Chapter { get; set; }
    public string NextStoryID { get; set; }
    public bool AutoAdvance { get; set; }
}

[System.Serializable]
public class StoryCondition
{
    public string StoryID { get; set; }
    public string ConditionType { get; set; }
    public string Target { get; set; }
    public string Value { get; set; }
    public string Description { get; set; }
}

[System.Serializable]
public class StoryAction
{
    public string StoryID { get; set; }
    public string ActionType { get; set; }
    public string Target { get; set; }
    public string Parameter { get; set; }
    public int Order { get; set; }
    public string Description { get; set; }
}

[System.Serializable]
public class StoryTriggers
{
    public string CurrentStoryID { get; set; }
    public string TriggerType { get; set; }
    public string TriggerTargetID { get; set; }
    public string NextStoryID { get; set; }
}

#endregion

public class StoryProgressManager : MonoBehaviour
{
    public static StoryProgressManager Instance;

    private Dictionary<string, StoryNodes> storyNodes = new Dictionary<string, StoryNodes>();
    private Dictionary<string, List<StoryCondition>> storyConditions = new Dictionary<string, List<StoryCondition>>();
    private Dictionary<string, List<StoryAction>> storyActions = new Dictionary<string, List<StoryAction>>();
    private List<StoryTriggers> storyTriggersList = new List<StoryTriggers>();
    private Dictionary<string, Func<string, string, bool>> conditionCheckers;

    [Header("CSV 파일 경로")]
    [SerializeField] private string storyNodesPath = "StoryData/StoryNodes";
    [SerializeField] private string storyConditionsPath = "StoryData/StoryConditions";
    [SerializeField] private string storyActionsPath = "StoryData/StoryActions";
    [SerializeField] private string storyTriggersPath = "StoryData/StoryTriggers";

    #region Unity Lifecycle & Event Management
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeStorySystem();
        }
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnItemCollected += HandleGameEvent;
            GameEventManager.Instance.OnItemInspected += HandleGameEvent;
            GameEventManager.Instance.OnDialogueFinished += HandleGameEvent;
            GameEventManager.Instance.OnBattleOver += HandleGameEvent;
        }
    }

    void OnDisable()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnItemCollected -= HandleGameEvent;
            GameEventManager.Instance.OnItemInspected -= HandleGameEvent;
            GameEventManager.Instance.OnDialogueFinished -= HandleGameEvent;
            GameEventManager.Instance.OnBattleOver -= HandleGameEvent;
        }
    }

    void Start()
    {
        ExecuteStoryNode(GameDataManager.Instance.CurrentStoryID);
    }
    #endregion

    private void InitializeStorySystem()
    {
        LoadStoryData();
        InitializeConditionCheckers();
    }

    private void LoadStoryData()
    {
        storyNodes = LoadData<StoryNodes>(storyNodesPath).ToDictionary(node => node.StoryID);

        storyConditions.Clear();
        var conditionsList = LoadData<StoryCondition>(storyConditionsPath);
        foreach (var c in conditionsList)
        {
            if (!storyConditions.ContainsKey(c.StoryID)) storyConditions[c.StoryID] = new List<StoryCondition>();
            storyConditions[c.StoryID].Add(c);
        }

        storyActions.Clear();
        var actionsList = LoadData<StoryAction>(storyActionsPath);
        foreach (var a in actionsList)
        {
            if (!storyActions.ContainsKey(a.StoryID)) storyActions[a.StoryID] = new List<StoryAction>();
            storyActions[a.StoryID].Add(a);
        }

        storyTriggersList = LoadData<StoryTriggers>(storyTriggersPath);
        Debug.Log($"[StoryProgressManager] {storyTriggersList.Count}개의 스토리 트리거를 로드했습니다.");
    }

    private List<T> LoadData<T>(string path)
    {
        TextAsset csvFile = Resources.Load<TextAsset>(path);
        if (csvFile == null)
        {
            Debug.LogError($"[StoryProgressManager] CSV 파일을 찾을 수 없습니다: {path}");
            return new List<T>();
        }

        try
        {
            using (var reader = new StringReader(csvFile.text))
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    PrepareHeaderForMatch = args => args.Header.ToLower().Replace(" ", "").Replace("_", "")
                };

                using (var csv = new CsvReader(reader, config))
                {
                    return csv.GetRecords<T>().ToList();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[StoryProgressManager] {typeof(T).Name} 파싱 실패: {e.Message}");
            return new List<T>();
        }
    }

    private void InitializeConditionCheckers()
    {
        conditionCheckers = new Dictionary<string, Func<string, string, bool>>
        {
            ["item_obtained"] = (target, value) => GameDataManager.Instance.HasItem(target) == (value != "0"),
            ["item_inspected"] = (target, value) => GameDataManager.Instance.HasInspectedItem(target) == (value != "0"),
            ["dialogue_finished"] = (target, value) => GameDataManager.Instance.HasFinishedDialogue(target) == (value != "0"),
            ["quest_completed"] = (target, value) => GameDataManager.Instance.IsQuestCompleted(target) == (value != "0")
        };
    }

    private void ExecuteStoryNode(string storyID)
    {
        if (!storyNodes.ContainsKey(storyID)) return;

        var currentNode = storyNodes[storyID];

        GameDataManager.Instance.SetCurrentStoryID(storyID);
        GameEventManager.Instance.TriggerStoryAdvanced(storyID);

        if (storyActions.ContainsKey(storyID))
        {
            StartCoroutine(ExecuteActionsCoroutine(storyActions[storyID]));
        }
        else
        {
            OnAllActionsCompleted();
        }
    }

    private IEnumerator ExecuteActionsCoroutine(List<StoryAction> actions)
    {
        foreach (var action in actions.OrderBy(a => a.Order))
        {
            switch (action.ActionType)
            {
                case "show_dialogue":
                    DialogueManager.Instance.StartDialogue(action.Target);
                    yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
                    break;
                case "change_scene":
                    GameManager.Instance.ChangeState(GameState.Loading);
                    GameManager.Instance.ChangeScene(action.Target);
                    yield break; 
                case "enable_player":
                    GameManager.Instance.ChangeState(GameState.Field);
                    break;
                case "disable_player":
                    GameManager.Instance.ChangeState(GameState.Dialogue);
                    break;
                case "start_battle":
                    GameEventManager.Instance.TriggerBattleStartRequested(Resources.Load<MonsterDataSO>(action.Target), null, action.Parameter);
                    yield break; 
                case "complete_quest":
                    GameDataManager.Instance.SetQuestCompleted(action.Target);
                    break;
                case "LISTEN_FOR_KEY_INPUT":
                    yield return new WaitForSeconds(0.2f);
                    KeyInputListener.Instance.StartListeningFor(action.Target, action.Parameter);
                    break;
                /*
                case "START_UI_TUTORIAL":
                    UITutorialManager.Instance.StartTutorial(action.Target);
                    yield return new WaitUntil(() => !UITutorialManager.Instance.IsTutorialRunning);
                    break; */
            }
        }
        OnAllActionsCompleted();
    }

    private void OnAllActionsCompleted()
    {
        var currentNode = storyNodes[GameDataManager.Instance.CurrentStoryID];
        if (currentNode.AutoAdvance)
        {
            TryAdvanceToNextStory();
        }
    }

    private void TryAdvanceToNextStory()
    {
        var currentStoryID = GameDataManager.Instance.CurrentStoryID;
        if (!storyNodes.ContainsKey(currentStoryID)) return;

        string nextStoryID = storyNodes[currentStoryID].NextStoryID;
        if (string.IsNullOrEmpty(nextStoryID)) return;

        if (CanAdvanceStory(nextStoryID))
        {
            ExecuteStoryNode(nextStoryID);
        }
    }

    private bool CanAdvanceStory(string targetStoryID)
    {
        if (!storyConditions.ContainsKey(targetStoryID)) return true;
        foreach (var condition in storyConditions[targetStoryID])
        {
            if (!conditionCheckers.ContainsKey(condition.ConditionType) ||
                !conditionCheckers[condition.ConditionType](condition.Target, condition.Value))
            {
                return false;
            }
        }
        return true;
    }

    /*
     * 이벤트를 진행시키기 위한 조건을 탐색한다.
     */

    private void HandleGameEvent(string eventType, string eventTargetID)
    {
        Debug.Log($"[StoryProgressManager] 게임 이벤트 수신: {eventType} - {eventTargetID}. 스토리 진행을 확인합니다.");

        var currentStoryID = GameDataManager.Instance.CurrentStoryID;

        var matchedTrigger = storyTriggersList.FirstOrDefault(trigger =>
            trigger.CurrentStoryID == currentStoryID &&
            trigger.TriggerType == eventType &&
            trigger.TriggerTargetID == eventTargetID);

        if (matchedTrigger != null)
        {
            Debug.Log($"[StoryProgressManager] StoryTrigger 발견! {currentStoryID} -> {matchedTrigger.NextStoryID}");
            ExecuteStoryNode(matchedTrigger.NextStoryID);
        }
        else
        {
            TryAdvanceToNextStory();
        }
    }
}