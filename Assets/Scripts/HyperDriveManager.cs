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
        public Vector3 galacticCoordinates;
        public Color accentColor = new Color(1f, 0.48f, 0.04f, 1f);
        public bool enabled = true;
    }

    [Header("Destinations")]
    [SerializeField] private List<HyperCruiseDestination> destinations = new List<HyperCruiseDestination>();
    [SerializeField] private Vector3 currentGalacticCoordinates;

    [Header("Warp Gauge")]
    [SerializeField] private float maximumWarpGauge = 100f;
    [SerializeField] private float startingWarpGauge = 72f;
    [SerializeField] private float warpRechargeRate = 2.5f;
    [SerializeField] private float baseWarpCost = 16f;
    [SerializeField] private float costPerCoordinateUnit = 0.62f;
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

    private static bool pendingArrivalTransition;
    private static float arrivalLensDistortionIntensity = -0.2f;
    private static float arrivalHyperCruiseThrust = 70f;

    public float WarpGauge01 => maximumWarpGauge > 0f ? Mathf.Clamp01(currentWarpGauge / maximumWarpGauge) : 0f;
    public bool IsJumping => isJumping;

    private void Awake()
    {
        currentWarpGauge = Mathf.Clamp(startingWarpGauge, 0f, maximumWarpGauge);
        EnsureDefaultDestinations();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }

    private void Start()
    {
        RefreshCurrentLocationFromScene(SceneManager.GetActiveScene().name);
        SelectFirstRemoteDestination();
        ConfigureOutsideOnlyPostProcessing();

        if (pendingArrivalTransition)
        {
            pendingArrivalTransition = false;
            SetLensDistortionIntensity(arrivalLensDistortionIntensity);
            ApplyHyperCruiseSpeed(arrivalHyperCruiseThrust);
            StartCoroutine(RecoverFromHyperCruiseArrival());
        }
        else
        {
            SetLensDistortionIntensity(normalLensDistortionIntensity);
        }

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

        if (IsCurrentScene(destination))
        {
            SetStatus("ALREADY IN LOCAL ORBIT", DimOrange);
            return false;
        }

        float cost = GetWarpCost(destination);
        if (currentWarpGauge < cost)
        {
            SetStatus("INSUFFICIENT WARP CHARGE", new Color(1f, 0.18f, 0.04f, 1f));
            return false;
        }

        StartCoroutine(RunHyperCruiseRoutine(destination, cost));
        return true;
    }

    private IEnumerator RunHyperCruiseRoutine(HyperCruiseDestination destination, float cost)
    {
        isJumping = true;
        currentWarpGauge = Mathf.Max(0f, currentWarpGauge - cost);
        SetStatus("HYPER CRUISE JUMP", NeonBlue);
        RefreshHud();
        ConfigureOutsideOnlyPostProcessing();
        ApplyHyperCruiseSpeed(hyperCruiseThrust);

        if (!string.IsNullOrWhiteSpace(destination.sceneName) && Application.CanStreamedLevelBeLoaded(destination.sceneName))
        {
            float currentIntensity = GetLensDistortionIntensity(normalLensDistortionIntensity);
            yield return AnimateLensDistortion(currentIntensity, hyperCruiseLensDistortionIntensity, departureDistortionDuration);

            pendingArrivalTransition = true;
            arrivalLensDistortionIntensity = hyperCruiseLensDistortionIntensity;
            arrivalHyperCruiseThrust = hyperCruiseThrust;
#if UNITY_EDITOR
            ClearEditorVolumeSelectionBeforeSceneLoad();
#endif
            SceneManager.LoadScene(destination.sceneName);
        }
        else
        {
            Debug.LogWarning($"Hyper Cruise destination scene is not loadable: {destination.sceneName}");
            SetStatus("DESTINATION SCENE OFFLINE", new Color(1f, 0.18f, 0.04f, 1f));
            SetLensDistortionIntensity(normalLensDistortionIntensity);
            isJumping = false;
        }
    }

    private IEnumerator RecoverFromHyperCruiseArrival()
    {
        yield return AnimateLensDistortion(arrivalLensDistortionIntensity, normalLensDistortionIntensity, arrivalDistortionDuration);
        SetStatus("HYPER CRUISE READY", NeonOrange);
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene newScene)
    {
        RefreshCurrentLocationFromScene(newScene.name);
        SelectFirstRemoteDestination();
        isJumping = false;
        SetStatus("HYPER CRUISE READY", NeonOrange);
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
            galacticCoordinates = new Vector3(0f, 0f, 0f),
            accentColor = new Color(0.12f, 0.78f, 1f, 1f)
        });
        destinations.Add(new HyperCruiseDestination
        {
            destinationName = "SATURN",
            sceneName = "Saturn",
            galacticCoordinates = new Vector3(74f, 8f, 39f),
            accentColor = new Color(1f, 0.55f, 0.08f, 1f)
        });
    }

    private void RefreshCurrentLocationFromScene(string sceneName)
    {
        for (int i = 0; i < destinations.Count; i++)
        {
            HyperCruiseDestination destination = destinations[i];
            if (destination != null && destination.sceneName == sceneName)
            {
                currentGalacticCoordinates = destination.galacticCoordinates;
                return;
            }
        }
    }

    private void SelectFirstRemoteDestination()
    {
        for (int i = 0; i < destinations.Count; i++)
        {
            if (destinations[i] != null && destinations[i].enabled && !IsCurrentScene(destinations[i]))
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

    private bool IsCurrentScene(HyperCruiseDestination destination)
    {
        return destination != null && SceneManager.GetActiveScene().name == destination.sceneName;
    }

    private float GetWarpCost(HyperCruiseDestination destination)
    {
        float distance = Vector3.Distance(currentGalacticCoordinates, destination.galacticCoordinates);
        return Mathf.Clamp(baseWarpCost + distance * costPerCoordinateUnit, 0f, maximumWarpCost);
    }

    private float GetDistance(HyperCruiseDestination destination)
    {
        return Vector3.Distance(currentGalacticCoordinates, destination.galacticCoordinates);
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
            float distance = GetDistance(selected);
            if (selectedInfoText != null)
            {
                selectedInfoText.text = $"{distance:0.0} LY  /  {cost:0} WG";
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
            bool isLocal = IsCurrentScene(destination);
            bool canAfford = currentWarpGauge >= cost;

            destinationButtons[i].interactable = destination.enabled && !isJumping && !isLocal;
            destinationLabels[i].text = destination.destinationName;
            destinationMetaLabels[i].text = isLocal ? "LOCAL" : $"{GetDistance(destination):0.0} LY";

            Color labelColor = isSelected ? Color.Lerp(NeonOrange, Color.white, 0.22f) : NeonOrange;
            if (!canAfford && !isLocal)
            {
                labelColor = new Color(1f, 0.25f, 0.06f, 0.55f);
            }

            destinationLabels[i].color = labelColor;
            destinationMetaLabels[i].color = isSelected ? NeonBlue : labelColor;
        }
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

    private void ApplyHyperCruiseSpeed(float thrust)
    {
        ShipController shipController = FindFirstObjectByType<ShipController>();
        if (shipController != null)
        {
            shipController.ApplyHyperCruiseBoost(thrust);
        }
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

#if UNITY_EDITOR
    private void ClearEditorVolumeSelectionBeforeSceneLoad()
    {
        if (UnityEditor.Selection.activeObject is Volume)
        {
            UnityEditor.Selection.activeObject = null;
            return;
        }

        GameObject selectedObject = UnityEditor.Selection.activeGameObject;
        if (selectedObject != null && selectedObject.GetComponent<Volume>() != null)
        {
            UnityEditor.Selection.activeObject = null;
        }
    }
#endif

}
