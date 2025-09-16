using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class CutsceneDialogue
{
    public string speaker;
    public string text;
    public float duration = 3f;
}

public class EmergencyCutsceneController : MonoBehaviour
{
    [Header("Timeline")]
    public PlayableDirector timeline;

    [Header("캐릭터")]
    public Transform chedaCharacter;
    public Transform playerCharacter; // 미오
    public Transform tunaCharacter;   // 튜나 (나중에 등장)

    [Header("말풍선 시스템")]
    public CutsceneSpeechBubbleManager speechManager;

    [Header("페이드 효과")]
    public CanvasGroup fadePanel;

    [Header("위치 설정")]
    public Vector3 chedaStartPos = new Vector3(-8f, 0f, 0f);
    public Vector3 chedaCenterPos = new Vector3(-2f, 0f, 0f);
    public Vector3 playerCenterPos = new Vector3(2f, 0f, 0f);
    public Vector3 tunaEnterPos = new Vector3(8f, 0f, 0f);
    public Vector3 tunaCenterPos = new Vector3(0f, 0f, 0f);

    // 실제 대화 데이터
    private List<CutsceneDialogue> emergencyDialogues = new List<CutsceneDialogue>()
    {
        new CutsceneDialogue { speaker = "미오", text = "밖에서 무슨 소리가 들리는 것 같은데.", duration = 3f },
        new CutsceneDialogue { speaker = "체다", text = "미오! 여기있었구나! 빨리 도망쳐야해!", duration = 3f },
        new CutsceneDialogue { speaker = "체다", text = "지금 마을이 갑자기…!", duration = 2.5f },
        new CutsceneDialogue { speaker = "미오", text = "체다? 대체 무슨…? …...!", duration = 3f },
        new CutsceneDialogue { speaker = "체다", text = "…...! 미오! 위험해!", duration = 2f },
        new CutsceneDialogue { speaker = "체다", text = "윽!", duration = 1.5f },
        new CutsceneDialogue { speaker = "미오", text = "체다! 안돼!", duration = 2f },
        new CutsceneDialogue { speaker = "튜나", text = "조심해, 미오! 이쪽으로도 온다!", duration = 3f },
        new CutsceneDialogue { speaker = "미오", text = "체다…체다…...", duration = 2.5f },
        new CutsceneDialogue { speaker = "튜나", text = "정신차려, 미오! 우선 저 몬스터를 물리치자. 모두가 위험해지기 전에!", duration = 4f },
        new CutsceneDialogue { speaker = "미오", text = "하지만, 난…싸움 같은 건 할 줄 몰라…", duration = 3f },
        new CutsceneDialogue { speaker = "튜나", text = "걱정마! 넌 방금 아주 강력한 묘어의 힘을 얻었으니까.", duration = 3.5f },
        new CutsceneDialogue { speaker = "튜나", text = "내가 도와줄테니 내말을 듣고 그대로 따라해봐!", duration = 3f }
    };

    void Start()
    {
        InitializeCutscene();
    }

    void InitializeCutscene()
    {
        // 초기 설정
        if (chedaCharacter != null)
            chedaCharacter.gameObject.SetActive(false);

        if (tunaCharacter != null)
            tunaCharacter.gameObject.SetActive(false);

        if (fadePanel != null)
            fadePanel.alpha = 1f;

        // Timeline 시작
        if (timeline != null)
            timeline.Play();
    }

    // ===== Timeline Signal 메서드들 =====

    public void OnCutsceneStart()
    {
        StartCoroutine(FadeIn());
    }

    public void OnPlayerInitialThought()
    {
        // 첫 번째 대사: 미오의 독백
        if (speechManager != null)
            speechManager.ShowMessage("미오", emergencyDialogues[0].text);
    }

    public void OnChedaAppear()
    {
        StartCoroutine(ChedaAppearWithDialogue());
    }

    public void OnChedaInDanger()
    {
        StartCoroutine(ChedaInDangerSequence());
    }

    public void OnTunaAppear()
    {
        StartCoroutine(TunaAppearSequence());
    }

    public void OnTutorialIntro()
    {
        StartCoroutine(TutorialIntroSequence());
    }

    public void OnTransitionToBattle()
    {
        StartCoroutine(TransitionToBattle());
    }

    // ===== 구현부 =====

    IEnumerator FadeIn()
    {
        if (fadePanel == null) yield break;

        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fadePanel.alpha = 1f - (elapsed / duration);
            yield return null;
        }

