using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;
using UnityEngine.EventSystems;

public class BattleUIManager : MonoBehaviour
{
    [Header("UI 요소")]
    public Image monsterImage;
    public Button attackButton;
    public Button escapeButton;
    public GameObject skillSelectionPanel;
    public TextMeshProUGUI statusText;
    public RectTransform arrowImage; 

    [Header("⭐ 스킬 버튼 (페이지 시스템)")]
    public Button[] attackSkillButtons; 
    public Button cancelSkillButton;
    public Button prevButton;
    public Button nextButton;

    [Header("HP 바 (Fill Amount 방식)")]
    public Image playerHPBarFill;
    public Image monsterHPBarFill;

    string[] skillNames = {
        "안녕하세요", "반갑습니다", "감사합니다", "괜찮아요",
        "사랑한다", "연락해주세요", "도착하다", "당신",
        "나", "싫다", "좋다", "죄송합니다"
    };

    int currentPage = 0;
    private Coroutine statusMessageCoroutine;

    void Awake()
    {
        Debug.Log("[BattleUIManager] ━━━ Awake 호출 ━━━");

        if (BattleEventManager.Instance != null)
        {
            Debug.Log("[BattleUIManager] ✅ BattleEventManager 찾음, 이벤트 구독 시작");

            BattleEventManager.Instance.OnBattleStateChanged += HandleBattleStateChange;
            BattleEventManager.Instance.OnRequestUpdateStatus += ShowStatusMessage;
            BattleEventManager.Instance.OnPlayerHPChanged += UpdatePlayerHPBar;
            BattleEventManager.Instance.OnMonsterHPChanged += UpdateMonsterHPBar;

            Debug.Log("[BattleUIManager] ✅ 모든 이벤트 구독 완료!");
        }
        else
        {
            Debug.LogError("[BattleUIManager] ❌ BattleEventManager.Instance가 NULL!");
        }

        // ⭐ UI 요소 null 체크
        Debug.Log($"[BattleUIManager] playerHPBarFill: {(playerHPBarFill != null ? "OK" : "NULL")}");
        Debug.Log($"[BattleUIManager] monsterHPBarFill: {(monsterHPBarFill != null ? "OK" : "NULL")}");
        Debug.Log("[BattleUIManager] ━━━━━━━━━━━━━━━━━━━");
    }

    void OnDestroy()
    {
        if (BattleEventManager.Instance != null)
        {
            BattleEventManager.Instance.OnBattleStateChanged -= HandleBattleStateChange;
            BattleEventManager.Instance.OnRequestUpdateStatus -= ShowStatusMessage;
            BattleEventManager.Instance.OnPlayerHPChanged -= UpdatePlayerHPBar;
            BattleEventManager.Instance.OnMonsterHPChanged -= UpdateMonsterHPBar;
        }
    }

    void Start()
    {
        InitializeUIState();
        SetupButtonEvents();

        SetupEventTrigger(attackButton);
        SetupEventTrigger(escapeButton);

        foreach (var btn in attackSkillButtons)
        {
            SetupEventTrigger(btn);
        }

        SetupEventTrigger(cancelSkillButton);
        SetupEventTrigger(prevButton);
        SetupEventTrigger(nextButton);
    }

    private void UpdatePlayerHPBar(int current, int max)
    {
        if (playerHPBarFill == null) return;
        float fillAmount = max > 0 ? (float)current / max : 0f;
        playerHPBarFill.DOFillAmount(fillAmount, 0.5f).SetEase(Ease.OutQuad);
    }

    private void UpdateMonsterHPBar(int current, int max)
    {
        if (monsterHPBarFill == null) return;
        float fillAmount = max > 0 ? (float)current / max : 0f;
        monsterHPBarFill.DOFillAmount(fillAmount, 0.5f).SetEase(Ease.OutQuad);
    }

    private void InitializeUIState()
    {
        statusText?.gameObject.SetActive(false);
        skillSelectionPanel?.SetActive(false);

        if (arrowImage != null)
            arrowImage.gameObject.SetActive(false);
    }

    private void SetupButtonEvents()
    {
        attackButton?.onClick.AddListener(() => {
            Debug.Log("[BattleUIManager] 공격하기 클릭");
            BattleEventManager.Instance?.PlayerRequestsAttack();
        });

        escapeButton?.onClick.AddListener(() => {
            Debug.Log("[BattleUIManager] 도망가기 클릭");
            BattleEventManager.Instance?.AttemptEscape();
        });

        prevButton?.onClick.AddListener(() =>
        {
            if (currentPage > 0) { currentPage--; UpdateSkillPage(); }
        });

        nextButton?.onClick.AddListener(() =>
        {
            if (currentPage < 3) { currentPage++; UpdateSkillPage(); }
        });

        cancelSkillButton?.onClick.AddListener(() =>
        {
            Debug.Log("[BattleUIManager] 스킬 선택 취소");
            skillSelectionPanel.SetActive(false);
            ShowMainUI(true);
        });
    }

