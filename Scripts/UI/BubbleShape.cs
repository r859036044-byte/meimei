using Godot;

/// <summary>
/// 對話氣泡的外形：圓角矩形＋「∠」形尾巴合成「同一個」多邊形再畫外框，
/// 所以尾巴和氣泡是一整塊，銜接處沒有縫、沒有多出來的線。
/// 掛在 PanelContainer（氣泡）底下當 Node2D 子節點（ShowBehindParent），尺寸直接讀父節點 Size。
///
/// ∠ 樣式：一邊是水平的（和氣泡底邊同一條線），另一邊斜斜收回氣泡側邊；
/// 左右側的尾巴貼在下緣：氣泡底邊直接延伸到尖端（尾巴那一側的下角是直角），斜邊從尖端往上連回氣泡側邊。
/// 外框顏色層：外框(深色) → [女主：粉紅內框] → 底色；每一層都是上一層往內縮，所以尾巴那一圈也一路包到尖端。
/// </summary>
public partial class BubbleShape : Node2D
{
    public enum Side { None, Left, Right, Top }

    public Side  TailSide = Side.None;     // 尾巴在氣泡的哪一邊（Left＝在左緣、朝左）
    public bool  TailVisible = true;
    public float K = 1f;                   // 整體放大倍率（圓角、框、尾巴都跟著放大）
    public float Radius = 18f, Border = 3f, TailLen = 16f, TailBaseW = 13f;   // 尾巴小小的：長 16、底 13（再乘 K）
    public bool  DoubleBorder;             // 女主：深色框 ＋ 粉紅內框
    public Color OutlineColor = new Color(0.28f, 0.14f, 0.24f);
    public Color InnerBorderColor = new Color("e8b5ce");
    public Color FillColor = Colors.White;

    Vector2 _lastSize; Side _lastSide; bool _lastVis;

    public override void _Ready() { ShowBehindParent = true; }

    public override void _Process(double delta)
    {
        if (GetParent() is not Control box) return;
        if (box.Size != _lastSize || TailSide != _lastSide || TailVisible != _lastVis)
        {
            _lastSize = box.Size; _lastSide = TailSide; _lastVis = TailVisible;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (GetParent() is not Control box) return;
        Vector2 sz = box.Size;
        if (sz.X < 4f || sz.Y < 4f) return;

        bool tailShown = TailSide != Side.None && TailVisible;
        Vector2[] outer = RoundedRect(sz, Mathf.Min(Radius * K, Mathf.Min(sz.X, sz.Y) * 0.5f),
            squareBL: tailShown && TailSide == Side.Left, squareBR: tailShown && TailSide == Side.Right);
        if (TailSide != Side.None && TailVisible)
        {
            var merged = Geometry2D.MergePolygons(outer, TailPoly(sz));
            if (merged.Count > 0) outer = Largest(merged);
        }

        float b = Border * K;
        DrawColoredPolygon(outer, OutlineColor);
        if (DoubleBorder)
        {
            DrawColoredPolygon(Inset(outer, b), InnerBorderColor);
            DrawColoredPolygon(Inset(outer, b * 2f), FillColor);
        }
        else
            DrawColoredPolygon(Inset(outer, b), FillColor);
    }

    /// <summary>∠ 形尾巴三角形（底邊稍微伸進氣泡裡，合併時才會完全銜接）。尖端在邊的中心線上。</summary>
    Vector2[] TailPoly(Vector2 sz)
    {
        float len = TailLen * K, bw = TailBaseW * K, ov = 3f * K;
        switch (TailSide)
        {
            case Side.Left:
            {   // ∠：下緣水平延伸出去到尖端（和氣泡底邊同一條線），斜邊從尖端往上連回氣泡左緣
                return new[] { new Vector2(ov, sz.Y - bw), new Vector2(-len, sz.Y), new Vector2(ov, sz.Y) };
            }
            case Side.Right:
            {
                return new[] { new Vector2(sz.X - ov, sz.Y - bw), new Vector2(sz.X + len, sz.Y), new Vector2(sz.X - ov, sz.Y) };
            }
            default:   // Top：尖端在上緣中心，右緣垂直（x＝中心），左緣斜
            {   float cx = sz.X * 0.5f;
                return new[] { new Vector2(cx - bw, ov), new Vector2(cx, -len), new Vector2(cx, ov) };
            }
        }
    }

    static Vector2[] RoundedRect(Vector2 sz, float r, bool squareBL = false, bool squareBR = false)
    {
        const int seg = 10;
        var pts = new System.Collections.Generic.List<Vector2>();
        void Corner(Vector2 c, float a0)
        {
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.DegToRad(a0 + 90f * i / seg);
                pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        }
        Corner(new Vector2(sz.X - r, r),        -90f);   // 右上
        if (squareBR) pts.Add(new Vector2(sz.X, sz.Y)); else Corner(new Vector2(sz.X - r, sz.Y - r),   0f);   // 右下
        if (squareBL) pts.Add(new Vector2(0f, sz.Y));   else Corner(new Vector2(r, sz.Y - r),         90f);   // 左下
        Corner(new Vector2(r, r),               180f);   // 左上
        return pts.ToArray();
    }

    static Vector2[] Inset(Vector2[] poly, float d)
    {
        var res = Geometry2D.OffsetPolygon(poly, -d, Geometry2D.PolyJoinType.Round);
        return res.Count > 0 ? Largest(res) : poly;
    }

    static Vector2[] Largest(Godot.Collections.Array<Vector2[]> polys)
    {
        Vector2[] best = polys[0]; float bestA = -1f;
        foreach (var p in polys)
        {
            float a = 0f;
            for (int i = 0; i < p.Length; i++) { var u = p[i]; var v = p[(i + 1) % p.Length]; a += u.X * v.Y - v.X * u.Y; }
            a = Mathf.Abs(a);
            if (a > bestA) { bestA = a; best = p; }
        }
        return best;
    }
}
