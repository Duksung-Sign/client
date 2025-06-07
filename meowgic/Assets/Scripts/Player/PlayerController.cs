using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 movement;
    private Vector2 lastDirection;

    // 이동 제어용
    private bool canMove = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // 이동 가능할 때만 입력 받기
        if (canMove)
        {
            GetInput();
        }
        else
        {
            // 이동 불가능할 때는 입력을 0으로
            movement = Vector2.zero;

            // 애니메이터가 있으면 정지 상태로
            if (animator != null)
            {
                animator.SetBool("isWalk", false);
            }
        }
    }

    void FixedUpdate()
    {
        Move();
    }

    void GetInput()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");

        movement.x = inputX;
        movement.y = inputY;

        if (animator != null)
        {
            bool isMoving = (inputX != 0 || inputY != 0);
            animator.SetBool("isWalk", isMoving);

            if (isMoving)
            {
                if (Mathf.Abs(inputY) > Mathf.Abs(inputX))
                {
                    lastDirection = new Vector2(0, inputY);
                }
                else
                {
                    lastDirection = new Vector2(inputX, 0);
                }

                animator.SetFloat("xDir", lastDirection.x);
                animator.SetFloat("yDir", lastDirection.y);
            }
        }

        movement = movement.normalized;
    }

    void Move()
    {
        if (canMove)
        {
            rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
        }
    }

    // 외부에서 이동 제어하는 메서드들
    public void SetCanMove(bool canMove)
    {
        this.canMove = canMove;
        Debug.Log($"플레이어 이동: {(canMove ? "허용" : "금지")}");

        // 이동 금지될 때 즉시 정지
        if (!canMove)
        {
            movement = Vector2.zero;
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }
    }

    public bool CanMove()
    {
        return canMove;
    }
}