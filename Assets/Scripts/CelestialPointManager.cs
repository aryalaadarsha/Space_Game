using UnityEngine;

public class CelestialPointManager : MonoBehaviour
{
    [SerializeField] private GameObject cam;
    [SerializeField] private GameObject pointSphere;

    private void Update()
    {
        if (pointSphere != null)
        {
            pointSphere.transform.position = cam.transform.position;
            pointSphere.transform.rotation = new Quaternion(0, 0, 0, 1);
        }
    }
}
