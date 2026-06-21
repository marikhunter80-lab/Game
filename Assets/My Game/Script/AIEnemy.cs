using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AIEnemy : MonoBehaviour
{
    public enum State { Patrol, Chase, Search }

    [Header("Target")]
    public Transform player;
    public string playerTag = "Player";

    [Header("References")]
    public AIAnimator aiAnimator;
    public PlayerHealth playerHealth;

    [Header("Detection")]
    public float detectionRadius = 22f;
    public float fieldOfView = 140f;
    public float loseSightRadius = 32f;
    public LayerMask sightBlockMask = ~0;
    public float eyeHeight = 1.6f;

    [Header("Speeds")]
    public float patrolSpeed = 2.2f;
    public float chaseSpeed = 5f;
    [Tooltip("How fast the agent reaches its target speed. Low values feel sluggish.")]
    public float acceleration = 14f;
    [Tooltip("Turn speed in deg/sec. Low values make the zombie feel slow when it changes direction.")]
    public float angularSpeed = 600f;

    [Header("Aggression")]
    [Range(0f, 1f)] public float aggressiveChance = 0.35f;
    public float aggressiveSpeedMultiplier = 1.6f;
    [Tooltip("Play the run animation for any chase (not only aggressive ones) to avoid foot sliding.")]
    public bool runAnimWhenChasing = true;

    [Header("Attack")]
    public float attackRange = 2.2f;
    public float attackCooldown = 1.5f;
    public float attackStopDuration = 0.6f;
    [Tooltip("Damage applied to the player when an attack lands.")]
    public float attackDamage = 30f;
    [Tooltip("Delay between starting the attack animation and the hit landing (sync with the swing).")]
    public float attackHitDelay = 0.3f;

    [Header("Patrol")]
    public int patrolPointCount = 5;
    public float patrolAreaRadius = 12f;
    public float pointReachDistance = 1.2f;
    public float waitAtPointMin = 1f;
    public float waitAtPointMax = 3f;

    [Header("Search")]
    public float searchDuration = 6f;
    public float searchRadius = 5f;

    [Header("Robustness")]
    public float stuckCheckInterval = 1f;
    public float stuckDistanceThreshold = 0.2f;
    [Tooltip("Minimum time between path recalculations while chasing.")]
    public float repathInterval = 0.35f;
    [Tooltip("Only recalculate the chase path once the player has moved at least this far from the current destination.")]
    public float repathMoveThreshold = 0.75f;

    private NavMeshAgent agent;
    private State state = State.Patrol;

    private Vector3[] patrolPoints;
    private int currentPatrolIndex = 0;
    private float waitTimer = 0f;
    private bool waiting = false;

    private Vector3 lastKnownPlayerPos;
    private float searchTimer = 0f;
    private float repathTimer = 0f;
    private Vector3 currentDestination;

    private Vector3 lastPosition;
    private float stuckTimer = 0f;

    private bool isAggressive = false;
    private float attackTimer = 0f;
    private float attackStopTimer = 0f;

    private bool pendingHit = false;
    private float hitTimer = 0f;

    private Vector3 homePosition;
    // Reused buffer so the line-of-sight check allocates nothing each frame.
    private static readonly RaycastHit[] sightHits = new RaycastHit[8];

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (aiAnimator == null) aiAnimator = GetComponent<AIAnimator>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag(playerTag);
            if (p != null) player = p.transform;
        }

        if (playerHealth == null && player != null)
            playerHealth = player.GetComponentInParent<PlayerHealth>();
    }

    void Start()
    {
        homePosition = transform.position;
        ConfigureAgent();
        GeneratePatrolPoints();
        EnterPatrol();
        lastPosition = transform.position;
    }

    void ConfigureAgent()
    {
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.autoBraking = false; // keeps movement smooth between waypoints
        agent.updateRotation = true;
    }

    void Update()
    {
        if (attackTimer > 0f) attackTimer -= Time.deltaTime;
        ProcessPendingHit();

        // Once the player is dead, never linger on the corpse — peel off and head
        // back to a normal patrol around the spawn area.
        if (PlayerIsDead() && state != State.Patrol)
        {
            ReturnToPatrol();
        }

        switch (state)
        {
            case State.Patrol: TickPatrol(); break;
            case State.Chase:  TickChase();  break;
            case State.Search: TickSearch(); break;
        }

        HandleStuckCheck();
        UpdateAnimator();
    }

    void GeneratePatrolPoints()
    {
        patrolPoints = new Vector3[Mathf.Max(1, patrolPointCount)];
        Vector3 origin = transform.position;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (TryGetRandomNavPoint(origin, patrolAreaRadius, out Vector3 point))
                patrolPoints[i] = point;
            else
                patrolPoints[i] = origin;
        }
    }

    bool TryGetRandomNavPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 rand = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(rand.x, 0f, rand.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    void EnterPatrol()
    {
        state = State.Patrol;
        isAggressive = false;
        agent.speed = patrolSpeed;
        agent.isStopped = false;
        waiting = false;
        GoToNextPatrolPoint();
    }

    void TickPatrol()
    {
        if (CanSeePlayer())
        {
            EnterChase();
            return;
        }

        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                GoToNextPatrolPoint();
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= pointReachDistance)
        {
            waiting = true;
            waitTimer = Random.Range(waitAtPointMin, waitAtPointMax);
        }
    }

    void GoToNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        SetDestinationSafe(patrolPoints[currentPatrolIndex]);
    }

    void EnterChase()
    {
        state = State.Chase;
        agent.isStopped = false;

        isAggressive = Random.value < aggressiveChance;
        agent.speed = isAggressive ? chaseSpeed * aggressiveSpeedMultiplier : chaseSpeed;
    }

    void TickChase()
    {
        if (player == null)
        {
            EnterSearch();
            return;
        }

        if (attackStopTimer > 0f)
        {
            attackStopTimer -= Time.deltaTime;
            agent.isStopped = true;
            FaceTarget(player.position);
            if (!CanSeePlayer()) EnterSearch();
            return;
        }

        agent.isStopped = false;

        if (CanSeePlayer())
        {
            lastKnownPlayerPos = player.position;
            float dist = Vector3.Distance(transform.position, player.position);

            if (dist <= attackRange && attackTimer <= 0f)
            {
                aiAnimator?.TriggerAttack();
                attackTimer = attackCooldown;
                attackStopTimer = attackStopDuration;
                agent.isStopped = true;

                // Schedule the damage to land mid-swing instead of instantly.
                pendingHit = true;
                hitTimer = attackHitDelay;
                return;
            }

            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f)
            {
                // Only repath when the player has actually moved away from where
                // we're already headed — avoids constant path recalculation jitter.
                if (Vector3.Distance(lastKnownPlayerPos, currentDestination) > repathMoveThreshold)
                    SetDestinationSafe(lastKnownPlayerPos);
                repathTimer = repathInterval;
            }
        }
        else
        {
            EnterSearch();
        }
    }

    void EnterSearch()
    {
        state = State.Search;
        isAggressive = false;
        agent.isStopped = false;
        agent.speed = patrolSpeed;
        searchTimer = searchDuration;
        PickSearchPoint();
    }

    void TickSearch()
    {
        if (CanSeePlayer())
        {
            EnterChase();
            return;
        }

        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            RebuildPatrolAround(lastKnownPlayerPos);
            EnterPatrol();
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= pointReachDistance)
            PickSearchPoint();
    }

    void PickSearchPoint()
    {
        if (TryGetRandomNavPoint(lastKnownPlayerPos, searchRadius, out Vector3 point))
            SetDestinationSafe(point);
    }

    void RebuildPatrolAround(Vector3 center)
    {
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (TryGetRandomNavPoint(center, patrolAreaRadius, out Vector3 point))
                patrolPoints[i] = point;
        }
        currentPatrolIndex = 0;
    }

    bool CanSeePlayer()
    {
        if (player == null) return false;
        if (PlayerIsDead()) return false; // don't re-aggro on a corpse

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Vector3 playerPos = player.position + Vector3.up * (eyeHeight * 0.6f);
        Vector3 toPlayer = playerPos - eyePos;
        float dist = toPlayer.magnitude;

        float activeRadius = (state == State.Chase || state == State.Search) ? loseSightRadius : detectionRadius;
        if (dist > activeRadius) return false;

        if (state == State.Patrol)
        {
            float angle = Vector3.Angle(transform.forward, toPlayer);
            if (angle > fieldOfView * 0.5f) return false;
        }

        return HasClearShot(eyePos, toPlayer.normalized, dist);
    }

    // Returns true if nothing except the player (or our own body) sits on the line.
    // Crucially ignores THIS enemy's colliders — the eye origin is inside the
    // zombie's own capsule, so a naive raycast hits itself first and reports the
    // player as "blocked", which is what made the detection range feel tiny.
    bool HasClearShot(Vector3 from, Vector3 dir, float dist)
    {
        int count = Physics.RaycastNonAlloc(from, dir, sightHits, dist, sightBlockMask, QueryTriggerInteraction.Ignore);

        float closestDist = float.MaxValue;
        Transform closest = null;
        for (int i = 0; i < count; i++)
        {
            Transform t = sightHits[i].transform;
            if (t == null) continue;
            if (t.IsChildOf(transform)) continue; // skip our own colliders

            if (sightHits[i].distance < closestDist)
            {
                closestDist = sightHits[i].distance;
                closest = t;
            }
        }

        if (closest == null) return true; // clear line of sight
        return closest.CompareTag(playerTag) || (player != null && closest.IsChildOf(player.root));
    }

    bool PlayerIsDead()
    {
        return playerHealth != null && playerHealth.IsDead();
    }

    // Rebuild the patrol route around the spawn point and resume patrolling there,
    // so the enemy walks off rather than standing on the player's last position.
    void ReturnToPatrol()
    {
        RebuildPatrolAround(homePosition);
        EnterPatrol();
    }

    void ProcessPendingHit()
    {
        if (!pendingHit) return;

        hitTimer -= Time.deltaTime;
        if (hitTimer > 0f) return;

        pendingHit = false;

        if (player == null || playerHealth == null || playerHealth.IsDead()) return;

        // Only connect if the player is still within (a slightly forgiving) range.
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackRange * 1.25f)
            playerHealth.TakeDamage(attackDamage);
    }

    void FaceTarget(Vector3 target)
    {
        Vector3 dir = (target - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);
    }

    void SetDestinationSafe(Vector3 target)
    {
        if (!agent.isOnNavMesh) return;

        Vector3 dest = target;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            dest = hit.position;

        currentDestination = dest;
        agent.SetDestination(dest);
    }

    void UpdateAnimator()
    {
        if (aiAnimator == null) return;

        // Use desiredVelocity as a fallback so the walk anim *triggers* even on the
        // frames where actual velocity briefly dips (e.g. right after repathing).
        float paramSpeed = 0f;
        if (!agent.isStopped)
            paramSpeed = Mathf.Max(agent.velocity.magnitude, agent.desiredVelocity.magnitude);

        aiAnimator.SetSpeed(paramSpeed);

        bool running = state == State.Chase && (isAggressive || runAnimWhenChasing);
        aiAnimator.SetRunning(running);

        // Foot-sync: scale playback to the REAL ground speed (not desiredVelocity)
        // so the legs stop "running in place" when the body is moving slowly.
        float worldSpeed = agent.isStopped ? 0f : agent.velocity.magnitude;
        aiAnimator.SetMoveSpeed(worldSpeed);
    }

    void HandleStuckCheck()
    {
        stuckTimer += Time.deltaTime;
        if (stuckTimer < stuckCheckInterval) return;

        float moved = Vector3.Distance(transform.position, lastPosition);
        bool shouldBeMoving = !waiting && !agent.isStopped && agent.hasPath && agent.remainingDistance > pointReachDistance;

        if (shouldBeMoving && moved < stuckDistanceThreshold)
        {
            if (state == State.Patrol) GoToNextPatrolPoint();
            else if (state == State.Search) PickSearchPoint();
            else if (state == State.Chase && player != null) SetDestinationSafe(player.position);
        }

        lastPosition = transform.position;
        stuckTimer = 0f;
    }

    public State GetState()
    {
        return state;
    }

    public bool IsAggressive()
    {
        return isAggressive;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, loseSightRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, patrolAreaRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (patrolPoints != null)
        {
            Gizmos.color = Color.green;
            foreach (var p in patrolPoints)
                Gizmos.DrawSphere(p, 0.3f);
        }
    }
}