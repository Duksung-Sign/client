using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;



public class SkillBookManager : MonoBehaviour
{
    public static SkillBookManager Instance;

    [Header("UI References")]
    public GameObject skillBookPanel;
    public Transform skillContainer;
    public GameObject skillSlotPrefab;
    public Button closeButton;

    [Header("Skill Data")]
    public SkillDataSO[] allSkills; // 모든 스킬 데이터

    [Header("Debug")]
    public bool debugMode = true;

    [Header("Page System")]
    public Button prevPageButton;
    public Button nextPageButton;
    public TextMeshProUGUI pageNumberText;

    private int currentPage = 0;
    private int totalPages = 4; // 4페이지 (기본 + 3개 페이지 아이템)


    // 스킬북 상태
    private bool isSkillBookUnlocked = false;
    private List<string> unlockedSkills = new List<string>();
    private List<GameObject> skillSlots = new List<GameObject>();

    // 키 입력
    public KeyCode skillBookKey = KeyCode.K; // 스킬북 열기 키

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            ResetSavedSkills();
            ResetItemCounts();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void ResetItemCounts()
    {
       
        PlayerPrefs.DeleteAll();

        PlayerPrefs.Save();
    }

    private void ResetSavedSkills()
    {
        // 스킬북 해금 상태
        PlayerPrefs.DeleteKey("SkillBookUnlocked");
        // 개별 스킬 해금 상태
        if (allSkills != null)
        {
            foreach (var skill in allSkills)
            {
                PlayerPrefs.DeleteKey($"Skill_{skill.GetSkillID()}_Unlocked");
            }
        }
        PlayerPrefs.Save();
    }

    void Start()
    {
        InitializeSkillBook();
        LoadSkillBookProgress();

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseSkillBook);

        if (skillBookPanel != null)
            skillBookPanel.SetActive(false);

        // 페이지 버튼 이벤트
        if (prevPageButton != null)
            prevPageButton.onClick.AddListener(PrevPage);
        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(NextPage);

