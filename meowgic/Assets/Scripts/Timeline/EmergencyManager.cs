using UnityEngine;
using UnityEngine.Playables;

public class EmergencyManager : MonoBehaviour
{
    public static EmergencyManager Instance;

    private PlayableDirector pausedTimeline;
    private bool waitingForInput = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (waitingForInput && Input.GetKeyDown(KeyCode.Space))
        {
            ResumeTimeline();
        }
    }

    public void RequestTimelinePause(PlayableDirector timeline)
    {
        pausedTimeline = timeline;
        timeline.Pause();
        waitingForInput = true;
        Debug.Log("스페이스바를 눌러주세요...");
    }

    private void ResumeTimeline()
    {
        if (pausedTimeline != null)
        {
            pausedTimeline.Resume();
            pausedTimeline = null;
            waitingForInput = false;
            Debug.Log("Timeline Resume");
        }
    }
}