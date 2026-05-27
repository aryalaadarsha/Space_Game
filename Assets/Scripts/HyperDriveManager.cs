using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HyperDriveManager : MonoBehaviour
{
    [SerializeField] private float hyperDriveChargeTime = 6f;
    [SerializeField] private float hyperDriveDuration = 5f;

    [SerializeField] private string NextSceneName = "SampleScene2"; // HyperDrive 후 로드할 씬 이름
    // HyperDrive 관련 로직을 여기에 구현
    public void ActivateHyperDrive()
    {
        // HyperDrive 활성화 로직
        StartCoroutine(ChargeAndRun());
    }


    private IEnumerator ChargeAndRun()
    {
        float elapsedTime = 0f;
        // HyperDrive 충전 로직
        while (elapsedTime < hyperDriveChargeTime)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
            Debug.Log($"Charging HyperDrive: {elapsedTime:F2}/{hyperDriveChargeTime} seconds");
        }
        Debug.Log("HyperDrive Activated!");
        // HyperDrive 작동 로직
        elapsedTime = 0f;
        while (elapsedTime < hyperDriveDuration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
            Debug.Log($"Running HyperDrive: {elapsedTime:F2}/{hyperDriveDuration} seconds");
        }

        StartCoroutine(ExitHyperDrive());
    }

    private IEnumerator ExitHyperDrive()
    {
        // HyperDrive 종료 로직
        Debug.Log("Exiting HyperDrive...");
        
        SceneManager.LoadScene(NextSceneName);
        yield return null;
    }
}
