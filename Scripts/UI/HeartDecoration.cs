using Godot;

/// <summary>原生繪製粉紅愛心，避免系統 emoji 字型忽略指定顏色。</summary>
public partial class HeartDecoration : Control
{
    public HeartDecoration() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        const int segments = 80;
        var points = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float t = Mathf.Tau * i / segments;
            float x = 16f * Mathf.Pow(Mathf.Sin(t), 3);
            float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t)
                - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
            points[i] = new Vector2((x + 17f) / 34f * Size.X, (13f - y) / 31f * Size.Y);
        }
        DrawColoredPolygon(points, new Color("ed70a3"));
        var outline = new Vector2[segments + 1];
        points.CopyTo(outline, 0);
        outline[segments] = points[0];
        DrawPolyline(outline, new Color("e85b95"), 0.8f, true);
    }
}
