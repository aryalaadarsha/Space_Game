using UnityEngine;
using UnityEngine.SceneManagement;

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
    [SerializeField] private float thrustSnapThreshold = 0.05f;
    [SerializeField] private float strafeForce = 30f;
    [SerializeField] private float rotationSpeed = 0.5f;
    [SerializeField] private float mouseSensitivity = 0.005f;

    [Header("Floating Origin")]
    [SerializeField] private bool useFloatingOrigin = true;
    [SerializeField] private float floatingOriginThreshold = 2000f;
    [SerializeField] private string farSpaceLayerName = "FarSpace";
    [SerializeField] private float defaultFarSpaceMovementScale = 0.1f;

    [Header("Mouse Snap Settings")]
    [Range(0f, 0.5f)] [SerializeField] private float snapZoneRadius = 0.04f;
    // [Range(0f, 20f)] [SerializeField] private float snapStrength = 5f;

    [Header("Target Lock Settings")]
    [SerializeField] private float lockTurnSpeed = 32f;
    [SerializeField] private float lockTurnAcceleration = 14f;
    [SerializeField] private float lockTurnDeceleration = 24f;
    [SerializeField] private float lockSlowdownAngle = 18f;
    [SerializeField] private float lockMinimumTurnSpeed = 2.5f;
    [SerializeField] private float lockSettleAngle = 0.35f;
    [SerializeField] private float lockSettleSmoothing = 5f;
    [SerializeField] private float lockDirectionSmoothing = 3.5f;
    [SerializeField] private float lockAngularDamping = 2.5f;
    [SerializeField] private bool lockPreservesCurrentRoll = true;

    private Rigidbody rb;
    private SpaceCraftManager SCM;
    private bool hasNavigationLock;
    private Vector3 navigationLockDirection;
    private float currentLockTurnSpeed;
    private bool hyperCruiseOverrideActive;

    private float actualThrust = 0f;
    private Vector2 moveInput;
    private float thrustInput;
    private float desiredThrust;
    private float actualThrustChangeRate;
    private bool isActuallyAccelerating;
    private float yawInput;
    private Vector2 mouseInput;
    private int cachedFarSpaceLayer = -2;
    private float suppressLookInputUntil;
    private Vector2 mouseNeutralPosition;
    private bool hasMouseNeutralPosition;

    public bool UseFloatingOrigin => useFloatingOrigin;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        SCM = GetComponentInParent<SpaceCraftManager>();
    }

    void Start()
    {
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        SetMouseNeutralPosition(screenCenter);
        TryWarpCursorToCenter();
    }

    public void SetInputs(Vector2 _strafe, float _thrustInput, float _yaw)
    {
        if (hyperCruiseOverrideActive)
        {
            moveInput = Vector2.zero;
            thrustInput = 0f;
            yawInput = 0f;
            return;
        }

        moveInput = _strafe;
        thrustInput = _thrustInput;
        yawInput = _yaw;
    }

    public void SetMouseInput(Vector2 _mousePos)
    {
        if (IsLookInputSuppressed())
        {
            SetMouseNeutralPosition(_mousePos);
            TryWarpCursorToCenter();
            return;
        }

        mouseInput = _mousePos;
    }

    public void ResetLookInputReference()
    {
        Vector2 neutralPosition = UnityEngine.InputSystem.Mouse.current != null
            ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        SetMouseNeutralPosition(neutralPosition);
        TryWarpCursorToCenter();
    }

    private void SetMouseNeutralPosition(Vector2 neutralPosition)
    {
        mouseNeutralPosition = neutralPosition;
        mouseInput = neutralPosition;
        hasMouseNeutralPosition = true;
    }

    private void TryWarpCursorToCenter()
    {
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            UnityEngine.InputSystem.Mouse.current.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }
    }

    public void SuppressLookInput(float duration)
    {
        suppressLookInputUntil = Mathf.Max(suppressLookInputUntil, Time.unscaledTime + Mathf.Max(0f, duration));
        ResetLookInputReference();
    }

    public void ResetThrust()
    {
        desiredThrust = 0f;
    }

    public void SetNavigationLock(Transform target)
    {
        bool wasLocked = hasNavigationLock;
        hasNavigationLock = target != null;
        if (target != null)
        {
            SetNavigationLockDirectionInternal((target.position - transform.position).normalized, wasLocked);
        }
        else
        {
            currentLockTurnSpeed = 0f;
        }
    }

    public void SetNavigationLockDirection(bool isLocked, Vector3 worldDirection)
    {
        bool wasLocked = hasNavigationLock;
        hasNavigationLock = isLocked && worldDirection.sqrMagnitude > 0.0001f;
        if (hasNavigationLock)
        {
            SetNavigationLockDirectionInternal(worldDirection.normalized, wasLocked);
        }
        else
        {
            currentLockTurnSpeed = 0f;
        }
    }

    void FixedUpdate()
    {
        UpdateThrust();
        if (hyperCruiseOverrideActive)
        {
            if (rb != null)
            {
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, 10f * Time.fixedDeltaTime);
            }

            AfterMovement();
            return;
        }

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
        float deltaTime = Time.fixedDeltaTime;
        float previousActualThrust = actualThrust;
        bool snappedToDesiredThrust = false;

        if (Mathf.Abs(thrustInput) > 0.1f)
        {
            desiredThrust += thrustInput * deltaTime * 20f;
        }
        desiredThrust = Mathf.Clamp(desiredThrust, -30f, 70f);

        if (Mathf.Abs(desiredThrust - actualThrust) <= thrustSnapThreshold)
        {
            actualThrust = desiredThrust;
            snappedToDesiredThrust = true;
        }
        else
        {
            actualThrust = Mathf.Lerp(actualThrust, desiredThrust, deltaTime * thrustLerpSpeed);
        }

        float actualThrustDelta = actualThrust - previousActualThrust;
        actualThrustChangeRate = !snappedToDesiredThrust && deltaTime > 0f ? Mathf.Abs(actualThrustDelta) / deltaTime : 0f;
        isActuallyAccelerating = actualThrustChangeRate > 0.001f;
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
        if (IsLookInputSuppressed())
        {
            return Vector2.zero;
        }

        if (!isInitialized)
        {
            initFrameCount++;
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            UnityEngine.InputSystem.Mouse.current.WarpCursorPosition(screenCenter);

            if (initFrameCount >= INIT_FRAMES)
                isInitialized = true;

            return Vector2.zero;
        }

        Vector2 neutralPosition = hasMouseNeutralPosition
            ? mouseNeutralPosition
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 mouseOffset = mouseInput - neutralPosition;
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

    private bool IsLookInputSuppressed()
    {
        return Time.unscaledTime < suppressLookInputUntil;
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
        Quaternion targetRotation = lockPreservesCurrentRoll
            ? GetRollPreservingTargetRotation(direction)
            : Quaternion.LookRotation(direction, Vector3.up);
        float angleToTarget = Quaternion.Angle(rb.rotation, targetRotation);

        if (angleToTarget <= lockSettleAngle)
        {
            float settle = 1f - Mathf.Exp(-lockSettleSmoothing * Time.fixedDeltaTime);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, settle));
            currentLockTurnSpeed = Mathf.MoveTowards(currentLockTurnSpeed, 0f, lockTurnDeceleration * Time.fixedDeltaTime);
            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, lockAngularDamping * Time.fixedDeltaTime);
            return;
        }

        float slowdown = Mathf.Clamp01(angleToTarget / Mathf.Max(0.001f, lockSlowdownAngle));
        float desiredTurnSpeed = lockTurnSpeed * Mathf.SmoothStep(0f, 1f, slowdown);
        desiredTurnSpeed = Mathf.Max(lockMinimumTurnSpeed, desiredTurnSpeed);
        float turnRateChange = desiredTurnSpeed > currentLockTurnSpeed ? lockTurnAcceleration : lockTurnDeceleration;
        currentLockTurnSpeed = Mathf.MoveTowards(currentLockTurnSpeed, desiredTurnSpeed, turnRateChange * Time.fixedDeltaTime);

        Quaternion nextRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, currentLockTurnSpeed * Time.fixedDeltaTime);

        rb.MoveRotation(nextRotation);
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, lockAngularDamping * Time.fixedDeltaTime);
    }

    private void SetNavigationLockDirectionInternal(Vector3 direction, bool wasLocked)
    {
        if (!wasLocked || navigationLockDirection.sqrMagnitude < 0.0001f)
        {
            currentLockTurnSpeed = 0f;
            navigationLockDirection = direction;
            return;
        }

        float smoothing = 1f - Mathf.Exp(-lockDirectionSmoothing * Time.deltaTime);
        navigationLockDirection = Vector3.Slerp(navigationLockDirection, direction, smoothing).normalized;
    }

    private Quaternion GetRollPreservingTargetRotation(Vector3 forwardDirection)
    {
        Vector3 currentUp = rb.rotation * Vector3.up;
        Vector3 targetUp = Vector3.ProjectOnPlane(currentUp, forwardDirection);

        if (targetUp.sqrMagnitude < 0.0001f)
        {
            Vector3 currentRight = rb.rotation * Vector3.right;
            targetUp = Vector3.ProjectOnPlane(currentRight, forwardDirection);
        }

        if (targetUp.sqrMagnitude < 0.0001f)
        {
            targetUp = Vector3.ProjectOnPlane(Vector3.up, forwardDirection);
        }

        if (targetUp.sqrMagnitude < 0.0001f)
        {
            targetUp = Vector3.up;
        }

        return Quaternion.LookRotation(forwardDirection, targetUp.normalized);
    }

    private void AfterMovement()
    {
        ApplyFloatingOriginIfNeeded();

        if (SCM != null)
        {
            SCM.OnMovementUpdated(desiredThrust, actualThrust, rb.linearVelocity, rb.angularVelocity, actualThrustChangeRate, isActuallyAccelerating);
        }
    }

    private void ApplyFloatingOriginIfNeeded()
    {
        if (!useFloatingOrigin || floatingOriginThreshold <= 0f)
        {
            return;
        }

        Vector3 originOffset = rb.position;
        if (originOffset.sqrMagnitude < floatingOriginThreshold * floatingOriginThreshold)
        {
            return;
        }

        RecenterWorld(originOffset);
    }

    private void RecenterWorld(Vector3 originOffset)
    {
        Transform playerRoot = transform.root;
        float farSpaceScale = GetFarSpaceMovementScale();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int j = 0; j < rootObjects.Length; j++)
            {
                GameObject rootObject = rootObjects[j];
                if (ShouldSkipFloatingOriginRoot(rootObject, playerRoot))
                {
                    continue;
                }

                float shiftScale = IsFarSpaceRoot(rootObject) ? farSpaceScale : 1f;
                Vector3 rootShift = originOffset * shiftScale;
                Rigidbody rootRigidbody = rootObject.GetComponent<Rigidbody>();
                if (rootRigidbody != null)
                {
                    TeleportRigidbody(rootRigidbody, rootRigidbody.position - rootShift);
                }
                else
                {
                    rootObject.transform.position -= rootShift;
                }
            }
        }

        TeleportRigidbody(rb, rb.position - originOffset);
        Physics.SyncTransforms();
    }

    private void TeleportRigidbody(Rigidbody targetRigidbody, Vector3 worldPosition)
    {
        targetRigidbody.position = worldPosition;
        targetRigidbody.transform.position = worldPosition;
    }

    private bool ShouldSkipFloatingOriginRoot(GameObject rootObject, Transform playerRoot)
    {
        if (rootObject == null)
        {
            return true;
        }

        Transform rootTransform = rootObject.transform;
        if (rootTransform == playerRoot || rootTransform.IsChildOf(playerRoot))
        {
            return true;
        }

        if (rootObject.GetComponent<Camera>() != null || rootObject.GetComponent<FarSpaceCameraSync>() != null)
        {
            return true;
        }

        return rootObject.GetComponent<Canvas>() != null
            || rootObject.name == "EventSystem"
            || rootObject.name == "Global Volume"
            || rootObject.name == "UniversalLight";
    }

    private bool IsFarSpaceRoot(GameObject rootObject)
    {
        int farSpaceLayer = GetFarSpaceLayer();
        Transform[] children = rootObject.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            GameObject childObject = children[i].gameObject;
            if (childObject.CompareTag("TrackingObject") || childObject.layer == farSpaceLayer)
            {
                return true;
            }
        }

        return false;
    }

    private int GetFarSpaceLayer()
    {
        if (cachedFarSpaceLayer == -2)
        {
            cachedFarSpaceLayer = LayerMask.NameToLayer(farSpaceLayerName);
        }

        return cachedFarSpaceLayer;
    }

    private float GetFarSpaceMovementScale()
    {
        FarSpaceCameraSync farSpaceSync = FindFirstObjectByType<FarSpaceCameraSync>();
        if (farSpaceSync != null && farSpaceSync.movementScale > 0.0001f)
        {
            return farSpaceSync.movementScale;
        }

        return defaultFarSpaceMovementScale;
    }

    public void ModifyThrustForce(float amount)
    {
        thrustForce *= amount;
    }

    public void SetFloatingOriginEnabled(bool isEnabled)
    {
        useFloatingOrigin = isEnabled;
    }

    public void SetHyperCruiseOverrideActive(bool isActive, bool clearVelocity = false)
    {
        hyperCruiseOverrideActive = isActive;
        moveInput = Vector2.zero;
        thrustInput = 0f;
        yawInput = 0f;
        SetNavigationLockDirection(false, Vector3.zero);

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb == null)
        {
            return;
        }

        rb.angularVelocity = Vector3.zero;
        if (isActive && clearVelocity)
        {
            desiredThrust = 0f;
            actualThrust = 0f;
            rb.linearVelocity = Vector3.zero;
        }
    }

    public void RotateHyperCruiseTowards(Vector3 forwardDirection, float maxDegreesDelta)
    {
        if (forwardDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        Vector3 direction = forwardDirection.normalized;
        Quaternion currentRotation = rb != null ? rb.rotation : transform.rotation;
        Quaternion targetRotation = rb != null && lockPreservesCurrentRoll
            ? GetRollPreservingTargetRotation(direction)
            : Quaternion.LookRotation(direction, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(currentRotation, targetRotation, Mathf.Max(0f, maxDegreesDelta));

        if (rb != null)
        {
            rb.rotation = nextRotation;
            rb.angularVelocity = Vector3.zero;
        }

        transform.rotation = nextRotation;
    }

    public void SetHyperCruiseRotation(Quaternion rotation)
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.rotation = rotation;
            rb.angularVelocity = Vector3.zero;
        }

        transform.rotation = rotation;
    }

    public void StabilizeHyperCruiseArrival(Vector3 forwardDirection, float thrust, float lookSuppressionDuration = 1.25f)
    {
        if (forwardDirection.sqrMagnitude < 0.0001f)
        {
            forwardDirection = transform.forward;
        }

        moveInput = Vector2.zero;
        thrustInput = 0f;
        yawInput = 0f;
        SuppressLookInput(lookSuppressionDuration);
        SetNavigationLockDirection(false, Vector3.zero);
        RotateHyperCruiseTowards(forwardDirection, 180f);

        desiredThrust = Mathf.Clamp(thrust, -30f, 70f);
        actualThrust = desiredThrust;
        actualThrustChangeRate = 0f;
        isActuallyAccelerating = false;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.linearVelocity = transform.forward * (actualThrust * thrustForce);
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void RecenterForHyperCruise(Vector3 originOffset)
    {
        if (originOffset.sqrMagnitude < 0.0001f)
        {
            return;
        }

        RecenterWorld(originOffset);
    }

    public void ApplyHyperCruiseBoost(float thrust)
    {
        desiredThrust = Mathf.Clamp(thrust, -30f, 70f);
        actualThrust = desiredThrust;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.linearVelocity = transform.forward * (actualThrust * thrustForce);
        }
    }

    public void MoveHyperCruisePosition(Vector3 worldPosition, Vector3 travelDirection, float thrust)
    {
        desiredThrust = Mathf.Clamp(thrust, -30f, 70f);
        actualThrust = desiredThrust;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        Vector3 velocityDirection = travelDirection.sqrMagnitude > 0.0001f
            ? travelDirection.normalized
            : transform.forward;

        if (rb != null)
        {
            rb.position = worldPosition;
            rb.transform.position = worldPosition;
            rb.linearVelocity = velocityDirection * (actualThrust * thrustForce);
        }
        else
        {
            transform.position = worldPosition;
        }
    }
}
