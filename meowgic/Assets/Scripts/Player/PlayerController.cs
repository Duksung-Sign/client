using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    [Header("이동 설정")]
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 movement;
    private bool canMove = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

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
        PositionPlayerOnLoad();
    }

    private void PositionPlayerOnLoad()
    {
        if (GameDataManager.Instance == null) return;

        string spawnPointName = GameDataManager.Instance.PlayerSpawnPointID;
        if (!string.IsNullOrEmpty(spawnPointName))
        {
            GameObject spawnPointObject = GameObject.Find(spawnPointName);
            if (spawnPointObject != null)
            {
                transform.position = spawnPointObject.transform.position;
                Debug.Log($"[PlayerController] 스폰 위치 '{spawnPointName}'로 이동했습니다.");
            }
            else
            {
                Debug.LogWarning($"[PlayerController] 스폰 위치 오브젝트 '{spawnPointName}'를 씬에서 찾을 수 없습니다!");
            }
            GameDataManager.Instance.ClearPlayerSpawnPoint();
        }
        else if (GameDataManager.Instance.LastPlayerPosition != Vector3.zero)
        {
            transform.position = GameDataManager.Instance.LastPlayerPosition;
            Debug.Log($"[PlayerController] 저장된 위치 {GameDataManager.Instance.LastPlayerPosition}에서 시작합니다.");
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