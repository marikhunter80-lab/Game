using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSPlayer : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    [Header("Look")]
    public Transform cameraTransform;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 89f;

    [Header("Head Bob")]
    public float headIdleBobAmount = 0.015f;
    public float headIdleBobSpeed = 1.6f;
    public float walkBobAmount = 0.05f;
    public float walkBobSpeed = 9f;
    public float runBobMultiplier = 1.6f;
    public float bobSmoothing = 8f;
    public float strafeTilt = 1.5f;
    public float tiltSmoothing = 6f;

    [Header("Flashlight")]
    public Flashlight flashlight;
 
    public float swayKickAmount = 0.04f;
    public float maxSwayOffset = 0.08f;
    public float rotKickAmount = 6f;
    public float maxRotOffset = 10f;
    public float springStiffness = 90f;
    public float springDamping = 9f;
    public float followSpeed = 18f;
    public float flIdleBobAmount = 0.008f;
    public float flIdleBobSpeed = 1.6f;

    private CharacterController controller;
    private Vector3 velocity;
    private float verticalRotation = 0f;
    private bool isGrounded;
    private float currentSpeed;

    private Vector3 camDefaultPos;
    private float bobTimer = 0f;
    private float currentTilt = 0f;
    private Vector3 currentBobOffset;

    private Vector3 flPosOffset;
    private Vector3 flPosVelocity;
    private Vector3 flRotOffset;
    private Vector3 flRotVelocity;
    private float flBobTimer = 0f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentSpeed = walkSpeed;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            camDefaultPos = cameraTransform.localPosition;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleLook();
        HandleMovement();
        HandleJumpAndGravity();
        HandleHeadBob();
        HandleFlashlight();
    }

    void HandleLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
    }

    void HandleMovement()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 input = (transform.right * h + transform.forward * v).normalized;

        if (Input.GetKey(KeyCode.LeftShift) && v > 0f)
            currentSpeed = runSpeed;
        else
            currentSpeed = walkSpeed;

        controller.Move(input * currentSpeed * Time.deltaTime);

        float targetTilt = -h * strafeTilt;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltSmoothing * Time.deltaTime);
    }

    void HandleJumpAndGravity()
    {
        if (isGrounded && Input.GetButtonDown("Jump"))
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleHeadBob()
    {
        if (cameraTransform == null) return;

        Vector3 horizontalVel = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
        bool moving = horizontalVel.magnitude > 0.1f && isGrounded;

        Vector3 targetOffset;

        if (moving)
        {
            bool running = currentSpeed >= runSpeed - 0.01f;
            float speedMul = running ? runBobMultiplier : 1f;

            bobTimer += Time.deltaTime * walkBobSpeed * speedMul;

            float bobY = Mathf.Sin(bobTimer) * walkBobAmount * speedMul;
            float bobX = Mathf.Cos(bobTimer * 0.5f) * walkBobAmount * 0.6f * speedMul;

            targetOffset = new Vector3(bobX, bobY, 0f);
        }
        else
        {
            bobTimer += Time.deltaTime * headIdleBobSpeed;

            float bobY = Mathf.Sin(bobTimer) * headIdleBobAmount;
            float bobX = Mathf.Sin(bobTimer * 0.4f) * headIdleBobAmount;

            targetOffset = new Vector3(bobX, bobY, 0f);
        }

        currentBobOffset = Vector3.Lerp(currentBobOffset, targetOffset, bobSmoothing * Time.deltaTime);
        cameraTransform.localPosition = camDefaultPos + currentBobOffset;
        cameraTransform.localRotation = Quaternion.Euler(verticalRotation, 0f, currentTilt);
    }

    void HandleFlashlight()
    {
        if (flashlight == null) return;

        Transform fl = flashlight.transform;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        flPosVelocity.x += -mouseX * swayKickAmount;
        flPosVelocity.y += -mouseY * swayKickAmount;

        flRotVelocity.x += mouseY * rotKickAmount;
        flRotVelocity.y += -mouseX * rotKickAmount;
        flRotVelocity.z += -mouseX * rotKickAmount;

        flPosOffset = Spring(flPosOffset, ref flPosVelocity, Vector3.zero);
        flPosOffset = ClampVector(flPosOffset, maxSwayOffset);

        flRotOffset = Spring(flRotOffset, ref flRotVelocity, Vector3.zero);
        flRotOffset = ClampVector(flRotOffset, maxRotOffset);

        flBobTimer += Time.deltaTime * flIdleBobSpeed;
        float fbobY = Mathf.Sin(flBobTimer) * flIdleBobAmount;
        float fbobX = Mathf.Cos(flBobTimer * 0.5f) * flIdleBobAmount * 0.6f;
    }

    Vector3 Spring(Vector3 current, ref Vector3 velocity, Vector3 target)
    {
        Vector3 displacement = current - target;
        Vector3 force = -springStiffness * displacement - springDamping * velocity;
        velocity += force * Time.deltaTime;
        return current + velocity * Time.deltaTime;
    }

    Vector3 ClampVector(Vector3 v, float max)
    {
        v.x = Mathf.Clamp(v.x, -max, max);
        v.y = Mathf.Clamp(v.y, -max, max);
        v.z = Mathf.Clamp(v.z, -max, max);
        return v;
    }
}