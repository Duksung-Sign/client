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
    private readonly float magnitudeThreshold = 0.5f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        GetInput();
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
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }
}