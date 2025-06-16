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
    private bool isShowingItemDescription = false;

    void Awake()
    {
        // 싱글톤 설정
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // talkPanel 찾기
        if (dialoguePanel == null)
            dialoguePanel = GameObject.Find("talkPanel");

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    void Start()
    {
        // talkPanel을 자동으로 찾아서 연결
        if (dialoguePanel == null)
        {
            GameObject foundTalkPanel = GameObject.Find("talkPanel");
            if (foundTalkPanel != null)
            {
                dialoguePanel = foundTalkPanel;
                Debug.Log("talkPanel을 자동으로 찾아서 연결했습니다!");
            }
            else
            {
                Debug.LogError("talkPanel을 찾을 수 없습니다! Hierarchy에서 talkPanel 이름을 확인해주세요.");
            }
        }
    }

    void Update()
    {
        // 대화 중일 때 스페이스바나 엔터키로 다음 라인
        if (isInDialogue && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            NextLine();
        }

        // 아이템 설명 중일 때 E키, 스페이스바, 엔터키로 닫기
        if (isShowingItemDescription &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            HideItemDescription();
        }
    }

    // 아이템 설명을 대화창에 표시하는 메서드
    public void ShowItemDescription(string description)
    {
        Debug.Log($"ShowItemDescription 호출됨: {description}");

        // talkPanel을 다시 한번 확인
        if (dialoguePanel == null)
        {
            GameObject foundTalkPanel = GameObject.Find("talkPanel");
            if (foundTalkPanel != null)
            {
                dialoguePanel = foundTalkPanel;
                Debug.Log("talkPanel을 다시 찾아서 연결했습니다!");
            }
            else
            {
                Debug.LogError("talkPanel을 찾을 수 없습니다!");
                return;
            }
        }

        isShowingItemDescription = true;
        isInDialogue = false; // 일반 대화가 아님

        // 플레이어 움직임 제한
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.SetCanMove(false);
            Debug.Log("플레이어 움직임 제한됨");
        }

        // 대화창 활성화 전 로그
        Debug.Log($"대화창 활성화 시도: {dialoguePanel.name}");
        dialoguePanel.SetActive(true);

        // 활성화 후 상태 확인
        Debug.Log($"대화창 활성화 후 상태: {dialoguePanel.activeInHierarchy}");

        // 텍스트 설정
        if (dialogueText != null)
        {
            dialogueText.text = description;
            Debug.Log($"대화 텍스트 설정됨: {description}");
        }
        else
        {
            Debug.LogError("dialogueText가 null입니다!");
        }

        // 화자 이름 설정 (플레이어)
        var stats = FindObjectOfType<PlayerStats>();
        string name = stats != null && !string.IsNullOrEmpty(stats.playerName)
                      ? stats.playerName
                      : "플레이어";

        if (speakerNameText != null)
            speakerNameText.text = name;

        // 플레이어 초상화만 표시 (NPC 초상화는 숨김)
        if (leftPortrait != null && playerPortrait != null)
        {
            leftPortrait.sprite = playerPortrait;
            SetPortraitAlpha(leftPortrait, 1f);
        }

        if (rightPortrait != null)
        {
            SetPortraitAlpha(rightPortrait, 0f); // NPC 초상화 숨김
        }

        // 게임 상태 변경
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(true);
            Debug.Log("게임 상태 변경: 대화 모드");
        }
    }

    // 아이템 설명 숨기기
    public void HideItemDescription()
    {
        Debug.Log("HideItemDescription 호출됨");

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            Debug.Log("대화창 비활성화됨");
        }

        isShowingItemDescription = false;

        // 플레이어 움직임 복원
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.SetCanMove(true);
            Debug.Log("플레이어 움직임 복원됨");
        }

        // 게임 상태 복원
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(false);
            Debug.Log("게임 상태 복원됨");
        }
    }

    // InteractionDetector에서 호출될 메서드
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
        isShowingItemDescription = false;

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
        isShowingItemDescription = false;

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
        return isInDialogue || isShowingItemDescription;
    }

    public void SetCurrentSpeaker(string speakerName, bool isPlayer)
    {
        Debug.Log($"=== SetCurrentSpeaker 호출: Speaker={speakerName}, isPlayer={isPlayer} ===");

        // 1) 화자 이름 설정
        if (speakerNameText != null)
            speakerNameText.text = speakerName;

        // 2) 포트레이트 디버깅 정보
        Debug.Log($"leftPortrait null 여부: {leftPortrait == null}");
        Debug.Log($"rightPortrait null 여부: {rightPortrait == null}");
        Debug.Log($"playerPortrait null 여부: {playerPortrait == null}");
        Debug.Log($"npcPortrait null 여부: {npcPortrait == null}");

        // 3) 포트레이트 설정 및 활성화 (GameObject.SetActive 대신 alpha 사용)
        if (leftPortrait != null)
        {
            // 플레이어 포트레이트는 항상 활성화하고 스프라이트 재설정
            leftPortrait.gameObject.SetActive(true);
            if (playerPortrait != null)
                leftPortrait.sprite = playerPortrait;

            // 알파값으로 강조/비강조 표현
            SetPortraitAlpha(leftPortrait, isPlayer ? 1f : 0.5f);
            Debug.Log($"leftPortrait 알파값: {(isPlayer ? 1f : 0.5f)}");
        }

        if (rightPortrait != null)
        {
            // NPC 포트레이트도 항상 활성화하고 스프라이트 재설정
            rightPortrait.gameObject.SetActive(true);
            if (npcPortrait != null)
                rightPortrait.sprite = npcPortrait;

            // 알파값으로 강조/비강조 표현
            SetPortraitAlpha(rightPortrait, !isPlayer ? 1f : 0.5f);
            Debug.Log($"rightPortrait 알파값: {(!isPlayer ? 1f : 0.5f)}");
            Debug.Log($"rightPortrait 스프라이트: {(rightPortrait.sprite != null ? rightPortrait.sprite.name : "null")}");
        }
    }

    public void StartQuestDialogue(DialogueDataSO questDialogueData, int questDialogueID)
    {
        Debug.Log($"StartQuestDialogue 호출: DialogueID {questDialogueID}");

        // 퀘스트 다이얼로그 데이터 직접 설정
        currentDialogueData = questDialogueData;
        currentDialogueID = questDialogueID;
        currentLineIndex = 0;
        isInDialogue = true;
        isShowingItemDescription = false;

        // 해당 DialogueSet이 존재하는지 확인
        DialogueSet dialogueSet = questDialogueData.GetDialogueSet(questDialogueID);
        if (dialogueSet == null)
        {
            Debug.LogError($"questDialogueID {questDialogueID}에 해당하는 DialogueSet을 찾을 수 없습니다!");
            EndDialogue();
            return;
        }

        Debug.Log($"퀘스트 다이얼로그 시작: {dialogueSet.npcName}");

        // 플레이어 움직임 제한
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerController.SetCanMove(false);
        }

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        // 게임 상태 변경
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(true);
        }

        // 첫 번째 대화 라인 표시
        DisplayCurrentLine();
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