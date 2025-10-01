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
    public ParticleSystem spawnEffect;

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

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
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
        }
    }

    private void HandleLineDisplayed(string lineID)
    {
        if (lineID == spawnTriggerLineID && tunaInstance == null)
        {
            SpawnTuna();
        }
    }

    private void HandleDialogueFinished(string eventType, string dialogueId)
    {
        if (dialogueId == "library_tuna_meeting")
        {
            CameraController camController = Camera.main.GetComponent<CameraController>();
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
        }
    }

    private void SpawnTuna()
    {
        CameraController camController = Camera.main.GetComponent<CameraController>();
        if (camController != null)
        {
            camController.ZoomTo(zoomTargetSize, zoomDuration);
        }

        tunaInstance = Instantiate(tunaPrefab, spawnPosition.position, Quaternion.identity);

        if (spawnSound != null)
        {
            audioSource.PlayOneShot(spawnSound);
        }

        if (spawnEffect != null)
        {
            Instantiate(spawnEffect, spawnPosition.position, Quaternion.identity);
        }

        tunaInstance.transform.localScale = Vector3.zero;
        tunaInstance.transform.DOScale(1f, 0.5f)
            .SetEase(Ease.OutBack);

        tunaInstance.transform.DOMoveY(spawnPosition.position.y + 0.2f, 0.3f)
            .SetLoops(2, LoopType.Yoyo);

        Debug.Log("[TunaSpawner] 튜나 등장!");
    }

    private void MoveTunaToDoor()
    {
        Vector3 startPos = tunaInstance.transform.position;
        Vector3 targetPos = doorPosition.position;

        Tween moveTween = null; 

        moveTween = tunaInstance.transform.DOMove(targetPos, moveDuration)
            .SetEase(Ease.InOutQuad)
            .OnUpdate(() => {
                float progress = moveTween.ElapsedPercentage();
                float baseY = Mathf.Lerp(startPos.y, targetPos.y, progress);
                float floatOffset = Mathf.Sin(Time.time * (Mathf.PI * 2 / floatSpeed)) * floatHeight;
                tunaInstance.transform.position = new Vector3(
                    tunaInstance.transform.position.x,
                    baseY + floatOffset,
                    tunaInstance.transform.position.z
                );
            })
            .OnComplete(() => {
                Debug.Log("[TunaSpawner] 튜나가 문 앞에 도착했습니다.");
            });
    }
}