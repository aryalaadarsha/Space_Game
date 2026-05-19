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
    [SerializeField] private float followDistance = 50f;  // 이 거리를 유지하려고 시도
    [SerializeField] private float detectionRange = 200f; // 이 범위 내에서만 플레이어를 추적
    [SerializeField] private float stoppingDistance = 30f; // 이 거리 이내면 전진 멈춤
    [SerializeField] private float turnSmoothTime = 0.5f;  // 회전 부드러움

    private Rigidbody rb;
    private bool playerDetected = false;
    private float actualThrust = 0f;
    private float desiredThrust = 0f;
    private Vector3 lastLookDirection = Vector3.forward;
    
    // 우회 시스템 - 포물선 궤적
    private float evadeTimer = 0f;
    private float evadeDuration = 5f; // 우회 지속 시간
    private float evadeOrbitAngle = 0f; // 현재 궤도 각도
    private float evadeOrbitAngleSpeed = 60f; // 궤도 각도 변화 속도 (도/초)
    private float evadeOrbitRadius = 40f; // 궤도 반경
    private float evadeSideDirection = 1f; // 좌(1) 또는 우(-1) 방향

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // 플레이어를 찾지 못한 경우 자동으로 검색
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }
    }

    void Update()
    {
        if (playerTransform == null)
            return;

        // 플레이어와의 거리 계산
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        playerDetected = distanceToPlayer < detectionRange;
    }

    void FixedUpdate()
    {
        if (playerTransform == null || !playerDetected)
        {
            // 플레이어를 감지하지 못한 경우 천천히 감속
            desiredThrust = Mathf.Lerp(desiredThrust, 0f, Time.fixedDeltaTime * 2f);
            evadeTimer = 0f;
            UpdateThrust();
            ApplyTranslation(Vector2.zero);
            return;
        }

        // 플레이어 방향 계산
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // AI 이동 결정
        Vector2 strafeInput = CalculateStrafeInput(directionToPlayer, distanceToPlayer);
        float thrustInput = CalculateThrustInput(distanceToPlayer, directionToPlayer);

        UpdateThrust(thrustInput);
        ApplyTranslation(strafeInput);
        LookAtPlayer(directionToPlayer);
    }

    private Vector2 CalculateStrafeInput(Vector3 directionToPlayer, float distanceToPlayer)
    {
        Vector2 strafeInput = Vector2.zero;
        
        // 플레이어에게 너무 가까우면 우회 시작
        if (distanceToPlayer < stoppingDistance)
        {
            // 우회 시작
            if (evadeTimer <= 0f)
            {
                // 새로운 우회 시작: 좌/우 랜덤 선택
                evadeSideDirection = Random.value > 0.5f ? 1f : -1f;
                evadeOrbitAngle = 0f;
                evadeTimer = evadeDuration;
            }
            
            // 궤도 각도 업데이트
            evadeOrbitAngle += evadeOrbitAngleSpeed * evadeSideDirection * Time.fixedDeltaTime;
            
            // 플레이어 기준 로컬 좌표계에서 포물선/원형 궤적 계산
            float angleRad = evadeOrbitAngle * Mathf.Deg2Rad;
            
            // Y(높이)도 변함 - 포물선 모양
            float heightOffset = Mathf.Sin(angleRad * 0.5f) * 20f; // 상하 변화
            
            // 플레이어의 로컬 좌표계 기준 오프셋
            Vector3 orbitalOffset = new Vector3(
                Mathf.Cos(angleRad) * evadeOrbitRadius,
                heightOffset,
                Mathf.Sin(angleRad) * evadeOrbitRadius
            );
            
            // 목표 위치 (월드 좌표) - 플레이어의 현재 위치를 중심으로 우회
            Vector3 targetPosition = playerTransform.position + orbitalOffset;
            Vector3 directionToTarget = (targetPosition - transform.position).normalized;
            
            // 월드 벡터를 로컬 좌표계로 변환
            Vector3 localDirection = Quaternion.Inverse(transform.rotation) * directionToTarget;
            
            // 연속값 스트래프 입력 (더 부드러운 이동)
            strafeInput = new Vector2(localDirection.x, localDirection.y) * 0.8f;
            strafeInput = new Vector2(Mathf.Clamp(strafeInput.x, -1f, 1f), Mathf.Clamp(strafeInput.y, -1f, 1f));
            
            evadeTimer -= Time.fixedDeltaTime;
        }
        else
        {
            // 월드 벡터를 로컬 좌표계로 변환
            Vector3 localDirection = Quaternion.Inverse(transform.rotation) * directionToPlayer;
            
            // 연속값 스트래프 입력
            strafeInput = new Vector2(localDirection.x, localDirection.y) * 0.3f;
            strafeInput = new Vector2(Mathf.Clamp(strafeInput.x, -1f, 1f), Mathf.Clamp(strafeInput.y, -1f, 1f));

            evadeTimer = 0f; // 우회 타이머 리셋
        }
        
        return strafeInput;
    }

    private float CalculateThrustInput(float distanceToPlayer, Vector3 directionToPlayer)
    {
        // 플레이어와의 거리에 따라 추력 결정 (부드러운 전환)
        if (distanceToPlayer > followDistance)
        {
            // 플레이어가 멀면 가속
            return 1f;
        }
        else if (distanceToPlayer < stoppingDistance)
        {
            // 플레이어가 가까우면 우회하면서 추진력 유지
            return 0.5f;
        }
        else
        {
            // 적당한 거리에서는 followDistance 기준으로 선형 보간
            float ratio = (distanceToPlayer - stoppingDistance) / (followDistance - stoppingDistance);
            return Mathf.Lerp(0.5f, 0.3f, ratio);
        }
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
        // 전방향 속도
        Vector3 forwardVelocity = transform.forward * (actualThrust * thrustForce);
        
        // 기존 측면 속도 유지 (감쇄)
        Vector3 currentLateralVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, transform.forward);
        currentLateralVelocity *= 0.95f; // 약간의 감쇄로 안정성 향상
        
        // 새로운 선속도 설정
        rb.linearVelocity = currentLateralVelocity + forwardVelocity;

        // 측면 힘 적용
        Vector3 strafeVec = new Vector3(strafeInput.x, strafeInput.y, 0);
        rb.AddRelativeForce(strafeVec * strafeForce * Time.fixedDeltaTime, ForceMode.Acceleration);
    }

    private void LookAtPlayer(Vector3 directionToPlayer)
    {
        // 현재 기함이 바라보는 방향과 목표 방향을 부드럽게 보간
        lastLookDirection = Vector3.Lerp(lastLookDirection, directionToPlayer, Time.fixedDeltaTime / turnSmoothTime);
        lastLookDirection.Normalize();
        
        // 목표 회전 계산
        Quaternion targetRotation = Quaternion.LookRotation(lastLookDirection);
        
        // 현재 회전과 목표 회전의 차이 계산
        Quaternion rotationDifference = targetRotation * Quaternion.Inverse(transform.rotation);
        
        // 쿼터니언을 오일러 각도로 변환하여 토크 적용
        rotationDifference.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f)
            angle -= 360f;

        // 각도를 라디안으로 변환
        float angleRad = angle * Mathf.Deg2Rad;
        Vector3 torque = axis * angleRad * rotationSpeed * Time.fixedDeltaTime;
        rb.AddTorque(torque, ForceMode.VelocityChange);
        rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, 2f);
    }
}
