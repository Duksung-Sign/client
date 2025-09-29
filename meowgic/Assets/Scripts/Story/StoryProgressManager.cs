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

[System.Serializable]
public class StoryNodes
{
    public string StoryID { get; set; }
    public string SceneName { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Chapter { get; set; }
    public string NextStoryID { get; set; }
    public bool AutoAdvance { get; set; }
    public bool IsCheckpoint { get; set; }
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

public class StoryProgressManager : MonoBehaviour
{
    public static StoryProgressManager Instance;

    private Dictionary<string, StoryNodes> storyNodes = new Dictionary<string, StoryNodes>();
    private Dictionary<string, List<StoryCondition>> storyConditions = new Dictionary<string, List<StoryCondition>>();
    private Dictionary<string, List<StoryAction>> storyActions = new Dictionary<string, List<StoryAction>>();
    private List<StoryTriggers> storyTriggersList = new List<StoryTriggers>();
    private Dictionary<string, Func<string, string, bool>> conditionCheckers;

    private Queue<StoryAction> postSceneLoadActions;

    [Header("CSV 파일 경로")]
    [SerializeField] private string storyNodesPath = "StoryData/StoryNodes";
    [SerializeField] private string storyConditionsPath = "StoryData/StoryConditions";
    [SerializeField] private string storyActionsPath = "StoryData/StoryActions";
    [SerializeField] private string storyTriggersPath = "StoryData/StoryTriggers";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeStorySystem();
            postSceneLoadActions = new Queue<StoryAction>();
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

    public void ContinueStory()
    {
        if (GameDataManager.Instance != null && storyNodes != null)
        {
            string storyIDToContinue = GameDataManager.Instance.CurrentStoryID;

            if (!storyNodes.ContainsKey(storyIDToContinue))
            {
                Debug.LogError($"[StoryProgressManager] 이어하기 실패: 저장된 StoryID '{storyIDToContinue}'를 찾을 수 없습니다.");
                return;
            }
            string targetScene = storyNodes[storyIDToContinue].SceneName;
            string currentScene = SceneManager.GetActiveScene().name;

            Debug.Log($"[StoryProgressManager] 이어하기 요청: {targetScene} 씬의 {storyIDToContinue} 스토리부터 시작합니다.");

            if (currentScene == targetScene)
            {
                ExecuteStoryNode(storyIDToContinue);
            }
            else
            {
                FadeManager.Instance.LoadSceneWithFade(targetScene, () => {
                    ExecuteStoryNode(storyIDToContinue);
                });
            }
        }
    }

    public void StartNewGame(string startingStoryID)
    {
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.ResetData();
            Debug.Log($"[StoryProgressManager] 새 게임 시작 요청. 데이터 초기화 완료.");

            string startingScene = "Library";

            FadeManager.Instance.LoadSceneWithFade(startingScene, () => {
                Debug.Log($"[StoryProgressManager] '{startingScene}' 씬 로드 완료. '{startingStoryID}' 스토리 실행.");
                ExecuteStoryNode(startingStoryID);
            });
        }
        else
        {
            Debug.LogError("[StoryProgressManager] GameDataManager.Instance가 null입니다!");
        }
    }


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
        if (!storyNodes.ContainsKey(storyID))
        {
            Debug.LogError($"[StoryProgressManager] 존재하지 않는 StoryID: {storyID}");
            return;
        }

        var currentNode = storyNodes[storyID];

        GameDataManager.Instance.SetCurrentStoryID(storyID);

        if (currentNode.IsCheckpoint)
        {
            string nextStoryID = currentNode.NextStoryID;
            if (!string.IsNullOrEmpty(nextStoryID))
            {
                GameDataManager.Instance.SetLastCheckpoint(nextStoryID);
                Debug.Log($"[StoryProgressManager] 체크포인트 저장: 다음 시작 노드는 {nextStoryID} 입니다.");
            }
        }

        GameEventManager.Instance.TriggerStoryAdvanced(storyID);

        if (storyActions.ContainsKey(storyID))
        {
            var actionsToExecute = storyActions[storyID].OrderBy(a => a.Order).ToList();
            StartCoroutine(ExecuteActionsCoroutine(actionsToExecute));
        }
        else
        {
            OnAllActionsCompleted();
        }
    }

    private IEnumerator ExecuteActionsCoroutine(List<StoryAction> actions)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            var action = actions[i];

            switch (action.ActionType)
            {
                case "show_dialogue":
                    DialogueManager.Instance.StartDialogue(action.Target);
                    yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
                    break;
                case "change_scene":
                    GameManager.Instance.ChangeState(GameState.Loading);
                    postSceneLoadActions.Clear();
                    for (int j = i + 1; j < actions.Count; j++)
                    {
                        postSceneLoadActions.Enqueue(actions[j]);
                    }
                    FadeManager.Instance.LoadSceneWithFade(action.Target, OnSceneLoadComplete);
                    yield break; 
                case "enable_player":
                    GameManager.Instance.ChangeState(GameState.Field);
                    break;
                case "disable_player":
                    GameManager.Instance.ChangeState(GameState.Dialogue);
                    break;
                case "enable_interaction":
                    if (InteractionManager.Instance != null)
                    {
                        InteractionManager.Instance.EnableObject(action.Target);
                    }
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

    private void OnSceneLoadComplete()
    {
        Debug.Log("[StoryProgressManager] 씬 전환 완료. 저장된 후속 액션을 실행합니다.");
        if (postSceneLoadActions.Count > 0)
        {
            var actionsToRun = postSceneLoadActions.ToList();
            postSceneLoadActions.Clear();
            StartCoroutine(ExecuteActionsCoroutine(actionsToRun));
        }
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

        if (!storyNodes.ContainsKey(currentStoryID))
        {
            return;
        }

        string nextStoryID = storyNodes[currentStoryID].NextStoryID;

        if (string.IsNullOrEmpty(nextStoryID))
        {
            return;
        }

        if (CanAdvanceStory(nextStoryID))
        {
            if (PlayerController.Instance != null)
            {
                GameDataManager.Instance.SetLastPlayerPosition(PlayerController.Instance.transform.position);
            }

            GameDataManager.Instance.SetCurrentStoryID(nextStoryID);
            GameDataManager.Instance.SaveState();

            ExecuteStoryNode(nextStoryID);
        }
        else
        {
            GameDataManager.Instance.SaveState();
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
        Debug.Log($"[StoryProgressManager] 게임 이벤트 수신: {eventType} - {eventTargetID}.");

        var currentStoryID = GameDataManager.Instance.CurrentStoryID;
        var matchedTrigger = storyTriggersList.FirstOrDefault(trigger =>
            trigger.CurrentStoryID == currentStoryID &&
            trigger.TriggerType == eventType &&
            trigger.TriggerTargetID == eventTargetID);

        if (matchedTrigger != null)
        {
            string nextStoryID = matchedTrigger.NextStoryID;
            Debug.Log($"[StoryProgressManager] StoryTrigger 발견! {currentStoryID} -> {nextStoryID}");

            if (PlayerController.Instance != null)
            {
                GameDataManager.Instance.SetLastPlayerPosition(PlayerController.Instance.transform.position);
            }

            GameDataManager.Instance.SetCurrentStoryID(nextStoryID);
            GameDataManager.Instance.SaveState();
            Debug.Log($"[StoryProgressManager] 진행 상황 저장 완료. 다음 스토리: {nextStoryID}");

            ExecuteStoryNode(nextStoryID);
        }
    }
}