using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class HyperDriveManager : MonoBehaviour
{
    [System.Serializable]
    public class HyperCruiseDestination
    {
        public string destinationName = "EARTH";
        public string sceneName = "Earth";
        public string targetObjectName = "Earth";
        public Vector3 galacticCoordinates;
        public Vector3 solarSystemPositionAu;
        public Color accentColor = new Color(1f, 0.48f, 0.04f, 1f);
        public bool enabled = true;
    }

    [Header("Destinations")]
    [SerializeField] private List<HyperCruiseDestination> destinations = new List<HyperCruiseDestination>();
    [SerializeField] private Vector3 currentGalacticCoordinates;
    [SerializeField] private Vector3 currentSolarSystemPositionAu = new Vector3(1f, 0f, 0f);
    [SerializeField] private float localDestinationDistanceAu = 0.01f;

    [Header("Warp Gauge")]
    [SerializeField] private float maximumWarpGauge = 100f;
    [SerializeField] private float startingWarpGauge = 72f;
    [SerializeField] private float warpRechargeRate = 2.5f;
    [SerializeField] private float baseWarpCost = 16f;
    [SerializeField] private float costPerCoordinateUnit = 0.62f;
    [SerializeField] private float costPerAstronomicalUnit = 6f;
    [SerializeField] private float maximumWarpCost = 96f;

    [Header("HUD")]
    [SerializeField] private bool createHudOnStart = true;
    [SerializeField] private int hudSortingOrder = 280;

    [Header("Hyper Cruise Transition")]
    [SerializeField] private float normalLensDistortionIntensity = -0.2f;
    [SerializeField] private float hyperCruiseLensDistortionIntensity = -0.78f;
    [SerializeField] private float departureDistortionDuration = 4f;
    [SerializeField] private float arrivalDistortionDuration = 4f;
    [SerializeField] private float hyperCruiseThrust = 70f;
    [SerializeField] private float arrivalExitThrust = 0f;
    [SerializeField] private float arrivalLookSuppressionDuration = 1.25f;
    [SerializeField] private float turnTowardTargetSpeed = 85f;
    [SerializeField] private float turnCompletionAngle = 2f;
    [SerializeField] private float minimumTurnDuration = 0.75f;
    [SerializeField] private float maximumTurnDuration = 3f;
    [SerializeField] private float arrivalDisplayedDistanceKilometers = 50000f;
    [SerializeField] private float arrivalDistanceFromTarget = 120f;
    [SerializeField] private float arrivalRadiusMultiplier = 2.35f;
    [SerializeField] private bool ignoreDestinationCollisionOnArrival = true;
    [SerializeField] private string farSpaceLayerName = "FarSpace";
    [SerializeField] private string trackingTargetTag = "TrackingObject";

    private readonly List<Button> destinationButtons = new List<Button>();
    private readonly List<TMP_Text> destinationLabels = new List<TMP_Text>();
    private readonly List<TMP_Text> destinationMetaLabels = new List<TMP_Text>();

    private float currentWarpGauge;
    private int selectedDestinationIndex;
    private bool isJumping;

    private Canvas hudCanvas;
    private RectTransform destinationPanel;
    private Image destinationPanelGlow;
    private TMP_Text destinationHeaderText;
    private TMP_Text selectedInfoText;
    private RectTransform warpFillRect;
    private Image warpFillImage;
    private TMP_Text warpGaugeText;
    private TMP_Text warpStatusText;

    private static readonly Color NeonOrange = new Color(1f, 0.43f, 0.02f, 1f);
    private static readonly Color DimOrange = new Color(1f, 0.24f, 0f, 0.36f);
    private static readonly Color NeonBlue = new Color(0.05f, 0.78f, 1f, 1f);
    private static readonly Color PanelDark = new Color(0.035f, 0.018f, 0.006f, 0.72f);

    public float WarpGauge01 => maximumWarpGauge > 0f ? Mathf.Clamp01(currentWarpGauge / maximumWarpGauge) : 0f;
    public bool IsJumping => isJumping;

    private void Awake()
    {
        currentWarpGauge = Mathf.Clamp(startingWarpGauge, 0f, maximumWarpGauge);
        EnsureDefaultDestinations();
    }

    private void Start()
    {
        RefreshCurrentLocationFromNearestDestination();
        SelectFirstRemoteDestination();
        ConfigureOutsideOnlyPostProcessing();
        SetLensDistortionIntensity(normalLensDistortionIntensity);

        if (createHudOnStart)
        {
            EnsureHud();
            RefreshHud();
        }
    }

    private void Update()
    {
        if (!isJumping)
        {
            currentWarpGauge = Mathf.MoveTowards(currentWarpGauge, maximumWarpGauge, warpRechargeRate * Time.deltaTime);
        }

        RefreshHud();
    }

    public void ActivateHyperDrive()
    {
        TryStartHyperCruise();
    }

    public bool TryStartHyperCruise()
    {
        if (isJumping || !TryGetSelectedDestination(out HyperCruiseDestination destination))
        {
            return false;
        }

        if (IsLocalDestination(destination))
        {
            SetStatus("ALREADY IN LOCAL ORBIT", DimOrange);
            return false;
        }

        Transform target = FindDestinationTarget(destination);
        if (target == null)
        {
            SetStatus("DESTINATION TARGET OFFLINE", new Color(1f, 0.18f, 0.04f, 1f));
            return false;
        }

        float cost = GetWarpCost(destination);
        if (currentWarpGauge < cost)
        {
            SetStatus("INSUFFICIENT WARP CHARGE", new Color(1f, 0.18f, 0.04f, 1f));
            return false;
        }

        StartCoroutine(RunHyperCruiseRoutine(destination, target, cost));
        return true;
    }

    private IEnumerator RunHyperCruiseRoutine(HyperCruiseDestination destination, Transform target, float cost)
    {
        ShipController shipController = FindFirstObjectByType<ShipController>();
        if (shipController == null || target == null)
        {
            SetStatus("HYPER CRUISE OFFLINE", new Color(1f, 0.18f, 0.04f, 1f));
            yield break;
        }

        isJumping = true;
        currentWarpGauge = Mathf.Max(0f, currentWarpGauge - cost);
        SetStatus("HYPER CRUISE JUMP", NeonBlue);
        RefreshHud();
        ConfigureOutsideOnlyPostProcessing();

        TargetingManager targetingManager = FindFirstObjectByType<TargetingManager>();
        if (targetingManager != null)
        {
            targetingManager.UnlockTarget();
        }

        bool restoreFloatingOrigin = shipController.UseFloatingOrigin;
        shipController.SetHyperCruiseOverrideActive(true, true);
        SetStatus("ALIGNING VECTOR", NeonBlue);
        yield return TurnShipTowardDestination(shipController, target);

        shipController.SetFloatingOriginEnabled(false);
        SetStatus("HYPER CRUISE JUMP", NeonBlue);

        float currentIntensity = GetLensDistortionIntensity(normalLensDistortionIntensity);
        yield return AnimateLensDistortionAndMove(
            currentIntensity,
            hyperCruiseLensDistortionIntensity,
            departureDistortionDuration,
            shipController,
            target);

        MoveShipNearDestination(shipController, target);

        if (restoreFloatingOrigin)
        {
            shipController.SetFloatingOriginEnabled(true);
        }

        currentGalacticCoordinates = destination.galacticCoordinates;
        currentSolarSystemPositionAu = GetSolarSystemPositionAu(destination);
        SelectFirstRemoteDestination();
        yield return AnimateLensDistortion(hyperCruiseLensDistortionIntensity, normalLensDistortionIntensity, arrivalDistortionDuration);
        if (targetingManager != null)
        {
            targetingManager.UnlockTarget();
        }

        StabilizeArrivalView(shipController, target);
        shipController.SetHyperCruiseOverrideActive(false);
        isJumping = false;
        SetStatus("HYPER CRUISE READY", NeonOrange);
        RefreshHud();
    }

    private void EnsureDefaultDestinations()
    {
        if (destinations.Count > 0)
        {
            return;
        }

        destinations.Add(new HyperCruiseDestination
        {
            destinationName = "EARTH",
            sceneName = "Earth",
            targetObjectName = "Earth",
            galacticCoordinates = new Vector3(0f, 0f, 0f),
            solarSystemPositionAu = new Vector3(1f, 0f, 0f),
            accentColor = new Color(0.12f, 0.78f, 1f, 1f)
        });
        destinations.Add(new HyperCruiseDestination
        {
            destinationName = "SATURN",
            sceneName = "Saturn",
            targetObjectName = "Saturn",
            galacticCoordinates = new Vector3(74f, 8f, 39f),
            solarSystemPositionAu = new Vector3(9.58f, 0f, 0f),
            accentColor = new Color(1f, 0.55f, 0.08f, 1f)
        });
    }

    private void RefreshCurrentLocationFromNearestDestination()
    {
        float bestDistance = float.MaxValue;
        HyperCruiseDestination nearestDestination = null;

        for (int i = 0; i < destinations.Count; i++)
        {
            HyperCruiseDestination destination = destinations[i];
            Transform target = FindDestinationTarget(destination);
            if (destination == null || target == null)
            {
                continue;
            }

            float distance = GetNavigationDistanceToTarget(target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearestDestination = destination;
            }
        }

        if (nearestDestination != null && bestDistance <= GetLocalOrbitDistance(nearestDestination))
        {
            currentGalacticCoordinates = nearestDestination.galacticCoordinates;
            currentSolarSystemPositionAu = GetSolarSystemPositionAu(nearestDestination);
        }
    }

    private void SelectFirstRemoteDestination()
    {
        for (int i = 0; i < destinations.Count; i++)
        {
            if (destinations[i] != null && destinations[i].enabled && !IsLocalDestination(destinations[i]))
            {
                selectedDestinationIndex = i;
                return;
            }
        }

        selectedDestinationIndex = Mathf.Clamp(selectedDestinationIndex, 0, Mathf.Max(0, destinations.Count - 1));
    }

    private bool TryGetSelectedDestination(out HyperCruiseDestination destination)
    {
        destination = null;
        if (destinations.Count == 0)
        {
            return false;
        }

        selectedDestinationIndex = Mathf.Clamp(selectedDestinationIndex, 0, destinations.Count - 1);
        destination = destinations[selectedDestinationIndex];
        return destination != null && destination.enabled;
    }

    private bool IsLocalDestination(HyperCruiseDestination destination)
    {
        return destination != null && GetDistanceAu(destination) <= Mathf.Max(0.000001f, localDestinationDistanceAu);
    }

    private float GetWarpCost(HyperCruiseDestination destination)
    {
        float distanceAu = GetDistanceAu(destination);
        float costPerUnit = costPerAstronomicalUnit > 0.0001f ? costPerAstronomicalUnit : costPerCoordinateUnit;
        return Mathf.Clamp(baseWarpCost + distanceAu * costPerUnit, 0f, maximumWarpCost);
    }

    private float GetDistance(HyperCruiseDestination destination)
    {
        return GetDistanceAu(destination);
    }

    private float GetDistanceAu(HyperCruiseDestination destination)
    {
        return Vector3.Distance(currentSolarSystemPositionAu, GetSolarSystemPositionAu(destination));
    }

    private double GetDistanceKilometers(HyperCruiseDestination destination)
    {
        return GetDistanceAu(destination) * SpaceDistanceUtility.KilometersPerAstronomicalUnit;
    }

    private Vector3 GetSolarSystemPositionAu(HyperCruiseDestination destination)
    {
        if (destination == null)
        {
            return currentSolarSystemPositionAu;
        }

        if (destination.solarSystemPositionAu.sqrMagnitude > 0.0001f)
        {
            return destination.solarSystemPositionAu;
        }

        string normalizedName = NormalizeDestinationName(destination.destinationName);
        if (normalizedName == "EARTH")
        {
            return new Vector3(1f, 0f, 0f);
        }

        if (normalizedName == "SATURN")
        {
            return new Vector3(9.58f, 0f, 0f);
        }

        float legacyDistance = destination.galacticCoordinates.magnitude;
        return legacyDistance > 0.0001f
            ? new Vector3(legacyDistance, 0f, 0f)
            : Vector3.zero;
    }

    private void SelectDestination(int index)
    {
        if (isJumping)
        {
            return;
        }

        selectedDestinationIndex = Mathf.Clamp(index, 0, destinations.Count - 1);
        SetStatus("DESTINATION SELECTED", NeonOrange);
        RefreshHud();
    }

    private void SelectAndStartDestination(int index)
    {
        SelectDestination(index);
        TryStartHyperCruise();
    }

    private void EnsureHud()
    {
        if (hudCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Hyper Cruise HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        hudCanvas = canvasObject.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = hudSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CreateDestinationPanel(canvasObject.transform);
        CreateWarpGauge(canvasObject.transform);
    }

    private void CreateDestinationPanel(Transform parent)
    {
        GameObject panelObject = new GameObject("B_HyperCruise_DestinationPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        destinationPanel = panelObject.GetComponent<RectTransform>();
        destinationPanel.anchorMin = new Vector2(1f, 1f);
        destinationPanel.anchorMax = new Vector2(1f, 1f);
        destinationPanel.pivot = new Vector2(1f, 1f);
        destinationPanel.anchoredPosition = new Vector2(-82f, -76f);
        destinationPanel.sizeDelta = new Vector2(430f, 214f);
        destinationPanel.localRotation = Quaternion.Euler(0f, 0f, -5.5f);

        destinationPanelGlow = panelObject.GetComponent<Image>();
        destinationPanelGlow.color = PanelDark;
        destinationPanelGlow.raycastTarget = false;

        AddLine(destinationPanel, "Top Neon Rail", new Vector2(0.5f, 1f), new Vector2(0f, -13f), new Vector2(410f, 3f), NeonOrange);
        AddLine(destinationPanel, "Header Rail", new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(385f, 2f), DimOrange);
        AddLine(destinationPanel, "Bottom Neon Rail", new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(410f, 3f), DimOrange);
        AddLine(destinationPanel, "Left Cut Rail", new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(3f, 150f), DimOrange);

        destinationHeaderText = CreateText(destinationPanel, "Hyper Cruise Header", new Vector2(24f, -22f), new Vector2(210f, 28f), 19f, FontStyles.Bold, TextAlignmentOptions.Left);
        destinationHeaderText.text = "HYPER CRUISE";

        selectedInfoText = CreateText(destinationPanel, "Selected Route Info", new Vector2(222f, -22f), new Vector2(178f, 28f), 14f, FontStyles.Bold, TextAlignmentOptions.Right);

        for (int i = 0; i < destinations.Count; i++)
        {
            CreateDestinationButton(i);
        }
    }

    private void CreateDestinationButton(int index)
    {
        GameObject buttonObject = new GameObject($"Destination_{index:00}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(destinationPanel, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -62f - index * 45f);
        rect.sizeDelta = new Vector2(-44f, 36f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 0.32f, 0f, 0.11f);

        Button button = buttonObject.GetComponent<Button>();
        int destinationIndex = index;
        button.onClick.AddListener(() => SelectAndStartDestination(destinationIndex));
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 0.42f, 0f, 0.2f);
        colors.highlightedColor = new Color(1f, 0.55f, 0.08f, 0.42f);
        colors.pressedColor = new Color(1f, 0.78f, 0.16f, 0.58f);
        colors.disabledColor = new Color(0.35f, 0.12f, 0f, 0.16f);
        button.colors = colors;

        TMP_Text label = CreateText(rect, "Destination Name", new Vector2(14f, -4f), new Vector2(218f, 18f), 16f, FontStyles.Bold, TextAlignmentOptions.Left);
        TMP_Text meta = CreateText(rect, "Destination Meta", new Vector2(234f, -5f), new Vector2(136f, 18f), 13f, FontStyles.Bold, TextAlignmentOptions.Right);
        label.color = NeonOrange;
        meta.color = NeonOrange;

        AddLine(rect, "Button Accent", new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(3f, 24f), NeonOrange);
        AddLine(rect, "Button Lower Rail", new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(360f, 1.5f), DimOrange);

        destinationButtons.Add(button);
        destinationLabels.Add(label);
        destinationMetaLabels.Add(meta);
    }

    private void CreateWarpGauge(Transform parent)
    {
        GameObject gaugeObject = new GameObject("Blue_WarpGauge", typeof(RectTransform), typeof(Image));
        gaugeObject.transform.SetParent(parent, false);

        RectTransform gaugeRect = gaugeObject.GetComponent<RectTransform>();
        gaugeRect.anchorMin = new Vector2(1f, 0f);
        gaugeRect.anchorMax = new Vector2(1f, 0f);
        gaugeRect.pivot = new Vector2(1f, 0f);
        gaugeRect.anchoredPosition = new Vector2(-76f, 74f);
        gaugeRect.sizeDelta = new Vector2(414f, 72f);

        Image background = gaugeObject.GetComponent<Image>();
        background.color = new Color(0.01f, 0.035f, 0.055f, 0.64f);
        background.raycastTarget = false;

        AddLine(gaugeRect, "Gauge Top Rail", new Vector2(0.5f, 1f), new Vector2(0f, -9f), new Vector2(392f, 2.5f), NeonBlue);
        AddLine(gaugeRect, "Gauge Bottom Rail", new Vector2(0.5f, 0f), new Vector2(0f, 9f), new Vector2(392f, 2.5f), new Color(0.05f, 0.78f, 1f, 0.45f));

        warpGaugeText = CreateText(gaugeRect, "Warp Gauge Label", new Vector2(22f, -14f), new Vector2(164f, 20f), 15f, FontStyles.Bold, TextAlignmentOptions.Left);
        warpGaugeText.color = NeonBlue;

        warpStatusText = CreateText(gaugeRect, "Warp Gauge Status", new Vector2(202f, -14f), new Vector2(178f, 20f), 13f, FontStyles.Bold, TextAlignmentOptions.Right);
        warpStatusText.color = NeonBlue;
        warpStatusText.text = "HYPER CRUISE READY";

        GameObject fillRootObject = new GameObject("Warp Fill Root", typeof(RectTransform), typeof(Image));
        fillRootObject.transform.SetParent(gaugeRect, false);
        RectTransform fillRoot = fillRootObject.GetComponent<RectTransform>();
        fillRoot.anchorMin = new Vector2(0f, 0f);
        fillRoot.anchorMax = new Vector2(1f, 0f);
        fillRoot.pivot = new Vector2(0.5f, 0f);
        fillRoot.anchoredPosition = new Vector2(0f, 18f);
        fillRoot.sizeDelta = new Vector2(-48f, 18f);
        fillRootObject.GetComponent<Image>().color = new Color(0.02f, 0.28f, 0.42f, 0.28f);

        GameObject fillObject = new GameObject("Warp Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(fillRoot, false);
        warpFillRect = fillObject.GetComponent<RectTransform>();
        warpFillRect.anchorMin = new Vector2(0f, 0f);
        warpFillRect.anchorMax = new Vector2(1f, 1f);
        warpFillRect.pivot = new Vector2(0f, 0.5f);
        warpFillRect.offsetMin = Vector2.zero;
        warpFillRect.offsetMax = Vector2.zero;
        warpFillImage = fillObject.GetComponent<Image>();
        warpFillImage.color = NeonBlue;
        warpFillImage.raycastTarget = false;
    }

    private TMP_Text CreateText(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.raycastTarget = false;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.color = NeonOrange;
        return text;
    }

    private Image AddLine(Transform parent, string objectName, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject lineObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        lineObject.transform.SetParent(parent, false);

        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = lineObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void RefreshHud()
    {
        if (hudCanvas == null)
        {
            return;
        }

        if (warpFillRect != null)
        {
            warpFillRect.anchorMax = new Vector2(WarpGauge01, 1f);
        }

        if (warpFillImage != null)
        {
            float pulse = 0.72f + Mathf.Sin(Time.unscaledTime * 8f) * 0.16f;
            warpFillImage.color = Color.Lerp(new Color(0.02f, 0.36f, 0.52f, 0.86f), NeonBlue, pulse);
        }

        if (TryGetSelectedDestination(out HyperCruiseDestination selected))
        {
            float cost = GetWarpCost(selected);
            if (selectedInfoText != null)
            {
                selectedInfoText.text = $"{SpaceDistanceUtility.FormatKilometers(GetDistanceKilometers(selected))}  /  {cost:0} WG";
            }

            if (warpGaugeText != null)
            {
                warpGaugeText.text = $"WARP CHARGE {currentWarpGauge:0}%";
            }
        }

        for (int i = 0; i < destinationButtons.Count; i++)
        {
            if (i >= destinations.Count || destinations[i] == null)
            {
                continue;
            }

            HyperCruiseDestination destination = destinations[i];
            float cost = GetWarpCost(destination);
            bool isSelected = i == selectedDestinationIndex;
            bool isLocal = IsLocalDestination(destination);
            bool canAfford = currentWarpGauge >= cost;

            destinationButtons[i].interactable = destination.enabled && !isJumping && !isLocal;
            destinationLabels[i].text = destination.destinationName;
            destinationMetaLabels[i].text = isLocal ? "LOCAL" : SpaceDistanceUtility.FormatKilometers(GetDistanceKilometers(destination));

            Color labelColor = isSelected ? Color.Lerp(NeonOrange, Color.white, 0.22f) : NeonOrange;
            if (!canAfford && !isLocal)
            {
                labelColor = new Color(1f, 0.25f, 0.06f, 0.55f);
            }

            destinationLabels[i].color = labelColor;
            destinationMetaLabels[i].color = isSelected ? NeonBlue : labelColor;
        }
    }

    public bool TryGetAstronomicalDistanceToTarget(Transform target, out double kilometers)
    {
        kilometers = 0.0;
        if (target == null)
        {
            return false;
        }

        HyperCruiseDestination destination = FindDestinationForTarget(target);
        if (destination == null || IsLocalDestination(destination))
        {
            return false;
        }

        kilometers = GetDistanceKilometers(destination);
        return kilometers > 0.0;
    }

    private void SetStatus(string status, Color color)
    {
        if (warpStatusText == null)
        {
            return;
        }

        warpStatusText.text = status;
        warpStatusText.color = color;
    }

    private IEnumerator TurnShipTowardDestination(ShipController shipController, Transform target)
    {
        if (shipController == null || target == null)
        {
            yield break;
        }

        Vector3 directionToTarget = GetDirectionToTarget(shipController.transform.position, target);
        if (directionToTarget.sqrMagnitude < 0.0001f)
        {
            yield break;
        }

        Quaternion startRotation = shipController.transform.rotation;
        Quaternion targetRotation = GetRollPreservingRotation(startRotation, directionToTarget);
        float angleToTarget = Quaternion.Angle(startRotation, targetRotation);
        if (angleToTarget <= turnCompletionAngle)
        {
            shipController.SetHyperCruiseRotation(targetRotation);
            yield break;
        }

        float durationFromSpeed = angleToTarget / Mathf.Max(0.001f, turnTowardTargetSpeed);
        float duration = Mathf.Clamp(durationFromSpeed, Mathf.Max(0.01f, minimumTurnDuration), Mathf.Max(0.01f, maximumTurnDuration));
        float elapsed = 0f;

        while (elapsed < duration && shipController != null && target != null)
        {
            directionToTarget = GetDirectionToTarget(shipController.transform.position, target);
            if (directionToTarget.sqrMagnitude < 0.0001f)
            {
                yield break;
            }

            targetRotation = GetRollPreservingRotation(startRotation, directionToTarget);
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = SmootherStep(t);
            shipController.SetHyperCruiseRotation(Quaternion.Slerp(startRotation, targetRotation, easedT));

            yield return null;
        }

        if (shipController != null && target != null)
        {
            directionToTarget = GetDirectionToTarget(shipController.transform.position, target);
            if (directionToTarget.sqrMagnitude > 0.0001f)
            {
                shipController.SetHyperCruiseRotation(GetRollPreservingRotation(shipController.transform.rotation, directionToTarget));
            }
        }
    }

    private Quaternion GetRollPreservingRotation(Quaternion currentRotation, Vector3 forwardDirection)
    {
        Vector3 direction = forwardDirection.sqrMagnitude > 0.0001f
            ? forwardDirection.normalized
            : currentRotation * Vector3.forward;
        Vector3 currentUp = currentRotation * Vector3.up;
        Vector3 targetUp = Vector3.ProjectOnPlane(currentUp, direction);

        if (targetUp.sqrMagnitude < 0.0001f)
        {
            targetUp = Vector3.ProjectOnPlane(currentRotation * Vector3.right, direction);
        }

        if (targetUp.sqrMagnitude < 0.0001f)
        {
            targetUp = Vector3.ProjectOnPlane(Vector3.up, direction);
        }

        return Quaternion.LookRotation(direction, targetUp.sqrMagnitude > 0.0001f ? targetUp.normalized : Vector3.up);
    }

    private float SmootherStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private IEnumerator AnimateLensDistortionAndMove(
        float fromIntensity,
        float toIntensity,
        float duration,
        ShipController shipController,
        Transform target)
    {
        if (duration <= 0f)
        {
            SetLensDistortionIntensity(toIntensity);
            yield break;
        }

        Vector3 startPosition = shipController.transform.position;
        float elapsed = 0f;

        while (elapsed < duration && shipController != null && target != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            Vector3 destinationPosition = GetShipArrivalWorldPosition(shipController.transform.position, target);
            Vector3 nextPosition = Vector3.Lerp(startPosition, destinationPosition, easedT);
            Vector3 travelDirection = GetDirectionToTarget(shipController.transform.position, target);

            shipController.MoveHyperCruisePosition(nextPosition, travelDirection, hyperCruiseThrust);
            SetLensDistortionIntensity(Mathf.Lerp(fromIntensity, toIntensity, easedT));
            yield return null;
        }

        SetLensDistortionIntensity(toIntensity);
    }

    private void MoveShipNearDestination(ShipController shipController, Transform target)
    {
        if (shipController == null || target == null)
        {
            return;
        }

        ShiftDestinationWorldNearShip(shipController, target);
        IgnoreShipCollisionWithDestination(shipController, target);
        Vector3 travelDirection = GetDirectionToTarget(Vector3.zero, target);
        shipController.MoveHyperCruisePosition(Vector3.zero, travelDirection, hyperCruiseThrust);
        shipController.StabilizeHyperCruiseArrival(travelDirection, arrivalExitThrust, arrivalLookSuppressionDuration);
        Physics.SyncTransforms();
    }

    private void StabilizeArrivalView(ShipController shipController, Transform target)
    {
        if (shipController == null)
        {
            return;
        }

        Vector3 travelDirection = target != null
            ? GetDirectionToTarget(shipController.transform.position, target)
            : shipController.transform.forward;
        shipController.StabilizeHyperCruiseArrival(travelDirection, arrivalExitThrust, arrivalLookSuppressionDuration);

        CameraManager cameraManager = FindFirstObjectByType<CameraManager>();
        if (cameraManager != null)
        {
            cameraManager.ResetMotionEffects();
        }
    }

    private void IgnoreShipCollisionWithDestination(ShipController shipController, Transform target)
    {
        if (!ignoreDestinationCollisionOnArrival || shipController == null || target == null)
        {
            return;
        }

        Collider[] shipColliders = shipController.transform.root.GetComponentsInChildren<Collider>(true);
        Collider[] destinationColliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < shipColliders.Length; i++)
        {
            Collider shipCollider = shipColliders[i];
            if (shipCollider == null)
            {
                continue;
            }

            for (int j = 0; j < destinationColliders.Length; j++)
            {
                Collider destinationCollider = destinationColliders[j];
                if (destinationCollider == null || destinationCollider == shipCollider)
                {
                    continue;
                }

                Physics.IgnoreCollision(shipCollider, destinationCollider, true);
            }
        }
    }

    private void ShiftDestinationWorldNearShip(ShipController shipController, Transform target)
    {
        bool isFarSpaceTarget = IsFarSpaceTarget(target);
        float farSpaceScale = GetFarSpaceMovementScale();
        Vector3 currentViewerPosition = isFarSpaceTarget
            ? shipController.transform.position * farSpaceScale
            : shipController.transform.position;
        Vector3 targetPoint = GetTargetPoint(target);
        Vector3 directionToTarget = targetPoint - currentViewerPosition;

        if (directionToTarget.sqrMagnitude < 0.0001f)
        {
            directionToTarget = shipController.transform.forward;
        }

        directionToTarget.Normalize();
        Vector3 desiredTargetPoint = directionToTarget * GetArrivalOffset(target);
        Vector3 worldShift = targetPoint - desiredTargetPoint;
        ShiftMatchingWorldRoots(worldShift, isFarSpaceTarget, shipController.transform.root);
        Physics.SyncTransforms();
    }

    private void ShiftMatchingWorldRoots(Vector3 worldShift, bool shiftFarSpaceRoots, Transform playerRoot)
    {
        if (worldShift.sqrMagnitude < 0.0001f)
        {
            return;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int j = 0; j < rootObjects.Length; j++)
            {
                GameObject rootObject = rootObjects[j];
                if (ShouldSkipHyperCruiseShiftRoot(rootObject, playerRoot))
                {
                    continue;
                }

                bool isFarSpaceRoot = IsFarSpaceRoot(rootObject);
                if (isFarSpaceRoot != shiftFarSpaceRoots)
                {
                    continue;
                }

                rootObject.transform.position -= worldShift;
            }
        }
    }

    private bool ShouldSkipHyperCruiseShiftRoot(GameObject rootObject, Transform playerRoot)
    {
        if (rootObject == null)
        {
            return true;
        }

        Transform rootTransform = rootObject.transform;
        if (rootTransform == playerRoot || rootTransform.IsChildOf(playerRoot))
        {
            return true;
        }

        return rootObject.GetComponent<Camera>() != null
            || rootObject.GetComponent<FarSpaceCameraSync>() != null
            || rootObject.GetComponent<Canvas>() != null
            || rootObject.name == "EventSystem"
            || rootObject.name == "Global Volume"
            || rootObject.name == "UniversalLight";
    }

    private bool IsFarSpaceRoot(GameObject rootObject)
    {
        if (rootObject == null)
        {
            return false;
        }

        int farSpaceLayer = LayerMask.NameToLayer(farSpaceLayerName);
        Transform[] children = rootObject.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            GameObject childObject = children[i].gameObject;
            if ((farSpaceLayer >= 0 && childObject.layer == farSpaceLayer) || HasTag(childObject, trackingTargetTag))
            {
                return true;
            }
        }

        return false;
    }

    private Transform FindDestinationTarget(HyperCruiseDestination destination)
    {
        if (destination == null)
        {
            return null;
        }

        string targetName = !string.IsNullOrWhiteSpace(destination.targetObjectName)
            ? destination.targetObjectName
            : destination.destinationName;
        Transform exactTarget = FindTargetByExactName(targetName);
        if (exactTarget != null)
        {
            return exactTarget;
        }

        Transform taggedTarget = FindTaggedTargetByDestinationName(destination);
        if (taggedTarget != null)
        {
            return taggedTarget;
        }

        return FindAnyTransformByDestinationName(destination);
    }

    private HyperCruiseDestination FindDestinationForTarget(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        for (int i = 0; i < destinations.Count; i++)
        {
            HyperCruiseDestination destination = destinations[i];
            if (destination == null)
            {
                continue;
            }

            Transform destinationTarget = FindDestinationTarget(destination);
            if (destinationTarget == null)
            {
                continue;
            }

            if (target == destinationTarget
                || target.IsChildOf(destinationTarget)
                || destinationTarget.IsChildOf(target)
                || IsDestinationNameMatch(target.name, destination))
            {
                return destination;
            }
        }

        return null;
    }

    private Transform FindTargetByExactName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        GameObject targetObject = GameObject.Find(targetName);
        return targetObject != null ? targetObject.transform : null;
    }

    private Transform FindTaggedTargetByDestinationName(HyperCruiseDestination destination)
    {
        GameObject[] taggedTargets;
        try
        {
            taggedTargets = GameObject.FindGameObjectsWithTag(trackingTargetTag);
        }
        catch (UnityException)
        {
            return null;
        }

        for (int i = 0; i < taggedTargets.Length; i++)
        {
            if (taggedTargets[i] != null && IsDestinationNameMatch(taggedTargets[i].name, destination))
            {
                return taggedTargets[i].transform;
            }
        }

        return null;
    }

    private Transform FindAnyTransformByDestinationName(HyperCruiseDestination destination)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && IsDestinationNameMatch(transforms[i].name, destination))
            {
                return transforms[i];
            }
        }

        return null;
    }

    private bool IsDestinationNameMatch(string objectName, HyperCruiseDestination destination)
    {
        if (destination == null || string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        string normalizedObjectName = NormalizeDestinationName(objectName);
        string normalizedDestinationName = NormalizeDestinationName(destination.destinationName);
        string normalizedTargetName = NormalizeDestinationName(destination.targetObjectName);

        return normalizedObjectName == normalizedDestinationName
            || normalizedObjectName == normalizedTargetName
            || (!string.IsNullOrEmpty(normalizedDestinationName) && normalizedObjectName.Contains(normalizedDestinationName))
            || (!string.IsNullOrEmpty(normalizedTargetName) && normalizedObjectName.Contains(normalizedTargetName));
    }

    private string NormalizeDestinationName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace(" ", string.Empty).Replace("_", string.Empty).ToUpperInvariant();
    }

    private Vector3 GetShipArrivalWorldPosition(Vector3 shipWorldPosition, Transform target)
    {
        Vector3 targetPoint = GetTargetPoint(target);
        bool isFarSpaceTarget = IsFarSpaceTarget(target);
        float farSpaceScale = GetFarSpaceMovementScale();
        Vector3 viewerPosition = isFarSpaceTarget ? shipWorldPosition * farSpaceScale : shipWorldPosition;
        Vector3 awayFromTarget = viewerPosition - targetPoint;

        if (awayFromTarget.sqrMagnitude < 0.0001f)
        {
            awayFromTarget = -target.forward;
        }

        awayFromTarget.Normalize();
        float arrivalOffset = GetArrivalOffset(target);
        Vector3 viewerArrivalPosition = targetPoint + awayFromTarget * arrivalOffset;
        return isFarSpaceTarget ? viewerArrivalPosition / Mathf.Max(0.0001f, farSpaceScale) : viewerArrivalPosition;
    }

    private Vector3 GetDirectionToTarget(Vector3 shipWorldPosition, Transform target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        bool isFarSpaceTarget = IsFarSpaceTarget(target);
        float farSpaceScale = GetFarSpaceMovementScale();
        Vector3 viewerPosition = isFarSpaceTarget ? shipWorldPosition * farSpaceScale : shipWorldPosition;
        Vector3 direction = GetTargetPoint(target) - viewerPosition;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private float GetNavigationDistanceToTarget(Transform target)
    {
        ShipController shipController = FindFirstObjectByType<ShipController>();
        if (shipController == null || target == null)
        {
            return float.MaxValue;
        }

        bool isFarSpaceTarget = IsFarSpaceTarget(target);
        float farSpaceScale = GetFarSpaceMovementScale();
        Vector3 viewerPosition = isFarSpaceTarget
            ? shipController.transform.position * farSpaceScale
            : shipController.transform.position;
        float visualDistance = Vector3.Distance(viewerPosition, GetTargetPoint(target));
        return isFarSpaceTarget ? visualDistance / Mathf.Max(0.0001f, farSpaceScale) : visualDistance;
    }

    private float GetLocalOrbitDistance(HyperCruiseDestination destination)
    {
        Transform target = FindDestinationTarget(destination);
        if (target == null)
        {
            return arrivalDistanceFromTarget;
        }

        float localDistance = GetArrivalOffset(target) * 1.5f;
        return IsFarSpaceTarget(target)
            ? localDistance / Mathf.Max(0.0001f, GetFarSpaceMovementScale())
            : localDistance;
    }

    private float GetArrivalOffset(Transform target)
    {
        if (arrivalDisplayedDistanceKilometers > 0f)
        {
            return GetArrivalOffsetForDisplayedKilometers(target, arrivalDisplayedDistanceKilometers);
        }

        float targetRadius = EstimateTargetRadius(target);
        return Mathf.Max(arrivalDistanceFromTarget, targetRadius * arrivalRadiusMultiplier);
    }

    private float GetArrivalOffsetForDisplayedKilometers(Transform target, float displayedKilometers)
    {
        float distance = Mathf.Max(0f, displayedKilometers);
        return IsFarSpaceTarget(target)
            ? distance * Mathf.Max(0.0001f, GetFarSpaceMovementScale())
            : distance;
    }

    private Vector3 GetTargetPoint(Transform target)
    {
        if (TryGetTargetBounds(target, out Bounds bounds))
        {
            return bounds.center;
        }

        return target != null ? target.position : Vector3.zero;
    }

    private float EstimateTargetRadius(Transform target)
    {
        if (TryGetTargetBounds(target, out Bounds bounds))
        {
            return bounds.extents.magnitude;
        }

        return target != null ? Mathf.Max(target.lossyScale.x, target.lossyScale.y, target.lossyScale.z) : 1f;
    }

    private bool TryGetTargetBounds(Transform target, out Bounds bounds)
    {
        bounds = new Bounds(target != null ? target.position : Vector3.zero, Vector3.one);
        if (target == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || renderers[i] is ParticleSystemRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }

        return hasBounds;
    }

    private bool IsFarSpaceTarget(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        int farSpaceLayer = LayerMask.NameToLayer(farSpaceLayerName);
        Transform current = target;
        while (current != null)
        {
            if ((farSpaceLayer >= 0 && current.gameObject.layer == farSpaceLayer)
                || HasTag(current.gameObject, trackingTargetTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool HasTag(GameObject targetObject, string tagName)
    {
        if (targetObject == null || string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        try
        {
            return targetObject.CompareTag(tagName);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private float GetFarSpaceMovementScale()
    {
        FarSpaceCameraSync farSpaceSync = FindFirstObjectByType<FarSpaceCameraSync>();
        return farSpaceSync != null && farSpaceSync.movementScale > 0.0001f
            ? farSpaceSync.movementScale
            : 1f;
    }

    private IEnumerator AnimateLensDistortion(float fromIntensity, float toIntensity, float duration)
    {
        if (duration <= 0f)
        {
            SetLensDistortionIntensity(toIntensity);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            SetLensDistortionIntensity(Mathf.Lerp(fromIntensity, toIntensity, easedT));
            yield return null;
        }

        SetLensDistortionIntensity(toIntensity);
    }

    private void ConfigureOutsideOnlyPostProcessing()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            UniversalAdditionalCameraData cameraData = cameras[i].GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                continue;
            }

            bool isCockpitOverlay = cameraData.renderType == CameraRenderType.Overlay
                || cameras[i].name.Contains("Cockpit");
            cameraData.renderPostProcessing = !isCockpitOverlay;
        }
    }

    private float GetLensDistortionIntensity(float fallback)
    {
        LensDistortion lensDistortion = FindLensDistortion();
        return lensDistortion != null ? lensDistortion.intensity.value : fallback;
    }

    private void SetLensDistortionIntensity(float intensity)
    {
        LensDistortion lensDistortion = FindLensDistortion();
        if (lensDistortion == null)
        {
            return;
        }

        lensDistortion.active = true;
        lensDistortion.intensity.overrideState = true;
        lensDistortion.intensity.value = intensity;
    }

    private LensDistortion FindLensDistortion()
    {
        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            VolumeProfile profile = GetRuntimeProfile(volumes[i]);
            if (profile != null && profile.TryGet(out LensDistortion lensDistortion))
            {
                return lensDistortion;
            }
        }

        return null;
    }

    private VolumeProfile GetRuntimeProfile(Volume volume)
    {
        if (volume == null)
        {
            return null;
        }

        return volume.profile != null ? volume.profile : volume.sharedProfile;
    }

}
