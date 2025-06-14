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
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("DialogueManager Instance 설정됨");
        }
        else
        {
            Destroy(gameObject);
        }
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

        // 게임 시작 시 대화 UI 숨기기
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            Debug.Log($"대화창 초기화: {dialoguePanel.name} 비활성화");
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

    // 현재 말하는 캐릭터 강조 (초상화 위치 변경)
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

    // 테스트용 메서드
    [ContextMenu("Test Item Description")]
    public void TestItemDescription()
    {
        ShowItemDescription("테스트 아이템이다.\n이것은 테스트용 아이템 설명입니다.");
    }

    // Unity Inspector에서 수동으로 talkPanel 연결할 수 있는 메서드
    [ContextMenu("Find and Connect talkPanel")]
    public void FindAndConnectTalkPanel()
    {
        GameObject foundTalkPanel = GameObject.Find("talkPanel");
        if (foundTalkPanel != null)
        {
            dialoguePanel = foundTalkPanel;
            Debug.Log($"talkPanel 연결 성공: {foundTalkPanel.name}");
        }
        else
        {
            Debug.LogError("talkPanel을 찾을 수 없습니다!");
        }
    }
}