        fadePanel.alpha = 0f;
        fadePanel.gameObject.SetActive(false);
    }

    IEnumerator ChedaAppearWithDialogue()
    {
        // 체다 등장
        chedaCharacter.position = chedaStartPos;
        chedaCharacter.gameObject.SetActive(true);

        // 이동하면서 대사
        StartCoroutine(MoveTo(chedaCharacter, chedaCenterPos, 2f));

        yield return new WaitForSeconds(1f);

        // 대사 2: 체다 등장 대사
        speechManager.ShowMessage("체다", emergencyDialogues[1].text);
        yield return new WaitForSeconds(emergencyDialogues[1].duration);

        // 대사 3: 체다 설명
        speechManager.ShowMessage("체다", emergencyDialogues[2].text);
        yield return new WaitForSeconds(emergencyDialogues[2].duration);

        // 대사 4: 미오 반응
        speechManager.ShowMessage("미오", emergencyDialogues[3].text);
        yield return new WaitForSeconds(emergencyDialogues[3].duration);
    }

    IEnumerator ChedaInDangerSequence()
    {
        // 대사 5: 체다 경고
        speechManager.ShowMessage("체다", emergencyDialogues[4].text);
        yield return new WaitForSeconds(emergencyDialogues[4].duration);

        // 체다가 공격받는 연출 (위치 변경, 흔들림 등)
        StartCoroutine(ShakeCharacter(chedaCharacter, 0.5f));

        // 대사 6: 체다 고통
        speechManager.ShowMessage("체다", emergencyDialogues[5].text);
        yield return new WaitForSeconds(emergencyDialogues[5].duration);

        // 대사 7: 미오 놀람
        speechManager.ShowMessage("미오", emergencyDialogues[6].text);
        yield return new WaitForSeconds(emergencyDialogues[6].duration);
    }

    IEnumerator TunaAppearSequence()
    {
        // 튜나 등장
        tunaCharacter.position = tunaEnterPos;
        tunaCharacter.gameObject.SetActive(true);

        StartCoroutine(MoveTo(tunaCharacter, tunaCenterPos, 1.5f));

        // 대사 8: 튜나 등장 대사
        speechManager.ShowMessage("튜나", emergencyDialogues[7].text);
        yield return new WaitForSeconds(emergencyDialogues[7].duration);

        // 대사 9: 미오 절망
        speechManager.ShowMessage("미오", emergencyDialogues[8].text);
        yield return new WaitForSeconds(emergencyDialogues[8].duration);
    }

    IEnumerator TutorialIntroSequence()
    {
        // 대사 10: 튜나 격려
        speechManager.ShowMessage("튜나", emergencyDialogues[9].text);
        yield return new WaitForSeconds(emergencyDialogues[9].duration);

        // 대사 11: 미오 걱정
        speechManager.ShowMessage("미오", emergencyDialogues[10].text);
        yield return new WaitForSeconds(emergencyDialogues[10].duration);

        // 대사 12: 튜나 설명
        speechManager.ShowMessage("튜나", emergencyDialogues[11].text);
        yield return new WaitForSeconds(emergencyDialogues[11].duration);

        // 대사 13: 튜나 튜토리얼 시작
        speechManager.ShowMessage("튜나", emergencyDialogues[12].text);
        yield return new WaitForSeconds(emergencyDialogues[12].duration);
    }

    IEnumerator MoveTo(Transform target, Vector3 destination, float duration)
    {
        Vector3 startPos = target.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            target.position = Vector3.Lerp(startPos, destination, t);
            yield return null;
        }

        target.position = destination;
    }

    IEnumerator ShakeCharacter(Transform target, float duration)
    {
        Vector3 originalPos = target.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float x = originalPos.x + Random.Range(-0.1f, 0.1f);
            float y = originalPos.y + Random.Range(-0.1f, 0.1f);
            target.position = new Vector3(x, y, originalPos.z);

            elapsed += Time.deltaTime;
            yield return null;
        }

        target.position = originalPos;
    }

    IEnumerator TransitionToBattle()
    {
        // 페이드아웃
        if (fadePanel != null)
        {
            fadePanel.gameObject.SetActive(true);
            float duration = 1f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                fadePanel.alpha = elapsed / duration;
                yield return null;
            }
        }

        // 배틀 시작
        MonsterDataSO monster = Resources.Load<MonsterDataSO>("Monsters/Monster_Cat_01");
        if (monster != null && GameManager.Instance != null)
        {
            PlayerPrefs.SetString("PreviousScene", "library");
            GameManager.Instance.StartBattle(monster, "battle_tutorial");
        }
    }
}