    void UpdateSkillPage()
    {
        int startSkillIdx = currentPage * 3;

        for (int i = 0; i < attackSkillButtons.Length; i++)
        {
            int skillIdx = startSkillIdx + i;

            if (skillIdx < skillNames.Length)
            {
                attackSkillButtons[i].gameObject.SetActive(true);
                attackSkillButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = skillNames[skillIdx];

                attackSkillButtons[i].onClick.RemoveAllListeners();
                int capturedIdx = skillIdx;
                attackSkillButtons[i].onClick.AddListener(() => SelectSkill(capturedIdx));

                SetupEventTrigger(attackSkillButtons[i]);
            }
            else
            {
                attackSkillButtons[i].gameObject.SetActive(false);
            }
        }

        prevButton.interactable = (currentPage > 0);
        nextButton.interactable = (currentPage < (skillNames.Length / 3 - 1));

        Debug.Log($"[BattleUIManager] 페이지 {currentPage + 1}/4");
    }

    void SelectSkill(int idx)
    {
        string selectedSkillName = skillNames[idx];
        Debug.Log($"[BattleUIManager] 스킬 선택: {selectedSkillName}");

        skillSelectionPanel.SetActive(false);
        BattleEventManager.Instance?.SelectPlayerSkill(selectedSkillName);
    }

    private void HandleBattleStateChange(BattleState newState)
    {
        ShowMainUI(false);
        ShowSkillPanel(false);
        ShowStatusMessage(null, 0);

        switch (newState)
        {
            case BattleState.Initializing:
                ShowStatusMessage("전투 준비 중!", 2f);
                break;

            case BattleState.PlayerTurn_AwaitingInput:
                ShowMainUI(true);
                ShowStatusMessage("미오, 무엇을 할까?", 2f);
                break;

            case BattleState.PlayerTurn_SelectingSkill:
                ShowStatusMessage("어떤 공격을 할까?", 0);
                ShowSkillPanel(true);
                break;

            case BattleState.PlayerTurn_Gesture:
                ShowStatusMessage("묘어를 보여줘!", 0);
                break;

            case BattleState.ActionInProgress:
                break;

            case BattleState.MonsterTurn:
                ShowStatusMessage("몬스터의 차례!", 1f);
                break;

            case BattleState.BattleOver_Victory:
                ShowStatusMessage("이겼어, 미오!", 3f);
                break;

            case BattleState.BattleOver_Defeat:
                ShowStatusMessage("패배했다참치...", 3f);
                break;

            case BattleState.Escaped:
                ShowStatusMessage("도망쳤다!", 2f);
                break;
        }
    }

    private void ShowMainUI(bool show)
    {
        attackButton?.gameObject.SetActive(show);
        escapeButton?.gameObject.SetActive(show);
    }

    private void ShowSkillPanel(bool show)
    {
        skillSelectionPanel?.SetActive(show);

        if (show)
        {
            currentPage = 0;
            UpdateSkillPage();
        }
    }

    private void ShowStatusMessage(string message, float duration = 2.5f)
    {
        if (statusText == null) return;
        if (statusMessageCoroutine != null)
        {
            StopCoroutine(statusMessageCoroutine);
            statusText.DOKill();
        }

        if (string.IsNullOrEmpty(message))
        {
            statusText.gameObject.SetActive(false);
        }
        else
        {
            statusMessageCoroutine = StartCoroutine(ShowMessageCoroutine(message, duration));
        }
    }

    private IEnumerator ShowMessageCoroutine(string message, float duration)
    {
        statusText.text = message;
        statusText.gameObject.SetActive(true);
        statusText.alpha = 0;

        yield return statusText.DOFade(1f, 0.2f).WaitForCompletion();

        if (duration > 0)
        {
            yield return new WaitForSeconds(duration);
            yield return statusText.DOFade(0f, 0.2f).WaitForCompletion();
            statusText.gameObject.SetActive(false);
        }
    }

    public void ShowArrowAtButton(RectTransform btn)
    {
        if (arrowImage == null || btn == null) return;

        arrowImage.SetParent(btn.parent, false);
        float offset = -btn.rect.width * 0.5f - arrowImage.rect.width * 0.5f - 10f;
        arrowImage.anchoredPosition = btn.anchoredPosition + new Vector2(offset, 0f);
        arrowImage.gameObject.SetActive(true);
    }

    public void HideArrow()
    {
        if (arrowImage != null)
            arrowImage.gameObject.SetActive(false);
    }

    void SetupEventTrigger(Button btn)
    {
        if (btn == null) return;

        var trigger = btn.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

        // 기존 이벤트 제거
        trigger.triggers.Clear();

        // 마우스 진입 시 화살표 표시
        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) => ShowArrowAtButton(btn.GetComponent<RectTransform>()));
        trigger.triggers.Add(entryEnter);

        // 마우스 나가면 화살표 숨김
        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) => HideArrow());
        trigger.triggers.Add(entryExit);
    }
}