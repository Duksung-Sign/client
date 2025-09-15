using UnityEngine;
using UnityEngine.Playables;

public class PauseBehaviour : PlayableBehaviour
{
    private bool isPaused = false;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!isPaused && info.weight > 0)
        {
            PlayableDirector director = playable.GetGraph().GetResolver() as PlayableDirector;

            if (EmergencyManager.Instance != null)
            {
                EmergencyManager.Instance.RequestTimelinePause(director);
                isPaused = true;
            }
        }
    }
}