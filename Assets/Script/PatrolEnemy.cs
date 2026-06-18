using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Улучшенная система ИИ врага с State Machine
/// Состояния: Idle → Patrol → Chase → Attack
/// </summary>
public class PatrolEnemy : MonoBehaviour
{
    // ============ ENUM СОСТОЯНИЙ ============
    private enum EnemyState
    {
        Idle,           // Стоит на месте
        Patrol,         // Патрулирует карту
        Chase,          // Преследует игрока
        Attack,         // Атакует игрока
        Stuck,          // Застрял - ищет новый путь
    }

    // ============ ПАРАМЕТРЫ ПАТРУЛЯ ============
    [Header("Патруль")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float patrolRadius = 20f;
    [SerializeField] private float lookPause = 0.5f;
    [SerializeField] private float stuckTimeout = 3f;            // Если враг стоит на месте > 3 сек, он "застрял"

    // ============ ПАРАМЕТРЫ ОБНАРУЖЕНИЯ ============
    [Header("Обнаружение")]
    [SerializeField] private float detectRadius = 12f;
    [SerializeField] private float fieldOfViewAngle = 120f;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private bool useLineOfSight = true;
    [SerializeField] private float loseTargetTime = 2f;          // Теряем цель через N сек после потери видимости

    // ============ ПАРАМЕТРЫ ПРЕСЛЕДОВАНИЯ ============
    [Header("Преследование")]
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float attackDistance = 2f;
    [SerializeField] private float maxPathDistance = 50f;        // Макс расстояние для поиска пути
    [SerializeField] private int damage = 10;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float rotationSpeed = 5f;

    // ============ ПАРАМЕТРЫ АНИМАТОРА ============
    [Header("Аниматор")]
    [SerializeField] private Animator animator;

    // ============ ПАРАМЕТРЫ ФОНАРИКА ============
    [Header("Фонарик")]
    [SerializeField] private float maxFlickerDistance = 10f;
    [SerializeField] private float minFlickerDistance = 2f;

    // ============ ВНУТРЕННИЕ ПЕРЕМЕННЫЕ ============
    private Transform player;
    private Light playerFlashlight;
    private NavMeshAgent agent;
    private EnemyState currentState = EnemyState.Idle;
    private EnemyState nextState = EnemyState.Idle;
    
    private bool canAttack = true;
    private float originalIntensity;
    private Vector3 currentPatrolTarget;
    
    private float lastPlayerSeenTime = 0f;
    private float stuckTimer = 0f;
    private Vector3 lastPosition;
    private float movementThreshold = 0.1f;                      // Если враг прошел < этого, считаем, что застрял
    private bool isPaused = false;

    void Start()
    {
        InitializeEnemy();
    }

    void InitializeEnemy()
    {
        // Поиск игрока
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (!player)
        {
            Debug.LogError("❌ PatrolEnemy: Player не найден! Добавьте тег 'Player' игроку!");
            enabled = false;
            return;
        }

        // Инициализация NavMeshAgent
        agent = GetComponent<NavMeshAgent>();
        if (!agent) agent = gameObject.AddComponent<NavMeshAgent>();

        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0.2f;
        agent.autoBraking = true;
        agent.updateRotation = false;                            // Сами контролируем ротацию

        // Поиск аниматора
        if (!animator) animator = GetComponent<Animator>();

        // Поиск фонарика игрока
        playerFlashlight = player.GetComponentInChildren<Light>();
        if (playerFlashlight) originalIntensity = playerFlashlight.intensity;

        lastPosition = transform.position;
        TransitionToState(EnemyState.Patrol);
    }

    void Update()
    {
        if (!player || !agent) return;

        // Обновляем логику текущего состояния
        UpdateState();

        // Общие обновления (анимация, фонарик)
        UpdateAnimation();
        UpdateFlashlight();

        // Отладка
        // Debug.Log($"State: {currentState} | Dist: {Vector3.Distance(transform.position, player.position):F2}");
    }

    // ============ УПРАВЛЕНИЕ СОСТОЯНИЯМИ ============
    void UpdateState()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdleState();
                break;
            case EnemyState.Patrol:
                UpdatePatrolState();
                break;
            case EnemyState.Chase:
                UpdateChaseState();
                break;
            case EnemyState.Attack:
                UpdateAttackState();
                break;
            case EnemyState.Stuck:
                UpdateStuckState();
                break;
        }

