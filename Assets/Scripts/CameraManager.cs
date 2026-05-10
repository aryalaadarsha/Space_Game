using UnityEngine;
using System.Collections;


/// <summary>
/// A manager for the camera, responsible for handling camera effects based on the ship's movement,
/// </summary
public class CameraManager : MonoBehaviour
{
    [SerializeField] private float camShakeIntensityModifier = 100f;
    [SerializeField] private float camShakeThreshold = 100f;
    [SerializeField] private float camShakeInterval = 1f;

    private Vector3 baseLocalPosition;
    private Coroutine shakeRoutine;
    private float currentShakeIntensity;

    void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    public void OnThrustChanged(float actualThrust, float desiredThrust)
    {
        float thrustDifference = Mathf.Abs(desiredThrust - actualThrust);
        float shakeIntensity = thrustDifference;

        if (shakeIntensity > camShakeThreshold)
        {
            StartShake(shakeIntensity * camShakeIntensityModifier);
            Debug.Log($"Thrust changed: Actual={actualThrust:F2}, Desired={desiredThrust:F2}, ShakeIntensity={shakeIntensity:F2}");
        }
        else
        {
            StopShake();
            Debug.Log($"Thrust changed: Actual={actualThrust:F2}, Desired={desiredThrust:F2}, No shake (intensity {shakeIntensity:F2} below threshold)");
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
        Debug.Log($"Camera shake coroutine started with intensity: {currentShakeIntensity}");
        float sampleInterval = camShakeInterval <= 0.0001f ? 0.05f : camShakeInterval;

        while (true)
        {
            transform.localPosition = baseLocalPosition + new Vector3(
                Random.Range(-currentShakeIntensity, currentShakeIntensity),
                Random.Range(-currentShakeIntensity, currentShakeIntensity),
                0f
            ) * 0.01f;

            yield return new WaitForSeconds(sampleInterval);
        }
    }
}
