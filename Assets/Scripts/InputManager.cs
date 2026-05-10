using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A manager for handling player input, translating it into a structured format (InputData) that can be easily consumed by other components like ShipController and SpaceCraftManager.
/// </summary>
public class InputManager : MonoBehaviour
{
    private SpaceshipControls_Action controls; 
    public InputData inputData;

    void Awake()
    {
        controls = new SpaceshipControls_Action();
        
        // X키(Stop)가 눌린 프레임에만 stopTrigger를 켬
        controls.Player.Stop.performed += ctx => {
            inputData.stopTrigger = true;
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
        inputData.isBrowsing = controls.Player.Browse.IsPressed();
    }

    // Manager가 데이터를 읽어간 후 트리거를 다시 끔
    void LateUpdate()
    {
        inputData.stopTrigger = false;
    }
}