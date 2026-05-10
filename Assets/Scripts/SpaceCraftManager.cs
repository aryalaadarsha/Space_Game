using UnityEngine;

/// <summary>
/// A manager for the spacecraft, responsible for coordinating between the InputManager, ShipController, and ShipUIManager. It processes player input, updates the ship's movement, and relays movement data to the UI for display.
/// </summary>
public class SpaceCraftManager : MonoBehaviour
{
    public InputManager inputManager;
    public ShipController sController;
    public ShipUIManager sUIManager;
    public CameraManager cManager;

    private MovementData currentMovementData;
    private bool wasBrowsing = false;

    void Awake()
    {
        if (inputManager == null) inputManager = GetComponentInChildren<InputManager>();
        if (sController == null)  sController = GetComponentInChildren<ShipController>();
        if (sUIManager == null)   sUIManager = GetComponentInChildren<ShipUIManager>  ();
        if (cManager == null)     cManager = GetComponentInChildren<CameraManager>    ();
    }

    void Update()
    {
        if (inputManager == null || sController == null || sUIManager == null) return;
        ProcessInput();
    }

    private void ProcessInput()
    {
        InputData input = inputManager.inputData;

        if (input.stopTrigger)
        {
            sController.ResetThrust();
        }

        sController.SetInputs(input.strafeInput, input.thrustInput, input.yawInput);

        // Handle browse (look-around) mode: when browsing, update camera and keep ship mouse centered
        if (cManager != null && input.isBrowsing)
        {
            if (!wasBrowsing)
            {
                cManager.StartBrowse();
                wasBrowsing = true;
            }

            cManager.UpdateBrowse(input.mouseInput);

            // Keep ship mouse input near screen center so ship doesn't react to mouse while browsing
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            sController.SetMouseInput(screenCenter);
        }
        else
        {
            if (wasBrowsing)
            {
                if (cManager != null) cManager.StopBrowse();
                wasBrowsing = false;
            }

            sController.SetMouseInput(input.mouseInput);
        }
    }

    public void OnMovementUpdated(float desiredThrust, float actualThrust, Vector3 velocity, Vector3 angularVelocity)
    {
        currentMovementData.desiredThrust = desiredThrust;
        currentMovementData.actualThrust = actualThrust;
        currentMovementData.velocity = velocity;
        currentMovementData.angularVelocity = angularVelocity;
        
        if (sUIManager != null)
        {
            sUIManager.ReceiveMovementData(currentMovementData);
        }
        if (cManager != null)
        {
            cManager.OnThrustChanged(actualThrust, desiredThrust);
        }
    }
}

public struct MovementData
{
    public float actualThrust;
    public float desiredThrust;
    public Vector3 velocity;
    public Vector3 angularVelocity;
}