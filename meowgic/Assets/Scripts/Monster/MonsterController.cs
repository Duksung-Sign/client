using UnityEngine;

public class MonsterController : MonoBehaviour, IInteractable
{
    [Header("몬스터 데이터")]
    public MonsterDataSO monsterData;
    private string monsterUniqueID;

    [Header("AI")]
    public MonsterState currentState = MonsterState.Idle;
    private Transform player;
    private Vector3 initialPosition;
    private Vector3 patrolTarget;
    private float stateTimer;
    private const float IDLE_TIME = 2f;
    private const float PATROL_TIME = 3f;

    private Rigidbody2D rb;
    private bool isInitialized = false;

    public float PatrolRange => monsterData.patrolRange;
    public float ChaseRange => monsterData.chaseRange;
    public float MoveSpeed => monsterData.moveSpeed;

    public enum MonsterState { Idle, Patrol, Chase, Return }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (monsterData == null)
        {
            Debug.LogError($"MonsterData가 설정되지 않았습니다: {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        monsterUniqueID = GenerateMonsterID();

        if (GameDataManager.Instance.IsMonsterDefeated(monsterUniqueID))
        {
            Destroy(gameObject);
            return;
        }

        initialPosition = transform.position;
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;

        ChangeState(MonsterState.Idle);
        isInitialized = true;
    }

    private string GenerateMonsterID()
    {
        return $"{monsterData.monsterID}_{transform.position.x:F1}_{transform.position.y:F1}";
    }

    void Update()
    {
        if (!isInitialized || player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        switch (currentState)
        {
            case MonsterState.Idle:
                if (distanceToPlayer <= ChaseRange) ChangeState(MonsterState.Chase);
                else if (stateTimer <= 0) ChangeState(MonsterState.Patrol);
                break;
            case MonsterState.Patrol:
                if (distanceToPlayer <= ChaseRange) ChangeState(MonsterState.Chase);
                else if (Vector3.Distance(transform.position, patrolTarget) < 0.5f || stateTimer <= 0) ChangeState(MonsterState.Idle);
                MoveTowards(patrolTarget);
                break;
            case MonsterState.Chase:
                if (distanceToPlayer > ChaseRange * 1.5f) ChangeState(MonsterState.Return);
                MoveTowards(player.position);
                break;
            case MonsterState.Return:
                if (Vector3.Distance(transform.position, initialPosition) < 0.5f) ChangeState(MonsterState.Idle);
                MoveTowards(initialPosition);
                break;
        }
        stateTimer -= Time.deltaTime;
    }

    public void Interact()
    {
        if (!isInitialized) return;

        Debug.Log($"[MonsterController] 전투 시작 요청: {monsterData.displayName}");
        isInitialized = false; // 중복 상호작용 방지

        GameEventManager.Instance.TriggerBattleStartRequested(monsterData, monsterUniqueID);
    }

    public string GetInteractionPromptText()
    {
        return "";
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Interact();
        }
    }

    private void ChangeState(MonsterState newState)
    {
        currentState = newState;
        switch (newState)
        {
            case MonsterState.Idle:
                stateTimer = IDLE_TIME;
                rb.velocity = Vector2.zero;
                break;
            case MonsterState.Patrol:
                stateTimer = PATROL_TIME;
                patrolTarget = initialPosition + (Vector3)(Random.insideUnitCircle * PatrolRange);
                break;
        }
    }

    private void MoveTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        rb.velocity = direction * MoveSpeed;
        if (direction.x != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(direction.x), 1, 1);
        }
    }

    public bool IsCurrentlyInteractable()
    {
        return true;
    }
}