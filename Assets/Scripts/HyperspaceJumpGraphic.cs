using UnityEngine;
using UnityEngine.UI;

public sealed class HyperspaceJumpGraphic : MaskableGraphic
{
    [SerializeField] private int streakCount = 148;
    [SerializeField] private float travelSpeed = 3.8f;
    [SerializeField] private float baseWidth = 1.6f;
    [SerializeField] private Color coreColor = new Color(0.86f, 0.98f, 1f, 1f);
    [SerializeField] private Color edgeColor = new Color(0.04f, 0.72f, 1f, 0.82f);

    private float intensity;
    private float time;

    public void SetIntensity(float value)
    {
        intensity = Mathf.Clamp01(value);
        SetVerticesDirty();
    }

    private void Update()
    {
        if (intensity <= 0.001f)
        {
            return;
        }

        time += Time.unscaledDeltaTime;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (intensity <= 0.001f)
        {
            return;
        }

        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float radius = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height) * 0.58f;
        int count = Mathf.Max(8, streakCount);

        for (int i = 0; i < count; i++)
        {
            float seed = Frac(Mathf.Sin(i * 41.231f) * 8912.53f);
            float seedB = Frac(Mathf.Sin(i * 12.743f + 4.1f) * 5134.37f);
            float angle = (i / (float)count) * Mathf.PI * 2f + seed * 0.22f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 normal = new Vector2(-direction.y, direction.x);

            float phase = Frac(seedB + time * travelSpeed * Mathf.Lerp(0.45f, 1.25f, seed));
            float ease = phase * phase;
            float inner = Mathf.Lerp(0.02f, 0.78f, ease) * radius;
            float length = Mathf.Lerp(0.08f, 0.38f, intensity) * radius * Mathf.Lerp(0.45f, 1.2f, seed);
            float outer = Mathf.Min(radius, inner + length);
            float width = baseWidth * Mathf.Lerp(0.6f, 2.8f, intensity) * Mathf.Lerp(0.65f, 1.4f, seedB);
            float alpha = intensity * Mathf.SmoothStep(0f, 1f, phase) * (1f - Mathf.SmoothStep(0.76f, 1f, phase));

            Color streakColor = Color.Lerp(edgeColor, coreColor, seed);
            streakColor.a *= alpha;

            AddQuad(vh, center + direction * inner, center + direction * outer, normal * width, streakColor);
        }

        if (intensity > 0.45f)
        {
            float flashSize = Mathf.Lerp(34f, 180f, intensity);
            Color flashColor = coreColor;
            flashColor.a = (intensity - 0.45f) * 0.22f;
            AddQuad(vh, center - Vector2.right * flashSize, center + Vector2.right * flashSize, Vector2.up * flashSize, flashColor);
        }
    }

    private static void AddQuad(VertexHelper vh, Vector2 start, Vector2 end, Vector2 halfWidth, Color color)
    {
        int index = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = start - halfWidth;
        vh.AddVert(vertex);
        vertex.position = start + halfWidth;
        vh.AddVert(vertex);
        vertex.position = end + halfWidth;
        vh.AddVert(vertex);
        vertex.position = end - halfWidth;
        vh.AddVert(vertex);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 3, index);
    }

    private static float Frac(float value)
    {
        return value - Mathf.Floor(value);
    }
}
