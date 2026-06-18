using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Здоровье")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float invincibilityDuration = 0.5f;    // Иммунитет после урона
    
    [Header("События")]
    public UnityEvent<int> OnHealthChanged;                         // Текущее здоровье
    public UnityEvent OnDamageReceived;                             // Когда получен урон
    public UnityEvent OnDeath;                                      // Когда игрок умер
    
    private int currentHealth;
    private bool isInvincible = false;
    private float invincibilityTimer = 0f;
    private bool isDead = false;
    
    void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth);
    }
    
    void Update()
    {
        // Обновляем инвинцибильность
        if (isInvincible)
        {
            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0)
            {
                isInvincible = false;
            }
        }
    }
    
    public void TakeDamage(int damage)
    {
        // Не берем урон если мертв или в инвинцибильности
        if (isDead || isInvincible) return;
        
        if (damage < 0) damage = 0;
        
        currentHealth -= damage;
        Debug.Log($"❤ Player Health: {currentHealth}/{maxHealth} (-{damage})");
        
        // Включаем инвинцибильность
        isInvincible = true;
        invincibilityTimer = invincibilityDuration;
        
        OnHealthChanged?.Invoke(currentHealth);
        OnDamageReceived?.Invoke();
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        Debug.Log($"💚 Player Healed: {currentHealth}/{maxHealth} (+{amount})");
        
        OnHealthChanged?.Invoke(currentHealth);
    }
    
    void Die()
    {
        if (isDead) return;
        
        isDead = true;
        currentHealth = 0;
        Debug.Log("💀 Player is DEAD!");
        
        OnHealthChanged?.Invoke(currentHealth);
        OnDeath?.Invoke();
        
        if (SimpleDeath.Instance != null)
        {
            SimpleDeath.Instance.Die();
        }
    }
    
    // ============ GETTERS ============
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float HealthPercentage => (float)currentHealth / maxHealth;
    public bool IsDead => isDead;
    public bool IsInvincible => isInvincible;
    
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        isInvincible = false;
        OnHealthChanged?.Invoke(currentHealth);
    }
}