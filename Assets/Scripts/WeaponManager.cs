using System.Collections;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [SerializeField] private GameObject weaponObj1;
    [SerializeField] private GameObject weaponObj2;

    [SerializeField] private float releaseLength = 1f;
    [SerializeField] private float releaseTime = 1f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ReleaseWeapon();
    }

    private void ReleaseWeapon()
    {
        StartCoroutine(ReleaseWeaponAnim());
    }

    private IEnumerator ReleaseWeaponAnim()
    {
        float elapsedTime = 0f;
        while (elapsedTime < releaseTime)
        {
            transform.localPosition = Vector3.Lerp(Vector3.zero, releaseLength * Vector3.forward, elapsedTime / releaseTime);
            elapsedTime += Time.deltaTime;
            
            yield return null;
        }
        
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator ReturnWeaponAnim()
    {
        float elapsedTime = 0f;
        while (elapsedTime < releaseTime)
        {
            transform.localPosition = Vector3.Lerp(releaseLength * Vector3.forward, Vector3.zero, elapsedTime / releaseTime);
            elapsedTime += Time.deltaTime;
            
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);
    }
}
