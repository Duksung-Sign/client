using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    public IEnumerator PlayCutscene(string cutsceneID)
    {
        yield return new WaitUntil(() => GameManager.Instance != null);

        GameManager.Instance.SetDialogueState(true);
        switch (cutsceneID)
        {
            case "emergency_cutscene":
                yield return StartCoroutine(EmergencyCutscene());
                break;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(false);
        }
    }

    private IEnumerator EmergencyCutscene()
    {
        Debug.Log("긴급상황 컷씬 시작");
        yield return SceneManager.LoadSceneAsync("EmergencyCutscene");
        Debug.Log("Emergency 씬 전환 완료");

        yield return new WaitForSeconds(1f);

        yield return StartCoroutine(WaitForTimelineCompletion());
        Debug.Log("타임라인 재생 완료");

        MonsterDataSO monsterToBattle = Resources.Load<MonsterDataSO>("Monsters/Monster_Cat_01");
        if (monsterToBattle != null)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartBattle(monsterToBattle, "first_battle_tutorial");
            }
            else
            {
                Debug.LogError("GameManager.Instance가 null입니다!");
            }
        }
        else
        {
            Debug.LogError("전투할 몬스터 데이터를 찾을 수 없습니다: Monster_Cat_01");
        }
    }

    private IEnumerator WaitForTimelineCompletion()
    {
        Debug.Log("Timeline 검색 시작");

        PlayableDirector director = null;
        float searchTimeout = 5f; 
        float searchTimer = 0f;

        while (director == null && searchTimer < searchTimeout)
        {
            director = FindObjectOfType<PlayableDirector>();
            if (director == null)
            {
                yield return new WaitForSeconds(0.1f);
                searchTimer += 0.1f;
            }
        }

        if (director == null)
        {
            Debug.LogError("PlayableDirector를 찾을 수 없습니다!");
            yield return new WaitForSeconds(2f); 
        }

        Debug.Log($"PlayableDirector 발견: {director.gameObject.name}");

        if (director.playableAsset != null)
        {
            Debug.Log($"Timeline Asset: {director.playableAsset.name}, 길이: {director.duration}초");
            Debug.Log($"Play On Awake: {director.playOnAwake}");
        }

        float waitForPlayTimeout = 3f;
        float waitTimer = 0f;
        while (director.state != PlayState.Playing && waitTimer < waitForPlayTimeout)
        {
            yield return new WaitForSeconds(0.1f);
            waitTimer += 0.1f;
        }

        if (director.state == PlayState.Playing)
        {
            Debug.Log("Timeline 재생 중... 완료까지 대기");

            yield return new WaitUntil(() => director.state != PlayState.Playing);
        }
        else
        {
            Debug.LogWarning("Timeline이 재생되지 않음. Play On Awake가 비활성화되어 있을 수 있습니다.");
            if (director.playableAsset != null)
            {
                Debug.Log("수동으로 Timeline 재생");
                director.Play();
                yield return new WaitUntil(() => director.state != PlayState.Playing);
            }
            else
            {
                Debug.LogError("Timeline Asset이 설정되지 않았습니다!");
                yield return new WaitForSeconds(2f);
            }
        }
    }
}