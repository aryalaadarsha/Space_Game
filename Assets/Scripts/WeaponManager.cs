using System.Collections;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [SerializeField] private GameObject weaponObj1;
    [SerializeField] private GameObject weaponObj2;

    [SerializeField] private float releaseLength = 1f;
    [SerializeField] private float releaseTime = 1f;
    [SerializeField] private float overshootStrength = 1.25f;
    [SerializeField] private float rattleAmplitude = 0.06f;
    [SerializeField] private float rattleFrequency = 18f;
    [SerializeField] private float settleTime = 0.18f;
    [SerializeField] private float settleDamping = 12f;
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
        yield return AnimateWeapon(Vector3.zero, releaseLength * Vector3.forward);
    }

    private IEnumerator RetractWeaponAnim()
    {
        yield return AnimateWeapon(releaseLength * Vector3.forward, Vector3.zero);
    }

    private IEnumerator AnimateWeapon(Vector3 startPos, Vector3 endPos)
    {
        float elapsedTime = 0f;
        Vector3 axis = (endPos - startPos).sqrMagnitude > 0.0001f ? (endPos - startPos).normalized : Vector3.forward;

        while (elapsedTime < releaseTime)
        {
            float t = Mathf.Clamp01(elapsedTime / releaseTime);
            float easedT = EaseOutBack(t, overshootStrength);

            float decay = 1f - t;
            float rattle = Mathf.Sin(t * Mathf.PI * 2f * rattleFrequency) * rattleAmplitude * decay;
            transform.localPosition = Vector3.LerpUnclamped(startPos, endPos, easedT) + axis * rattle;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        float settleElapsed = 0f;
        while (settleElapsed < settleTime)
        {
            float t = settleElapsed / settleTime;
            float wobble = Mathf.Sin(t * Mathf.PI * 8f) * (rattleAmplitude * 0.5f) * Mathf.Exp(-settleDamping * t);
            transform.localPosition = endPos + axis * wobble;

            settleElapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = endPos;
        yield return new WaitForSeconds(0.06f);
    }

    private float EaseOutBack(float t, float strength)
    {
        float x = t - 1f;
        return 1f + (strength + 1f) * x * x * x + strength * x * x;
    }
}
