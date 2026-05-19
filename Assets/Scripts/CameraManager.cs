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

    public void OnThrustChanged(float actualThrust, float desiredThrust)
    {
        float thrustDifference = Mathf.Abs(desiredThrust - actualThrust);
        float shakeIntensity = thrustDifference;

        if (shakeIntensity > camShakeThreshold)
        {
            StartShake(shakeIntensity * camShakeIntensityModifier);
            // Debug.Log($"Thrust changed: Actual={actualThrust:F2}, Desired={desiredThrust:F2}, ShakeIntensity={shakeIntensity:F2}");
        }
        else
        {
            StopShake();
            // Debug.Log($"Thrust changed: Actual={actualThrust:F2}, Desired={desiredThrust:F2}, No shake (intensity {shakeIntensity:F2} below threshold)");
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

        transform.localPosition = baseLocalPosition + cameraLookOffset;
    }

    private IEnumerator ShakeCamera()
    {
        Debug.Log($"Camera shake coroutine started with intensity: {currentShakeIntensity}");
        float sampleInterval = camShakeInterval <= 0.0001f ? 0.05f : camShakeInterval;

        while (true)
        {
            Vector3 shakeOffset = new Vector3(
                Random.Range(-currentShakeIntensity, currentShakeIntensity),
                Random.Range(-currentShakeIntensity, currentShakeIntensity),
                0f
            ) * 0.01f;

            transform.localPosition = baseLocalPosition + shakeOffset + cameraLookOffset;

            yield return new WaitForSeconds(sampleInterval);
        }
    }

    // Called when entering browse (look-around) mode
    public void StartBrowse()
    {
        isBrowsing = true;
        // initialize target to current so transition is smooth
        baseLocalRotationQ = transform.localRotation;
        targetLocalRotation = baseLocalRotationQ;
    }

    // Called when exiting browse mode
    public void StopBrowse()
    {
        isBrowsing = false;
        targetLocalRotation = baseLocalRotationQ;
    }

    // Update browse orientation based on absolute mouse position
    public void UpdateBrowse(Vector2 mousePosition)
    {
        if (!isBrowsing) return;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 offset = mousePosition - screenCenter;

        float nx = 0f;
        float ny = 0f;
        if (Mathf.Abs(screenCenter.x) > 0.0001f) nx = offset.x / screenCenter.x; // -1..1
        if (Mathf.Abs(screenCenter.y) > 0.0001f) ny = offset.y / screenCenter.y; // -1..1

        float yaw = Mathf.Clamp(nx * browseMaxAngle, -browseMaxAngle, browseMaxAngle);
        float pitch = Mathf.Clamp(-ny * browseMaxAngle, -browseMaxAngle, browseMaxAngle);

        Quaternion browseRotation = Quaternion.Euler(pitch, yaw, 0f);
        targetLocalRotation = baseLocalRotationQ * browseRotation;

        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRotation, Time.deltaTime * browseSmoothing);
    }

    void LateUpdate()
    {
        // 카메라 look offset 부드럽게 적용
        cameraLookOffset = Vector3.Lerp(cameraLookOffset, targetCameraLookOffset, Time.deltaTime * cameraLookSmoothSpeed);

        if (!isBrowsing)
        {
            // smoothly return to base rotation when not browsing
            transform.localRotation = Quaternion.Slerp(transform.localRotation, baseLocalRotationQ, Time.deltaTime * browseReturnSpeed);
        }
    }

    // 카메라 방향 입력 처리 (Q/E/R/F)
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
}
