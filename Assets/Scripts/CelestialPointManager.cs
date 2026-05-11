using System.Collections.Generic;
using UnityEngine;

public class CelestialPointManager : MonoBehaviour
{
    [SerializeField] private GameObject cam;
    [SerializeField] private GameObject pointSphere;
    [SerializeField] private GameObject rayCasterOrigin;

    [SerializeField] private List<GameObject> celestialPoints;
    

    private Ray point_Ray;
    private RaycastHit point_RayHit;
    private float rayDistance = 100f;

    private void Update()
    {
        if (cam == null || pointSphere == null || rayCasterOrigin == null)
        {
            return;
        }

        SetPointSphere(pointSphere);
        point_Ray = new Ray(rayCasterOrigin.transform.position, rayCasterOrigin.transform.forward);

        if (Physics.Raycast(point_Ray, out point_RayHit, rayDistance))
        {
            if (IsCelestialPoint(point_RayHit.collider.gameObject))
            {
                Debug.Log("Hit Celestial Point: " + point_RayHit.collider.gameObject.name);
            }
        }
    }

    public void SetPointSphere(GameObject _pointSphere)
    {
        _pointSphere.transform.position = cam.transform.position;
        // keep rotation unchanged so pointSphere remains fixed
    }

    private bool IsCelestialPoint(GameObject hitObject)
    {
        if (celestialPoints == null || celestialPoints.Count == 0)
        {
            return true;
        }

        foreach (GameObject celestialPoint in celestialPoints)
        {
            if (celestialPoint == null)
            {
                continue;
            }

            if (hitObject == celestialPoint || hitObject.transform.IsChildOf(celestialPoint.transform))
            {
                return true;
            }
        }

        return false;
    }
    
    private void OnDrawGizmos()
    {
        if (point_RayHit.collider != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(point_Ray.origin, point_Ray.origin + point_Ray.direction * rayDistance);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(point_Ray.origin, point_Ray.origin + point_Ray.direction * rayDistance);
        }
    }
}
