using UnityEngine;

// Drives the enemy Animator. Robust against the common setup where the real
// Animator (with the controller) lives on a child mesh while this script and
// the AIEnemy brain live on the root NavMeshAgent object.
public class AIAnimator : MonoBehaviour
{
    [Header("References")]
    public Animator animator;

    [Header("Settings")]
    [Tooltip("Smoothing time for the Speed parameter (seconds).")]
    public float speedDamping = 0.1f;

    [Header("Foot-Sync (fixes 'running in place' / foot sliding)")]
    [Tooltip("Scale animation playback to the agent's real speed so the feet match the ground.")]
    public bool matchAnimationToMovement = true;
    [Tooltip("World speed (m/s) at which the WALK clip looks natural at 1x playback. Raise this if the legs still look too fast.")]
    public float walkReferenceSpeed = 2.6f;
    [Tooltip("World speed (m/s) at which the RUN clip looks natural at 1x playback.")]
    public float runReferenceSpeed = 5.5f;
    [Tooltip("Lower clamp for the playback multiplier (keeps a tiny shuffle from freezing).")]
    public float minPlayback = 0.5f;
    [Tooltip("Upper clamp for the playback multiplier (stops crazy fast legs).")]
    public float maxPlayback = 1.4f;
    [Tooltip("How quickly playback eases toward its target (higher = snappier).")]
    public float playbackSmoothing = 10f;
    [Tooltip("Seconds to force normal (1x) playback after an attack so the swing isn't slowed.")]
    public float attackPlaybackLock = 1.0f;

    private bool running;
    private float currentPlayback = 1f;
    private float attackLockTimer;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    void Awake()
    {
        ResolveAnimator();

        if (animator == null)
        {
            Debug.LogWarning($"[AIAnimator] No Animator found on '{name}' or its children. Animations will not play.", this);
            return;
        }

        if (animator.runtimeAnimatorController == null)
            Debug.LogWarning($"[AIAnimator] Animator on '{animator.name}' has no Controller assigned (expected the 'Enemy' controller).", this);
    }

    // Prefer an Animator that actually has a controller, searching children if needed.
    void ResolveAnimator()
    {
        if (animator != null && animator.runtimeAnimatorController != null) return;

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Animator[] found = GetComponentsInChildren<Animator>(true);
            foreach (Animator a in found)
            {
                if (a.runtimeAnimatorController != null)
                {
                    animator = a;
                    return;
                }
            }
            // fall back to any animator (even controller-less) so we at least have a reference
            if (animator == null && found.Length > 0)
                animator = found[0];
        }
    }

    public void SetSpeed(float speed)
    {
        if (animator == null) return;
        animator.SetFloat(SpeedHash, speed, speedDamping, Time.deltaTime);
    }

    public void SetRunning(bool running)
    {
        this.running = running;
        if (animator == null) return;
        animator.SetBool(IsRunningHash, running);
    }

    // Pass the agent's ACTUAL world speed (agent.velocity.magnitude). Scales the
    // Animator's global playback so the clip cadence matches how fast the body is
    // really moving — this is what kills the "running in place" foot sliding.
    public void SetMoveSpeed(float worldSpeed)
    {
        if (animator == null || !matchAnimationToMovement) return;

        float blend = 1f - Mathf.Exp(-playbackSmoothing * Time.deltaTime);

        // While an attack is playing, hold normal speed so the swing isn't slowed.
        if (attackLockTimer > 0f)
        {
            attackLockTimer -= Time.deltaTime;
            currentPlayback = Mathf.Lerp(currentPlayback, 1f, blend);
            animator.speed = currentPlayback;
            return;
        }

        float target;
        if (worldSpeed < 0.05f)
        {
            target = 1f; // idle: let breathing/idle clip play at its authored speed
        }
        else
        {
            float reference = running ? runReferenceSpeed : walkReferenceSpeed;
            target = reference > 0.01f ? worldSpeed / reference : 1f;
            target = Mathf.Clamp(target, minPlayback, maxPlayback);
        }

        currentPlayback = Mathf.Lerp(currentPlayback, target, blend);
        animator.speed = currentPlayback;
    }

    public void TriggerAttack()
    {
        if (animator == null) return;
        animator.SetTrigger(AttackHash);

        // Snap to normal speed and hold it for the swing duration.
        attackLockTimer = attackPlaybackLock;
        currentPlayback = 1f;
        animator.speed = 1f;
    }
}
