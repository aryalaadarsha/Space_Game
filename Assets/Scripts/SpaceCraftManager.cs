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
    public WeaponManager wManager;
    public HyperDriveManager hManager;
    public TargetingManager tManager;

    private MovementData currentMovementData;
    private bool wasBrowsing = false;

    void Awake()
    {
        if (inputManager == null) inputManager = GetComponentInChildren<InputManager>();
        if (sController == null)  sController = GetComponentInChildren<ShipController>();
        if (sUIManager == null)   sUIManager = GetComponentInChildren<ShipUIManager>  ();
        if (cManager == null)     cManager = GetComponentInChildren<CameraManager>    ();
        if (wManager == null)     wManager = GetComponentInChildren<WeaponManager>    ();
        if (hManager == null)     hManager = GetComponentInChildren<HyperDriveManager>();
        if (tManager == null)     tManager = GetComponentInChildren<TargetingManager>();
        if (tManager == null)     tManager = gameObject.AddComponent<TargetingManager>();

        Camera targetCamera = cManager != null ? cManager.GetComponent<Camera>() : GetComponentInChildren<Camera>();
        tManager.Initialize(transform, targetCamera);

        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (inputManager == null || sController == null || sUIManager == null) return;
        ProcessInput();
    }

    private void ProcessInput()
    {
        InputData input = inputManager.inputData;
        HandleStopTrigger(input);
        HandleSetInputs(input);

        // Handle browse (look-around) mode: when browsing, update camera and keep ship mouse centered
        HandleBrowsing(input);
        HandleCameraLook(input);
        HandleToggleWeapon(input);
        HandleFireWeapons(input);
        HandleHyperDrive(input);
        HandleTargeting(input);
    }

    private void HandleToggleWeapon(InputData input)
    {
        if (input.toggleWeapon)
        {
            if (wManager != null)
            {
                wManager.ToggleWeapon();
            }
        }
    }

    private void HandleBrowsing(InputData input)
    {
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

    private void HandleCameraLook(InputData input)
    {
        if (cManager != null)
        {
            cManager.UpdateCameraLook(input.cameraLookInput);
        }
    }

    private void HandleSetInputs(InputData input)
    {
        sController.SetInputs(input.strafeInput, input.thrustInput, input.yawInput);
    }

    private void HandleStopTrigger(InputData input)
    {
        if (input.stopTrigger)
        {
            sController.ResetThrust();
        }
    }

    private void HandleFireWeapons(InputData input)
    {
        if (wManager != null)
        {
            wManager.FireWeapon(input.fireLeftWeapon, input.fireRightWeapon);
        }
    }

    public void OnMovementUpdated(
        float desiredThrust,
        float actualThrust,
        Vector3 velocity,
        Vector3 angularVelocity,
        float actualThrustChangeRate,
        bool isActuallyAccelerating)
    {
        currentMovementData.desiredThrust = desiredThrust;
        currentMovementData.actualThrust = actualThrust;
        currentMovementData.velocity = velocity;
        currentMovementData.angularVelocity = angularVelocity;
        currentMovementData.actualThrustChangeRate = actualThrustChangeRate;
        currentMovementData.isActuallyAccelerating = isActuallyAccelerating;
        
        if (sUIManager != null)
        {
            sUIManager.ReceiveMovementData(currentMovementData);
        }
        if (cManager != null)
        {
            cManager.OnThrustChanged(actualThrust, desiredThrust, actualThrustChangeRate, isActuallyAccelerating, angularVelocity);
        }
    }

    private void HandleHyperDrive(InputData input)
    {
        if (input.hyperDriveTrigger)
        {
            OnHyperDriveActivated();
        }
    }

    private void HandleTargeting(InputData input)
    {
        if (tManager == null)
        {
            return;
        }

        if (input.targetLockTrigger)
        {
            tManager.ToggleLock();
        }

        if (sController != null)
        {
            if (tManager.TryGetLockedDirection(out Vector3 lockDirection))
            {
                sController.SetNavigationLockDirection(true, lockDirection);
            }
            else
            {
                sController.SetNavigationLockDirection(false, Vector3.zero);
            }
        }
    }

    private void OnHyperDriveActivated()
    {
        if (hManager != null)
        {
            hManager.ActivateHyperDrive();
        }
    }
}

public struct MovementData
{
    public float actualThrust;
    public float desiredThrust;
    public Vector3 velocity;
    public Vector3 angularVelocity;
    public float actualThrustChangeRate;
    public bool isActuallyAccelerating;
}
