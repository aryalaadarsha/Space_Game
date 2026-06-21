using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HologramThrustGauge : MonoBehaviour
{
    [SerializeField] private RectTransform negativeFill;
    [SerializeField] private RectTransform positiveFill;
    [SerializeField] private RectTransform valueMarker;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Image[] segmentImages;
    [SerializeField] private Color positiveSegmentColor = new Color(1f, 0.47f, 0f, 0.95f);
    [SerializeField] private Color negativeSegmentColor = new Color(0f, 0.68f, 1f, 0.9f);
    [SerializeField] private Color inactiveSegmentColor = new Color(0.85f, 0.24f, 0f, 0.35f);
    [SerializeField] private float minimumValue = -30f;
    [SerializeField] private float maximumValue = 70f;
    [SerializeField] private float responseSpeed = 12f;

    private float displayedValue;
    private float targetValue;
    private bool initialized;

    public void SetValue(float value)
    {
        targetValue = Mathf.Clamp(value, minimumValue, maximumValue);

        if (!initialized)
        {
            displayedValue = targetValue;
            initialized = true;
            ApplyValue(displayedValue);
        }
    }

    private void OnEnable()
    {
        ApplyValue(displayedValue);
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        displayedValue = Mathf.Lerp(displayedValue, targetValue, Time.deltaTime * responseSpeed);
        ApplyValue(displayedValue);
    }

    private void ApplyValue(float value)
    {
        float zero = Mathf.InverseLerp(minimumValue, maximumValue, 0f);
        float amount = Mathf.InverseLerp(minimumValue, maximumValue, value);

        if (segmentImages != null && segmentImages.Length > 0)
        {
            ApplySegmentGauge(value);
        }
        else
        {
            SetHorizontalAnchors(negativeFill, Mathf.Min(amount, zero), zero);
            SetHorizontalAnchors(positiveFill, zero, Mathf.Max(amount, zero));
            SetHorizontalAnchors(valueMarker, amount, amount);
        }

        if (valueText != null)
        {
            valueText.text = value.ToString("F1");
        }
    }

    private void ApplySegmentGauge(float value)
    {
        int zeroBoundary = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.InverseLerp(minimumValue, maximumValue, 0f) * segmentImages.Length),
            1,
            segmentImages.Length - 1);

        float positiveAmount = maximumValue > 0f ? Mathf.Clamp01(value / maximumValue) : 0f;
        float negativeAmount = minimumValue < 0f ? Mathf.Clamp01(value / minimumValue) : 0f;
        float scaledPositive = positiveAmount * (segmentImages.Length - zeroBoundary);
        float scaledNegative = negativeAmount * zeroBoundary;

        for (int i = 0; i < segmentImages.Length; i++)
        {
            Image segment = segmentImages[i];
            if (segment == null)
            {
                continue;
            }

            bool isPositiveSide = i >= zeroBoundary;
            float litAmount = isPositiveSide
                ? Mathf.Clamp01(scaledPositive - (i - zeroBoundary))
                : Mathf.Clamp01(scaledNegative - ((zeroBoundary - 1) - i));
            Color activeColor = isPositiveSide ? positiveSegmentColor : negativeSegmentColor;

            segment.color = Color.Lerp(inactiveSegmentColor, activeColor, litAmount);
        }
    }

    private static void SetHorizontalAnchors(RectTransform target, float min, float max)
    {
        if (target == null)
        {
            return;
        }

        target.anchorMin = new Vector2(Mathf.Clamp01(min), target.anchorMin.y);
        target.anchorMax = new Vector2(Mathf.Clamp01(max), target.anchorMax.y);
        target.offsetMin = new Vector2(0f, target.offsetMin.y);
        target.offsetMax = new Vector2(0f, target.offsetMax.y);
    }
}
