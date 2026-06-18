using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float mouseSensitivity = 2f;
    
    [Header("Настройки камеры")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float minLookAngle = -90f;
    [SerializeField] private float maxLookAngle = 90f;
    
    private CharacterController controller;
    private float verticalRotation = 0f;
    private bool isDead = false;
    
    void Start()
    {
        // Вместо Rigidbody используем CharacterController
        controller = GetComponent<CharacterController>();
        
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();
        
        // Настройка CharacterController
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.center = new Vector3(0, 1f, 0);
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    void Update()
    {
        if (isDead) return;
        
        // Поворот камеры
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, minLookAngle, maxLookAngle);
        cameraHolder.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        
        transform.Rotate(Vector3.up * mouseX);
        
        // Движение
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
        moveDirection.Normalize();
        
        // Гравитация
        if (!controller.isGrounded)
            moveDirection.y -= 9.81f * Time.deltaTime;
        
        controller.Move(moveDirection * walkSpeed * Time.deltaTime);
    }
    
    public void Die()
    {
        isDead = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}