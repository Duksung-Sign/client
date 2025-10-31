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

    private const string KEY_BGM = "Settings.BGM";
    private const string KEY_SFX = "Settings.SFX";


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

        (bgmVolume, sfxVolume) = PeekSavedVolumes();
        SetBgmVolume(bgmVolume);
        SetsfxVolume(sfxVolume);
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

    public void PlayFootstepSound(float volumeScale = 1f)
    {
        if (currentMapProfile != null && currentMapProfile.footstepSound != null)
        {
            sfxSource.PlayOneShot(currentMapProfile.footstepSound, sfxVolume * volumeScale);
        }
    }

    public void PlayTypingSound(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.pitch = Random.Range(0.9f, 1.1f);
            sfxSource.PlayOneShot(clip);
        }
    }

    public void PlaySeSound(AudioClip clip, float volumeScale = 1f)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
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

    public void SetBgmVolume(float volume, bool save = false)
    {
        bgmVolume = Mathf.Clamp01(volume);

        if (_activeBgmSource) _activeBgmSource.volume = bgmVolume;
        if (_inactiveBgmSource) _inactiveBgmSource.volume = Mathf.Min(_inactiveBgmSource.volume, bgmVolume);

        if (save)
        {
            PlayerPrefs.SetFloat(KEY_BGM, bgmVolume);
            PlayerPrefs.Save();
        }
    }

    public void SetsfxVolume(float volume, bool save = false)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource) sfxSource.volume = sfxVolume;

        if (save)
        {
            PlayerPrefs.SetFloat(KEY_SFX, sfxVolume);
            PlayerPrefs.Save();
        }
    }
    public (float bgm, float sfx) PeekSavedVolumes()
    {
        float bgmSaved = PlayerPrefs.HasKey(KEY_BGM) ? Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_BGM)) : bgmVolume;
        float sfxSaved = PlayerPrefs.HasKey(KEY_SFX) ? Mathf.Clamp01(PlayerPrefs.GetFloat(KEY_SFX)) : sfxVolume;
        return (bgmSaved, sfxSaved);
    }

    /// <summary>
    /// BGM을 페이드아웃하고 정지
    /// </summary>
    public void FadeOutBGM(float duration = 2f)
    {
        if (_activeBgmSource != null && _activeBgmSource.isPlaying)
        {
            _activeBgmSource.DOFade(0f, duration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => {
                    _activeBgmSource.Stop();
                    Debug.Log("[AudioManager] BGM 페이드아웃 완료 및 정지");
                });
        }
    }
}