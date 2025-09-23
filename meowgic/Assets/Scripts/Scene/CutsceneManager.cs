using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;

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
        GameManager.Instance.ChangeState(GameState.Cutscene);

        switch (cutsceneID)
        {
            case "emergency_cutscene":
                yield return StartCoroutine(EmergencyCutscene());
                break;
        }
    }

    private IEnumerator EmergencyCutscene()
    {
        yield return SceneManager.LoadSceneAsync("EmergencyCutscene");
        yield return StartCoroutine(WaitForTimelineCompletion());

        MonsterDataSO monsterToBattle = Resources.Load<MonsterDataSO>("Monsters/Monster_Cat_01");
        if (monsterToBattle != null)
        {
            string battleDialogueID = "first_battle_tutorial";
            GameEventManager.Instance.TriggerBattleStartRequested(monsterToBattle, null, battleDialogueID);
        }
        else
        {
            Debug.LogError("전투할 몬스터 데이터를 찾을 수 없습니다: Monster_Cat_01");
            GameManager.Instance.ChangeState(GameState.Field);
        }
    }

    private IEnumerator WaitForTimelineCompletion()
    {
        PlayableDirector director = FindObjectOfType<PlayableDirector>();
        if (director != null)
        {
            yield return new WaitUntil(() => director.state != PlayState.Playing);
        }
        else
        {
            Debug.LogError("PlayableDirector를 찾을 수 없습니다!");
        }
    }
}