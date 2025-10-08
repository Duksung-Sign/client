using UnityEngine;

public class FootstepSound : MonoBehaviour
{
    [Header("발소리 설정")]
    [SerializeField] private float volume = 0.3f;
    [SerializeField] private bool enableFootsteps = true;

    [Tooltip("최소 발소리 재생 간격 (초)")]
    [SerializeField] private float cooldown = 0.3f;
    private float lastPlayTime = -1f;

    public void PlayFootstep()
    {
        if (!enableFootsteps) return;

        if (Time.time < lastPlayTime + cooldown)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            lastPlayTime = Time.time; 
            AudioManager.Instance.PlayFootstepSound(volume);
        }
    }

    public void EnableFootsteps() => enableFootsteps = true;
    public void DisableFootsteps() => enableFootsteps = false;
    public void SetVolume(float newVolume) => volume = Mathf.Clamp01(newVolume);
}