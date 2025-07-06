using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

// ===== 데이터 클래스들 =====
[System.Serializable]
public class StoryNodeData
{
    public string id;
    public string displayName;
    public string description;
    public string chapter;
    public string nextStoryID;
    public bool autoAdvance;

    public StoryNodeData() { }

    public StoryNodeData(string id, string displayName, string description, string chapter, string nextStoryID, bool autoAdvance)
    {
        this.id = id;
        this.displayName = displayName;
        this.description = description;
        this.chapter = chapter;
        this.nextStoryID = nextStoryID;
        this.autoAdvance = autoAdvance;
    }
}

[System.Serializable]
public class StoryCondition
{
    public string storyID;
    public string conditionType;
    public string target;
    public string value;
    public string description;

    public StoryCondition() { }

    public StoryCondition(string storyID, string conditionType, string target, string value, string description)
    {
        this.storyID = storyID;
        this.conditionType = conditionType;
        this.target = target;
        this.value = value;
        this.description = description;
    }
}

[System.Serializable]
public class StoryAction
{
    public string storyID;
    public string actionType;
    public string target;
    public string parameter;
    public int order;
    public string description;

    public StoryAction() { }

    public StoryAction(string storyID, string actionType, string target, string parameter, int order, string description)
    {
        this.storyID = storyID;
        this.actionType = actionType;
        this.target = target;
        this.parameter = parameter;
        this.order = order;
        this.description = description;
    }
}

// ===== CSV 파싱 유틸리티 =====
public static class StoryCSVParser
{
    public static List<StoryNodeData> ParseStoryNodes(string csvText)
    {
        var nodes = new List<StoryNodeData>();

        try
        {
            string[] lines = csvText.Split('\n');

            // 헤더 라인 건너뛰기
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] values = ParseCSVLine(line);
                if (values.Length >= 6)
                {
                    var node = new StoryNodeData(
                        values[0].Trim(), // StoryID
                        values[1].Trim(), // DisplayName
                        values[2].Trim(), // Description
                        values[3].Trim(), // Chapter
                        values[4].Trim(), // NextStoryID
                        ParseBool(values[5]) // AutoAdvance
                    );
                    nodes.Add(node);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"StoryNodes CSV 파싱 실패: {e.Message}");
        }

        return nodes;
    }

    public static List<StoryCondition> ParseStoryConditions(string csvText)
    {
        var conditions = new List<StoryCondition>();

        try
        {
            string[] lines = csvText.Split('\n');

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] values = ParseCSVLine(line);
                if (values.Length >= 5)
                {
                    var condition = new StoryCondition(
                        values[0].Trim(), // StoryID
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
            Debug.LogError($"StoryConditions CSV 파싱 실패: {e.Message}");
        }

        return conditions;
    }

    public static List<StoryAction> ParseStoryActions(string csvText)
    {
        var actions = new List<StoryAction>();

        try
        {
            string[] lines = csvText.Split('\n');

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] values = ParseCSVLine(line);
                if (values.Length >= 6)
                {
                    var action = new StoryAction(
                        values[0].Trim(), // StoryID
                        values[1].Trim(), // ActionType
                        values[2].Trim(), // Target
                        values[3].Trim(), // Parameter
                        ParseInt(values[4]), // Order
                        values[5].Trim()  // Description
                    );
                    actions.Add(action);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"StoryActions CSV 파싱 실패: {e.Message}");
        }

        return actions;
    }

    // 간단한 CSV 라인 파싱 (쉼표 분리, 따옴표 처리)
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

// ===== 메인 StoryProgressManager =====
public class StoryProgressManager : MonoBehaviour
{
    public static StoryProgressManager Instance;

    [Header("현재 스토리 상태")]
    [SerializeField] private string currentStoryID = "prologue";

    [Header("CSV 파일 경로")]
    [SerializeField] private string storyNodesPath = "StoryData/StoryNodes";
    [SerializeField] private string storyConditionsPath = "StoryData/StoryConditions";
    [SerializeField] private string storyActionsPath = "StoryData/StoryActions";

    [Header("디버그")]
    public bool debugMode = true;
    public bool logStoryChanges = true;
    public bool logConditionChecks = true;
    public bool logActionExecutions = true;

