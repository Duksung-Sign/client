using UnityEngine;
using System.Collections;
using DG.Tweening;
using UnityEngine.Rendering;

public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance;

    private bool isPlayingCutscene = false;
    public bool IsPlayingCutscene() => isPlayingCutscene;

    [Header("튜나 설정")]
    public GameObject tunaPrefab;

    [Header("볼륨 프로필")]
    public VolumeProfile libraryAfterProfile;

    private GameObject tunaInstance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameEventManager.Instance.OnGameStateChanged += HandleGameStateChange;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayCutscene(string cutsceneID)
    {
        if (isPlayingCutscene) return;
        StartCoroutine(CutsceneCoroutine(cutsceneID));
    }

    IEnumerator CutsceneCoroutine(string cutsceneID)
    {
        isPlayingCutscene = true;
        GameManager.Instance.ChangeState(GameState.Cutscene);

        if (cutsceneID == "cheddar_appears")
        {
            yield return StartCoroutine(CheddarAppearsCutscene());
        }

        GameManager.Instance.ChangeState(GameState.Field);
        isPlayingCutscene = false;
    }

    IEnumerator CheddarAppearsCutscene()
    {
        SceneContext sceneContext = FindObjectOfType<SceneContext>();
        if (sceneContext != null)
        {
            MapAudioProfile afterProfile = Resources.Load<MapAudioProfile>("Audio/Audio_Library_After");
            if (afterProfile != null)
            {
                sceneContext.audioProfile = afterProfile;
                AudioManager.Instance.CrossfadeBGM(afterProfile.backgroundMusic);
            }
        }

        Volume globalVolume = FindObjectOfType<Volume>();
        if (globalVolume != null && libraryAfterProfile != null)
        {
            globalVolume.profile = libraryAfterProfile;
            Debug.Log("[Cutscene] Volume Profile 변경 완료");
        }

        yield return new WaitForSeconds(0.5f);

        SpawnTuna();

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue("mio_feels_something");
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        }

        yield return new WaitForSeconds(0.5f);

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue("cheddar_shout_mio");
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        }

        yield return new WaitForSeconds(0.3f);

        if (AudioManager.Instance != null)
        {
            AudioClip doorOpenSound = Resources.Load<AudioClip>("Sounds/door_close");
            if (doorOpenSound != null)
            {
                AudioManager.Instance.PlaySFX(doorOpenSound, 0.3f);
            }
        }

        yield return new WaitForSeconds(0.3f);

        GameObject cheddar = GameObject.Find("Cheddar");
        if (cheddar != null)
        {
            FootstepSound cheddarFootstep = cheddar.GetComponent<FootstepSound>(); 

            Vector3[] waypoints = new Vector3[]
            {
                new Vector3(0.148f, -3.94f, 0f),
                new Vector3(0.148f, -2.56f, 0f),
                new Vector3(3.29f, -2.56f, 0f),
                new Vector3(3.29f, -0.21f, 0f),
                new Vector3(6.99f, -0.21f, 0f),
                new Vector3(6.99f, 0.2f, 0f),
            };

            cheddar.transform.position = waypoints[0];
            cheddar.SetActive(true);

            Animator animator = cheddar.GetComponent<Animator>();

            CameraController cameraController = FindObjectOfType<CameraController>();
            Transform originalTarget = null;

            if (cameraController != null)
            {
                originalTarget = cameraController.target;
                cameraController.target = cheddar.transform;
                cameraController.ZoomTo(1.5f, 0.5f);
            }

            yield return new WaitForSeconds(0.5f);

            if (cheddarFootstep != null)
            {
                cheddarFootstep.EnableFootsteps(); 
            }

            for (int i = 1; i < waypoints.Length; i++)
            {
                Vector3 currentPos = waypoints[i - 1];
                Vector3 nextPos = waypoints[i];
                Vector3 direction = (nextPos - currentPos).normalized;

                if (animator != null)
                {
                    animator.SetBool("isWalk", true);

                    if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                    {
                        animator.SetFloat("xDir", Mathf.Sign(direction.x));
                        animator.SetFloat("yDir", 0f);
                    }
                    else
                    {
                        animator.SetFloat("xDir", 0f);
                        animator.SetFloat("yDir", Mathf.Sign(direction.y));
                    }
                }

                float distance = Vector3.Distance(currentPos, nextPos);
                float duration = distance / 2f;

                yield return cheddar.transform.DOMove(nextPos, duration)
                    .SetEase(Ease.Linear)
                    .WaitForCompletion();

                if (i < waypoints.Length - 1)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }

            if (cheddarFootstep != null)
            {
                cheddarFootstep.DisableFootsteps(); 
            }

            if (animator != null)
            {
                animator.SetBool("isWalk", false);
            }

            if (cameraController != null && originalTarget != null)
            {
                cameraController.target = originalTarget;
                cameraController.ResetZoom(0.8f);
                yield return new WaitForSeconds(0.8f);
            }
        }
        else
        {
            Debug.LogError("[Cutscene] Cheddar를 찾을 수 없습니다!");
        }

        yield return new WaitForSeconds(0.3f);

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue("cheddar_conversation");
            yield return new WaitUntil(() => !DialogueManager.Instance.IsInDialogue());
        }

        yield return new WaitForSeconds(0.5f);

        DespawnTuna();

        yield return new WaitForSeconds(0.5f);
    }

    private void SpawnTuna()
    {
        if (tunaPrefab == null)
        {
            Debug.LogWarning("[CutsceneManager] 튜나 프리팹이 설정되지 않았습니다.");
            return;
        }

        GameObject spawnPoint = GameObject.Find("TunaSpawnPoint");
        if (spawnPoint == null)
        {
            Debug.LogError("[CutsceneManager] TunaSpawnPoint를 찾을 수 없습니다!");
            return;
        }

        Vector3 spawnPos = spawnPoint.transform.position;
        tunaInstance = Instantiate(tunaPrefab, spawnPos, Quaternion.identity);

        tunaInstance.transform.localScale = Vector3.zero;
        tunaInstance.transform.DOScale(0.5f, 0.5f)
            .SetEase(Ease.OutBack);

        tunaInstance.transform.DOMoveY(spawnPos.y + 0.2f, 0.3f)
            .SetLoops(2, LoopType.Yoyo);

        Debug.Log("[CutsceneManager] 튜나 등장!");
    }

    private void DespawnTuna()
    {
        if (tunaInstance == null) return;

        tunaInstance.transform.DOScale(0f, 0.5f)
            .SetEase(Ease.InBack)
            .OnComplete(() => {
                Destroy(tunaInstance);
                tunaInstance = null;
                Debug.Log("[CutsceneManager] 튜나 퇴장!");
            });
    }

    private void HandleGameStateChange(GameState newState)
    {
        if (newState == GameState.Loading && isPlayingCutscene)
        {
            StopAllCoroutines();
            isPlayingCutscene = false;

            if (tunaInstance != null)
            {
                Destroy(tunaInstance);
                tunaInstance = null;
            }
            Debug.Log("[CutsceneManager] 씬 로딩으로 인해 모든 컷씬을 중단합니다.");
        }
    }

    void OnDestroy()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnGameStateChanged -= HandleGameStateChange;
        }
    }
}