        if (skillBookPanel != null)
            skillBookPanel.SetActive(false);
    }

    public void PrevPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            RefreshSkillSlots();
            UpdatePageUI();
        }
    }

    public void NextPage()
    {
        if (currentPage < totalPages - 1)
        {
            currentPage++;
            RefreshSkillSlots();
            UpdatePageUI();
        }
    }

    void UpdatePageUI()
    {
        if (pageNumberText != null)
            pageNumberText.text = $"{currentPage + 1} / {totalPages}";

        if (prevPageButton != null)
            prevPageButton.interactable = currentPage > 0;

        if (nextPageButton != null)
            nextPageButton.interactable = currentPage < totalPages - 1;
    }

    void Update()
    {
        if (!isSkillBookUnlocked) return;

        if (Input.GetKeyDown(skillBookKey))
        {
            if (skillBookPanel.activeSelf)
                CloseSkillBook();
            else
                OpenSkillBook();
        }
    }

    void InitializeSkillBook()
    {
        // 스킬 슬롯 생성
        CreateSkillSlots();

        if (debugMode)
            Debug.Log("[SkillBook] 스킬북 초기화 완료");
    }

    void CreateSkillSlots()
    {
        if (skillContainer == null || skillSlotPrefab == null) return;

        // 기존 슬롯 정리
        foreach (Transform child in skillContainer)
        {
            Destroy(child.gameObject);
        }
        skillSlots.Clear();

        // 새 슬롯 생성 (4개)
        for (int i = 0; i < 4; i++)
        {
            GameObject slot = Instantiate(skillSlotPrefab, skillContainer);
            skillSlots.Add(slot);

            // 슬롯 초기 설정
            SetupSkillSlot(slot, i);
        }
    }

    void SetupSkillSlot(GameObject slot, int slotIndex)
    {
        // UI 요소 참조
        Image skillImage = slot.transform.Find("SkillImage")?.GetComponent<Image>();
        TextMeshProUGUI skillName = slot.transform.Find("SkillName")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI skillDescription = slot.transform.Find("SkillDescription")?.GetComponent<TextMeshProUGUI>();
        GameObject lockIcon = slot.transform.Find("LockIcon")?.gameObject;

        // 스킬 데이터 가져오기
        SkillDataSO skillData = GetSkillDataBySlot(slotIndex);

        // 해금 여부 판단
        bool isUnlocked = skillData != null &&
                          (skillData.isDefaultSkill || IsSkillUnlocked(skillData.GetSkillID()));

        if (skillData != null && isUnlocked)
        {
            // 해금된 스킬만 보여주기
            if (skillImage != null)
            {
                skillImage.gameObject.SetActive(true);
                skillImage.sprite = skillData.skillImage;
            }
            if (skillName != null)
            {
                skillName.gameObject.SetActive(true);
                skillName.text = skillData.GetSkillName();
            }
            if (skillDescription != null)
            {
                skillDescription.gameObject.SetActive(true);
                skillDescription.text = skillData.skillDescription;
            }
            if (lockIcon != null)
                lockIcon.SetActive(false);
        }
        else
        {
            // 미해금 스킬: 이미지·이름·설명 모두 끄고, 자물쇠만 켜기
            if (skillImage != null)
                skillImage.gameObject.SetActive(false);
            if (skillName != null)
                skillName.gameObject.SetActive(false);
            if (skillDescription != null)
                skillDescription.gameObject.SetActive(false);
            if (lockIcon != null)
                lockIcon.SetActive(true);
        }
    }


    SkillDataSO GetSkillDataBySlot(int slotIndex)
    {
        if (allSkills == null) return null;

        // 현재 페이지의 스킬 인덱스 계산
        int skillIndex = currentPage * 4 + slotIndex;

        if (skillIndex >= allSkills.Length)
            return null;

        return allSkills[skillIndex];
    }


    public void UnlockSkillBook()
    {
        if (isSkillBookUnlocked) return;

        isSkillBookUnlocked = true;
        PlayerPrefs.SetInt("SkillBookUnlocked", 1);

        // 기본 스킬들 해금
        UnlockDefaultSkills();

        if (debugMode)
            Debug.Log("[SkillBook] 스킬북이 해금되었습니다!");
    }

    void UnlockDefaultSkills()
    {
        if (allSkills == null) return;

        foreach (var skill in allSkills)
        {
            if (skill.isDefaultSkill)
            {
                UnlockSkill(skill.GetSkillID());
            }
        }
    }

    public void UnlockSkill(string skillID)
    {
        if (unlockedSkills.Contains(skillID)) return;

        unlockedSkills.Add(skillID);
        PlayerPrefs.SetString($"Skill_{skillID}_Unlocked", "true");
        PlayerPrefs.Save();

        // UI 업데이트
        RefreshSkillSlots();

        if (debugMode)
            Debug.Log($"[SkillBook] 스킬 해금: {skillID}");
    }

    public void UnlockSkillByPageItem(string pageItemID)
    {
        if (allSkills == null) return;

        foreach (var skill in allSkills)
        {
            if (skill.requiredPageItemID == pageItemID)
            {
                UnlockSkill(skill.GetSkillID());

                if (debugMode)
                    Debug.Log($"[SkillBook] 페이지 아이템 {pageItemID}로 스킬 해금: {skill.GetSkillID()}");
            }
        }
    }

    bool IsSkillUnlocked(string skillID)
    {
        return unlockedSkills.Contains(skillID) ||
               PlayerPrefs.GetString($"Skill_{skillID}_Unlocked", "false") == "true";
    }

    void RefreshSkillSlots()
    {
        for (int i = 0; i < skillSlots.Count; i++)
        {
            SetupSkillSlot(skillSlots[i], i);
        }
    }

    public void OpenSkillBook()
    {
        if (!isSkillBookUnlocked) return;

        if (skillBookPanel != null)
        {
            skillBookPanel.SetActive(true);
            RefreshSkillSlots();

            // 게임 상태 변경
            if (GameManager.Instance != null)
                GameManager.Instance.SetMenuState(true);

            if (debugMode)
                Debug.Log("[SkillBook] 스킬북 열림");
        }
    }

    public void CloseSkillBook()
    {
        if (skillBookPanel != null)
        {
            skillBookPanel.SetActive(false);
            // 이 부분이 반드시 필요합니다.
            if (GameManager.Instance != null)
                GameManager.Instance.SetMenuState(false);

            if (debugMode)
                Debug.Log("[SkillBook] 스킬북 닫힘");
        }
    }

    void ShowSkillDetails(SkillDataSO skillData)
    {
        // 스킬 상세 정보 표시 (간단한 로그로 대체)
        if (debugMode)
        {
            Debug.Log($"=== {skillData.GetSkillName()} ===");
            Debug.Log(skillData.skillDescription);
        }
    }

    void LoadSkillBookProgress()
    {
        // 스킬북 해금 상태 로드
        isSkillBookUnlocked = PlayerPrefs.GetInt("SkillBookUnlocked", 0) == 1;

        // 해금된 스킬들 로드
        unlockedSkills.Clear();
        if (allSkills != null)
        {
            foreach (var skill in allSkills)
            {
                if (PlayerPrefs.GetString($"Skill_{skill.GetSkillID()}_Unlocked", "false") == "true")
                {
                    unlockedSkills.Add(skill.GetSkillID());
                }
            }
        }
    }

    // 외부에서 스킬북 상태 확인
    public bool IsSkillBookUnlocked()
    {
        return isSkillBookUnlocked;
    }

    // 디버그 메서드들
    [ContextMenu("Unlock Skill Book")]
    public void DebugUnlockSkillBook()
    {
        UnlockSkillBook();
    }

    [ContextMenu("Reset Skill Book")]
    public void DebugResetSkillBook()
    {
        PlayerPrefs.DeleteKey("SkillBookUnlocked");
        if (allSkills != null)
        {
            foreach (var skill in allSkills)
            {
                PlayerPrefs.DeleteKey($"Skill_{skill.GetSkillID()}_Unlocked");
            }
        }
        PlayerPrefs.Save();

        isSkillBookUnlocked = false;
        unlockedSkills.Clear();
        RefreshSkillSlots();

        Debug.Log("[SkillBook] 스킬북 리셋 완료");
    }
}
