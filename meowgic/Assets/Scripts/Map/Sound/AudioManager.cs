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
        _inactiveBgmSource.DOFade(1, crossfadeDuration).SetEase(Ease.Linear);

        _activeBgmSource.DOFade(0, crossfadeDuration).SetEase(Ease.Linear);

        AudioSource temp = _activeBgmSource;
        _activeBgmSource = _inactiveBgmSource;
        _inactiveBgmSource = temp;
    }
}