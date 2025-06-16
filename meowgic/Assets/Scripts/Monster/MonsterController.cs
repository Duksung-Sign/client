using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MonsterController : MonoBehaviour
{
    [Header("컴포넌트 참조")]
    public MonsterDataSO monsterData;
    private Transform player;
    private Rigidbody2D rb;
    private Collider2D triggerCollider;

    public enum MonsterState
    {
        Idle,
        Patrol,
        Chase,
        Return,
        BattleStarted
    }

    [Header("AI 상태")]
    public MonsterState currentState = MonsterState.Idle;

    [Header("범위 설정")]
    public bool overrideRanges = false;
    [SerializeField, Range(1f, 10f)]
    private float customPatrolRange = 3f;
    [SerializeField, Range(1f, 8f)]
    private float customChaseRange = 2f;
    [SerializeField, Range(0.5f, 5f)]
    private float customMoveSpeed = 2f;

    [Header("이동 관련")]
    private Vector3 initialPosition;
    private Vector3 patrolTarget;
    private float stateTimer;
    private float idleTime = 2f;
    private float patrolTime = 3f;

    [Header("고유 식별자")]
    public string uniqueIdentifier = "";

    // 몬스터 고유 ID
    private string monsterUniqueID;

    // 안전 상태 관리
    private bool battleStarted = false;
    private bool isDestroyed = false;
    private bool isInitialized = false;

    // 프로퍼티
    public float PatrolRange => overrideRanges ? customPatrolRange : (monsterData != null ? monsterData.patrolRange : 3f);
    public float ChaseRange => overrideRanges ? customChaseRange : (monsterData != null ? monsterData.chaseRange : 2f);
    public float MoveSpeed => overrideRanges ? customMoveSpeed : (monsterData != null ? monsterData.moveSpeed : 2f);

    void Start()
    {
        try
        {
            SafeInitialize();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"MonsterController 초기화 실패: {e.Message}");
            SafeDestroy();
        }
    }

    void SafeInitialize()
    {
        // 몬스터 고유 ID 생성
        monsterUniqueID = GenerateMonsterID();

        // 이미 죽은 몬스터인지 확인
        if (GameManager.Instance != null && GameManager.Instance.IsMonsterDefeated(monsterUniqueID))
        {
            Debug.Log($"이미 죽은 몬스터 제거: {monsterUniqueID}");
            SafeDestroy();
            return;
        }

        // 기본 데이터 유효성 검사
        if (monsterData == null)
        {
            Debug.LogError($"MonsterData가 설정되지 않았습니다: {gameObject.name}");
            SafeDestroy();
            return;
        }

        // 컴포넌트 초기화
        initialPosition = transform.position;
        rb = GetComponent<Rigidbody2D>();
        triggerCollider = GetComponent<Collider2D>();

        // 필수 컴포넌트 확인
        if (rb == null)
        {
            Debug.LogError($"Rigidbody2D가 없습니다: {gameObject.name}");
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        if (triggerCollider == null)
        {
            Debug.LogError($"Collider2D가 없습니다: {gameObject.name}");
            triggerCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        // Trigger 설정 확인
        if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning($"{gameObject.name}: Collider를 Trigger로 설정합니다.");
            triggerCollider.isTrigger = true;
        }

        // 플레이어 찾기
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        else
        {
            Debug.LogWarning("Player 태그를 가진 오브젝트를 찾을 수 없습니다!");
        }

        // 초기 상태 설정
        SetRandomPatrolTarget();
        currentState = MonsterState.Idle;
        stateTimer = idleTime;
        isInitialized = true;

        Debug.Log($"몬스터 초기화 완료: {monsterData.monsterName} (ID: {monsterUniqueID})");
    }

    string GenerateMonsterID()
    {
        if (!string.IsNullOrEmpty(uniqueIdentifier))
        {
            return uniqueIdentifier;
        }

        Vector3 pos = transform.position;
        return $"{(monsterData != null ? monsterData.monsterName : "Unknown")}_{pos.x:F2}_{pos.y:F2}";
    }

    void Update()
    {
        // 안전 상태 체크
        if (!isInitialized || battleStarted || isDestroyed || currentState == MonsterState.BattleStarted)
            return;

        if (player == null) return;

        try
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            float distanceToInitial = Vector3.Distance(transform.position, initialPosition);

            // 상태 전환 로직
            switch (currentState)
            {
                case MonsterState.Idle:
                    HandleIdleState(distanceToPlayer);
                    break;
                case MonsterState.Patrol:
                    HandlePatrolState(distanceToPlayer);
                    break;
                case MonsterState.Chase:
                    HandleChaseState(distanceToPlayer, distanceToInitial);
                    break;
                case MonsterState.Return:
                    HandleReturnState(distanceToPlayer);
                    break;
            }

            stateTimer -= Time.deltaTime;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"MonsterController Update 오류: {e.Message}");
        }
    }

    void HandleIdleState(float distanceToPlayer)
    {
        if (distanceToPlayer <= ChaseRange)
        {
            ChangeState(MonsterState.Chase);
            return;
        }

        if (stateTimer <= 0)
        {
            ChangeState(MonsterState.Patrol);
        }
    }

    void HandlePatrolState(float distanceToPlayer)
    {
        if (distanceToPlayer <= ChaseRange)
        {
            ChangeState(MonsterState.Chase);
            return;
        }

        MoveTowards(patrolTarget);

        if (Vector3.Distance(transform.position, patrolTarget) < 0.5f || stateTimer <= 0)
        {
            ChangeState(MonsterState.Idle);
        }
    }

    void HandleChaseState(float distanceToPlayer, float distanceToInitial)
    {
        MoveTowards(player.position);

        if (distanceToPlayer > ChaseRange * 1.5f || distanceToInitial > PatrolRange * 2f)
        {
            ChangeState(MonsterState.Return);
        }
    }

    void HandleReturnState(float distanceToPlayer)
    {
        MoveTowards(initialPosition);

        if (Vector3.Distance(transform.position, initialPosition) < 0.5f)
        {
            ChangeState(MonsterState.Idle);
        }

        if (distanceToPlayer <= ChaseRange * 0.7f)
        {
            ChangeState(MonsterState.Chase);
        }
    }

    void ChangeState(MonsterState newState)
    {
        currentState = newState;

        switch (newState)
        {
            case MonsterState.Idle:
                stateTimer = idleTime;
                if (rb != null) rb.velocity = Vector2.zero;
                break;
            case MonsterState.Patrol:
                SetRandomPatrolTarget();
                stateTimer = patrolTime;
                break;
            case MonsterState.Chase:
                stateTimer = float.MaxValue;
                break;
            case MonsterState.Return:
                stateTimer = float.MaxValue;
                break;
            case MonsterState.BattleStarted:
                if (rb != null) rb.velocity = Vector2.zero;
                break;
        }
    }

    void SetRandomPatrolTarget()
    {
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        float randomDistance = Random.Range(1f, PatrolRange);
        patrolTarget = initialPosition + (Vector3)(randomDirection * randomDistance);
    }

    void MoveTowards(Vector3 target)
    {
        if (rb == null) return;

        Vector3 direction = (target - transform.position).normalized;
        rb.velocity = direction * MoveSpeed;

        if (direction.x != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (direction.x > 0 ? 1 : -1);
            transform.localScale = scale;
        }
    }

    // 안전한 충돌 감지
    void OnTriggerEnter2D(Collider2D other)
    {
        try
        {
            // 안전 상태 체크
            if (!isInitialized || battleStarted || isDestroyed || currentState == MonsterState.BattleStarted)
            {
                Debug.Log($"몬스터 {monsterUniqueID}: 충돌 무시 (안전 상태)");
                return;
            }

            // 플레이어 태그 확인
            if (other == null || !other.CompareTag("Player"))
            {
                return;
            }

            Debug.Log($"몬스터 {monsterUniqueID}: 플레이어와 충돌 감지");
            StartBattleSafe();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"몬스터 충돌 처리 오류: {e.Message}");
        }
    }

    void StartBattleSafe()
    {
        try
        {
            // 중복 배틀 시작 방지
            if (battleStarted || isDestroyed)
            {
                Debug.Log($"몬스터 {monsterUniqueID}: 이미 배틀 시작됨 - 무시");
                return;
            }

            // GameManager 유효성 확인
            if (GameManager.Instance == null)
            {
                Debug.LogError("GameManager.Instance가 null입니다!");
                return;
            }

            if (monsterData == null)
            {
                Debug.LogError("monsterData가 null입니다!");
                return;
            }

            // 배틀 시작 플래그 설정
            battleStarted = true;
            currentState = MonsterState.BattleStarted;

            // 충돌 감지 비활성화
            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
                Debug.Log($"몬스터 {monsterUniqueID}: 충돌 감지 비활성화");
            }

            // 이동 중지
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }

            // 몬스터 ID 저장
            PlayerPrefs.SetString("CurrentMonsterID", monsterUniqueID);

            Debug.Log($"안전한 배틀 시작: {monsterData.monsterName} (ID: {monsterUniqueID})");

            // GameManager에 배틀 시작 요청
            GameManager.Instance.StartBattle(monsterData, gameObject);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"배틀 시작 오류: {e.Message}");

            // 오류 복구
            battleStarted = false;
            currentState = MonsterState.Idle;
            if (triggerCollider != null)
            {
                triggerCollider.enabled = true;
            }
        }
    }

    void SafeDestroy()
    {
        if (isDestroyed) return;

        isDestroyed = true;
        Debug.Log($"몬스터 {monsterUniqueID} 안전 파괴");

        StartCoroutine(DelayedDestroy());
    }

    IEnumerator DelayedDestroy()
    {
        // 충돌 감지 비활성화
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        // 이동 중지
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }

        yield return new WaitForSeconds(0.1f);

        Destroy(gameObject);
    }

    public string GetMonsterID()
    {
        return monsterUniqueID;
    }
}