using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuestItemHandler : MonoBehaviour
{
    [Header("Quest Dialogue Settings")]
    [Tooltip("CSV에 정의된 퀘스트 대화 ID")]
    public string questDialogueSetID = "quest_ancient_artifact";

    [Header("NPC Reference")]
    public NPCController questNPC;  // 실제 NPC 참조

    [Header("Target Quest Items - 방식 1: 배열")]
    public string[] targetQuestItemIDs = { "History_book01", "ancient_artifact", "cursed_gem" };

    [Header("Target Quest Items - 방식 2: 특정 아이템")]
    public string specificQuestItemID = "";  // 특정 아이템 ID만 체크
    public bool useQuestItemTag = false;     // isQuestItem 태그로 체크

    [Header("Monster Spawn Settings")]
    public GameObject[] monsterPrefabs;
    public Transform[] spawnPoints;
    public int monstersToSpawn = 3;
    public float spawnDelay = 1f;

    [Header("Audio")]
    public AudioClip questItemSound;
    public AudioClip monsterSpawnSound;

    [Header("Effects")]
    public GameObject questItemEffect;
    public GameObject monsterSpawnEffect;

    [Header("Player Reference")]
    public PlayerStats playerStats;  // 직접 참조

    private AudioSource audioSource;
    private bool questItemTriggered = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        ValidateSettings();
    }

    void ValidateSettings()
    {
        if (string.IsNullOrEmpty(questDialogueSetID))
        {
            Debug.LogError("QuestItemHandler: questDialogueSetID가 설정되지 않았습니다!");
            return;
        }

        if (questNPC == null)
        {
            Debug.LogWarning("QuestItemHandler: Quest NPC가 설정되지 않았습니다!");
        }

        if (playerStats == null)
        {
            Debug.LogWarning("QuestItemHandler: PlayerStats가 설정되지 않았습니다!");
        }

        Debug.Log($"QuestItemHandler 초기화 완료 - DialogueSetID: {questDialogueSetID}");
    }

    public void OnQuestItemObtained(ItemDataSO questItem, Vector3 obtainPosition)
    {
        if (questItemTriggered) return;

        string itemName = questItem != null ? questItem.GetDisplayName() : "알 수 없는 아이템";
        Debug.Log($"=== 퀘스트 아이템 획득: {itemName} ===");

        questItemTriggered = true;
        StartCoroutine(HandleQuestItemSequence(questItem, obtainPosition));
    }

    IEnumerator HandleQuestItemSequence(ItemDataSO questItem, Vector3 obtainPosition)
    {
        // 1. 퀘스트 아이템 획득 사운드 재생
        if (questItemSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(questItemSound);
        }

        // 2. 퀘스트 아이템 획득 이펙트 생성
        if (questItemEffect != null)
        {
            GameObject effect = Instantiate(questItemEffect, obtainPosition, Quaternion.identity);
            Destroy(effect, 3f);
        }

        yield return new WaitForSeconds(0.5f);

        // 3. DialogueManager 검증
        if (DialogueManager.Instance == null)
        {
            Debug.LogError("DialogueManager.Instance가 null입니다!");
            yield break;
        }

        // 4. 퀘스트 대화 시작
        Debug.Log($"퀘스트 다이얼로그 시작: DialogueSetID {questDialogueSetID}");

        StartQuestDialogue();

        // 5. 다이얼로그가 끝날 때까지 대기
        yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());

        Debug.Log("퀘스트 다이얼로그 종료됨");

        yield return new WaitForSeconds(0.5f);

        // 6. 마지막으로 몬스터 생성
        yield return StartCoroutine(SpawnMonsters());
    }

    void StartQuestDialogue()
    {
        DialogueManager.Instance.StartDialogue(questDialogueSetID);
    }

    Sprite GetPlayerPortrait()
    {
        if (playerStats != null && playerStats.playerPortrait != null)
        {
            Debug.Log($"PlayerStats에서 플레이어 포트레이트 가져옴: {playerStats.playerPortrait.name}");
            return playerStats.playerPortrait;
        }
        else
        {
            Debug.LogWarning("PlayerStats 또는 playerPortrait가 null입니다!");
            return null;
        }
    }

    Sprite GetNPCPortrait()
    {
        if (questNPC != null && questNPC.npcPortrait != null)
        {
            Debug.Log($"QuestNPC에서 NPC 포트레이트 가져옴: {questNPC.npcPortrait.name}");
            return questNPC.npcPortrait;
        }
        else
        {
            Debug.LogWarning("QuestNPC 또는 npcPortrait가 null입니다!");
            return null;
        }
    }

    IEnumerator SpawnMonsters()
    {
        if (monsterPrefabs.Length == 0 || spawnPoints.Length == 0)
        {
            Debug.LogWarning("몬스터 프리팹이나 스폰 포인트가 설정되지 않았습니다!");
            yield break;
        }

        Debug.Log("몬스터 생성 시작!");

        if (monsterSpawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(monsterSpawnSound);
        }

        for (int i = 0; i < monstersToSpawn; i++)
        {
            GameObject monsterPrefab = monsterPrefabs[Random.Range(0, monsterPrefabs.Length)];
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

            GameObject spawnedMonster = Instantiate(monsterPrefab, spawnPoint.position, spawnPoint.rotation);

            if (monsterSpawnEffect != null)
            {
                GameObject effect = Instantiate(monsterSpawnEffect, spawnPoint.position, Quaternion.identity);
                Destroy(effect, 2f);
            }

            Debug.Log($"몬스터 생성: {monsterPrefab.name} at {spawnPoint.name}");

            if (i < monstersToSpawn - 1)
                yield return new WaitForSeconds(spawnDelay);
        }

        Debug.Log("모든 몬스터 생성 완료!");
    }

    public void OnSpecificQuestItemObtained(string questItemID, Vector3 obtainPosition)
    {
        Debug.Log($"OnSpecificQuestItemObtained 호출: {questItemID}");

        bool isTargetItem = false;

        // 방식 1: targetQuestItemIDs 배열 체크
        if (targetQuestItemIDs != null)
        {
            foreach (string targetID in targetQuestItemIDs)
            {
                if (questItemID == targetID)
                {
                    isTargetItem = true;
                    break;
                }
            }
        }

        // 방식 2: specificQuestItemID 체크
        if (!isTargetItem && !string.IsNullOrEmpty(specificQuestItemID))
        {
            isTargetItem = (questItemID == specificQuestItemID);
        }

        if (isTargetItem)
        {
            Debug.Log($"퀘스트 트리거 아이템 확인됨: {questItemID}");
            ItemDataSO questItem = LoadQuestItemData(questItemID);
            OnQuestItemObtained(questItem, obtainPosition);
        }
        else
        {
            Debug.Log($"이 아이템은 퀘스트 트리거가 아닙니다: {questItemID}");
        }
    }

    ItemDataSO LoadQuestItemData(string questItemID)
    {
        // Resources에서 로드 시도
        ItemDataSO questItem = Resources.Load<ItemDataSO>($"Items/{questItemID}");

        if (questItem == null)
        {
            // 씬에서 찾기 시도
            GameObject itemObj = GameObject.Find(questItemID);
            if (itemObj != null)
            {
                ItemManager itemManager = itemObj.GetComponent<ItemManager>();
                if (itemManager != null)
                    questItem = itemManager.itemData;
            }
        }

        if (questItem == null)
        {
            Debug.LogWarning($"퀘스트 아이템 데이터를 찾을 수 없습니다: {questItemID}");
        }

        return questItem;
    }

    // 개발자용 디버그 메서드들
    [ContextMenu("Test Quest Dialogue")]
    public void TestQuestDialogue()
    {
        if (DialogueManager.Instance != null)
        {
            Debug.Log($"테스트 대화 시작: {questDialogueSetID}");
            StartQuestDialogue();
        }
        else
        {
            Debug.LogError("DialogueManager.Instance가 null입니다!");
        }
    }

    [ContextMenu("Spawn Monsters Only")]
    public void SpawnMonstersOnly()
    {
        StartCoroutine(SpawnMonsters());
    }

    [ContextMenu("Reset Quest Trigger")]
    public void ResetQuestTrigger()
    {
        questItemTriggered = false;
        Debug.Log("퀘스트 트리거 리셋됨");
    }

    [ContextMenu("Debug Settings")]
    public void DebugSettings()
    {
        Debug.Log("=== QuestItemHandler 설정 정보 ===");
        Debug.Log($"Quest Dialogue Set ID: {questDialogueSetID}");
        Debug.Log($"Quest NPC: {(questNPC != null ? questNPC.name : "NULL")}");
        Debug.Log($"Player Stats: {(playerStats != null ? playerStats.name : "NULL")}");
        Debug.Log($"Target Item IDs: [{string.Join(", ", targetQuestItemIDs)}]");
        Debug.Log($"Specific Item ID: {specificQuestItemID}");
        Debug.Log($"Use Quest Item Tag: {useQuestItemTag}");
        Debug.Log($"Quest Triggered: {questItemTriggered}");
    }
}