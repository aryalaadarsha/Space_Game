using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TargetingManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform shipTransform;

    [Header("Target Search")]
    [SerializeField] private string targetTag = "TrackingObject";
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private float maxTargetDistance = 35000f;
    [SerializeField] private float maxAimAngle = 3.5f;
    [SerializeField] private float targetRefreshInterval = 0.5f;

    [Header("HUD")]
    [SerializeField] private Color targetColor = new Color(1f, 0.45f, 0.05f, 0.95f);
    [SerializeField] private Color lockedColor = new Color(1f, 0.78f, 0.16f, 1f);

    private readonly List<Transform> targetCandidates = new List<Transform>();
    private Transform currentTarget;
    private Transform lockedTarget;
    private float nextRefreshTime;

    private Canvas hudCanvas;
    private RectTransform hudCanvasRect;
    private RectTransform indicatorRoot;
    private TargetReticleGraphic reticleGraphic;
    private TMP_Text nameText;
    private TMP_Text distanceText;
    private TMP_Text lockText;
    private CanvasGroup indicatorGroup;
    private FarSpaceCameraSync farSpaceSync;

    public Transform CurrentTarget => currentTarget;
    public Transform LockedTarget => lockedTarget;
    public bool IsLocked => lockedTarget != null;

    public void Initialize(Transform ship, Camera cameraOverride)
    {
        if (shipTransform == null)
        {
            shipTransform = ship;
        }

        Camera viewingCamera = FindUnifiedViewingCamera();
        if (viewingCamera != null)
        {
            targetCamera = viewingCamera;
        }
        else if (targetCamera == null)
        {
            targetCamera = cameraOverride;
        }

        UpdateFarSpaceSyncReference();
    }

    private void Awake()
    {
        EnsureReferences();
        EnsureHud();
    }

    private void Update()
    {
        EnsureReferences();
        RefreshTargetsIfNeeded();

        if (lockedTarget != null && !IsTargetValid(lockedTarget))
        {
            UnlockTarget();
        }

        currentTarget = lockedTarget != null ? lockedTarget : FindTargetUnderAim();
        UpdateHud(currentTarget);
    }

    public void ToggleLock()
    {
        if (lockedTarget != null)
        {
            UnlockTarget();
            return;
        }

        RefreshTargets();
        currentTarget = FindTargetUnderAim();
        if (currentTarget != null)
        {
            lockedTarget = currentTarget;
        }
    }

    public void UnlockTarget()
    {
        lockedTarget = null;
    }

    public bool TryGetLockedDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        if (lockedTarget == null || targetCamera == null)
        {
            return false;
        }

        Vector3 toTarget = GetTargetPoint(lockedTarget) - targetCamera.transform.position;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        direction = toTarget.normalized;
        return true;
    }

    private void EnsureReferences()
    {
        if (shipTransform == null)
        {
            shipTransform = transform;
        }

        Camera viewingCamera = FindUnifiedViewingCamera();
        if (viewingCamera != null && targetCamera != viewingCamera)
        {
            targetCamera = viewingCamera;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            targetCamera = GetComponentInChildren<Camera>();
        }

        UpdateFarSpaceSyncReference();
    }

    private Camera FindUnifiedViewingCamera()
    {
        FarSpaceCameraSync farCameraSync = FindFirstObjectByType<FarSpaceCameraSync>();
        if (farCameraSync != null)
        {
            Camera syncedCamera = farCameraSync.GetComponent<Camera>();
            if (syncedCamera != null)
            {
                return syncedCamera;
            }
        }

        return Camera.main;
    }

    private void UpdateFarSpaceSyncReference()
    {
        farSpaceSync = targetCamera != null ? targetCamera.GetComponent<FarSpaceCameraSync>() : null;
    }

    private void RefreshTargetsIfNeeded()
    {
        if (Time.unscaledTime < nextRefreshTime && targetCandidates.Count > 0)
        {
            return;
        }

        RefreshTargets();
    }

    private void RefreshTargets()
    {
        nextRefreshTime = Time.unscaledTime + targetRefreshInterval;
        targetCandidates.Clear();

        GameObject[] taggedTargets;
        try
        {
            taggedTargets = GameObject.FindGameObjectsWithTag(targetTag);
        }
        catch (UnityException)
        {
            taggedTargets = new GameObject[0];
        }

        foreach (GameObject target in taggedTargets)
        {
            if (target != null && target.activeInHierarchy)
            {
                targetCandidates.Add(target.transform);
            }
        }
    }

    private Transform FindTargetUnderAim()
    {
        if (targetCamera == null)
        {
            return null;
        }

        Ray ray = new Ray(targetCamera.transform.position, targetCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxTargetDistance, targetLayers, QueryTriggerInteraction.Ignore))
        {
            Transform rayTarget = FindCandidateRoot(hit.transform);
            if (rayTarget != null)
            {
                return rayTarget;
            }
        }

        Transform bestTarget = null;
        float bestScore = float.MaxValue;
        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 cameraForward = targetCamera.transform.forward;

        foreach (Transform candidate in targetCandidates)
        {
            if (!IsTargetValid(candidate))
            {
                continue;
            }

            Vector3 targetPoint = GetTargetPoint(candidate);
            Vector3 toTarget = targetPoint - cameraPosition;
            float distance = toTarget.magnitude;
            if (distance < 0.001f || distance > maxTargetDistance)
            {
                continue;
            }

            Vector3 viewport = targetCamera.WorldToViewportPoint(targetPoint);
            if (viewport.z <= 0f || viewport.x < -0.1f || viewport.x > 1.1f || viewport.y < -0.1f || viewport.y > 1.1f)
            {
                continue;
            }

            float aimAngle = Vector3.Angle(cameraForward, toTarget / distance);
            float angularRadius = EstimateAngularRadius(candidate, cameraPosition);
            float allowedAngle = Mathf.Max(maxAimAngle, angularRadius * 1.25f);
            if (aimAngle > allowedAngle)
            {
                continue;
            }

            float score = aimAngle - angularRadius * 0.15f + distance * 0.00001f;
            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private Transform FindCandidateRoot(Transform hitTransform)
    {
        foreach (Transform candidate in targetCandidates)
        {
            if (candidate == null)
            {
                continue;
            }

            if (hitTransform == candidate || hitTransform.IsChildOf(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool IsTargetValid(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy || targetCamera == null)
        {
            return false;
        }

        return Vector3.Distance(targetCamera.transform.position, GetTargetPoint(target)) <= maxTargetDistance * 1.25f;
    }

    private float EstimateAngularRadius(Transform target, Vector3 viewerPosition)
    {
        Bounds bounds = new Bounds(target.position, Vector3.one * 2f);
        bool hasBounds = false;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        float radius = hasBounds ? bounds.extents.magnitude : Mathf.Max(target.lossyScale.x, target.lossyScale.y, target.lossyScale.z);
        float distance = Mathf.Max(0.001f, Vector3.Distance(viewerPosition, bounds.center));
        return Mathf.Atan2(radius, distance) * Mathf.Rad2Deg;
    }

    private Vector3 GetTargetPoint(Transform target)
    {
        if (TryGetTargetBounds(target, out Bounds bounds))
        {
            return bounds.center;
        }

        return target.position;
    }

    private bool TryGetTargetBounds(Transform target, out Bounds bounds)
    {
        bounds = new Bounds(target.position, Vector3.one * 2f);
        bool hasBounds = false;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private void EnsureHud()
    {
        if (indicatorRoot != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Targeting HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        hudCanvas = canvasObject.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 250;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        hudCanvasRect = canvasObject.GetComponent<RectTransform>();

        GameObject rootObject = new GameObject("Target Indicator", typeof(RectTransform), typeof(CanvasGroup));
        rootObject.transform.SetParent(canvasObject.transform, false);
        indicatorRoot = rootObject.GetComponent<RectTransform>();
        indicatorRoot.sizeDelta = new Vector2(260f, 112f);
        indicatorGroup = rootObject.GetComponent<CanvasGroup>();
        indicatorGroup.interactable = false;
        indicatorGroup.blocksRaycasts = false;

        GameObject ringObject = new GameObject("Reticle Ring", typeof(RectTransform), typeof(TargetReticleGraphic));
        ringObject.transform.SetParent(rootObject.transform, false);
        RectTransform ringRect = ringObject.GetComponent<RectTransform>();
        ringRect.anchorMin = new Vector2(0f, 0.5f);
        ringRect.anchorMax = new Vector2(0f, 0.5f);
        ringRect.pivot = new Vector2(0.5f, 0.5f);
        ringRect.anchoredPosition = new Vector2(54f, 0f);
        ringRect.sizeDelta = new Vector2(96f, 96f);
        reticleGraphic = ringObject.GetComponent<TargetReticleGraphic>();
        reticleGraphic.raycastTarget = false;

        nameText = CreateText(rootObject.transform, "Target Name", new Vector2(112f, 14f), new Vector2(170f, 26f), 20f, FontStyles.Bold);
        distanceText = CreateText(rootObject.transform, "Target Distance", new Vector2(112f, -14f), new Vector2(170f, 24f), 18f, FontStyles.Normal);
        lockText = CreateText(rootObject.transform, "Lock State", new Vector2(112f, 42f), new Vector2(170f, 22f), 16f, FontStyles.Bold);

        indicatorRoot.gameObject.SetActive(false);
    }

    private TMP_Text CreateText(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.raycastTarget = false;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = targetColor;
        return text;
    }

    private void UpdateHud(Transform target)
    {
        EnsureHud();

        if (target == null || targetCamera == null)
        {
            indicatorRoot.gameObject.SetActive(false);
            return;
        }

        Vector3 targetPoint = GetTargetPoint(target);
        Vector3 screenPosition = targetCamera.WorldToScreenPoint(targetPoint);
        if (screenPosition.z <= 0f)
        {
            indicatorRoot.gameObject.SetActive(false);
            return;
        }

        indicatorRoot.gameObject.SetActive(true);
        Color activeColor = IsLocked ? lockedColor : targetColor;
        reticleGraphic.color = activeColor;
        reticleGraphic.SetVerticesDirty();
        nameText.color = activeColor;
        distanceText.color = activeColor;
        lockText.color = activeColor;

        float pulse = IsLocked ? 1f + Mathf.Sin(Time.unscaledTime * 9f) * 0.035f : 1f;
        indicatorRoot.localScale = Vector3.one * pulse;

        nameText.text = target.name.ToUpperInvariant();
        distanceText.text = FormatDistance(GetNavigationDistance(targetPoint));
        lockText.text = IsLocked ? "LOCKED" : string.Empty;

        float margin = 70f;
        Vector2 clampedScreen = new Vector2(
            Mathf.Clamp(screenPosition.x, margin, Screen.width - margin),
            Mathf.Clamp(screenPosition.y, margin, Screen.height - margin)
        );

        RectTransformUtility.ScreenPointToLocalPointInRectangle(hudCanvasRect, clampedScreen, null, out Vector2 localPoint);
        indicatorRoot.anchoredPosition = localPoint;
        indicatorGroup.alpha = IsLocked ? 1f : 0.88f;
    }

    private string FormatDistance(float distance)
    {
        if (distance >= 1000f)
        {
            return $"{distance / 1000f:0.0} km";
        }

        return $"{distance:0} m";
    }

    private float GetNavigationDistance(Vector3 targetPoint)
    {
        if (targetCamera == null)
        {
            return 0f;
        }

        float visualDistance = Vector3.Distance(targetCamera.transform.position, targetPoint);
        if (farSpaceSync != null && farSpaceSync.movementScale > 0.0001f)
        {
            return visualDistance / farSpaceSync.movementScale;
        }

        return visualDistance;
    }
}
