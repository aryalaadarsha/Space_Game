using UnityEngine;
using UnityEngine.UI;

public sealed class TargetReticleGraphic : Graphic
{
    [SerializeField] private float ringThickness = 3f;
    [SerializeField] private float segmentGapDegrees = 28f;
    [SerializeField] private float tickLength = 12f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.42f;
        float innerRadius = Mathf.Max(0f, radius - ringThickness);
        float quarterArc = (360f - segmentGapDegrees * 4f) * 0.25f;

        for (int i = 0; i < 4; i++)
        {
            float start = i * 90f + segmentGapDegrees * 0.5f;
            AddArc(vh, center, innerRadius, radius, start, start + quarterArc);
        }

        AddTick(vh, center, radius + 2f, radius + tickLength, 0f);
        AddTick(vh, center, radius + 2f, radius + tickLength, 90f);
        AddTick(vh, center, radius + 2f, radius + tickLength, 180f);
        AddTick(vh, center, radius + 2f, radius + tickLength, 270f);
        AddLine(vh, center + new Vector2(radius * 0.25f, 0f), center + new Vector2(radius * 1.35f, 0f), ringThickness * 0.7f);
    }

    private void AddArc(VertexHelper vh, Vector2 center, float innerRadius, float outerRadius, float startDegrees, float endDegrees)
    {
        int steps = Mathf.Max(5, Mathf.CeilToInt(Mathf.Abs(endDegrees - startDegrees) / 5f));
        float step = (endDegrees - startDegrees) / steps;

        for (int i = 0; i < steps; i++)
        {
            float a0 = (startDegrees + step * i) * Mathf.Deg2Rad;
            float a1 = (startDegrees + step * (i + 1)) * Mathf.Deg2Rad;

            Vector2 inner0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerRadius;
            Vector2 outer0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * outerRadius;
            Vector2 outer1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outerRadius;
            Vector2 inner1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * innerRadius;

            AddQuad(vh, inner0, outer0, outer1, inner1);
        }
    }

    private void AddTick(VertexHelper vh, Vector2 center, float innerRadius, float outerRadius, float degrees)
    {
        float angle = degrees * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        AddLine(vh, center + direction * innerRadius, center + direction * outerRadius, ringThickness);
    }

    private void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width)
    {
        Vector2 normal = Vector2.Perpendicular((b - a).normalized) * (width * 0.5f);
        AddQuad(vh, a - normal, a + normal, b + normal, b - normal);
    }

    private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int startIndex = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = a;
        vh.AddVert(vertex);
        vertex.position = b;
        vh.AddVert(vertex);
        vertex.position = c;
        vh.AddVert(vertex);
        vertex.position = d;
        vh.AddVert(vertex);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }
}
