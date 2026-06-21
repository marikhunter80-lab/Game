using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    float currentHealth;
    public GameObject gameOverPanel;

    [Header("UI")]
    public Slider healthSlider;
    public CanvasGroup damagePlayerVigniete;
    bool isDead = false;

    private Coroutine flashCoroutine;

    void Start()
    {
        ResetHealth();
        if (damagePlayerVigniete != null)
        {
            damagePlayerVigniete.alpha = 0f;
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = maxHealth;
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(currentHealth - amount, 0f);

        if (healthSlider != null)
            healthSlider.value = currentHealth;

        if (damagePlayerVigniete != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashDamageEffect());
        }

        if (currentHealth <= 0f)
            Die();
    }

    IEnumerator FlashDamageEffect()
    {
        damagePlayerVigniete.alpha = 1f;
        while (damagePlayerVigniete.alpha > 0f)
        {
            damagePlayerVigniete.alpha -= Time.deltaTime * 2.5f; // Viteza de fade-out (cu cât e mai mare numărul, cu atât dispare mai repede)
            yield return null;
        }
        damagePlayerVigniete.alpha = 0f;
    }

    void Die()
    {
        isDead = true;
        gameOverPanel.SetActive(true);
    }

    public bool IsDead() => isDead;
}