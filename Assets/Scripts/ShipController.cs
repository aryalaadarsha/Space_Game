using UnityEngine;

[RequireComponent(typeof(Rigidbody))]

/// <summary>
/// A controller for the spaceship, responsible for handling movement based on player input, applying forces and
/// torques to the Rigidbody, and communicating movement data to the SpaceCraftManager for UI updates.
/// </summary>
public class ShipController : MonoBehaviour
{
    private bool isInitialized = false;
    private int initFrameCount = 0;
    private const int INIT_FRAMES = 3;

    [Header("Engine Settings")]
    [SerializeField] private float thrustForce = 1f;
    [SerializeField] private float thrustLerpSpeed = 0.5f;
    [SerializeField] private float strafeForce = 30f;
    [SerializeField] private float rotationSpeed = 0.5f;
    [SerializeField] private float mouseSensitivity = 0.005f;

    [Header("Mouse Snap Settings")]
    [Range(0f, 0.5f)] [SerializeField] private float snapZoneRadius = 0.04f;
    // [Range(0f, 20f)] [SerializeField] private float snapStrength = 5f;

    [Header("Target Lock Settings")]
    [SerializeField] private float lockTurnSpeed = 80f;
    [SerializeField] private float lockAngularDamping = 8f;

    private Rigidbody rb;
    private SpaceCraftManager SCM;
    private bool hasNavigationLock;
    private Vector3 navigationLockDirection;

    private float actualThrust = 0f;
    private Vector2 moveInput;
    private float thrustInput;
    private float desiredThrust;
    private float yawInput;
    private Vector2 mouseInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        SCM = GetComponentInParent<SpaceCraftManager>();
    }

    void Start()
    {
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        UnityEngine.InputSystem.Mouse.current.WarpCursorPosition(screenCenter);
    }

    public void SetInputs(Vector2 _strafe, float _thrustInput, float _yaw)
    {
        moveInput = _strafe;
        thrustInput = _thrustInput;
        yawInput = _yaw;
    }

    public void SetMouseInput(Vector2 _mousePos)
    {
        mouseInput = _mousePos;
    }

    public void ResetThrust()
    {
        desiredThrust = 0f;
    }

    public void SetNavigationLock(Transform target)
    {
        hasNavigationLock = target != null;
        if (target != null)
        {
            navigationLockDirection = (target.position - transform.position).normalized;
        }
    }

    public void SetNavigationLockDirection(bool isLocked, Vector3 worldDirection)
    {
        hasNavigationLock = isLocked && worldDirection.sqrMagnitude > 0.0001f;
        if (hasNavigationLock)
        {
            navigationLockDirection = worldDirection.normalized;
        }
    }

    void FixedUpdate()
    {
        UpdateThrust();
        ApplyTranslation();
        if (hasNavigationLock)
        {
            ApplyNavigationLockRotation();
        }
        else
        {
            Vector2 processedRotation = HandleShipMouseControl();
            ApplyRotation(processedRotation);
        }

        AfterMovement();
    }

    private void UpdateThrust()
    {
        if (Mathf.Abs(thrustInput) > 0.1f)
        {
            desiredThrust += thrustInput * Time.deltaTime * 20f;
        }
        desiredThrust = Mathf.Clamp(desiredThrust, -30f, 70f);

        actualThrust = Mathf.Lerp(actualThrust, desiredThrust, Time.deltaTime * thrustLerpSpeed);
    }

    private void ApplyTranslation()
    {
        Vector3 forwardVelocity = transform.forward * (actualThrust * thrustForce);
        Vector3 lateralVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, transform.forward);
        rb.linearVelocity = lateralVelocity + forwardVelocity;

        Vector3 strafeVec = new Vector3(moveInput.x, moveInput.y, 0);
        rb.AddRelativeForce(strafeVec * strafeForce * Time.fixedDeltaTime, ForceMode.Acceleration);
    }

    private Vector2 HandleShipMouseControl()
    {
        if (!isInitialized)
        {
            initFrameCount++;
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            UnityEngine.InputSystem.Mouse.current.WarpCursorPosition(screenCenter);

            if (initFrameCount >= INIT_FRAMES)
                isInitialized = true;

            return Vector2.zero;
        }

        Vector2 screenCenter2 = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 mouseOffset = mouseInput - screenCenter2;
        Vector2 normalizedOffset = new Vector2(mouseOffset.x / Screen.width, mouseOffset.y / Screen.height);
        float distanceToCenter = normalizedOffset.magnitude;

        if (distanceToCenter < snapZoneRadius)
        {
            // if (distanceToCenter > 0.0001f)
            // {
            //     Vector2 snappedPos = Vector2.Lerp(mouseInput, screenCenter2, Time.fixedDeltaTime * snapStrength);
            //     UnityEngine.InputSystem.Mouse.current.WarpCursorPosition(snappedPos);
            // }
            return Vector2.zero;
        }

        return mouseOffset;
    }

    private void ApplyRotation(Vector2 rotationInput)
    {
        float pitch = -rotationInput.y * mouseSensitivity;
        float roll = -rotationInput.x * mouseSensitivity;
        float yaw = yawInput * rotationSpeed * 0.5f;

        pitch = Mathf.Clamp(pitch, -1f, 1f);
        roll = Mathf.Clamp(roll, -1f, 1f);

        Vector3 torque = new Vector3(pitch, yaw, roll) * rotationSpeed * Time.fixedDeltaTime;
        rb.AddRelativeTorque(torque, ForceMode.VelocityChange);
        rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, 1f);
    }

    private void ApplyNavigationLockRotation()
    {
        if (navigationLockDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 direction = navigationLockDirection.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.98f ? transform.up : Vector3.up;
        Quaternion targetRotation = Quaternion.LookRotation(direction, up);
        Quaternion nextRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, lockTurnSpeed * Time.fixedDeltaTime);

        rb.MoveRotation(nextRotation);
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, lockAngularDamping * Time.fixedDeltaTime);
    }

    private void AfterMovement()
    {
        if (SCM != null)
        {
            SCM.OnMovementUpdated(desiredThrust, actualThrust, rb.linearVelocity, rb.angularVelocity);
        }
    }

    public void ModifyThrustForce(float amount)
    {
        thrustForce *= amount;
    }
}
