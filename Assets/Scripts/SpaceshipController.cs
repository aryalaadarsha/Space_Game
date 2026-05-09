using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class SpaceshipController : MonoBehaviour
{
    private bool isInitialized = false;
    private int initFrameCount = 0;
    private const int INIT_FRAMES = 3; // 초기화 대기 프레임 수

    [Header("Engine Settings")]
    public float thrustForce = 50f;
    public float strafeForce = 30f;
    public float rotationSpeed = 0.5f;
    public float mouseSensitivity = 0.005f; 

    [Header("Mouse Snap Settings")]
    [Range(0f, 0.5f)] public float snapZoneRadius = 0.04f; 
    [Range(0f, 20f)] public float snapStrength = 5f;

    [Header("Camera Settings")]
    public bool isThirdPerson = false;
    public bool isZoomedOut = false;
    public float zoomOutDistance = 0.1f;

    private Rigidbody rb;
    private SpaceshipControls_Action controls; 

    private float currentThrust = 0f;
    private Vector2 moveInput;
    private float thrustInput;
    private float yawInput;
    private Vector2 mouseInput;
    private Func<Vector2> mouseControlFunc;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        controls = new SpaceshipControls_Action();
        controls.Player.Stop.performed += ctx => currentThrust = 0f;
    }

    void OnEnable() {
        controls.Player.Enable();
        
        controls.Player.Browse.performed += ctx => TurnBrowseState(true);
    }
    void OnDisable() => controls.Player.Disable();

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Mouse.current.WarpCursorPosition(screenCenter);

        isInitialized = false;
        initFrameCount = 0;

        mouseControlFunc = HandleShipMouseControl;
    }

    void Update()
    {
        HandleInput();
        UpdateThrust();
    }

    void FixedUpdate()
    {
        // 기능을 함수별로 분리하여 실행
        ApplyTranslation();
        Vector2 processedRotation = HandleShipMouseControl();
        ApplyRotation(processedRotation);
    }

    // 1. 입력 값 읽기 분리
    private void HandleInput()
    {
        thrustInput = controls.Player.Thrust.ReadValue<float>();
        moveInput = controls.Player.Strafe.ReadValue<Vector2>();
        yawInput = controls.Player.Yaw.ReadValue<float>();

        mouseInput = Mouse.current.position.ReadValue();
    }

    // 2. 추력 계산 로직 분리 (Update에서 처리)
    private void UpdateThrust()
    {
        if (Mathf.Abs(thrustInput) > 0.1f)
        {
            currentThrust += thrustInput * Time.deltaTime * 10f;
        }
        currentThrust = Mathf.Clamp(currentThrust, -10f, 30f);
    }

    // 3. 실제 물리 이동 (전진/후진은 가변, 좌우/상하는 고정 힘)
    private void ApplyTranslation()
    {
        // [전진/후진] - currentThrust(W/S로 조절된 값)에 영향을 받음
        Vector3 forwardVec = Vector3.forward * currentThrust * thrustForce * Time.fixedDeltaTime * 0.1f;
        rb.AddRelativeForce(forwardVec);

        // [상하좌우 스트레이프] - currentThrust와 무관하게 strafeForce를 직접 사용
        // moveInput.x (좌우), moveInput.y (상하)
        Vector3 strafeVec = new Vector3(moveInput.x, moveInput.y, 0);
        
        // 입력이 있을 때만 고정된 strafeForce를 가함
        // Time.fixedDeltaTime을 곱해 프레임율에 상관없이 일정한 힘을 유지합니다.
        rb.AddRelativeForce(strafeVec * strafeForce * Time.fixedDeltaTime, ForceMode.Acceleration);
    }

    private Vector2 HandleShipMouseControl()
    {
        // 초기화 프레임 동안 회전 입력 차단
        if (!isInitialized)
        {
            initFrameCount++;
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Mouse.current.WarpCursorPosition(screenCenter);

            if (initFrameCount >= INIT_FRAMES)
                isInitialized = true;

            return Vector2.zero; // 회전 완전 차단
        }

        Vector2 screenCenter2 = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 mouseOffset = mouseInput - screenCenter2;
        
        Vector2 normalizedOffset = new Vector2(mouseOffset.x / Screen.width, mouseOffset.y / Screen.height);
        float distanceToCenter = normalizedOffset.magnitude;

        if (distanceToCenter < snapZoneRadius)
        {
            if (distanceToCenter > 0.0001f)
            {
                Vector2 snappedPos = Vector2.Lerp(mouseInput, screenCenter2, Time.fixedDeltaTime * snapStrength);
                Mouse.current.WarpCursorPosition(snappedPos);
            }
            return Vector2.zero;
        }

        return mouseOffset;
    }

    

    // 5. 실제 물리 회전 적용
    private void ApplyRotation(Vector2 rotationInput)
    {
        // Pitch: Mouse Y, Roll: Mouse X, Yaw: A/D
        float pitch = -rotationInput.y * mouseSensitivity;
        float roll = -rotationInput.x * mouseSensitivity;
        float yaw = yawInput * rotationSpeed * 0.5f; // Yaw는 버튼 입력이므로 별도 감도 조절

        pitch = Mathf.Clamp(pitch, -1f, 1f);
        roll = Mathf.Clamp(roll, -1f, 1f);

        Vector3 torque = new Vector3(pitch, yaw, roll) * rotationSpeed * Time.fixedDeltaTime;
        rb.AddRelativeTorque(torque, ForceMode.VelocityChange);
        rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, 1f); // 회전 속도 제한
    }

    private void TurnBrowseState(bool isBrowsing)
    {
        if (isBrowsing)
        {
            
        }
        else
        {
            
        }
    }
}