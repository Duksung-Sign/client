using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 movement;
    private bool canMove = true; 

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnGameStateChanged += HandleGameStateChange;
        }
    }

    void OnDestroy()
    {
        if (GameEventManager.Instance != null)
        {
            GameEventManager.Instance.OnGameStateChanged -= HandleGameStateChange;
        }
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            HandleGameStateChange(GameManager.Instance.CurrentState);
        }
    }

    private void HandleGameStateChange(GameState newState)
    {
        canMove = (newState == GameState.Field || newState == GameState.Cutscene);

        if (!canMove)
        {
            rb.velocity = Vector2.zero;
            animator.SetBool("isWalk", false);
        }
    }

    void Update()
    {
        if (canMove)
        {
            GetInput();
        }
        else
        {
            movement = Vector2.zero;
        }
    }

    void FixedUpdate()
    {
        if (canMove)
        {
            rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
        }
    }

    void GetInput()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        bool isMoving = movement != Vector2.zero;
        animator.SetBool("isWalk", isMoving);

        if (isMoving)
        {
            animator.SetFloat("xDir", movement.x);
            animator.SetFloat("yDir", movement.y);
        }

        movement.Normalize();
    }
}