using UnityEngine;
using DG.Tweening;

public class TunaSpawner : MonoBehaviour
{
    [Header("튜나 설정")]
    public GameObject tunaPrefab;
    public Transform spawnPosition;
    public string spawnTriggerLineID = "hello_tuna_002";

    [Header("연출")]
    public AudioClip spawnSound;

    [Header("문 소리 연출")]
    public AudioClip doorBangSound;

    [Header("카메라 줌")]
    public float zoomTargetSize = 2f;
    public float zoomDuration = 0.8f;

    [Header("이동 설정")]
    public Transform doorPosition;
    public float moveDuration = 1.5f;
    public float floatHeight = 0.4f;
    public float floatSpeed = 0.4f;

    private GameObject tunaInstance;
    private AudioSource audioSource;
    private bool hasRestoredState = false;

    private Vector3 cachedTunaPosition;
    private bool wasTunaSpawned = false;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void Start()
    {
        RestoreTunaState();
    }

    void Update()
    {
        if (tunaInstance != null)
        {
            cachedTunaPosition = tunaInstance.transform.position;
            wasTunaSpawned = true;
        }
    }

    void OnEnable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnLineDisplayed += HandleLineDisplayed;
        }

        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnDialogueFinished += HandleDialogueFinished;
            GameEventManager.Instance.OnCustomEvent += HandleCustomEvent;
        }
    }

    void OnDisable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnLineDisplayed -= HandleLineDisplayed;
        }

        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnDialogueFinished -= HandleDialogueFinished;
            GameEventManager.Instance.OnCustomEvent -= HandleCustomEvent;
        }

        SaveTunaState();
    }

    private void RestoreTunaState()
    {
        if (GameDataManager.Instance == null)
        {
            Debug.LogWarning("[TunaSpawner] GameDataManager가 없습니다.");
            return;
        }

        if (hasRestoredState)
        {
            Debug.Log("[TunaSpawner] 이미 상태를 복원했습니다.");
            return;
        }

        string currentStory = GameDataManager.Instance.CurrentStoryID;

        string[] tunaStories = new string[]
        {
            "wait_for_skill_tutorial",
            "after_skill_tutorial",
            "library_tuna_meeting_story",
            "library_after_tuna"
        };

        bool shouldTunaExist = System.Array.Exists(tunaStories, story => story == currentStory);

        Debug.Log($"[TunaSpawner] 현재 스토리: {currentStory}, 튜나 존재 여부: {GameDataManager.Instance.IsTunaSpawned}, 튜나가 있어야 하는가: {shouldTunaExist}");

        if (shouldTunaExist && GameDataManager.Instance.IsTunaSpawned)
        {
            Vector3 restoredPosition = GameDataManager.Instance.TunaPosition;

            if (restoredPosition != Vector3.zero)
            {
                tunaInstance = Instantiate(tunaPrefab, restoredPosition, Quaternion.identity);
                cachedTunaPosition = restoredPosition;
                wasTunaSpawned = true;
                Debug.Log($"[TunaSpawner] 튜나 상태 복원: 위치 {restoredPosition}");
            }
            else
            {
                tunaInstance = Instantiate(tunaPrefab, spawnPosition.position, Quaternion.identity);
                cachedTunaPosition = spawnPosition.position;
                wasTunaSpawned = true;
                Debug.Log("[TunaSpawner] 튜나 기본 위치에 복원");
            }

            hasRestoredState = true;
        }
        else if (!shouldTunaExist)
        {
            Debug.Log($"[TunaSpawner] 현재 스토리({currentStory})에서는 튜나가 등장하지 않습니다.");
        }
    }

    private void SaveTunaState()
    {
        if (GameDataManager.Instance == null) return;

        bool isTunaAlive = tunaInstance != null;

        if (isTunaAlive)
        {
            cachedTunaPosition = tunaInstance.transform.position;
            wasTunaSpawned = true;
        }

        if (wasTunaSpawned)
        {
            GameDataManager.Instance.SetTunaSpawned(true, cachedTunaPosition);
            Debug.Log($"[TunaSpawner] 튜나 상태 저장: 위치 {cachedTunaPosition} (캐시 사용)");
        }
        else
        {
            GameDataManager.Instance.SetTunaSpawned(false);
            Debug.Log("[TunaSpawner] 튜나 상태 저장: 비생성");
        }

        GameDataManager.Instance.SaveState();
    }

    private void HandleLineDisplayed(string lineID)
    {
        if (lineID == spawnTriggerLineID && tunaInstance == null)
        {
            SpawnTuna();
        }

        if (lineID == "after_tutorial_004")
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PauseBGM();
            }

            if (doorBangSound != null)
            {
                audioSource.PlayOneShot(doorBangSound, 1.0f);
            }

            Debug.Log("[TunaSpawner] 브금 일시정지 및 문 소리 재생");
        }
    }

    private void HandleDialogueFinished(string eventType, string dialogueId)
    {
        if (dialogueId == "library_tuna_meeting")
        {
            CameraController camController = Camera.main?.GetComponent<CameraController>();
            if (camController != null)
            {
                camController.ResetZoom(1f);
            }
        }
        else if (dialogueId == "library_tuna_tutorial")
        {
            if (tunaInstance != null && doorPosition != null)
            {
                MoveTunaToDoor();
            }
            else
            {
                Debug.LogWarning("[TunaSpawner] 튜나 또는 문 위치가 없어서 이동할 수 없습니다.");
            }
        }
    }

    private void HandleCustomEvent(string eventType, string eventID)
    {
        if (eventID == "TUNA_ARRIVED_AT_DOOR")
        {
            SaveTunaState();
        }
    }

    private void SpawnTuna()
    {
        CameraController camController = Camera.main?.GetComponent<CameraController>();
        if (camController != null)
        {
            camController.ZoomTo(zoomTargetSize, zoomDuration, () => {
                CreateTunaInstance();
            });
        }
        else
        {
            CreateTunaInstance();
        }
    }

    private void CreateTunaInstance()
    {
        if (tunaInstance != null)
        {
            Debug.LogWarning("[TunaSpawner] 튜나가 이미 존재합니다.");
            return;
        }

        tunaInstance = Instantiate(tunaPrefab, spawnPosition.position, Quaternion.identity);

        if (spawnSound != null)
        {
            audioSource.PlayOneShot(spawnSound, 0.15f);
        }

        tunaInstance.transform.localScale = Vector3.zero;
        tunaInstance.transform.DOScale(1f, 0.5f)
            .SetEase(Ease.OutBack);

        tunaInstance.transform.DOMoveY(spawnPosition.position.y + 0.2f, 0.3f)
            .SetLoops(2, LoopType.Yoyo);

        cachedTunaPosition = tunaInstance.transform.position;
        wasTunaSpawned = true;

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SetTunaSpawned(true, cachedTunaPosition);
            GameDataManager.Instance.SaveState();
        }

        Debug.Log("[TunaSpawner] 튜나 등장!");
    }

    private void MoveTunaToDoor()
    {
        if (tunaInstance == null)
        {
            Debug.LogWarning("[TunaSpawner] 튜나가 없어서 이동할 수 없습니다.");
            return;
        }

        if (doorPosition == null)
        {
            Debug.LogWarning("[TunaSpawner] 문 위치가 설정되지 않았습니다.");
            return;
        }

        Vector3 startPos = tunaInstance.transform.position;
        Vector3 targetPos = doorPosition.position;

        Tween moveTween = null;

        moveTween = tunaInstance.transform.DOMove(targetPos, moveDuration)
            .SetEase(Ease.InOutQuad)
            .OnUpdate(() => {
                if (tunaInstance == null || moveTween == null) return;

                float progress = moveTween.ElapsedPercentage();
                float baseY = Mathf.Lerp(startPos.y, targetPos.y, progress);
                float floatOffset = Mathf.Sin(Time.time * (Mathf.PI * 2 / floatSpeed)) * floatHeight;
                tunaInstance.transform.position = new Vector3(
                    tunaInstance.transform.position.x,
                    baseY + floatOffset,
                    tunaInstance.transform.position.z
                );

                cachedTunaPosition = tunaInstance.transform.position;
            })
            .OnComplete(() => {
                Debug.Log("[TunaSpawner] 튜나가 문 앞에 도착했습니다.");

                if (GameEventManager.Instance != null)
                {
                    GameEventManager.Instance.TriggerCustomEvent("TUNA_ARRIVED_AT_DOOR");
                }
            });
    }

    void OnApplicationQuit()
    {
        SaveTunaState();
    }
}