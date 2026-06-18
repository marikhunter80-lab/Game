using UnityEngine;
using UnityEngine.Events;

public class OptimizedPlayerHealth : MonoBehaviour
{
    [Header("Настройки здоровья")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float invincibilityTime = 0.5f;
    
    [Header("События")]
    public UnityEvent<int, int> OnHealthChanged; // текущее, максимальное
    public UnityEvent OnDamage;
    public UnityEvent OnDeath;
    public UnityEvent OnHeal;
    
    // Кэшированные значения
    private int currentHealth;
    private float invincibilityTimer;
    private bool isInvincible;
    private bool isDead;
    
    // Свойства
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public float HealthPercentage => (float)currentHealth / maxHealth;
    
    void Awake()
    {
        currentHealth = maxHealth;
    }
    
    public void TakeDamage(int damage)
    {
        if (isDead || isInvincible) return;
        
        currentHealth = Mathf.Max(0, currentHealth - damage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        if (damage > 0)
        {
            OnDamage?.Invoke();
            SetInvincible(invincibilityTime);
        }
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public void Heal(int amount)
    {
        if (isDead) return;
        
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        if (amount > 0)
        {
            OnHeal?.Invoke();
        }
    }
    
    public void SetInvincible(float duration)
    {
        if (!isInvincible)
        {
            StartCoroutine(InvincibilityRoutine(duration));
        }
    }
    
    private System.Collections.IEnumerator InvincibilityRoutine(float duration)
    {
        isInvincible = true;
        invincibilityTimer = duration;
        
        while (invincibilityTimer > 0)
        {
            invincibilityTimer -= Time.deltaTime;
            yield return null;
        }
        
        isInvincible = false;
    }
    
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        currentHealth = 0;
        OnDeath?.Invoke();
        
        // Здесь можно добавить логику смерти
    }
    
    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
        isInvincible = false;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}