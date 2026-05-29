using UnityEngine;
using UnityEngine.InputSystem;

public class UniverseStateController : MonoBehaviour
{
    [Header("카메라 시스템")]
    [Tooltip("배경(먼 우주)을 찍는 카메라 오브젝트")]
    public Camera farCamera; 
    [Tooltip("시차(Parallax) 꼼수를 담당하는 스크립트")]
    public FarSpaceCameraSync farCameraSync; 

    [Header("환경(월드) 루트 오브젝트")]
    public GameObject normalSpaceRoot;  // 1:1 스케일의 일반 우주 (정거장 등)
    public GameObject supercruiseRoot;  // 수작업으로 만든 미니어처 우주

    private bool isSupercruise = false;

    void Start()
    {
        // 게임 시작 시 초기 상태는 Normal Space로 강제 설정
        SetNormalSpace();
    }

    void Update()
    {
        // H 키를 누를 때마다 상태 토글 (Input System 사용)
        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            isSupercruise = !isSupercruise;

            if (isSupercruise)
            {
                SetSupercruise();
            }
            else
            {
                SetNormalSpace();
            }
        }
    }

    private void SetSupercruise()
    {
        Debug.Log("초광속 항해(Supercruise) 진입: 미니어처 우주 활성화");

        // 1. 이중 카메라 꼼수 끄기 (이제 메인 카메라 하나로만 미니어처 공간을 이동함)
        if (farCamera != null) farCamera.enabled = false;
        if (farCameraSync != null) farCameraSync.enabled = false;

        // 2. 물리적 환경 스위칭
        normalSpaceRoot.SetActive(false);
        supercruiseRoot.SetActive(true);

        // TODO: 여기서 ShipController의 속도를 100배 늘리는 등의 코드 추가 가능

    }

    private void SetNormalSpace()
    {
        Debug.Log("일반 우주(Normal Space) 복귀: 이중 카메라 활성화");

        // 1. 이중 카메라 꼼수 켜기 (거대 행성을 배경으로 띄움)
        if (farCamera != null) farCamera.enabled = true;
        if (farCameraSync != null) farCameraSync.enabled = true;

        // 2. 물리적 환경 스위칭
        supercruiseRoot.SetActive(false);
        normalSpaceRoot.SetActive(true);

        // TODO: 여기서 ShipController의 속도를 원래대로 복구
        
    }
}