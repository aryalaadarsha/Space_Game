using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A manager for handling player input, translating it into a structured format (InputData) that can be easily consumed by other components like ShipController and SpaceCraftManager.
/// </summary>
public class InputManager : MonoBehaviour
{
    private SpaceshipControls_Action controls; 
    public InputData inputData;
    [SerializeField] private float browseToggleCooldown = 0.08f;
    private float lastBrowseToggleTime = -10f;

    void Awake()
    {
        controls = new SpaceshipControls_Action();
        
        // X키(Stop)가 눌린 프레임에만 stopTrigger를 켬
        controls.Player.Stop.performed += ctx => {
            inputData.stopTrigger = true;
        };
    
        controls.Player.Browse.performed += ctx => {
            float now = Time.unscaledTime;
            if (now - lastBrowseToggleTime < browseToggleCooldown) return;
            lastBrowseToggleTime = now;

            inputData.isBrowsing = !inputData.isBrowsing;

            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                UnityEngine.InputSystem.Mouse.current.WarpCursorPosition(screenCenter);
            }
        };

        controls.Player.HyperDrive.started += ctx => {
            
        };
    }

    void OnEnable() => controls.Player.Enable();
    void OnDisable() => controls.Player.Disable();


    void Update()
    {
        inputData.thrustInput = (int) controls.Player.Thrust.ReadValue<float>();
        inputData.strafeInput = controls.Player.Strafe.ReadValue<Vector2>();
        inputData.yawInput = controls.Player.Yaw.ReadValue<float>();
        inputData.mouseInput = Mouse.current.position.ReadValue();
        inputData.toggleWeapon = controls.Player.Toggle_Weapon.triggered;
        inputData.fireLeftWeapon = controls.Player.Attack_Left.IsPressed();
        inputData.fireRightWeapon = controls.Player.Attack_Right.IsPressed();
        inputData.hyperDriveTrigger = controls.Player.HyperDrive.triggered;
        
        // 카메라 방향 입력 (Q/E/R/F) - New Input System 사용
        Vector2 cameraLook = Vector2.zero;
        if (Keyboard.current.qKey.isPressed) cameraLook.x -= 1f;
        if (Keyboard.current.eKey.isPressed) cameraLook.x += 1f;
        if (Keyboard.current.rKey.isPressed) cameraLook.y += 1f;
        if (Keyboard.current.fKey.isPressed) cameraLook.y -= 1f;
        inputData.cameraLookInput = cameraLook;
    }

    // Manager가 데이터를 읽어간 후 트리거를 다시 끔
    void LateUpdate()
    {
        inputData.stopTrigger = false;
        inputData.toggleWeapon = false;
        inputData.hyperDriveTrigger = false;
    }
}