using TMPro;
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

    [SerializeField] private TMP_Text desiredThrustText;
    [SerializeField] private TMP_Text actualThrustText;
    [SerializeField] private HologramThrustGauge desiredThrustGauge;
    [SerializeField] private HologramThrustGauge actualThrustGauge;

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

        if (desiredThrustText != null)
        {
            desiredThrustText.text = $"{desiredThrust:F2}";
        }

        if (actualThrustText != null)
        {
            actualThrustText.text = $"{actualThrust:F2}";
        }

        if (desiredThrustGauge != null)
        {
            desiredThrustGauge.SetValue(desiredThrust);
        }

        if (actualThrustGauge != null)
        {
            actualThrustGauge.SetValue(actualThrust);
        }
    }
}
