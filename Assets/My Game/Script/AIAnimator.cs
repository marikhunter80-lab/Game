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
        if (animator == null) return;
        animator.SetBool(IsRunningHash, running);
    }

    public void TriggerAttack()
    {
        if (animator == null) return;
        animator.SetTrigger(AttackHash);
    }
}
