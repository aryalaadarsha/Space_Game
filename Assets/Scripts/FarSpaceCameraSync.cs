using UnityEngine;

public class FarSpaceCameraSync : MonoBehaviour
{
    [Header("카메라 연결")]
    public Transform mainCamera; // 우주선에 붙어있는 Main Camera를 연결하세요.

    [Header("스케일 설정")]
    [Tooltip("숫자가 작을수록 행성이 엄청나게 멀리 있는 것처럼 보입니다. (예: 0.001)")]
    public float movementScale = 0.001f; 

    void LateUpdate()
    {
        if (mainCamera == null) return;

        // 1. 회전은 100% 똑같이 따라갑니다. (고개를 돌리면 별도 돌아야 함)
        transform.rotation = mainCamera.rotation;

        // 2. 이동은 극단적으로 축소해서 따라갑니다. (위 시뮬레이터에서 본 원리)
        transform.position = mainCamera.position * movementScale;
    }
}