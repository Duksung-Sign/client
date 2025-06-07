using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("플레이어 참조")]
    public PlayerController playerController;

    // 게임 상태들
    private bool isInDialogue = false;
    private bool isInMenu = false;
    private bool isPaused = false;

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
        // 플레이어 컨트롤러를 찾기
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        if (playerController == null)
        {
            Debug.LogWarning("PlayerController를 찾을 수 없습니다!");
        }
    }

    // 대화 상태 제어
    public void SetDialogueState(bool inDialogue)
    {
        isInDialogue = inDialogue;
        UpdatePlayerMovement();

        Debug.Log($"대화 상태: {(inDialogue ? "시작" : "종료")}");
    }

    // 메뉴 상태 제어 (나중에 인벤토리 등에서 사용)
    public void SetMenuState(bool inMenu)
    {
        isInMenu = inMenu;
        UpdatePlayerMovement();
    }

    // 일시정지 상태 제어
    public void SetPauseState(bool paused)
    {
        isPaused = paused;
        UpdatePlayerMovement();

        // 일시정지 시 시간도 멈춤
        Time.timeScale = paused ? 0f : 1f;
    }

    // 플레이어 이동 가능 여부 업데이트
    void UpdatePlayerMovement()
    {
        if (playerController != null)
        {
            // 대화 중이거나 메뉴 중이거나 일시정지 중이면 이동 불가
            bool canMove = !isInDialogue && !isInMenu && !isPaused;
            playerController.SetCanMove(canMove);
        }
    }

    // 상태 확인 메서드들
    public bool IsInDialogue() => isInDialogue;
    public bool IsInMenu() => isInMenu;
    public bool IsPaused() => isPaused;
    public bool CanPlayerMove() => !isInDialogue && !isInMenu && !isPaused;
}