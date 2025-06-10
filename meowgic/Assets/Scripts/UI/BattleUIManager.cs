using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    [Header("UI 요소")]
    public Image monsterImage;
    public TextMeshProUGUI descriptionText;
    public Button attackButton;
    public Button escapeButton;

    [Header("스킬 선택 UI")]
    public GameObject skillSelectionPanel;
    public Button helloSkillButton;
    public Button sorrySkillButton;
    public Button thankSkillButton;

    [Header("결과 표시 UI")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;

    private BattleManager battleManager;

    // 스킬 버튼들을 배열로 관리
    private Button[] skillButtons;

    void Start()
    {
        battleManager = FindObjectOfType<BattleManager>();

        // 스킬 버튼 배열 초기화
        skillButtons = new Button[] { helloSkillButton, sorrySkillButton, thankSkillButton };

        attackButton.onClick.AddListener(OnAttackButtonClicked);
        escapeButton.onClick.AddListener(OnEscapeButtonClicked);

        helloSkillButton.onClick.AddListener(() => OnSkillSelected("hello"));
        sorrySkillButton.onClick.AddListener(() => OnSkillSelected("sorry"));
        thankSkillButton.onClick.AddListener(() => OnSkillSelected("thank"));

        // 기본적으로 나오는 UI
        ShowMainBattleUI();
    }

    public void ShowMainBattleUI()
    {
        skillSelectionPanel.SetActive(false);
        resultPanel.SetActive(false);
        attackButton.gameObject.SetActive(true);
        escapeButton.gameObject.SetActive(true);

        // 버튼들을 다시 활성화
        attackButton.interactable = true;
        escapeButton.interactable = true;

        descriptionText.text = "무엇을 할까?";

        Debug.Log("메인 배틀 UI 표시");
    }

    public void ShowSkillSelection()
    {
        skillSelectionPanel.SetActive(true);
        attackButton.gameObject.SetActive(false);
        escapeButton.gameObject.SetActive(false);

        // 스킬 버튼들 활성화
        EnableSkillButtons(true);

        descriptionText.text = "어떤 스킬을 사용할까?";
    }

    public void ShowResult(string message)
    {
        resultPanel.SetActive(true);
        resultText.text = message;
        skillSelectionPanel.SetActive(false);

        // 결과를 보여줄 때는 모든 버튼 비활성화
        DisableBattleButtons();
    }

    public void SetMonsterImage(Sprite sprite)
    {
        if (monsterImage != null && sprite != null)
        {
            monsterImage.sprite = sprite;
        }
    }

    void OnAttackButtonClicked()
    {
        ShowSkillSelection();
    }

    void OnEscapeButtonClicked()
    {
        if (battleManager != null)
        {
            battleManager.AttemptEscape();
        }
    }

    void OnSkillSelected(string skillName)
    {
        if (battleManager != null)
        {
            battleManager.UseSkill(skillName);
        }
    }

    void OnBackButtonClicked()
    {
        ShowMainBattleUI();
    }

    /// <summary>
    /// 메시지 텍스트를 지우고 배틀 UI를 초기 상태로 되돌립니다
    /// </summary>
    public void ClearMessage()
    {
        if (resultText != null)
        {
            resultText.text = "";
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 배틀 버튼들을 다시 활성화합니다
    /// </summary>
    public void ShowBattleButtons()
    {
        // 메인 배틀 UI를 다시 보여줌
        ShowMainBattleUI();
    }

    /// <summary>
    /// 스킬 사용 중이거나 몬스터 턴일 때 버튼들을 비활성화
    /// </summary>
    public void DisableBattleButtons()
    {
        // 메인 버튼들 비활성화
        if (attackButton != null)
        {
            attackButton.interactable = false;
        }

        if (escapeButton != null)
        {
            escapeButton.interactable = false;
        }

        // 스킬 버튼들 비활성화
        EnableSkillButtons(false);
    }

    /// <summary>
    /// 스킬 버튼들의 활성화/비활성화를 제어
    /// </summary>
    void EnableSkillButtons(bool enable)
    {
        if (skillButtons != null)
        {
            foreach (var button in skillButtons)
            {
                if (button != null)
                {
                    button.interactable = enable;
                }
            }
        }
    }

    /// <summary>
    /// 결과 패널을 숨기고 메인 UI로 돌아갑니다 (Continue 버튼용)
    /// </summary>
    public void HideResultAndShowMain()
    {
        ClearMessage();
        ShowMainBattleUI();
    }
}