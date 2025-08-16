using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorController : MonoBehaviour
{
    [Header("씬 이동 설정")]
    [SerializeField] private string targetSceneName = "Library_basement";

    [Header("사운드")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip doorOpenSound;
    [SerializeField] private AudioClip doorLockedSound;

    [Header("이동 딜레이")]
    [SerializeField] private float sceneTransitionDelay = 1.0f; // 사운드 재생 후 이동

    [Header("디버그")]
    [SerializeField] private bool debugMode = false;

    private bool isOpening = false;

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void OnDoorClick()
    {
        if (isOpening) return; // 이미 열리는 중이면 무시

        bool hasKey = ItemManager.HasItem("key");

        if (hasKey)
        {
            if (debugMode)
                Debug.Log("[DoorController] 열쇠 보유 확인됨. 문 열기 시작.");

            OpenDoor();
        }
        else
        {
            if (debugMode)
                Debug.Log("[DoorController] 열쇠 없음. 잠긴 문 대화 시작.");

            PlayLockedSound();
            DialogueManager.Instance.StartDialogue("library_door_locked");
        }
    }

    private void OpenDoor()
    {
        isOpening = true;

        // 사운드 재생
        PlayOpenSound();

        // 플레이어 조작 금지 (이동 중)
        DisablePlayerMovement();

        // 딜레이 후 씬 이동
        StartCoroutine(TransitionToBasement());
    }

    private void PlayOpenSound()
    {
        if (audioSource != null && doorOpenSound != null)
        {
            audioSource.PlayOneShot(doorOpenSound);
        }
    }

    private void PlayLockedSound()
    {
        if (audioSource != null && doorLockedSound != null)
        {
            audioSource.PlayOneShot(doorLockedSound);
        }
    }

    private void DisablePlayerMovement()
    {
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            player.SetCanMove(false);
        }
    }

    private IEnumerator TransitionToBasement()
    {
        if (debugMode)
            Debug.Log($"[DoorController] {sceneTransitionDelay}초 후 {targetSceneName} 씬으로 이동");

        // 문 열리는 애니메이션 대기
        yield return new WaitForSeconds(sceneTransitionDelay);

        // 스토리 진행도 업데이트 (지하실 진입)
        UpdateStoryProgress();

        // 씬 이동
        LoadTargetScene();
    }

    private void UpdateStoryProgress()
    {
        // 지하실 진입 이벤트를 스토리 시스템에 알림
        if (StoryProgressManager.Instance != null)
        {
            // 지하실 진입 상태 저장
            PlayerPrefs.SetInt("Map_Library_basement_Entered", 1);
            PlayerPrefs.Save();

            // 스토리 진행 시도
            StoryProgressManager.Instance.TryAdvanceToNextStory();
        }
    }

    private void LoadTargetScene()
    {
        if (debugMode)
            Debug.Log($"[DoorController] {targetSceneName} 씬 로드 시작");

        // 현재 씬 이름 저장 (나중에 돌아올 수 있도록)
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();

        // 씬 이동
        SceneManager.LoadScene(targetSceneName);
    }

    // 외부에서 강제로 문 열기 (스토리 진행용)
    public void ForceOpenDoor()
    {
        if (debugMode)
            Debug.Log("[DoorController] 강제로 문 열기 실행됨");

        OpenDoor();
    }

    // 문이 열려있는지 확인
    public bool IsDoorOpen()
    {
        return isOpening;
    }

    // 개발자용 디버그 메서드들
    [ContextMenu("Test Door Open")]
    public void TestDoorOpen()
    {
        Debug.Log("[DoorController] 테스트: 문 열기");
        OpenDoor();
    }

    [ContextMenu("Test Door Locked")]
    public void TestDoorLocked()
    {
        Debug.Log("[DoorController] 테스트: 잠긴 문");
        PlayLockedSound();
        DialogueManager.Instance.StartDialogue("library_door_locked");
    }

    [ContextMenu("Debug: Check Key Status")]
    public void DebugCheckKeyStatus()
    {
        bool hasKey = ItemManager.HasItem("key");
        Debug.Log($"[DoorController] 열쇠 보유 상태: {hasKey}");
    }
}