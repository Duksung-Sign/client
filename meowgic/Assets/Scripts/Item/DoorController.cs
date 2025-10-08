using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorController : MonoBehaviour, IInteractable
{
    [Header("고유 ID")]
    [SerializeField] private string doorID = "library_basement_door";

    [Header("씬 이동 설정")]
    [SerializeField] private string targetSceneName = "Library_basement";

    [Header("필요 아이템")]
    [SerializeField] private string requiredItemID = "key";

    [Header("사운드")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorLockedSound;

    [Header("이동 딜레이")]
    [SerializeField] private float sceneTransitionDelay = 1.0f;

    [Header("스토리 제한")]
    [SerializeField] private bool checkStoryRestriction = false;
    [SerializeField] private string[] blockedStoryIDs;
    [SerializeField] private string blockedDialogueID = "door_blocked";

    [Header("디버그")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private string targetSpawnPointID = "spawn_from_basement";

    private bool isOpening = false;

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void Interact()
    {
        if (isOpening) return;

        if (checkStoryRestriction && IsStoryBlocked())
        {
            ShowBlockedDialogue();
            return;
        }

        bool hasKey = GameDataManager.Instance.HasItem(requiredItemID);

        if (hasKey)
        {
            if (debugMode) Debug.Log($"[DoorController] 아이템 '{requiredItemID}' 보유 확인. 문 열기 시작.");
            OpenDoor();
        }
        else
        {
            if (debugMode) Debug.Log($"[DoorController] 아이템 '{requiredItemID}' 없음. 잠김 이벤트 발생.");
            PlayLockedSound();
            GameEventManager.Instance.TriggerLockedDoorInteracted(doorID);
        }
    }

    private bool IsStoryBlocked()
    {
        if (GameDataManager.Instance == null || blockedStoryIDs == null || blockedStoryIDs.Length == 0)
            return false;

        string currentStory = GameDataManager.Instance.CurrentStoryID;

        foreach (string blockedID in blockedStoryIDs)
        {
            if (currentStory == blockedID)
            {
                if (debugMode) Debug.Log($"[DoorController] 스토리 '{currentStory}'에서는 이 문을 사용할 수 없습니다.");
                return true;
            }
        }

        return false;
    }

    private void ShowBlockedDialogue()
    {
        if (DialogueManager.Instance != null && !string.IsNullOrEmpty(blockedDialogueID))
        {
            DialogueManager.Instance.StartDialogue(blockedDialogueID);
        }
    }

    public string GetInteractionPromptText()
    {
        return "조사하기";
    }

    private void OpenDoor()
    {
        isOpening = true;
        PlayOpenSound();
        GameManager.Instance.ChangeState(GameState.Cutscene);
        StartCoroutine(TransitionToScene());
    }

    private IEnumerator TransitionToScene()
    {
        if (debugMode) Debug.Log($"[DoorController] {sceneTransitionDelay}초 후 {targetSceneName} 씬으로 이동");
        yield return new WaitForSeconds(sceneTransitionDelay);
        LoadTargetScene();
    }

    private void LoadTargetScene()
    {
        GameDataManager.Instance.SetNextPlayerSpawnPoint(targetSpawnPointID);
        GameDataManager.Instance.SetPreviousScene(SceneManager.GetActiveScene().name);
        GameManager.Instance.ChangeScene(targetSceneName, () => {
            GameManager.Instance.ChangeState(GameState.Field);
            if (GameEventManager.Instance != null)
            {
                GameEventManager.Instance.TriggerMapEntered(targetSceneName);
            }
        });
    }

    private void PlayOpenSound()
    {
        if (audioSource != null && doorOpenSound != null)
        {
            audioSource.PlayOneShot(doorOpenSound, 0.3f);
        }
    }

    private void PlayLockedSound()
    {
        if (audioSource != null && doorLockedSound != null)
        {
            audioSource.PlayOneShot(doorLockedSound, 0.3f);
        }
    }

    public bool IsCurrentlyInteractable()
    {
        return true;
    }
}