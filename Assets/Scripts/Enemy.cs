using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;

    [Header("Movement Settings")]
    [SerializeField] private float thrustForce = 0.8f;
    [SerializeField] private float thrustLerpSpeed = 0.3f;
    [SerializeField] private float strafeForce = 20f;
    [SerializeField] private float rotationSpeed = 2f;

    [Header("AI Behavior Settings")]
    [SerializeField] private float followDistance = 50f;
    [SerializeField] private float detectionRange = 200f;
    [SerializeField] private float stoppingDistance = 30f;
    [SerializeField] private float turnSmoothTime = 0.5f;

    private Rigidbody rb;
    private bool playerDetected;
    private float actualThrust;
    private float desiredThrust;
    private Vector3 lastLookDirection = Vector3.forward;

    private float evadeTimer;
    private float evadeDuration = 5f;
    private float evadeOrbitAngle;
    private float evadeOrbitAngleSpeed = 60f;
    private float evadeOrbitRadius = 40f;
    private float evadeSideDirection = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ResolvePlayerTransform();
    }

    public void Initialize(Transform player)
    {
        playerTransform = player;
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            ResolvePlayerTransform();
        }

        if (playerTransform == null)
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        playerDetected = distanceToPlayer < detectionRange;
    }

    private void FixedUpdate()
    {
        if (playerTransform == null || !playerDetected)
        {
            desiredThrust = Mathf.Lerp(desiredThrust, 0f, Time.fixedDeltaTime * 2f);
            evadeTimer = 0f;
            UpdateThrust();
            ApplyTranslation(Vector2.zero);
            return;
        }

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        Vector2 strafeInput = CalculateStrafeInput(directionToPlayer, distanceToPlayer);
        float thrustInput = CalculateThrustInput(distanceToPlayer);

        UpdateThrust(thrustInput);
        ApplyTranslation(strafeInput);
        LookAtPlayer(directionToPlayer);
    }

    private Vector2 CalculateStrafeInput(Vector3 directionToPlayer, float distanceToPlayer)
    {
        Vector2 strafeInput;

        if (distanceToPlayer < stoppingDistance)
        {
            if (evadeTimer <= 0f)
            {
                evadeSideDirection = Random.value > 0.5f ? 1f : -1f;
                evadeOrbitAngle = 0f;
                evadeTimer = evadeDuration;
            }

            evadeOrbitAngle += evadeOrbitAngleSpeed * evadeSideDirection * Time.fixedDeltaTime;
            float angleRad = evadeOrbitAngle * Mathf.Deg2Rad;
            float heightOffset = Mathf.Sin(angleRad * 0.5f) * 20f;
            Vector3 orbitalOffset = new Vector3(
                Mathf.Cos(angleRad) * evadeOrbitRadius,
                heightOffset,
                Mathf.Sin(angleRad) * evadeOrbitRadius);

            Vector3 targetPosition = playerTransform.position + orbitalOffset;
            Vector3 directionToTarget = (targetPosition - transform.position).normalized;
            Vector3 localDirection = Quaternion.Inverse(transform.rotation) * directionToTarget;
            strafeInput = new Vector2(localDirection.x, localDirection.y) * 0.8f;
            evadeTimer -= Time.fixedDeltaTime;
        }
        else
        {
            Vector3 localDirection = Quaternion.Inverse(transform.rotation) * directionToPlayer;
            strafeInput = new Vector2(localDirection.x, localDirection.y) * 0.3f;
            evadeTimer = 0f;
        }

        return new Vector2(Mathf.Clamp(strafeInput.x, -1f, 1f), Mathf.Clamp(strafeInput.y, -1f, 1f));
    }

    private float CalculateThrustInput(float distanceToPlayer)
    {
        if (distanceToPlayer > followDistance)
        {
            return 1f;
        }

        if (distanceToPlayer < stoppingDistance)
        {
            return 0.5f;
        }

        float ratio = (distanceToPlayer - stoppingDistance) / (followDistance - stoppingDistance);
        return Mathf.Lerp(0.5f, 0.3f, ratio);
    }

    private void UpdateThrust(float thrustInput = 0f)
    {
        if (Mathf.Abs(thrustInput) > 0.1f)
        {
            desiredThrust += thrustInput * Time.fixedDeltaTime * 15f;
        }

        desiredThrust = Mathf.Clamp(desiredThrust, -20f, 40f);
        actualThrust = Mathf.Lerp(actualThrust, desiredThrust, Time.fixedDeltaTime * thrustLerpSpeed);
    }

    private void ApplyTranslation(Vector2 strafeInput)
    {
        Vector3 forwardVelocity = transform.forward * (actualThrust * thrustForce);
        Vector3 currentLateralVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, transform.forward);
        currentLateralVelocity *= 0.95f;

        rb.linearVelocity = currentLateralVelocity + forwardVelocity;

        Vector3 strafeVec = new Vector3(strafeInput.x, strafeInput.y, 0f);
        rb.AddRelativeForce(strafeVec * strafeForce * Time.fixedDeltaTime, ForceMode.Acceleration);
    }

    private void LookAtPlayer(Vector3 directionToPlayer)
    {
        lastLookDirection = Vector3.Lerp(lastLookDirection, directionToPlayer, Time.fixedDeltaTime / turnSmoothTime);
        lastLookDirection.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(lastLookDirection);
        Quaternion rotationDifference = targetRotation * Quaternion.Inverse(transform.rotation);
        rotationDifference.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f)
        {
            angle -= 360f;
        }

        float angleRad = angle * Mathf.Deg2Rad;
        Vector3 torque = axis * angleRad * rotationSpeed * Time.fixedDeltaTime;
        rb.AddTorque(torque, ForceMode.VelocityChange);
        rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, 2f);
    }

    private void ResolvePlayerTransform()
    {
        if (playerTransform != null)
        {
            return;
        }

        GameObject player = null;
        try
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            player = null;
        }

        if (player == null)
        {
            player = GameObject.Find("Player");
        }

        if (player != null)
        {
            playerTransform = player.transform;
        }
    }
}
