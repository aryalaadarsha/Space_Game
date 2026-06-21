using UnityEngine;
using System.Collections;

/// <summary>
/// Handles camera effects based on ship movement.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [SerializeField] private float camShakeIntensityModifier = 1f;
    [SerializeField] private float camShakeThreshold = 2.5f;
    [SerializeField] private float camShakeFullAccelerationRate = 30f;
    [SerializeField] private float camShakeInterval = 0.025f;
    [SerializeField] private float maxCamShakeOffset = 0.0012f;
    [Range(0f, 1f)] [SerializeField] private float brakingShakeMultiplier = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float turningShakeMultiplier = 0.25f;
    [SerializeField] private float turningSuppressionStart = 0.15f;
    [SerializeField] private float turningSuppressionEnd = 0.9f;
    [SerializeField] private float cameraLookOffsetAmount = 0.03f;
    [SerializeField] private float cameraLookSmoothSpeed = 8f;

    private Vector3 baseLocalPosition;
    private Vector3 cameraLookOffset = Vector3.zero;
    private Vector3 targetCameraLookOffset = Vector3.zero;
    private Coroutine shakeRoutine;
    private float currentShakeIntensity;

    // Browse (look-around) state
    [SerializeField] private float browseMaxAngle = 90f;
    [SerializeField] private float browseSmoothing = 8f;
    [SerializeField] private float browseReturnSpeed = 6f;
    private bool isBrowsing = false;
    private Quaternion baseLocalRotationQ;
    private Quaternion targetLocalRotation;

    void Awake()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotationQ = transform.localRotation;
        targetLocalRotation = baseLocalRotationQ;
    }

    public void OnThrustChanged(
        float actualThrust,
        float desiredThrust,
        float actualThrustChangeRate,
        bool isActuallyAccelerating,
        Vector3 angularVelocity)
    {
        if (isActuallyAccelerating && actualThrustChangeRate > camShakeThreshold)
        {
            float shakeAmount = Mathf.InverseLerp(camShakeThreshold, camShakeFullAccelerationRate, actualThrustChangeRate);
            shakeAmount *= camShakeIntensityModifier;

            if (IsBrakingOrReversing(actualThrust, desiredThrust))
            {
                shakeAmount *= brakingShakeMultiplier;
            }

            float turnAmount = Mathf.InverseLerp(turningSuppressionStart, turningSuppressionEnd, angularVelocity.magnitude);
            shakeAmount *= Mathf.Lerp(1f, turningShakeMultiplier, turnAmount);

            float shakeIntensity = Mathf.Clamp01(shakeAmount) * maxCamShakeOffset;
            StartShake(shakeIntensity);
        }
        else
        {
            StopShake();
        }
    }

    private void StartShake(float intensity)
    {
        currentShakeIntensity = Mathf.Min(intensity, maxCamShakeOffset);

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

        currentShakeIntensity = 0f;
        transform.localPosition = baseLocalPosition + cameraLookOffset;
    }

    public void StartBrowse()
    {
        isBrowsing = true;
        baseLocalRotationQ = transform.localRotation;
        targetLocalRotation = baseLocalRotationQ;
    }

    public void StopBrowse()
    {
        isBrowsing = false;
        targetLocalRotation = baseLocalRotationQ;
    }

    public void UpdateBrowse(Vector2 mousePosition)
    {
        if (!isBrowsing) return;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 offset = mousePosition - screenCenter;

        float nx = 0f;
        float ny = 0f;
        if (Mathf.Abs(screenCenter.x) > 0.0001f) nx = offset.x / screenCenter.x;
        if (Mathf.Abs(screenCenter.y) > 0.0001f) ny = offset.y / screenCenter.y;

        float yaw = Mathf.Clamp(nx * browseMaxAngle, -browseMaxAngle, browseMaxAngle);
        float pitch = Mathf.Clamp(-ny * browseMaxAngle, -browseMaxAngle, browseMaxAngle);

        Quaternion browseRotation = Quaternion.Euler(pitch, yaw, 0f);
        targetLocalRotation = baseLocalRotationQ * browseRotation;

        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRotation, Time.deltaTime * browseSmoothing);
    }

    void LateUpdate()
    {
        cameraLookOffset = Vector3.Lerp(cameraLookOffset, targetCameraLookOffset, Time.deltaTime * cameraLookSmoothSpeed);

        if (!isBrowsing)
        {
            transform.localRotation = Quaternion.Slerp(transform.localRotation, baseLocalRotationQ, Time.deltaTime * browseReturnSpeed);
        }

        if (shakeRoutine == null)
        {
            transform.localPosition = baseLocalPosition + cameraLookOffset;
        }
    }

    public void UpdateCameraLook(Vector2 lookInput)
    {
        if (lookInput.sqrMagnitude > 0.0001f)
        {
            targetCameraLookOffset = new Vector3(lookInput.x, lookInput.y, 0f) * cameraLookOffsetAmount;
        }
        else
        {
            targetCameraLookOffset = Vector3.zero;
        }
    }

    private IEnumerator ShakeCamera()
    {
        float sampleInterval = camShakeInterval <= 0.0001f ? 0.05f : camShakeInterval;

        while (true)
        {
            Vector3 shakeOffset = new Vector3(
                Random.Range(-currentShakeIntensity, currentShakeIntensity),
                Random.Range(-currentShakeIntensity, currentShakeIntensity),
                0f
            );

            transform.localPosition = baseLocalPosition + shakeOffset + cameraLookOffset;

            yield return new WaitForSeconds(sampleInterval);
        }
    }

    private bool IsBrakingOrReversing(float actualThrust, float desiredThrust)
    {
        if (Mathf.Abs(actualThrust) < 0.5f)
        {
            return false;
        }

        float requestedChange = desiredThrust - actualThrust;
        return Mathf.Abs(requestedChange) > 0.0001f && Mathf.Sign(requestedChange) != Mathf.Sign(actualThrust);
    }
}