    // 스토리 데이터
    private Dictionary<string, StoryNodeData> storyNodes = new Dictionary<string, StoryNodeData>();
    private Dictionary<string, List<StoryCondition>> storyConditions = new Dictionary<string, List<StoryCondition>>();
    private Dictionary<string, List<StoryAction>> storyActions = new Dictionary<string, List<StoryAction>>();

    // 조건 체크 함수들
    private Dictionary<string, Func<string, string, bool>> conditionCheckers;

    // 액션 실행 함수들
    private Dictionary<string, Action<string, string>> actionExecutors;

    // 이벤트들
    public event Action<string> OnStoryChanged;
    public event Action<string, StoryNodeData> OnStoryNodeEntered;
    public event Action<string, string, bool> OnConditionChecked;
    public event Action<string, string, string> OnActionExecuted;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeStorySystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 저장된 스토리 진행도 로드
        LoadCurrentStoryProgress();

        // 현재 스토리 노드 실행
        ExecuteCurrentStoryNode();
    }

    void InitializeStorySystem()
    {
        InitializeConditionCheckers();
        InitializeActionExecutors();
        LoadStoryDataFromCSV();

        if (debugMode)
        {
            Debug.Log($"[StoryProgress] 시스템 초기화 완료");
            Debug.Log($"[StoryProgress] 스토리 노드: {storyNodes.Count}개");
            Debug.Log($"[StoryProgress] 조건: {storyConditions.Values.Sum(list => list.Count)}개");
            Debug.Log($"[StoryProgress] 액션: {storyActions.Values.Sum(list => list.Count)}개");
        }
    }

    // ===== CSV 데이터 로드 =====
    void LoadStoryDataFromCSV()
    {
        LoadStoryNodes();
        LoadStoryConditions();
        LoadStoryActions();
    }

    void LoadStoryNodes()
    {
        try
        {
            TextAsset csvFile = Resources.Load<TextAsset>(storyNodesPath);
            if (csvFile == null)
            {
                Debug.LogError($"[StoryProgress] CSV 파일을 찾을 수 없습니다: {storyNodesPath}");
                return;
            }

            var nodes = StoryCSVParser.ParseStoryNodes(csvFile.text);
            storyNodes.Clear();

            foreach (var node in nodes)
            {
                storyNodes[node.id] = node;
            }

            if (debugMode)
                Debug.Log($"[StoryProgress] StoryNodes 로드 완료: {nodes.Count}개");
        }
        catch (Exception e)
        {
            Debug.LogError($"[StoryProgress] StoryNodes 로드 실패: {e.Message}");
        }
    }

    void LoadStoryConditions()
    {
        try
        {
            TextAsset csvFile = Resources.Load<TextAsset>(storyConditionsPath);
            if (csvFile == null)
            {
                Debug.LogWarning($"[StoryProgress] CSV 파일을 찾을 수 없습니다: {storyConditionsPath} (선택사항)");
                return;
            }

            var conditions = StoryCSVParser.ParseStoryConditions(csvFile.text);
            storyConditions.Clear();

            foreach (var condition in conditions)
            {
                if (!storyConditions.ContainsKey(condition.storyID))
                    storyConditions[condition.storyID] = new List<StoryCondition>();

                storyConditions[condition.storyID].Add(condition);
            }

            if (debugMode)
                Debug.Log($"[StoryProgress] StoryConditions 로드 완료: {conditions.Count}개");
        }
        catch (Exception e)
        {
            Debug.LogError($"[StoryProgress] StoryConditions 로드 실패: {e.Message}");
        }
    }

    void LoadStoryActions()
    {
        try
        {
            TextAsset csvFile = Resources.Load<TextAsset>(storyActionsPath);
            if (csvFile == null)
            {
                Debug.LogWarning($"[StoryProgress] CSV 파일을 찾을 수 없습니다: {storyActionsPath} (선택사항)");
                return;
            }

            var actions = StoryCSVParser.ParseStoryActions(csvFile.text);
            storyActions.Clear();

            foreach (var action in actions)
            {
                if (!storyActions.ContainsKey(action.storyID))
                    storyActions[action.storyID] = new List<StoryAction>();

                storyActions[action.storyID].Add(action);
            }

            // 액션들을 order 순으로 정렬
            foreach (var actionList in storyActions.Values)
            {
                actionList.Sort((a, b) => a.order.CompareTo(b.order));
            }

            if (debugMode)
                Debug.Log($"[StoryProgress] StoryActions 로드 완료: {actions.Count}개");
        }
        catch (Exception e)
        {
            Debug.LogError($"[StoryProgress] StoryActions 로드 실패: {e.Message}");
        }
    }

    // ===== 조건 체크 시스템 =====
    void InitializeConditionCheckers()
    {
        conditionCheckers = new Dictionary<string, Func<string, string, bool>>()
        {
            ["none"] = (target, value) => true,
            ["prologue_finished"] = CheckPrologueFinished,
            ["dialogue_finished"] = CheckDialogueFinished,
            ["item_obtained"] = CheckItemObtained,
            ["quest_completed"] = CheckQuestCompleted,
            ["map_entered"] = CheckMapEntered,
            ["battle_won"] = CheckBattleWon,
            ["story_reached"] = CheckStoryReached
        };
    }

    bool CheckPrologueFinished(string target, string value)
    {
        // 프롤로그 완료 체크 로직
        return PlayerPrefs.GetInt($"Prologue_{target}_Finished", 0) == 1;
    }

    bool CheckDialogueFinished(string target, string value)
    {
        // 대화 완료 체크 로직  
        return PlayerPrefs.GetInt($"Dialogue_{target}_Finished", 0) == 1;
    }

    bool CheckItemObtained(string target, string value)
    {
        // 아이템 획득 체크 로직
        int requiredAmount = string.IsNullOrEmpty(value) ? 1 : int.Parse(value);
        int currentAmount = PlayerPrefs.GetInt($"Item_{target}_Count", 0);
        return currentAmount >= requiredAmount;
    }

    bool CheckQuestCompleted(string target, string value)
    {
        // 퀘스트 완료 체크 로직
        return PlayerPrefs.GetInt($"Quest_{target}_Completed", 0) == 1;
    }

    bool CheckMapEntered(string target, string value)
    {
        // 맵 진입 체크 로직
        return PlayerPrefs.GetInt($"Map_{target}_Entered", 0) == 1;
    }

    bool CheckBattleWon(string target, string value)
    {
        // 전투 승리 체크 로직
        return PlayerPrefs.GetInt($"Battle_{target}_Won", 0) == 1;
    }

    bool CheckStoryReached(string target, string value)
    {
        // 특정 스토리 도달 체크 로직
        return PlayerPrefs.GetInt($"Story_{target}_Reached", 0) == 1;
    }

    // ===== 액션 실행 시스템 =====
    void InitializeActionExecutors()
    {
        actionExecutors = new Dictionary<string, Action<string, string>>()
        {
            ["show_cutscene"] = ExecuteShowCutscene,
            ["show_dialogue"] = ExecuteShowDialogue,
            ["enable_player"] = ExecuteEnablePlayer,
            ["disable_player"] = ExecuteDisablePlayer,
            ["set_spawn_point"] = ExecuteSetSpawnPoint,
            ["start_quest"] = ExecuteStartQuest,
            ["complete_quest"] = ExecuteCompleteQuest,
            ["spawn_item"] = ExecuteSpawnItem,
            ["enable_transition"] = ExecuteEnableTransition,
            ["enable_interaction"] = ExecuteEnableInteraction,
            ["play_bgm"] = ExecutePlayBGM,
            ["spawn_npc"] = ExecuteSpawnNPC,
            ["start_battle"] = ExecuteStartBattle
        };
    }

    void ExecuteShowCutscene(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 컷신 재생: {target}");
        // 컷신 재생 로직 (CutsceneManager 등과 연동)
    }

    void ExecuteShowDialogue(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 대화 표시: {target}");
        // 새로운 CSV 기반 DialogueManager와 연동
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(target);
        }
    }

    void ExecuteEnablePlayer(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 플레이어 조작 허용");
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetCanMove(true);
        }
    }

    void ExecuteDisablePlayer(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 플레이어 조작 금지");
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetCanMove(false);
        }
    }

    void ExecuteSetSpawnPoint(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 스폰 포인트 설정: {target}");
        PlayerPrefs.SetString("CurrentSpawnPoint", target);
        PlayerPrefs.Save();
    }

    void ExecuteStartQuest(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 퀘스트 시작: {target}");
        // QuestManager와 연동
        PlayerPrefs.SetInt($"Quest_{target}_Started", 1);
        PlayerPrefs.Save();
    }

    void ExecuteCompleteQuest(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 퀘스트 완료: {target}");
        PlayerPrefs.SetInt($"Quest_{target}_Completed", 1);
        PlayerPrefs.Save();
    }

    void ExecuteSpawnItem(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 아이템 생성: {target}, 파라미터: {parameter}");
        // ItemManager와 연동하여 아이템 생성
    }

    void ExecuteEnableTransition(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 맵 이동 활성화: {target}");
        PlayerPrefs.SetInt($"MapTransition_{target}_Enabled", 1);
        PlayerPrefs.Save();
    }

    void ExecuteEnableInteraction(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 상호작용 활성화: {target}");
        PlayerPrefs.SetInt($"Interaction_{target}_Enabled", 1);
        PlayerPrefs.Save();
    }

    void ExecutePlayBGM(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 배경음악 재생: {target}");
        // AudioManager와 연동
    }

    void ExecuteSpawnNPC(string target, string parameter)
    {
        Debug.Log($"[StoryAction] NPC 생성: {target}");
        // NPCManager와 연동
    }

    void ExecuteStartBattle(string target, string parameter)
    {
        Debug.Log($"[StoryAction] 전투 시작: {target}");
        // BattleManager와 연동
        if (GameManager.Instance != null)
        {
            // GameManager.Instance.StartBattle(target);
        }
    }

    // ===== 메인 스토리 진행 로직 =====
    public void ExecuteCurrentStoryNode()
    {
        if (!storyNodes.ContainsKey(currentStoryID))
        {
            Debug.LogError($"[StoryProgress] 스토리 노드를 찾을 수 없습니다: {currentStoryID}");
            return;
        }

        var currentNode = storyNodes[currentStoryID];

        if (logStoryChanges)
            Debug.Log($"[StoryProgress] 스토리 노드 실행: {currentStoryID} - {currentNode.displayName}");

        // 이벤트 발생
        OnStoryNodeEntered?.Invoke(currentStoryID, currentNode);

        // 액션들 실행
        ExecuteStoryActions(currentStoryID);

        // 자동 진행이면 다음 스토리로
        if (currentNode.autoAdvance)
        {
            TryAdvanceToNextStory();
        }
    }

    public bool CanAdvanceStory(string targetStoryID)
    {
        if (!storyConditions.ContainsKey(targetStoryID))
            return true; // 조건이 없으면 진행 가능

        var conditions = storyConditions[targetStoryID];

        foreach (var condition in conditions)
        {
            if (!CheckCondition(condition))
            {
                if (logConditionChecks)
                    Debug.Log($"[StoryProgress] 조건 미충족: {targetStoryID} - {condition.conditionType}({condition.target})");
                return false;
            }
        }

        return true;
    }

    public void TryAdvanceToNextStory()
    {
        if (!storyNodes.ContainsKey(currentStoryID))
            return;

        var currentNode = storyNodes[currentStoryID];
        if (string.IsNullOrEmpty(currentNode.nextStoryID))
            return;

        TryAdvanceStory(currentNode.nextStoryID);
    }

    public void TryAdvanceStory(string targetStoryID)
    {
        if (string.IsNullOrEmpty(targetStoryID))
            return;

        if (!CanAdvanceStory(targetStoryID))
        {
            if (debugMode)
                Debug.Log($"[StoryProgress] 스토리 진행 불가: {targetStoryID} (조건 미충족)");
            return;
        }

        AdvanceStory(targetStoryID);
    }

    public void AdvanceStory(string targetStoryID)
    {
        if (!storyNodes.ContainsKey(targetStoryID))
        {
            Debug.LogError($"[StoryProgress] 존재하지 않는 스토리 ID: {targetStoryID}");
            return;
        }

        string previousStoryID = currentStoryID;
        currentStoryID = targetStoryID;

        // 스토리 진행 상태 저장
        SaveCurrentStoryProgress();

        if (logStoryChanges)
            Debug.Log($"[StoryProgress] 스토리 진행: {previousStoryID} → {currentStoryID}");

        // 이벤트 발생
        OnStoryChanged?.Invoke(currentStoryID);

        // 새 스토리 노드 실행
        ExecuteCurrentStoryNode();
    }

    bool CheckCondition(StoryCondition condition)
    {
        if (!conditionCheckers.ContainsKey(condition.conditionType))
        {
            Debug.LogWarning($"[StoryProgress] 알 수 없는 조건 타입: {condition.conditionType}");
            return true;
        }

        bool result = conditionCheckers[condition.conditionType](condition.target, condition.value);

        if (logConditionChecks)
        {
            OnConditionChecked?.Invoke(condition.conditionType, condition.target, result);
        }

        return result;
    }

    void ExecuteStoryActions(string storyID)
    {
        if (!storyActions.ContainsKey(storyID))
            return;

        var actions = storyActions[storyID];

        foreach (var action in actions)
        {
            ExecuteAction(action);
        }
    }

    void ExecuteAction(StoryAction action)
    {
        if (!actionExecutors.ContainsKey(action.actionType))
        {
            Debug.LogWarning($"[StoryProgress] 알 수 없는 액션 타입: {action.actionType}");
            return;
        }

        if (logActionExecutions)
        {
            Debug.Log($"[StoryAction] 실행: {action.actionType}({action.target}) - {action.description}");
            OnActionExecuted?.Invoke(action.actionType, action.target, action.parameter);
        }

        actionExecutors[action.actionType](action.target, action.parameter);
    }

    // ===== 저장/로드 =====
    void SaveCurrentStoryProgress()
    {
        PlayerPrefs.SetString("CurrentStoryID", currentStoryID);
        PlayerPrefs.Save();
    }

    void LoadCurrentStoryProgress()
    {
        currentStoryID = PlayerPrefs.GetString("CurrentStoryID", "prologue");
    }

    // ===== 공개 메서드들 =====
    public string GetCurrentStoryID()
    {
        return currentStoryID;
    }

    public StoryNodeData GetCurrentStoryNode()
    {
        return storyNodes.ContainsKey(currentStoryID) ? storyNodes[currentStoryID] : null;
    }

    public StoryNodeData GetStoryNode(string storyID)
    {
        return storyNodes.ContainsKey(storyID) ? storyNodes[storyID] : null;
    }

    // ===== 디버그 메서드들 =====
    [ContextMenu("Reload Story Data")]
    public void ReloadStoryData()
    {
        LoadStoryDataFromCSV();
        Debug.Log("[StoryProgress] 스토리 데이터 재로드 완료");
    }

    [ContextMenu("Show Current Story Info")]
    public void ShowCurrentStoryInfo()
    {
        var node = GetCurrentStoryNode();
        if (node != null)
        {
            Debug.Log($"=== 현재 스토리 정보 ===");
            Debug.Log($"ID: {node.id}");
            Debug.Log($"이름: {node.displayName}");
            Debug.Log($"설명: {node.description}");
            Debug.Log($"챕터: {node.chapter}");
            Debug.Log($"다음: {node.nextStoryID}");
            Debug.Log($"자동진행: {node.autoAdvance}");
        }
    }

    [ContextMenu("List All Story Nodes")]
    public void ListAllStoryNodes()
    {
        Debug.Log("=== 전체 스토리 노드 목록 ===");
        foreach (var kvp in storyNodes)
        {
            Debug.Log($"{kvp.Key}: {kvp.Value.displayName}");
        }
    }

    [ContextMenu("Reset Story Progress")]
    public void ResetStoryProgress()
    {
        currentStoryID = "prologue";
        SaveCurrentStoryProgress();
        Debug.Log("[StoryProgress] 스토리 진행도 초기화");
    }
}