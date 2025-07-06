using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CSV 데이터의 문자열 ID와 실제 게임 오브젝트/SO들 간의 매핑을 관리하는 시스템
/// </summary>
public class MappingSystemManager : MonoBehaviour
{
    public static MappingSystemManager Instance;

    [Header("매핑 시스템 설정")]
    public bool useResourcesFolder = true;
    public bool useCachedMappings = true;
    public bool debugMappings = true;

    // 캐시된 매핑들
    private Dictionary<string, Sprite> portraitCache = new Dictionary<string, Sprite>();
    private Dictionary<string, ItemDataSO> itemCache = new Dictionary<string, ItemDataSO>();
    private Dictionary<string, MonsterDataSO> monsterCache = new Dictionary<string, MonsterDataSO>();
    private Dictionary<string, AudioClip> audioCache = new Dictionary<string, AudioClip>();
    private Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();

    // 씬 내 오브젝트 매핑
    private Dictionary<string, NPCController> npcMappings = new Dictionary<string, NPCController>();
    private Dictionary<string, Transform> locationMappings = new Dictionary<string, Transform>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeMappingSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 씬이 로드될 때마다 씬 내 오브젝트들 매핑
        RefreshSceneMappings();
    }

    void InitializeMappingSystem()
    {
        if (debugMappings)
            Debug.Log("[MappingSystem] 매핑 시스템 초기화 시작");

        if (useResourcesFolder)
        {
            PreloadResourceMappings();
        }

        if (debugMappings)
            Debug.Log("[MappingSystem] 매핑 시스템 초기화 완료");
    }

    void PreloadResourceMappings()
    {
        // 주요 리소스들 미리 로드하여 캐시
        StartCoroutine(PreloadPortraits());
        StartCoroutine(PreloadItems());
        StartCoroutine(PreloadMonsters());
        StartCoroutine(PreloadAudio());
        StartCoroutine(PreloadPrefabs());
    }

    IEnumerator PreloadPortraits()
    {
        Sprite[] portraits = Resources.LoadAll<Sprite>("Portraits");
        foreach (var portrait in portraits)
        {
            portraitCache[portrait.name] = portrait;
        }

        if (debugMappings)
            Debug.Log($"[MappingSystem] 초상화 {portraits.Length}개 로드됨");

        yield return null;
    }

    IEnumerator PreloadItems()
    {
        ItemDataSO[] items = Resources.LoadAll<ItemDataSO>("Items");
        foreach (var item in items)
        {
            itemCache[item.itemID] = item;
        }

        if (debugMappings)
            Debug.Log($"[MappingSystem] 아이템 {items.Length}개 로드됨");

        yield return null;
    }

    IEnumerator PreloadMonsters()
    {
        MonsterDataSO[] monsters = Resources.LoadAll<MonsterDataSO>("Monsters");
        foreach (var monster in monsters)
        {
            // 몬스터는 이름을 키로 사용 (수정: monsterName -> GetDisplayName())
            string key = monster.GetDisplayName().ToLower().Replace(" ", "_");
            monsterCache[key] = monster;
        }

        if (debugMappings)
            Debug.Log($"[MappingSystem] 몬스터 {monsters.Length}개 로드됨");

        yield return null;
    }

    IEnumerator PreloadAudio()
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio");
        foreach (var clip in clips)
        {
            audioCache[clip.name] = clip;
        }

        if (debugMappings)
            Debug.Log($"[MappingSystem] 오디오 {clips.Length}개 로드됨");

        yield return null;
    }

    IEnumerator PreloadPrefabs()
    {
        GameObject[] prefabs = Resources.LoadAll<GameObject>("Prefabs");
        foreach (var prefab in prefabs)
        {
            prefabCache[prefab.name] = prefab;
        }

        if (debugMappings)
            Debug.Log($"[MappingSystem] 프리팹 {prefabs.Length}개 로드됨");

        yield return null;
    }

    public void RefreshSceneMappings()
    {
        // 씬 내 NPC들 매핑
        NPCController[] npcs = FindObjectsOfType<NPCController>();
        npcMappings.Clear();

        foreach (var npc in npcs)
        {
            string key = npc.npcName.ToLower().Replace(" ", "_");
            npcMappings[key] = npc;
        }

        // 씬 내 위치들 매핑 (특정 태그나 이름으로)
        GameObject[] locations = GameObject.FindGameObjectsWithTag("Location");
        locationMappings.Clear();

        foreach (var location in locations)
        {
            string key = location.name.ToLower().Replace(" ", "_");
            locationMappings[key] = location.transform;
        }

        if (debugMappings)
            Debug.Log($"[MappingSystem] 씬 매핑 새로고침 - NPC: {npcs.Length}개, 위치: {locations.Length}개");
    }

    // ===== 매핑 조회 메서드들 =====

    /// <summary>
    /// CSV의 문자열 ID로 초상화 Sprite 찾기
    /// 예: "player_portrait", "npc_blacksmith", "sage_01"
    /// </summary>
    public Sprite GetPortraitByID(string portraitID)
    {
        if (string.IsNullOrEmpty(portraitID))
            return null;

        // 1. 캐시에서 찾기
        if (useCachedMappings && portraitCache.ContainsKey(portraitID))
        {
            return portraitCache[portraitID];
        }

        // 2. Resources 폴더에서 동적 로드
        if (useResourcesFolder)
        {
            Sprite portrait = Resources.Load<Sprite>($"Portraits/{portraitID}");
            if (portrait != null)
            {
                if (useCachedMappings)
                    portraitCache[portraitID] = portrait;
                return portrait;
            }
        }

        if (debugMappings)
            Debug.LogWarning($"[MappingSystem] 초상화를 찾을 수 없음: {portraitID}");

        return null;
    }

    /// <summary>
    /// CSV의 문자열 ID로 아이템 데이터 찾기
    /// 예: "health_potion", "ancient_artifact", "iron_sword"
    /// </summary>
    public ItemDataSO GetItemByID(string itemID)
    {
        if (string.IsNullOrEmpty(itemID))
            return null;

        // 1. 캐시에서 찾기
        if (useCachedMappings && itemCache.ContainsKey(itemID))
        {
            return itemCache[itemID];
        }

        // 2. Resources 폴더에서 동적 로드
        if (useResourcesFolder)
        {
            ItemDataSO item = Resources.Load<ItemDataSO>($"Items/{itemID}");
            if (item != null)
            {
                if (useCachedMappings)
                    itemCache[itemID] = item;
                return item;
            }
        }

        if (debugMappings)
            Debug.LogWarning($"[MappingSystem] 아이템을 찾을 수 없음: {itemID}");

        return null;
    }

    /// <summary>
    /// CSV의 문자열 ID로 몬스터 데이터 찾기
    /// 예: "goblin", "dragon", "skeleton_warrior"
    /// </summary>
    public MonsterDataSO GetMonsterByID(string monsterID)
    {
        if (string.IsNullOrEmpty(monsterID))
            return null;

        string key = monsterID.ToLower().Replace(" ", "_");

        // 1. 캐시에서 찾기
        if (useCachedMappings && monsterCache.ContainsKey(key))
        {
            return monsterCache[key];
        }

        // 2. Resources 폴더에서 동적 로드
        if (useResourcesFolder)
        {
            MonsterDataSO monster = Resources.Load<MonsterDataSO>($"Monsters/{key}");
            if (monster != null)
            {
                if (useCachedMappings)
                    monsterCache[key] = monster;
                return monster;
            }
        }

        if (debugMappings)
            Debug.LogWarning($"[MappingSystem] 몬스터를 찾을 수 없음: {monsterID}");

        return null;
    }

    /// <summary>
    /// CSV의 문자열 ID로 씬 내 NPC 찾기
    /// 예: "village_elder", "blacksmith", "merchant"
    /// </summary>
    public NPCController GetNPCByID(string npcID)
    {
        if (string.IsNullOrEmpty(npcID))
            return null;

        string key = npcID.ToLower().Replace(" ", "_");

        if (npcMappings.ContainsKey(key))
        {
            return npcMappings[key];
        }

        if (debugMappings)
            Debug.LogWarning($"[MappingSystem] NPC를 찾을 수 없음: {npcID}");

        return null;
    }

    /// <summary>
    /// CSV의 문자열 ID로 씬 내 위치 찾기
    /// 예: "town_center", "forest_entrance", "cave_entrance"
    /// </summary>
    public Transform GetLocationByID(string locationID)
    {
        if (string.IsNullOrEmpty(locationID))
            return null;

        string key = locationID.ToLower().Replace(" ", "_");

        if (locationMappings.ContainsKey(key))
        {
            return locationMappings[key];
        }

        if (debugMappings)
            Debug.LogWarning($"[MappingSystem] 위치를 찾을 수 없음: {locationID}");

        return null;
    }

    /// <summary>
    /// CSV의 문자열 ID로 프리팹 찾기
    /// 예: "fireball_effect", "monster_goblin", "item_chest"
    /// </summary>
    public GameObject GetPrefabByID(string prefabID)
    {
        if (string.IsNullOrEmpty(prefabID))
            return null;

        // 1. 캐시에서 찾기
        if (useCachedMappings && prefabCache.ContainsKey(prefabID))
        {
            return prefabCache[prefabID];
        }

        // 2. Resources 폴더에서 동적 로드
        if (useResourcesFolder)
        {
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/{prefabID}");
            if (prefab != null)
            {
                if (useCachedMappings)
                    prefabCache[prefabID] = prefab;
                return prefab;
            }
        }

        if (debugMappings)
            Debug.LogWarning($"[MappingSystem] 프리팹을 찾을 수 없음: {prefabID}");

        return null;
    }

    /// <summary>
    /// CSV의 문자열 ID로 오디오 클립 찾기
    /// 예: "bgm_village", "sfx_sword_hit", "voice_npc_greeting"
    /// </summary>
    public AudioClip GetAudioByID(string audioID)
    {
        if (string.IsNullOrEmpty(audioID))
            return null;

        // 1. 캐시에서 찾기
        if (useCachedMappings && audioCache.ContainsKey(audioID))
        {
            return audioCache[audioID];
        }

        // 2. Resources 폴더에서 동적 로드
        if (useResourcesFolder)
        {
            AudioClip clip = Resources.Load<AudioClip>($"Audio/{audioID}");
            if (clip != null)
            {
                if (useCachedMappings)
                    audioCache[audioID] = clip;
                return clip;
            }
        }

        if (debugMappings)
            Debug.LogWarning($"[MappingSystem] 오디오 클립을 찾을 수 없음: {audioID}");

        return null;
    }

    // ===== 디버그 메서드들 =====
    [ContextMenu("Show All Cached Mappings")]
    public void ShowAllCachedMappings()
    {
        Debug.Log("=== 캐시된 매핑 현황 ===");
        Debug.Log($"초상화: {portraitCache.Count}개");
        Debug.Log($"아이템: {itemCache.Count}개");
        Debug.Log($"몬스터: {monsterCache.Count}개");
        Debug.Log($"오디오: {audioCache.Count}개");
        Debug.Log($"프리팹: {prefabCache.Count}개");
        Debug.Log($"NPC: {npcMappings.Count}개");
        Debug.Log($"위치: {locationMappings.Count}개");
    }

    [ContextMenu("Clear All Caches")]
    public void ClearAllCaches()
    {
        portraitCache.Clear();
        itemCache.Clear();
        monsterCache.Clear();
        audioCache.Clear();
        prefabCache.Clear();
        Debug.Log("[MappingSystem] 모든 캐시 클리어됨");
    }

    [ContextMenu("Refresh Scene Mappings")]
    public void RefreshSceneMappingsContext()
    {
        RefreshSceneMappings();
    }

    [ContextMenu("Test All Mappings")]
    public void TestAllMappings()
    {
        Debug.Log("=== 매핑 시스템 테스트 ===");

        // 테스트 예시들
        var testPortrait = GetPortraitByID("player_portrait");
        Debug.Log($"초상화 테스트: {(testPortrait != null ? "성공" : "실패")}");

        var testItem = GetItemByID("health_potion");
        Debug.Log($"아이템 테스트: {(testItem != null ? "성공" : "실패")}");

        var testMonster = GetMonsterByID("goblin");
        Debug.Log($"몬스터 테스트: {(testMonster != null ? "성공" : "실패")}");
    }
}

