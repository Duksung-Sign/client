using UnityEngine;

public class DialogueStarter : MonoBehaviour
{
    [Header("NPCDataSO 참조")]
    public NPCDataSO npcData;

    [Header("대화 ID 설정")]
    public int tutorialDialogueID = 0;
    public int postBattleDialogueID = 1;

    [Header("시작하자마자 대화 실행지")]
    public bool playOnStart = true;

    [Header("디버깅")]
    public bool debugMode = true;

    void Start()
    {
        // *** 자동 리셋 코드 완전 제거 ***
        // DialogueStarter.cs의 기존 자동 리셋 코드가 문제의 원인이었음
        // 매번 씬 로드할 때마다 FirstBattleCompleted를 삭제하고 있었음

        if (playOnStart)
        {
            Invoke("TriggerDialogue", 0.5f);
        }
    }

    public void TriggerDialogue()
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogError("DialogueManager가 씬에 없습니다!");
            return;
        }

        int dialogueID = GetDialogueID();

        if (debugMode)
        {
            Debug.Log($"DialogueStarter: 선택된 대화 ID = {dialogueID}");
        }

        DialogueManager.Instance.StartDialogue(npcData, dialogueID);
    }

    int GetDialogueID()
    {
        bool battleCompleted = PlayerPrefs.GetInt("FirstBattleCompleted", 0) == 1;

        if (debugMode)
        {
            Debug.Log($"배틀 완료 상태: {battleCompleted}");
        }

        if (battleCompleted)
        {
            Debug.Log("배틀 완료 후 대화 실행");
            return postBattleDialogueID;
        }
        else
        {
            Debug.Log("튜토리얼 대화 실행");
            return tutorialDialogueID;
        }
    }

    // 개발자용 수동 리셋 기능만 유지 (Context Menu)
    [ContextMenu("Reset Tutorial")]
    public void ResetTutorial()
    {
        PlayerPrefs.DeleteKey("FirstBattleCompleted");
        PlayerPrefs.Save();
        Debug.Log("튜토리얼로 리셋했습니다.");
    }

    [ContextMenu("Set Battle Completed")]
    public void SetBattleCompleted()
    {
        PlayerPrefs.SetInt("FirstBattleCompleted", 1);
        PlayerPrefs.Save();
        Debug.Log("배틀 완료 상태로 설정했습니다.");
    }

    [ContextMenu("Debug Current State")]
    public void DebugCurrentState()
    {
        bool battleCompleted = PlayerPrefs.GetInt("FirstBattleCompleted", 0) == 1;
        int dialogueID = GetDialogueID();
        Debug.Log($"배틀 완료: {battleCompleted}, 사용할 대화 ID: {dialogueID}");
    }
}