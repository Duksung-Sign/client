using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using DG.Tweening;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("오디오 소스")]
    [SerializeField] private AudioSource bgmSourceA;
    [SerializeField] private AudioSource bgmSourceB;
    [SerializeField] private AudioSource sfxSource;

    [Header("오디오 프로필")]
    private MapAudioProfile currentMapProfile;

    [Header("페이드 설정")]
    [SerializeField] private float crossfadeDuration = 2.0f;

    [Header("볼륨 설정")]
    [Range(0f, 1f)] public float bgmVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    private AudioSource _activeBgmSource;
    private AudioSource _inactiveBgmSource;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _activeBgmSource = bgmSourceA;
            _inactiveBgmSource = bgmSourceB;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        bgmSourceA.volume = 0;
        bgmSourceB.volume = 0;
        sfxSource.volume = sfxVolume;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnStoryAdvanced += HandleStoryEvent;
        }
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnStoryAdvanced -= HandleStoryEvent;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var sceneContext = FindObjectOfType<SceneContext>();
        if (sceneContext != null && sceneContext.audioProfile != null)
        {
            currentMapProfile = sceneContext.audioProfile;
            CrossfadeBGM(currentMapProfile.backgroundMusic);
        }
    }

    public void PlayFootstepSound()
    {
        if (currentMapProfile != null && currentMapProfile.footstepSound != null)
        {
            sfxSource.PlayOneShot(currentMapProfile.footstepSound);
        }
    }

    private void HandleStoryEvent(string storyID)
    {
    }

    public void CrossfadeBGM(AudioClip newClip)
    {
        if (_activeBgmSource.isPlaying && _activeBgmSource.clip == newClip) return;

        _inactiveBgmSource.clip = newClip;
        _inactiveBgmSource.Play();
        _inactiveBgmSource.DOFade(bgmVolume, crossfadeDuration).SetEase(Ease.InOutSine);
        _activeBgmSource.DOFade(0, crossfadeDuration).SetEase(Ease.InOutSine);

        AudioSource temp = _activeBgmSource;
        _activeBgmSource = _inactiveBgmSource;
        _inactiveBgmSource = temp;
    }

    public void PauseBGM()
    {
        if (_activeBgmSource != null && _activeBgmSource.isPlaying)
        {
            _activeBgmSource.Pause();
            Debug.Log("[AudioManager] BGM 일시정지");
        }
    }

    public void ResumeBGM()
    {
        if (_activeBgmSource != null && !_activeBgmSource.isPlaying)
        {
            _activeBgmSource.UnPause();
            Debug.Log("[AudioManager] BGM 재개");
        }
    }

    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }
    }
}