        // Проверяем переход в новое состояние
        if (nextState != currentState)
        {
            TransitionToState(nextState);
        }
    }

    void TransitionToState(EnemyState newState)
    {
        if (newState == currentState) return;

        // Выход из старого состояния
        switch (currentState)
        {
            case EnemyState.Attack:
                agent.isStopped = false;
                break;
        }

        // Вход в новое состояние
        currentState = newState;
        Debug.Log($"👾 Enemy State: {currentState}");

        switch (currentState)
        {
            case EnemyState.Patrol:
                agent.speed = patrolSpeed;
                SetRandomDestination();
                break;
            case EnemyState.Chase:
                agent.speed = chaseSpeed;
                agent.stoppingDistance = attackDistance * 0.8f;
                break;
            case EnemyState.Attack:
                agent.isStopped = true;
                break;
            case EnemyState.Stuck:
                agent.isStopped = true;
                stuckTimer = 0f;
                break;
        }
    }

    // ============ IDLE STATE ============
    void UpdateIdleState()
    {
        // Переходим в Patrol
        nextState = EnemyState.Patrol;
    }

    // ============ PATROL STATE ============
    void UpdatePatrolState()
    {
        // Проверяем обнаружение игрока
        if (CanSeePlayer())
        {
            lastPlayerSeenTime = Time.time;
            nextState = EnemyState.Chase;
            Debug.Log("👀 Враг заметил игрока!");
            return;
        }

        // Логика патруля
        if (!agent.hasPath || agent.pathPending)
            return;

        if (agent.remainingDistance <= agent.stoppingDistance && !isPaused)
        {
            if (lookPause > 0)
            {
                StartCoroutine(PauseThenMovePatrol());
            }
            else
            {
                SetRandomDestination();
            }
        }

        // Проверяем застревание
        CheckIfStuck();
    }

    // ============ CHASE STATE ============
    void UpdateChaseState()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        bool canSeePlayer = CanSeePlayer();

        // Если видим игрока - преследуем
        if (canSeePlayer)
        {
            lastPlayerSeenTime = Time.time;

            // Если достаточно близко - атакуем
            if (distToPlayer <= attackDistance)
            {
                nextState = EnemyState.Attack;
                return;
            }

            // Иначе - преследуем
            SetChaseTarget(player.position);
        }
        else
        {
            // Проверяем гистерезис - как долго не видим игрока
            float timeSinceLastSeen = Time.time - lastPlayerSeenTime;
            
            if (timeSinceLastSeen > loseTargetTime)
            {
                nextState = EnemyState.Patrol;
                Debug.Log("🚶 Враг потерял игрока");
                return;
            }

            // Продолжаем идти в последнюю известную позицию
            SetChaseTarget(player.position);
        }

        // Проверяем застревание
        CheckIfStuck();
    }

    // ============ ATTACK STATE ============
    void UpdateAttackState()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // Если игрок убежал - вернуться к преследованию
        if (distToPlayer > attackDistance * 1.5f)
        {
            nextState = EnemyState.Chase;
            return;
        }

        // Поворачиваемся к игроку
        if (player)
        {
            Vector3 dirToPlayer = (player.position - transform.position);
            dirToPlayer.y = 0;
            
            if (dirToPlayer != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(dirToPlayer);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            }
        }

        // Атакуем
        if (canAttack)
        {
            StartCoroutine(PerformAttack());
        }
    }

    // ============ STUCK STATE ============
    void UpdateStuckState()
    {
        stuckTimer += Time.deltaTime;

        if (stuckTimer > 2f)
        {
            // Пытаемся выйти из застревания
            SetRandomDestination();
            nextState = EnemyState.Patrol;
            Debug.Log("🔄 Enemy trying to unstuck...");
        }
    }

    // ============ ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ============
    void CheckIfStuck()
    {
        float distMoved = Vector3.Distance(transform.position, lastPosition);

        if (distMoved < movementThreshold)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > stuckTimeout)
            {
                nextState = EnemyState.Stuck;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPosition = transform.position;
    }

    bool CanSeePlayer()
    {
        if (!player) return false;

        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // Проверка максимального расстояния
        if (distToPlayer > detectRadius)
            return false;

        // Проверка угла обзора (конус зрения)
        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        if (angle > fieldOfViewAngle * 0.5f)
            return false;

        // Проверка линии видимости
        if (useLineOfSight)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up, dirToPlayer, out hit, distToPlayer, obstacleMask))
            {
                return false;
            }
        }

        return true;
    }

    void SetChaseTarget(Vector3 targetPos)
    {
        float distToTarget = Vector3.Distance(transform.position, targetPos);

        // Защита от установки слишком дальних целей
        if (distToTarget > maxPathDistance)
        {
            Debug.LogWarning($"⚠ Target too far: {distToTarget}m (max: {maxPathDistance}m)");
            return;
        }

        // Проверяем, что путь на NavMesh валиден
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(targetPos, out hit, 5f, NavMesh.AllAreas))
        {
            // Цель не на NavMesh - ищем ближайшую валидную позицию
            return;
        }

        agent.SetDestination(targetPos);
    }

    void SetRandomDestination()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
            randomDir += transform.position;
            randomDir.y = transform.position.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDir, out hit, patrolRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                currentPatrolTarget = hit.position;
                return;
            }
        }

        Debug.LogWarning("⚠ Cannot find random patrol destination on NavMesh");
    }

    IEnumerator PauseThenMovePatrol()
    {
        if (isPaused) yield break;

        isPaused = true;
        agent.isStopped = true;
        yield return new WaitForSeconds(lookPause);
        agent.isStopped = false;
        SetRandomDestination();
        isPaused = false;
    }

    IEnumerator PerformAttack()
    {
        canAttack = false;

        // Триггер атаки в аниматоре
        if (animator)
            animator.SetTrigger("Attack");

        yield return new WaitForSeconds(0.5f);

        // Наносим урон
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        if (player && distToPlayer <= attackDistance * 1.2f)
        {
            var health = player.GetComponent<PlayerHealth>();
            if (health)
            {
                health.TakeDamage(damage);
                Debug.Log($"⚔️ Enemy attacked! Damage: {damage}");
            }
        }

        if (animator)
            animator.ResetTrigger("Attack");

        yield return new WaitForSeconds(attackCooldown - 0.5f);
        canAttack = true;
    }

    void UpdateAnimation()
    {
        if (animator)
        {
            float speed = currentState == EnemyState.Attack ? 0 : agent.velocity.magnitude;
            animator.SetFloat("Speed", speed);
        }
    }

    void UpdateFlashlight()
    {
        if (!playerFlashlight) return;

        if (currentState == EnemyState.Chase || currentState == EnemyState.Attack)
        {
            float distToPlayer = Vector3.Distance(transform.position, player.position);
            if (distToPlayer <= maxFlickerDistance)
            {
                float t = Mathf.InverseLerp(maxFlickerDistance, minFlickerDistance, distToPlayer);
                float flick = Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(2f, 20f, t)));
                playerFlashlight.intensity = Mathf.Lerp(0.1f, originalIntensity, flick);
                return;
            }
        }

        playerFlashlight.intensity = Mathf.Lerp(playerFlashlight.intensity, originalIntensity, Time.deltaTime * 5f);
    }

    void OnDrawGizmosSelected()
    {
        // Радиус обнаружения
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        // Радиус атаки
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        // Конус зрения
        Vector3 forward = transform.forward;
        Vector3 left = Quaternion.Euler(0, -fieldOfViewAngle * 0.5f, 0) * forward;
        Vector3 right = Quaternion.Euler(0, fieldOfViewAngle * 0.5f, 0) * forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, left * detectRadius);
        Gizmos.DrawRay(transform.position, right * detectRadius);
    }
}