using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    public string CurrentStoryID { get; private set; }
    public HashSet<string> ObtainedItemIDs { get; private set; }
    public HashSet<string> FinishedDialogueIDs { get; private set; }
    public HashSet<string> InspectedItemIDs { get; private set; }
    public HashSet<string> DefeatedMonsterIDs { get; private set; }
    public MonsterDataSO CurrentBattleMonster { get; private set; }
    public string CurrentBattleDialogueID { get; private set; }
    public string CurrentBattleMonsterID { get; private set; }
    public string PreviousSceneName { get; private set; }
    public string PlayerSpawnPointID { get; private set; }
    public string LastSceneName { get; private set; }
    public string LastCheckpointID { get; private set; }
    public Vector3 LastPlayerPosition { get; private set; }
    public HashSet<string> CompletedQuestIDs { get; private set; } = new HashSet<string>();

    public bool IsTunaSpawned { get; private set; }
    public Vector3 TunaPosition { get; private set; }
    private bool canEscapeCurrentBattle = true;

    // ✅ 튜토리얼 플래그 추가
    [System.NonSerialized]
    public bool IsNextBattleTutorial = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadState();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetCurrentStoryID(string storyID) => CurrentStoryID = storyID;
    public void AddItem(string itemID) => ObtainedItemIDs.Add(itemID);
    public void MarkDialogueAsFinished(string dialogueID) => FinishedDialogueIDs.Add(dialogueID);
    public void MarkItemAsInspected(string itemID) => InspectedItemIDs.Add(itemID);

    public bool HasItem(string itemID) => ObtainedItemIDs.Contains(itemID);
    public bool HasFinishedDialogue(string dialogueID) => FinishedDialogueIDs.Contains(dialogueID);
    public bool HasInspectedItem(string itemID) => InspectedItemIDs.Contains(itemID);

    public bool IsMonsterDefeated(string monsterID) => DefeatedMonsterIDs.Contains(monsterID);
    public void AddDefeatedMonster(string monsterID) => DefeatedMonsterIDs.Add(monsterID);

    public bool CanEscapeCurrentBattle => canEscapeCurrentBattle;

    public void SetTunaSpawned(bool spawned, Vector3 position = default)
    {
        IsTunaSpawned = spawned;
        if (spawned)
        {
            TunaPosition = position;
            Debug.Log($"[GameDataManager] 튜나 상태: 생성됨, 위치: {position}");
        }
        else
        {
            TunaPosition = Vector3.zero;
            Debug.Log("[GameDataManager] 튜나 상태: 비생성");
        }
    }

    public void SaveState()
    {
        PlayerPrefs.SetString("CurrentStoryID", CurrentStoryID);
        PlayerPrefs.SetString("LastSceneName", LastSceneName);
        PlayerPrefs.SetString("LastCheckpointID", LastCheckpointID);
        PlayerPrefs.SetString("ObtainedItemIDs", string.Join(",", ObtainedItemIDs));
        PlayerPrefs.SetString("FinishedDialogueIDs", string.Join(",", FinishedDialogueIDs));
        PlayerPrefs.SetString("InspectedItemIDs", string.Join(",", InspectedItemIDs));
        PlayerPrefs.SetString("DefeatedMonsterIDs", string.Join(",", DefeatedMonsterIDs));
        PlayerPrefs.SetString("CompletedQuestIDs", string.Join(",", CompletedQuestIDs));

        PlayerPrefs.SetFloat("PlayerPosX", LastPlayerPosition.x);
        PlayerPrefs.SetFloat("PlayerPosY", LastPlayerPosition.y);
        PlayerPrefs.SetFloat("PlayerPosZ", LastPlayerPosition.z);

        PlayerPrefs.SetInt("IsTunaSpawned", IsTunaSpawned ? 1 : 0);
        PlayerPrefs.SetFloat("TunaPosX", TunaPosition.x);
        PlayerPrefs.SetFloat("TunaPosY", TunaPosition.y);
        PlayerPrefs.SetFloat("TunaPosZ", TunaPosition.z);

        PlayerPrefs.Save();
        Debug.Log("[GameDataManager] Game state saved.");
    }

    public void LoadState()
    {
        CurrentStoryID = PlayerPrefs.GetString("CurrentStoryID", "library_intro");
        LastSceneName = PlayerPrefs.GetString("LastSceneName", "Library");
        LastCheckpointID = PlayerPrefs.GetString("LastCheckpointID", "library_intro");

        ObtainedItemIDs = new HashSet<string>(PlayerPrefs.GetString("ObtainedItemIDs", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        FinishedDialogueIDs = new HashSet<string>(PlayerPrefs.GetString("FinishedDialogueIDs", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        InspectedItemIDs = new HashSet<string>(PlayerPrefs.GetString("InspectedItemIDs", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        DefeatedMonsterIDs = new HashSet<string>(PlayerPrefs.GetString("DefeatedMonsterIDs", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        CompletedQuestIDs = new HashSet<string>(PlayerPrefs.GetString("CompletedQuestIDs", "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));

        float posX = PlayerPrefs.GetFloat("PlayerPosX", 0);
        float posY = PlayerPrefs.GetFloat("PlayerPosY", 0);
        float posZ = PlayerPrefs.GetFloat("PlayerPosZ", 0);
        LastPlayerPosition = new Vector3(posX, posY, posZ);

        IsTunaSpawned = PlayerPrefs.GetInt("IsTunaSpawned", 0) == 1;
        float tunaPosX = PlayerPrefs.GetFloat("TunaPosX", 0);
        float tunaPosY = PlayerPrefs.GetFloat("TunaPosY", 0);
        float tunaPosZ = PlayerPrefs.GetFloat("TunaPosZ", 0);
        TunaPosition = new Vector3(tunaPosX, tunaPosY, tunaPosZ);

        Debug.Log($"[GameDataManager] Game state loaded. Tuna: {(IsTunaSpawned ? "Spawned" : "Not spawned")}");
    }

    public void ResetData()
    {
        PlayerPrefs.DeleteKey("CurrentStoryID");
        PlayerPrefs.DeleteKey("LastSceneName");
        PlayerPrefs.DeleteKey("LastCheckpointID");
        PlayerPrefs.DeleteKey("ObtainedItemIDs");
        PlayerPrefs.DeleteKey("FinishedDialogueIDs");
        PlayerPrefs.DeleteKey("InspectedItemIDs");
        PlayerPrefs.DeleteKey("DefeatedMonsterIDs");
        PlayerPrefs.DeleteKey("CompletedQuestIDs");
        PlayerPrefs.DeleteKey("PlayerSpawnPointID");

        PlayerPrefs.DeleteKey("PlayerPosX");
        PlayerPrefs.DeleteKey("PlayerPosY");
        PlayerPrefs.DeleteKey("PlayerPosZ");

        PlayerPrefs.DeleteKey("HasSeenDoodlePage");
        PlayerPrefs.DeleteKey("HasCompletedSkillBookTutorial");

        PlayerPrefs.DeleteKey("IsTunaSpawned");
        PlayerPrefs.DeleteKey("TunaPosX");
        PlayerPrefs.DeleteKey("TunaPosY");
        PlayerPrefs.DeleteKey("TunaPosZ");

        CurrentStoryID = "library_intro";
        LastSceneName = "Library";
        LastCheckpointID = "library_intro";
        ObtainedItemIDs = new HashSet<string>();
        FinishedDialogueIDs = new HashSet<string>();
        InspectedItemIDs = new HashSet<string>();
        DefeatedMonsterIDs = new HashSet<string>();
        CompletedQuestIDs = new HashSet<string>();

        LastPlayerPosition = Vector3.zero;
        PlayerSpawnPointID = "";

        IsTunaSpawned = false;
        TunaPosition = Vector3.zero;

        PlayerPrefs.Save();

        Debug.Log("[GameDataManager] 메모리와 PlayerPrefs의 게임 데이터가 초기화되었습니다.");
    }

    public void SetLastPlayerPosition(Vector3 position)
    {
        LastPlayerPosition = position;
    }

    public void SetLastCheckpoint(string checkpointID)
    {
        LastCheckpointID = checkpointID;
    }

    public void SetLastSceneName(string sceneName)
    {
        LastSceneName = sceneName;
    }

    public void SetCurrentBattleMonster(MonsterDataSO data, string id, string dialogueId = null)
    {
        CurrentBattleMonster = data;
        CurrentBattleMonsterID = id;
        CurrentBattleDialogueID = dialogueId;
        canEscapeCurrentBattle = true;
    }

    public void SetCurrentBattleMonster(MonsterDataSO monster, string uniqueID, string dialogueID, bool canEscape)
    {
        CurrentBattleMonster = monster;
        CurrentBattleMonsterID = uniqueID;
        CurrentBattleDialogueID = dialogueID;
        canEscapeCurrentBattle = canEscape;
    }

    public void SetPreviousScene(string sceneName)
    {
        PreviousSceneName = sceneName;
    }

    public bool IsQuestCompleted(string questID) => CompletedQuestIDs.Contains(questID);
    public void SetQuestCompleted(string questID) => CompletedQuestIDs.Add(questID);

    public void SetNextPlayerSpawnPoint(string spawnPointID)
    {
        PlayerSpawnPointID = spawnPointID;
    }

    public void ClearPlayerSpawnPoint()
    {
        PlayerSpawnPointID = null;
    }
}