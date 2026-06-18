using UnityEngine;
using UnityEngine.UI;

// Player health with a UI Slider readout. Slider is forced to max 100 and
// reset to full on Start. Reaching 0 health triggers a (debug) game over.
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("UI")]
    [Tooltip("Health bar slider. Max value is set to maxHealth and reset to full on Start.")]
    public Slider healthSlider;

    [Header("State")]
    public bool isDead = false;

    void Start()
    {
        ResetHealth();
    }

    // Full heal + slider reset. Call at level start / respawn.
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth; // max value 100
            healthSlider.value = maxHealth;    // value 100 at start
        }
    }

    // Called by the enemy when an attack lands. e.g. amount = 30.
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(currentHealth - amount, 0f);

        if (healthSlider != null)
            healthSlider.value = currentHealth;

        Debug.Log($"[PlayerHealth] -{amount} damage  ->  {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
            Die();
    }

    void Die()
    {
        isDead = true;
        Debug.Log("GAME OVER");
        // TODO next stage: show Game Over UI / restart scene here.
    }

    public bool IsDead()
    {
        return isDead;
    }
}