// ===== 매핑 시스템 사용 예시 =====
public static class MappingExamples
{
    /// <summary>
    /// 스토리 액션에서 아이템 생성하는 예시
    /// CSV: ActionType="spawn_item", Target="health_potion", Parameter="town_center"
    /// </summary>
    public static void SpawnItemExample(string itemID, string locationID)
    {
        // 1. CSV ID로 실제 데이터 찾기
        ItemDataSO itemData = MappingSystemManager.Instance.GetItemByID(itemID);
        Transform spawnLocation = MappingSystemManager.Instance.GetLocationByID(locationID);

        if (itemData != null && spawnLocation != null)
        {
            // 2. 실제 게임 오브젝트 생성
            GameObject itemPrefab = MappingSystemManager.Instance.GetPrefabByID($"item_{itemID}");
            if (itemPrefab != null)
            {
                GameObject spawnedItem = Object.Instantiate(itemPrefab, spawnLocation.position, spawnLocation.rotation);

                // 3. 아이템 데이터 연결
                ItemManager itemManager = spawnedItem.GetComponent<ItemManager>();
                if (itemManager != null)
                {
                    itemManager.itemData = itemData;
                }
            }
        }
    }

    /// <summary>
    /// 대화에서 NPC 초상화 가져오는 예시
    /// CSV: SpeakerName="village_elder"
    /// </summary>
    public static Sprite GetNPCPortraitExample(string npcID)
    {
        // 1. 씬 내 NPC에서 가져오기 시도
        NPCController npc = MappingSystemManager.Instance.GetNPCByID(npcID);
        if (npc != null && npc.npcPortrait != null)
        {
            return npc.npcPortrait;
        }

        // 2. Resources 폴더에서 가져오기 시도
        return MappingSystemManager.Instance.GetPortraitByID($"npc_{npcID}");
    }
}