using UnityEngine;
using System.Collections;

public class WeepingAngel : MonoBehaviour
{
    [Header("Настройки движения")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float activationRadius = 15f;
    [SerializeField] private float killDistance = 1.5f;
    
    [Header("Настройки зрения")]
    [SerializeField] private float fieldOfViewAngle = 60f;
    
    [Header("Фонарик")]
    [SerializeField] private float maxFlickerDistance = 10f;
    [SerializeField] private float minFlickerDistance = 2f;
    
    private Transform player;
    private Light playerFlashlight;
    private bool isPlayerDead = false;
    private float originalIntensity;
    private bool isChasing = false;
    
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        // Ищем фонарик
        if (player != null)
        {
            playerFlashlight = player.GetComponentInChildren<Light>();
            if (playerFlashlight != null)
            {
                originalIntensity = playerFlashlight.intensity;
                Debug.Log("✅ Ангел: фонарик найден!");
            }
            else
            {
                Debug.LogWarning("⚠ Ангел: фонарик не найден!");
            }
        }
        
        if (player == null)
            Debug.LogError("❌ ИГРОК НЕ НАЙДЕН!");
        else
            Debug.Log("✅ Ангел: игрок найден: " + player.name);
    }
    
    void Update()
    {
        if (player == null || isPlayerDead) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        
        if (distanceToPlayer <= activationRadius)
        {
            bool playerLooking = IsPlayerLookingAtMe();
            
            if (!playerLooking)
            {
                isChasing = true;
                MoveTowardsPlayer();
            }
            else
            {
                isChasing = false;
            }
            
            if (distanceToPlayer <= killDistance && !playerLooking)
            {
                StartCoroutine(KillPlayer());
            }
        }
        else
        {
            isChasing = false;
        }
        
        // Мерцание фонарика
        UpdateFlashlight(distanceToPlayer);
    }
    
    bool IsPlayerLookingAtMe()
    {
        Vector3 directionToAngel = (transform.position - player.position).normalized;
        float angle = Vector3.Angle(player.forward, directionToAngel);
        
        if (angle < fieldOfViewAngle * 0.5f)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.position, directionToAngel, out hit, activationRadius))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    void MoveTowardsPlayer()
    {
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
        
        transform.position += transform.forward * moveSpeed * Time.deltaTime;
    }
    
    IEnumerator KillPlayer()
    {
        if (isPlayerDead) yield break;
        
        isPlayerDead = true;
        
        yield return new WaitForSeconds(0.3f);
        
        if (SimpleDeath.Instance != null)
        {
            SimpleDeath.Instance.Die();
        }
        
        Debug.Log("💀 Игрок убит Ангелом!");
    }
    
    void UpdateFlashlight(float distance)
    {
        if (playerFlashlight == null) return;
        
        // Если ангел активен и игрок в радиусе мерцания
        if (isChasing && distance <= maxFlickerDistance)
        {
            // Чем ближе - тем быстрее и сильнее мерцание
            float t = Mathf.InverseLerp(maxFlickerDistance, minFlickerDistance, distance);
            
            // Частота мерцания
            float flickerSpeed = Mathf.Lerp(2f, 20f, t);
            
            // Синусоида для плавного мерцания
            float flicker = Mathf.Abs(Mathf.Sin(Time.time * flickerSpeed));
            
            // Минимальная яркость (чем ближе - тем темнее)
            float minIntensity = Mathf.Lerp(0.8f, 0.1f, t);
            
            // Применяем мерцание
            playerFlashlight.intensity = Mathf.Lerp(minIntensity, originalIntensity, flicker);
            
            // Случайные резкие скачки для страха
            if (Random.value < 0.05f)
            {
                playerFlashlight.intensity = Random.Range(0.1f, originalIntensity);
            }
        }
        else if (!isChasing && distance > maxFlickerDistance)
        {
            // Плавно возвращаем нормальный свет
            playerFlashlight.intensity = Mathf.Lerp(playerFlashlight.intensity, originalIntensity, Time.deltaTime * 3f);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Радиус активации
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, activationRadius);
        
        // Радиус мерцания фонарика
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxFlickerDistance);
        
        // Радиус убийства
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, killDistance);
    }
}