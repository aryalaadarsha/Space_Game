using UnityEngine;
using System.Collections;


/// <summary>
/// A manager for the camera, responsible for handling camera effects based on the ship's movement,
/// </summary
public class CameraManager : MonoBehaviour
{
    [SerializeField] private float camShakeIntensityModifier = 100f;
    [SerializeField] private float camShakeThreshold = 0.2f;
    [SerializeField] private float camShakeFrequency = 10f;

    private Vector3 baseLocalPosition;
    private Coroutine shakeRoutine;
    private float currentShakeIntensity;

    void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    public void OnThrustChanged(float actualThrust, float desiredThrust)
    {
        float thrustDifference = desiredThrust - actualThrust;
        thrustDifference *= camShakeIntensityModifier;
        // Implement camera effects based on thrust changes, such as shaking
        Debug.Log($"Camera Shake Intensity : {thrustDifference:F2}");
        

        if (thrustDifference > camShakeThreshold)
        {
            StartShake(thrustDifference);
        }
        else
        {
            StopShake();
        }
    }

    private void StartShake(float intensity)
    {
        currentShakeIntensity = intensity;

        if (shakeRoutine == null)
        {
            shakeRoutine = StartCoroutine(ShakeCamera());
        }
    }

    private void StopShake()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        transform.localPosition = baseLocalPosition;
    }

    private IEnumerator ShakeCamera()
    {
        float sampleInterval = camShakeFrequency <= 0.0001f ? 0.05f : 1f / camShakeFrequency;

        while (true)
        {
            transform.localPosition = baseLocalPosition + new Vector3(
                Random.Range(-currentShakeIntensity, currentShakeIntensity),
                Random.Range(-currentShakeIntensity, currentShakeIntensity),
                0f
            );

            yield return new WaitForSeconds(sampleInterval);
        }
    }
}
