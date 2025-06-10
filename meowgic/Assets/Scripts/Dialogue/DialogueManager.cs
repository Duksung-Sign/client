using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public TextMeshProUGUI dialogueText;
    public TextMeshProUGUI speakerNameText;

    [Header("Character Portraits")]
    public Image leftPortrait; // 플레이어
    public Image rightPortrait; // NPC

    [Header("NPC Data")]
    public NPCDataSO npcData;

    private DialogueDataSO currentDialogueData;
    private int currentDialogueID;
    private int currentLineIndex;
    private Sprite playerPortrait;
    private Sprite npcPortrait;
    private bool isInDialogue = false;

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

    void Start()
    {
        // 게임 시작 시 대화 UI 숨기기
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    void Update()
    {
        // 대화 중일 때 스페이스바나 엔터키로 다음 라인
        if (isInDialogue && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            NextLine();
        }
    }

    // InteractionDetector에서 호출할 메서드
    public void SetCharacterPortraits(Sprite player, Sprite npc)
    {
        playerPortrait = player;
        npcPortrait = npc;

        // UI 초상화 설정
        if (leftPortrait != null)
            leftPortrait.sprite = player;
        if (rightPortrait != null)
            rightPortrait.sprite = npc;
    }

    public void StartDialogue(NPCDataSO data, int dialogueID)
    {
        npcData = data; 

        currentDialogueID = dialogueID;
        currentLineIndex = 0;
        isInDialogue = true;

        if (npcData.dialogues != null && dialogueID >= 0 && dialogueID < npcData.dialogues.Length)
        {
            currentDialogueData = npcData.dialogues[dialogueID];
        }
        else
        {
            Debug.LogError($"npcData.dialogues[{dialogueID}]가 없습니다!");
            currentDialogueData = null;
        }

        // 플레이어 움직임 제한
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.SetCanMove(false);
        }

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        // 첫 번째 대화 라인 표시
        DisplayCurrentLine();
    }

    void DisplayCurrentLine()
    {
        if (currentDialogueData == null
            || currentDialogueData.IsDialogueFinished(currentDialogueID, currentLineIndex))
        {
            EndDialogue();
            return;
        }

        // 현재 라인 데이터 가져오기
        string text = currentDialogueData.GetDialogueText(currentDialogueID, currentLineIndex);
        string speakerName = currentDialogueData.GetSpeakerName(currentDialogueID, currentLineIndex);
        bool isPlayer = currentDialogueData.IsPlayerSpeaking(currentDialogueID, currentLineIndex);

        // UI 업데이트
        if (dialogueText != null)
            dialogueText.text = text;

        SetCurrentSpeaker(speakerName, isPlayer);
    }

    public void NextLine()
    {
        currentLineIndex++;
        DisplayCurrentLine();
    }

    public void EndDialogue()
    {
        isInDialogue = false;

        // 플레이어 움직임 복구
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.SetCanMove(true);
        }

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    public bool IsInDialogue()
    {
        return isInDialogue;
    }

    // 현재 말하는 캐릭터 강조 (고정 위치 방식)
    public void SetCurrentSpeaker(string speakerName, bool isPlayer)
    {
        if (speakerNameText != null)
            speakerNameText.text = speakerName;

        if (isPlayer)
        {
            // 플레이어가 말할 때 - 왼쪽 강조, 오른쪽 흐리게
            SetPortraitAlpha(leftPortrait, 1f);
            SetPortraitAlpha(rightPortrait, 0.5f);
        }
        else
        {
            // NPC가 말할 때 - 오른쪽 강조, 왼쪽 흐리게
            SetPortraitAlpha(leftPortrait, 0.5f);
            SetPortraitAlpha(rightPortrait, 1f);
        }
    }

    void SetPortraitAlpha(Image portrait, float alpha)
    {
        if (portrait != null)
        {
            Color color = portrait.color;
            color.a = alpha;
            portrait.color = color;
        }
    }

    // 플레이어 정보를 PlayerStats에서 직접 가져오는 방법
    public void SetPlayerInfo(PlayerStats playerStats)
    {
        if (playerStats != null)
        {
            playerPortrait = playerStats.GetPlayerPortrait();

            if (leftPortrait != null)
                leftPortrait.sprite = playerPortrait;
        }
    }
}