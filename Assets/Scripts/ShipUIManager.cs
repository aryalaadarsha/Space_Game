using UnityEngine;

/// <summary>
/// A manager for the ship's UI elements, responsible for receiving movement data and updating the UI accordingly.
/// </summary>
public class ShipUIManager : MonoBehaviour
{
    private MovementData currentMovementData;

    public float CurrentSpeed => currentMovementData.velocity.magnitude;
    public float CurrentActualThrust => currentMovementData.actualThrust;
    public Vector3 CurrentVelocity => currentMovementData.velocity;
    public Vector3 CurrentAngularVelocity => currentMovementData.angularVelocity;

    public void ReceiveMovementData(MovementData movementData)
    {
        currentMovementData = movementData;
        CalculateUIElements();
    }

    private void CalculateUIElements()
    {
        float speed = CurrentSpeed;
        float actualThrust = CurrentActualThrust;
        float desiredThrust = currentMovementData.desiredThrust;

        Debug.Log($"Speed: {speed:F2}, ActualThrust: {actualThrust:F2}, DesiredThrust: {desiredThrust:F2}");

        // Bind text, sliders, or other UI elements here when they are added.
    }
}
