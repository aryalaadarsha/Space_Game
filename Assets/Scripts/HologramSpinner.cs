using UnityEngine;

public sealed class HologramSpinner : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float rotationSpeed = 28f;
    [SerializeField] private float bobAmplitude = 0.015f;
    [SerializeField] private float bobFrequency = 1.4f;

    private Vector3 startLocalPosition;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        startLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);

        if (bobAmplitude <= 0f)
        {
            return;
        }

        var offset = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.localPosition = startLocalPosition + new Vector3(0f, offset, 0f);
    }
}
