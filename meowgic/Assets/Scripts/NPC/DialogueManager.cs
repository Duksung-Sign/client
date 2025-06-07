using System.Collections.Generic;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("Dialogue Data")]
    public DialogueDatabase dialogueDatabase;

    [Header("UI")]
    public GameObject dialogueUI;
    public TMPro.TextMeshProUGUI dialogueText;
    public TMPro.TextMeshProUGUI speakerText;

    private DialogueSet currentDialogue;
    private int currentLineIndex = 0;
    private bool isDialogueActive = false;
    private bool waitingForInput = false; // 입력 대기 상태 추가!

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (dialogueUI != null)
            dialogueUI.SetActive(false);
    }

    public void StartDialogue(int dialogueID)
    {
        if (dialogueDatabase == null)
        {
            Debug.LogError("DialogueDatabase가 할당되지 않았습니다!");
            return;
        }

        currentDialogue = dialogueDatabase.GetDialogueSet(dialogueID);

        if (currentDialogue == null)
        {
            Debug.LogWarning($"대화 ID {dialogueID}를 찾을 수 없습니다!");
            return;
        }

        currentLineIndex = 0;
        isDialogueActive = true;
        waitingForInput = false; // 시작할 때는 입력 대기하지 않음

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(true);
        }

        if (dialogueUI != null)
            dialogueUI.SetActive(true);

        ShowCurrentLine();
        StartCoroutine(WaitForInputDelay());
    }

    // 입력 지연을 위한 코루틴
    System.Collections.IEnumerator WaitForInputDelay()
    {
        yield return new WaitForEndOfFrame(); // 한 프레임 대기
        waitingForInput = true;
    }

    void ShowCurrentLine()
    {
        if (currentDialogue != null &&
            currentLineIndex < currentDialogue.dialogueLines.Count)
        {
            DialogueLine line = currentDialogue.dialogueLines[currentLineIndex];

            if (dialogueText != null)
                dialogueText.text = line.text;

            if (speakerText != null)
                speakerText.text = line.speakerName;

            Debug.Log($"대화 표시: [{line.speakerName}] {line.text}");
        }
    }

    public void NextLine()
    {
        if (!isDialogueActive || !waitingForInput) return;

        waitingForInput = false;
        currentLineIndex++;

        if (currentLineIndex >= currentDialogue.dialogueLines.Count)
        {
            EndDialogue();
        }
        else
        {
            ShowCurrentLine();
            StartCoroutine(WaitForInputDelay());
        }
    }

    void EndDialogue()
    {
        isDialogueActive = false;
        waitingForInput = false;

        // GameManager에 대화 종료 알리기
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(false);
        }

        if (dialogueUI != null)
            dialogueUI.SetActive(false);

        currentDialogue = null;
        currentLineIndex = 0;

        Debug.Log("대화 종료 - 플레이어 이동 재개");
    }

    void Update()
    {
        if (isDialogueActive && waitingForInput && Input.GetKeyDown(KeyCode.E))
        {
            NextLine();
        }

        if (isDialogueActive && Input.GetKeyDown(KeyCode.Escape))
        {
            EndDialogue();
        }
    }

    public bool IsInDialogue()
    {
        return isDialogueActive;
    }

    // 기존 코드와의 호환성을 위한 메서드들
    public DialogueLine GetDialogueLine(int dialogueID, int lineIndex)
    {
        if (dialogueDatabase == null) return null;
        return dialogueDatabase.GetDialogueLine(dialogueID, lineIndex);
    }

    public bool IsDialogueFinished(int dialogueID, int currentIndex)
    {
        if (dialogueDatabase == null) return true;
        return dialogueDatabase.IsDialogueFinished(dialogueID, currentIndex);
    }

    public int GetDialogueLineCount(int dialogueID)
    {
        if (dialogueDatabase == null) return 0;
        return dialogueDatabase.GetDialogueLineCount(dialogueID);
    }
}