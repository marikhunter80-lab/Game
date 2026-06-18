using UnityEngine;

public class FlashlightController : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    [SerializeField] private Light flashlightLight;  // Источник света
    [SerializeField] private GameObject flashlightModel;  // Модель фонарика
    
    [Header("Настройки")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F;  // Клавиша включения
    [SerializeField] private bool startOn = false;  // Включен ли при старте
    [SerializeField] private AudioClip toggleSound;  // Звук переключения
    
    [Header("Эффекты (опционально)")]
    [SerializeField] private float batteryDrainSpeed = 0f;  // Расход батареи
    [SerializeField] private float minIntensity = 0.5f;  // Минимальная яркость
    [SerializeField] private float maxIntensity = 3f;  // Максимальная яркость
    
    private AudioSource audioSource;
    private bool isOn;
    private float currentIntensity;
    
    void Start()
    {
        // Получаем или добавляем AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && toggleSound != null)
            audioSource = gameObject.AddComponent<AudioSource>();
        
        // Настройка начального состояния
        currentIntensity = maxIntensity;
        isOn = startOn;
        
        // Применяем начальное состояние
        UpdateLightState();
        
        // Если фонарик не указан, ищем на дочерних объектах
        if (flashlightLight == null)
            flashlightLight = GetComponentInChildren<Light>();
        
        if (flashlightModel == null)
            flashlightModel = gameObject;
        
        // Убеждаемся, что свет привязан к модели фонарика
        if (flashlightLight != null)
            flashlightLight.transform.parent = flashlightModel.transform;
    }
    
    void Update()
    {
        // Переключение по клавише F
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleFlashlight();
        }
        
        // Расход батареи (если включен)
        if (isOn && batteryDrainSpeed > 0)
        {
            currentIntensity -= batteryDrainSpeed * Time.deltaTime;
            currentIntensity = Mathf.Clamp(currentIntensity, minIntensity, maxIntensity);
            flashlightLight.intensity = currentIntensity;
            
            // Автовыключение при разряде
            if (currentIntensity <= minIntensity && isOn)
            {
                ToggleFlashlight();
            }
        }
    }
    
    // Публичный метод для переключения
    public void ToggleFlashlight()
    {
        isOn = !isOn;
        UpdateLightState();
        
        // Воспроизводим звук переключения
        if (toggleSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(toggleSound);
        }
    }
    
    // Публичный метод для включения
    public void TurnOn()
    {
        if (!isOn)
            ToggleFlashlight();
    }
    
    // Публичный метод для выключения
    public void TurnOff()
    {
        if (isOn)
            ToggleFlashlight();
    }
    
    // Обновление состояния света
    private void UpdateLightState()
    {
        if (flashlightLight != null)
            flashlightLight.enabled = isOn;
        
        // Бонус: можно добавить материал, который светится
        // Renderer renderer = flashlightModel.GetComponent<Renderer>();
        // if (renderer != null)
        //     renderer.material.EnableKeyword("_EMISSION");
    }
    
    // Проверка статуса
    public bool IsOn()
    {
        return isOn;
    }
}