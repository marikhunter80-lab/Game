using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    [Header("Настройки мыши")]
    [SerializeField] private float mouseSensitivity = 100f;
    [SerializeField] private bool invertY = false;
    
    [Header("Ограничения")]
    [SerializeField] private float minYAngle = -90f;
    [SerializeField] private float maxYAngle = 90f;
    
    [Header("Сглаживание")]
    [SerializeField] private bool smoothMovement = true;
    [SerializeField] private float smoothTime = 0.05f;
    
    private float xRotation = 0f;
    private Vector2 currentMouseDelta = Vector2.zero;
    private Vector2 currentMouseDeltaVelocity = Vector2.zero;
    
    void Start()
    {
        // Блокируем курсор в центре экрана
        Cursor.lockState = CursorLockMode.Locked;
        
        // Скрываем курсор (опционально)
        Cursor.visible = false;
    }
    
    void Update()
    {
        HandleMouseLook();
        
        // Нажмите Escape, чтобы разблокировать курсор (опционально)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        // Нажмите на левую кнопку мыши, чтобы снова заблокировать
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    private void HandleMouseLook()
    {
        // Получаем движение мыши
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        
        // Инвертируем Y-ось при необходимости
        if (invertY)
            mouseY = -mouseY;
        
        if (smoothMovement)
        {
            // Сглаженное движение
            currentMouseDelta = Vector2.SmoothDamp(
                currentMouseDelta, 
                new Vector2(mouseX, mouseY),
                ref currentMouseDeltaVelocity, 
                smoothTime
            );
            
            mouseX = currentMouseDelta.x;
            mouseY = currentMouseDelta.y;
        }
        
        // Вращение по вертикали (камера)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minYAngle, maxYAngle);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        
        // Вращение по горизонтали (игрок)
        transform.parent.Rotate(Vector3.up * mouseX);
    }
}