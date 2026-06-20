using UnityEngine;
using UnityEngine.Serialization;

public class FarSpaceCameraSync : MonoBehaviour
{
    [Header("Camera Link")]
    [FormerlySerializedAs("mainCamera")]
    public Transform viewSourceCamera;

    [Header("Scale Settings")]
    [Tooltip("Smaller values make far-space objects appear farther away.")]
    public float movementScale = 0.001f;

    private void LateUpdate()
    {
        if (viewSourceCamera == null)
        {
            return;
        }

        transform.rotation = viewSourceCamera.rotation;
        transform.position = viewSourceCamera.position * movementScale;
    }
}
