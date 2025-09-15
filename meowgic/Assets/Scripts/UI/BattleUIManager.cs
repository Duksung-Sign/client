using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BattleUIManager : MonoBehaviour
{
    [Header("UI 요소")]
    public Image monsterImage;
    public Button attackButton;
    public Button escapeButton;
    public GameObject skillSelectionPanel;
    public Button helloSkillButton;
    public Button sorrySkillButton;
    public Button thankSkillButton;

    [Header("채널 1: 캐릭터 대사 UI (전투 결과, 대사)")]
    public DynamicSpeechBubble canSpeechBubble;

    [Header("채널 2: 튜토리얼 UI (튜토리얼 전용)")]
    public DynamicSpeechBubble tutorSpeechBubble;

    [Header("채널 3: 시스템 상태 UI (미디어파이프 등)")]
    public TextMeshProUGUI statusText;

    private Coroutine hideDialogueCoroutine;
    private Coroutine hideTutorialCoroutine;

    private bool isDialogueActive = false;
    private bool isTutorialActive = false;
    private bool isStatusActive = false;

    void Awake()
    {
        if (BattleEventManager.Instance != null)
        {
            // 기존 이벤트 구독
            BattleEventManager.Instance.OnRequestShowTutorialMessage += ShowTutorialMessage;
            BattleEventManager.Instance.OnRequestShowCharacterDialogue += ShowCharacterDialogue;
            BattleEventManager.Instance.OnRequestUpdateStatus += UpdateStatusText;

            // 새로운 UI 제어 이벤트 구독
            BattleEventManager.Instance.OnRequestShowMainUI += ShowMainUI;
            BattleEventManager.Instance.OnRequestShowSkillPanel += ShowSkillPanel;
        }
    }

    void OnDestroy()
    {
        if (BattleEventManager.Instance != null)
        {
            // 기존 이벤트 구독 해제
            BattleEventManager.Instance.OnRequestShowTutorialMessage -= ShowTutorialMessage;
            BattleEventManager.Instance.OnRequestShowCharacterDialogue -= ShowCharacterDialogue;
            BattleEventManager.Instance.OnRequestUpdateStatus -= UpdateStatusText;

            // 새로운 UI 제어 이벤트 구독 해제
            BattleEventManager.Instance.OnRequestShowMainUI -= ShowMainUI;
            BattleEventManager.Instance.OnRequestShowSkillPanel -= ShowSkillPanel;
        }

        if (hideDialogueCoroutine != null)
        {
            StopCoroutine(hideDialogueCoroutine);
        }

        if (hideTutorialCoroutine != null)
        {
            StopCoroutine(hideTutorialCoroutine);
        }
    }

    void Start()
    {
        ValidateUIComponents();
        InitializeUIState();
        SetupButtonEvents();
        Debug.Log("BattleUIManager 초기화 완료");
    }

    private void ValidateUIComponents()
    {
        if (attackButton == null) Debug.LogError("Attack Button이 할당되지 않았습니다!");
        if (escapeButton == null) Debug.LogError("Escape Button이 할당되지 않았습니다!");
        if (skillSelectionPanel == null) Debug.LogError("Skill Selection Panel이 할당되지 않았습니다!");

        if (helloSkillButton == null) Debug.LogWarning("Hello Skill Button이 할당되지 않았습니다!");
        if (sorrySkillButton == null) Debug.LogWarning("Sorry Skill Button이 할당되지 않았습니다!");
        if (thankSkillButton == null) Debug.LogWarning("Thank Skill Button이 할당되지 않았습니다!");

        if (tutorSpeechBubble == null)
        {
            Debug.LogWarning("Tutor Speech Bubble이 할당되지 않았습니다! 백업 UI를 사용합니다.");
        }

        if (statusText == null)
        {
            Debug.LogWarning("Status Text가 할당되지 않았습니다!");
        }
    }

    private void InitializeUIState()
    {
        if (canSpeechBubble != null)
        {
            canSpeechBubble.gameObject.SetActive(false);
            isDialogueActive = false;
        }

        if (tutorSpeechBubble != null)
        {
            tutorSpeechBubble.gameObject.SetActive(false);
            isTutorialActive = false;
        }

        if (statusText != null)
        {
            statusText.gameObject.SetActive(false);
            isStatusActive = false;
        }

        if (skillSelectionPanel != null)
        {
            skillSelectionPanel.SetActive(false);
        }
    }

    private void SetupButtonEvents()
    {
        attackButton?.onClick.AddListener(OnAttackButtonClicked);
        escapeButton?.onClick.AddListener(OnEscapeButtonClicked);
        helloSkillButton?.onClick.AddListener(() => OnSkillButtonClicked("hello"));
        sorrySkillButton?.onClick.AddListener(() => OnSkillButtonClicked("sorry"));
        thankSkillButton?.onClick.AddListener(() => OnSkillButtonClicked("thank"));
    }

    public void SetMonsterImage(Sprite sprite)
    {
        if (monsterImage != null && sprite != null)
        {
            monsterImage.sprite = sprite;
            Debug.Log("몬스터 이미지 설정됨");
        }
        else
        {
            Debug.LogWarning($"몬스터 이미지 설정 실패 - Image: {monsterImage != null}, Sprite: {sprite != null}");
        }
    }

    /// <summary>
    /// 캐릭터 대사를 canSpeechBubble로 표시
    /// </summary>
    private void ShowCharacterDialogue(string dialogue)
    {
        if (string.IsNullOrEmpty(dialogue) || canSpeechBubble == null) return;

        if (hideDialogueCoroutine != null) StopCoroutine(hideDialogueCoroutine);

        canSpeechBubble.gameObject.SetActive(true);
        canSpeechBubble.SetText(dialogue);
        isDialogueActive = true;

        hideDialogueCoroutine = StartCoroutine(HideCharacterDialogueAfterDelay(3f));
    }

    private IEnumerator HideCharacterDialogueAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (canSpeechBubble != null && isDialogueActive)
        {
            canSpeechBubble.gameObject.SetActive(false);
        }
        isDialogueActive = false;
        hideDialogueCoroutine = null;
        Debug.Log("캐릭터 대사 숨김");
    }

    /// <summary>
    /// 튜토리얼 메시지를 tutorSpeechBubble로 표시
    /// </summary>
    private void ShowTutorialMessage(string message)
    {
        if (string.IsNullOrEmpty(message) || tutorSpeechBubble == null) return;

        if (hideTutorialCoroutine != null) StopCoroutine(hideTutorialCoroutine);

        tutorSpeechBubble.SetText(message);
        tutorSpeechBubble.gameObject.SetActive(true);
        isTutorialActive = true;

        hideTutorialCoroutine = StartCoroutine(HideTutorialMessageAfterDelay(3f));
    }

    private IEnumerator HideTutorialMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (tutorSpeechBubble != null && isTutorialActive)
        {
            tutorSpeechBubble.gameObject.SetActive(false);
            isTutorialActive = false;
            Debug.Log("튜토리얼 메시지 숨김");
        }
        hideTutorialCoroutine = null;
    }

    public void HideTutorialMessage()
    {
        if (hideTutorialCoroutine != null)
        {
            StopCoroutine(hideTutorialCoroutine);
            hideTutorialCoroutine = null;
        }

        if (tutorSpeechBubble != null && isTutorialActive)
        {
            tutorSpeechBubble.gameObject.SetActive(false);
            isTutorialActive = false;
            Debug.Log("튜토리얼 메시지 숨김");
        }
    }

    public void ShowSpeechBubble(DialogueLine line)
    {
        if (line != null)
        {
            BattleEventManager.Instance?.ShowTutorialMessage(line.text);
        }
    }

    public void HideSpeechBubble()
    {
        HideTutorialMessage();
    }

    /// <summary>
    /// 시스템 상태 메시지 표시 (미디어파이프 등)
    /// </summary>
    private void UpdateStatusText(string message)
    {
        if (statusText == null) return;
        if (string.IsNullOrEmpty(message))
        {
            statusText.gameObject.SetActive(false);
            isStatusActive = false;
        }
        else
        {
            statusText.text = message;
            statusText.gameObject.SetActive(true);
            isStatusActive = true;
        }
    }

    public void HideStatusText()
    {
        if (statusText != null && isStatusActive)
        {
            statusText.gameObject.SetActive(false);
            isStatusActive = false;
            Debug.Log("상태 텍스트 숨김");
        }
    }

    // ===== [수정됨] BattleManager가 명령하는 UI 제어 메서드들 =====

    /// <summary>
    /// BattleManager가 명령: 메인 UI(공격하기, 도망가기) 표시/숨김
    /// </summary>
    public void ShowMainUI(bool show)
    {
        if (attackButton != null)
        {
            attackButton.gameObject.SetActive(show);
            attackButton.interactable = show;
        }

        if (escapeButton != null)
        {
            escapeButton.gameObject.SetActive(show);
            escapeButton.interactable = show;
        }

        Debug.Log($"메인 UI {(show ? "표시" : "숨김")}");
    }

    /// <summary>
    /// BattleManager가 명령: 스킬 패널 표시/숨김
    /// </summary>
    public void ShowSkillPanel(bool show)
    {
        if (skillSelectionPanel != null)
        {
            skillSelectionPanel.SetActive(show);
        }
        Debug.Log($"스킬 패널 {(show ? "표시" : "숨김")}");
    }

    private void HideAllBubbles()
    {
        if (canSpeechBubble != null) canSpeechBubble.gameObject.SetActive(false);
        if (tutorSpeechBubble != null) tutorSpeechBubble.gameObject.SetActive(false);
        isDialogueActive = false;
        isTutorialActive = false;
    }

    // ===== [수정됨] 버튼 이벤트 핸들러 - 이제 BattleManager에 '보고'만 함 =====

    /// <summary>
    /// 공격 버튼 클릭 - BattleManager에게 보고만 함 (UI 변경은 BattleManager가 지시)
    /// </summary>
    void OnAttackButtonClicked()
    {
        Debug.Log("공격 버튼 클릭됨 -> BattleManager에 보고");
        BattleEventManager.Instance.PlayerRequestsAttack();
    }

    void OnEscapeButtonClicked()
    {
        BattleEventManager.Instance.AttemptEscape();
    }

    /// <summary>
    /// 스킬 버튼 클릭 - BattleManager에게 보고만 함 (패널 닫기는 BattleManager가 지시)
    /// </summary>
    void OnSkillButtonClicked(string skillName)
    {
        Debug.Log($"스킬 버튼 클릭됨: {skillName} -> BattleManager에 보고");
        BattleEventManager.Instance.SelectPlayerSkill(skillName);
    }

    public void DisableAllButtons()
    {
        if (attackButton != null)
            attackButton.interactable = false;

        if (escapeButton != null)
            escapeButton.interactable = false;

        Debug.Log("모든 버튼 비활성화");
    }

    public void ClearAllUI()
    {
        HideTutorialMessage();
        HideStatusText();

        if (hideDialogueCoroutine != null)
        {
            StopCoroutine(hideDialogueCoroutine);
            hideDialogueCoroutine = null;
        }

        if (canSpeechBubble != null)
            canSpeechBubble.gameObject.SetActive(false);

        isDialogueActive = false;
        isTutorialActive = false;
        isStatusActive = false;

        Debug.Log("모든 UI 정리됨");
    }
}