using UnityEngine;

public class Earth : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 15f; // Earth rotation speed in degrees per second

    void Update()
    {
        // Rotate the Earth around its own axis
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}
