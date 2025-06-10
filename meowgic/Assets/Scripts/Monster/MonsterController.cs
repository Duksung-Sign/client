using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MonsterController : MonoBehaviour
{
    [Header("컴포넌트 참조")]
    public MonsterDataSO monsterData;
    private Transform player;
    private Rigidbody2D rb;

    public enum MonsterState
    {
        Idle,
        Patrol,
        Chase,
        Return
    }

    [Header("AI 상태")]
    public MonsterState currentState = MonsterState.Idle;

    [Header("이동 관련")]
    private Vector3 initialPosition;
    private Vector3 patrolTarget;
    private float stateTimer;
    private float idleTime = 2f;
    private float patrolTime = 3f;

    [Header("디버그")]
    public bool showDebugGizmos = true;

    void Start()
    {
        initialPosition = transform.position;
        rb = GetComponent<Rigidbody2D>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        // 초기 상태 설정
        SetRandomPatrolTarget();
        currentState = MonsterState.Idle;
        stateTimer = idleTime;
    }

    void Update()
    {
        if (player == null) return;

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

    void HandleIdleState(float distanceToPlayer)
    {
        // 플레이어가 감지 범위에 들어오면 추격 시작
        if (distanceToPlayer <= monsterData.chaseRange)
        {
            ChangeState(MonsterState.Chase);
            return;
        }

        // 대기 시간이 끝나면 정찰 시작
        if (stateTimer <= 0)
        {
            ChangeState(MonsterState.Patrol);
        }
    }

    void HandlePatrolState(float distanceToPlayer)
    {
        // 플레이어가 감지 범위에 들어오면 추격 시작
        if (distanceToPlayer <= monsterData.chaseRange)
        {
            ChangeState(MonsterState.Chase);
            return;
        }

        // 정찰 목표점으로 이동
        MoveTowards(patrolTarget);

        // 목표점에 도달하거나 시간이 끝나면 대기 상태로
        if (Vector3.Distance(transform.position, patrolTarget) < 0.5f || stateTimer <= 0)
        {
            ChangeState(MonsterState.Idle);
        }
    }

    void HandleChaseState(float distanceToPlayer, float distanceToInitial)
    {
        // 플레이어를 추격
        MoveTowards(player.position);

        // 플레이어가 너무 멀어지거나 초기 위치에서 너무 멀어지면 복귀
        if (distanceToPlayer > monsterData.chaseRange * 1.5f ||
            distanceToInitial > monsterData.patrolRange * 2f)
        {
            ChangeState(MonsterState.Return);
        }
    }

    void HandleReturnState(float distanceToPlayer)
    {
        // 초기 위치로 복귀
        MoveTowards(initialPosition);

        // 초기 위치에 도달하면 대기 상태로
        if (Vector3.Distance(transform.position, initialPosition) < 0.5f)
        {
            ChangeState(MonsterState.Idle);
        }

        // 복귀 중에도 플레이어가 가까이 오면 다시 추격
        if (distanceToPlayer <= monsterData.chaseRange * 0.7f)
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
                rb.velocity = Vector2.zero;
                break;

            case MonsterState.Patrol:
                SetRandomPatrolTarget();
                stateTimer = patrolTime;
                break;

            case MonsterState.Chase:
                stateTimer = float.MaxValue; // 추격 중에는 시간 제한 없음
                break;

            case MonsterState.Return:
                stateTimer = float.MaxValue; // 복귀 중에는 시간 제한 없음
                break;
        }
    }

    void SetRandomPatrolTarget()
    {
        // 초기 위치 주변에서 랜덤한 정찰 목표점 설정
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        float randomDistance = Random.Range(1f, monsterData.patrolRange);
        patrolTarget = initialPosition + (Vector3)(randomDirection * randomDistance);
    }

    void MoveTowards(Vector3 target)
    {
        Vector3 direction = (target - transform.position).normalized;
        rb.velocity = direction * monsterData.moveSpeed;

        // 몬스터가 이동 방향을 바라보도록 회전 (옵션)
        if (direction.x != 0)
        {
            // 원래 크기를 유지하면서 방향만 바꾸기
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (direction.x > 0 ? 1 : -1);
            transform.localScale = scale;
        }
    }

    // 플레이어와 충돌 시 전투 씬으로 전환
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            StartBattle();
        }
    }

    void StartBattle()
    {
        // 현재 씬 이름 저장
        PlayerPrefs.SetString("PreviousScene", SceneManager.GetActiveScene().name);

        // GameManager에 몬스터 정보 전달
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetBattleMonster(monsterData);
            Debug.Log($"몬스터 설정 완료: {monsterData.monsterName}");
        }
        else
        {
            Debug.LogError("GameManager.Instance가 null입니다!");
        }
        SceneManager.LoadScene("BattleScene");
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Vector3 center = Application.isPlaying ? initialPosition : transform.position;

        // 정찰 범위 (노란색)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, monsterData != null ? monsterData.patrolRange : 3f);

        // 추격 범위 (빨간색)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, monsterData != null ? monsterData.chaseRange : 5f);

        // 현재 상태 표시
        if (Application.isPlaying)
        {
            Gizmos.color = GetStateColor();
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);

            // 정찰 목표점 표시
            if (currentState == MonsterState.Patrol)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(patrolTarget, 0.3f);
                Gizmos.DrawLine(transform.position, patrolTarget);
            }
        }
    }

    Color GetStateColor()
    {
        switch (currentState)
        {
            case MonsterState.Idle: return Color.white;
            case MonsterState.Patrol: return Color.blue;
            case MonsterState.Chase: return Color.red;
            case MonsterState.Return: return Color.green;
            default: return Color.gray;
        }
    }
}