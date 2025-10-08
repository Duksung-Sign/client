using UnityEngine;

public class FootstepSound : MonoBehaviour
{
    [Header("발소리 설정")]
    [SerializeField] private float volume = 0.3f;
    [SerializeField] private bool enableFootsteps = true;

    public void PlayFootstep()
    {
        if (!enableFootsteps) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFootstepSound(volume);
        }
    }

    public void EnableFootsteps() => enableFootsteps = true;
    public void DisableFootsteps() => enableFootsteps = false;
    public void SetVolume(float newVolume) => volume = Mathf.Clamp01(newVolume);
}