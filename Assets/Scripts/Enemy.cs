using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Enemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;

    [Header("Drift Settings")]
    [SerializeField] private float driftSpeed = 4f;
    [SerializeField] private float facingTurnSpeed = 6f;

    private Rigidbody rb;
    private Vector3 driftDirection = Vector3.right;
    private bool driftInitialized;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 0.15f;
        rb.angularDamping = Mathf.Max(rb.angularDamping, 1f);
        ResolvePlayerTransform();
    }

    public void Initialize(Transform player)
    {
        InitializeSlowDrift(player, transform.right, driftSpeed);
    }

    public void InitializeSlowDrift(Transform player, Vector3 worldDriftDirection, float speed)
    {
        playerTransform = player;
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        rb.useGravity = false;
        driftDirection = worldDriftDirection.sqrMagnitude > 0.0001f
            ? worldDriftDirection.normalized
            : transform.right;
        driftSpeed = Mathf.Max(0f, speed);
        driftInitialized = true;
    }

    private void FixedUpdate()
    {
        if (playerTransform == null)
        {
            ResolvePlayerTransform();
        }

        if (!driftInitialized)
        {
            InitializeSlowDrift(playerTransform, transform.right, driftSpeed);
        }

        rb.linearVelocity = driftDirection * driftSpeed;
        rb.angularVelocity = Vector3.zero;
        FacePlayer();
    }

    private void FacePlayer()
    {
        if (playerTransform == null)
        {
            return;
        }

        Vector3 lookDirection = playerTransform.position - rb.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 up = playerTransform.up.sqrMagnitude > 0.0001f ? playerTransform.up.normalized : Vector3.up;
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, facingTurnSpeed * Time.fixedDeltaTime));
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
