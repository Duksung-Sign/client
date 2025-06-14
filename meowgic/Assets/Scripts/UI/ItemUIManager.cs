using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ItemUIManager : MonoBehaviour
{
    public static ItemUIManager Instance;

    [Header("Item Description UI")]
    public GameObject descriptionUI;
    public TextMeshProUGUI descriptionText;
    public Canvas descriptionCanvas;

    [Header("Position Settings")]
    public Vector3 worldOffset = new Vector3(0, 1.5f, 0);  // 아이템 위쪽으로 얼마나 띄울지
    public bool followTarget = true;  // 타겟을 따라다닐지

    private Transform currentTarget;  // 현재 설명을 보여주는 아이템
    private Camera mainCamera;

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
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindObjectOfType<Camera>();

        // 캔버스를 월드 스페이스로 설정
        if (descriptionCanvas != null)
        {
            descriptionCanvas.renderMode = RenderMode.WorldSpace;
            descriptionCanvas.worldCamera = mainCamera;
        }

        HideItemDescription();
    }

    public void ShowItemDescription(string description, Transform itemTransform)
    {
        if (descriptionUI == null || descriptionText == null) return;

        currentTarget = itemTransform;
        descriptionText.text = description;
        descriptionUI.SetActive(true);

        // 위치 설정
        UpdateUIPosition();

        // 게임 상태 변경 (필요한 경우)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(true);
        }
    }

    public void ShowItemDescription(string description)
    {
        // 기존 메서드 호환성 유지 (위치 고정)
        ShowItemDescription(description, null);
    }

    public void HideItemDescription()
    {
        if (descriptionUI != null)
            descriptionUI.SetActive(false);

        currentTarget = null;

        // 게임 상태 복원
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDialogueState(false);
        }
    }

    void UpdateUIPosition()
    {
        if (currentTarget == null || descriptionCanvas == null) return;

        Vector3 worldPosition = currentTarget.position + worldOffset;
        descriptionCanvas.transform.position = worldPosition;

        // 카메라를 향하도록 회전 (빌보드 효과)
        if (mainCamera != null)
        {
            Vector3 directionToCamera = mainCamera.transform.position - descriptionCanvas.transform.position;
            directionToCamera.y = 0; // Y축 회전만 허용

            if (directionToCamera != Vector3.zero)
            {
                descriptionCanvas.transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }
    }

    void Update()
    {
        // UI 위치 업데이트 (타겟을 따라다니는 경우)
        if (followTarget && currentTarget != null && descriptionUI.activeInHierarchy)
        {
            UpdateUIPosition();
        }

        // 입력 처리
        if (descriptionUI != null && descriptionUI.activeInHierarchy)
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape))
            {
                HideItemDescription();
            }
        }
    }

    // 화면 좌표 기반으로 띄우는 방법 (대안)
    public void ShowItemDescriptionAtScreenPosition(string description, Vector3 worldPosition)
    {
        if (descriptionUI == null || descriptionText == null || mainCamera == null) return;

        // 월드 좌표를 스크린 좌표로 변환
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition + worldOffset);

        // 화면 밖으로 나가지 않도록 클램프
        screenPosition.x = Mathf.Clamp(screenPosition.x, 100, Screen.width - 100);
        screenPosition.y = Mathf.Clamp(screenPosition.y, 100, Screen.height - 100);

        descriptionText.text = description;
        descriptionUI.SetActive(true);

        // UI를 스크린 좌표로 위치 설정 (Screen Space Overlay 캔버스용)
        RectTransform rectTransform = descriptionUI.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.position = screenPosition;
        }
    }
}