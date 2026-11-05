using Godot;
using System.Collections.Generic;

/// <summary>
/// 眼淚圈：眼淚圖層顯示時，從眼淚的位置冒出「中間透明的白色圓圈」，往下並左右擺動，
/// 離開臉（臉圖層該處已經透明）就消失。
/// 位置都是畫布 px（2000x3200，跟圖層同一個座標系）。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("眼淚圈")]
    [Export] public bool TearRingEnabled = true;
    [Export] public float TearRingRadius = 11f;          // 圓圈半徑（畫布 px）
    [Export] public float TearRingWidth = 3f;            // 圓圈線寬
    [Export] public float TearRingAlpha = 0.9f;          // 白色不透明度
    [Export] public float TearRingFall = 55f;            // 起始下落速度（畫布 px/秒）
    [Export] public float TearRingAccel = 35f;           // 往下越來越快（px/秒²）
    [Export] public float TearRingSway = 9f;             // 左右擺動幅度（畫布 px）
    [Export] public float TearRingSwayHz = 1.4f;         // 左右擺動頻率（次/秒）
    [Export] public Vector2 TearRingInterval = new Vector2(0.45f, 0.9f);   // 每個眼淚圖層隔多久冒一個（秒，隨機）
    [Export] public int TearRingMax = 10;                // 同時最多幾個
    [Export] public float TearRingFadeOut = 0.12f;       // 離開臉之後淡掉的秒數

    // 眼淚圖層實際畫出內容的範圍（2000x3200 畫布 px，量 alpha bbox）：從這個範圍的下緣冒出來
    static readonly (string layer, Rect2 rc)[] _tearSrc =
    {
        ("表情/眼淚1", new Rect2(1030f, 660f, 75f, 16f)),
        ("表情/眼淚2", new Rect2(813f, 669f, 75f, 17f)),
        ("表情/眼淚3", new Rect2(795f, 684f, 18f, 27f)),
        ("表情/眼淚4", new Rect2(1105f, 669f, 20f, 24f)),
    };

    sealed partial class TearRing : Control
    {
        public float Radius, Width, Alpha;
        public override void _Draw()
        {
            DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 40, new Color(1f, 1f, 1f, 1f), Width, true);
        }
    }

    sealed class TearRingState
    {
        public TearRing Node; public Vector2 Origin; public float T, Phase, Dead = -1f;
    }

    readonly List<TearRingState> _tearRings = new();
    readonly float[] _tearTimer = new float[4];
    Image _tearFaceImg; bool _tearFaceTried;

    bool TearFaceInside(Vector2 canvasPos)
    {
        if (!_tearFaceTried)
        {
            _tearFaceTried = true;
            if (Layers.TryGetValue("臉", out var f) && f is TextureRect ft && ft.Texture != null)
            {
                _tearFaceImg = ft.Texture.GetImage();
                if (_tearFaceImg != null && _tearFaceImg.IsCompressed()) _tearFaceImg.Decompress();
            }
        }
        if (_tearFaceImg == null) return true;   // 取不到臉的圖就不判定
        Vector2 off = Layers.TryGetValue("臉", out var fl) && fl != null ? fl.Position : Vector2.Zero;
        var p = canvasPos - off;
        int x = Mathf.RoundToInt(p.X), y = Mathf.RoundToInt(p.Y);
        if (x < 0 || y < 0 || x >= _tearFaceImg.GetWidth() || y >= _tearFaceImg.GetHeight()) return false;
        return _tearFaceImg.GetPixel(x, y).A > 0.2f;
    }

    void UpdateTearRings(float dt)
    {
        if (!TearRingEnabled) { ClearTearRings(); return; }

        // 1) 生成：對應的眼淚圖層看得到才冒
        for (int i = 0; i < _tearSrc.Length; i++)
        {
            var (name, rc) = _tearSrc[i];
            if (!Layers.TryGetValue(name, out var tl) || tl == null || tl.GetParent() == null) continue;
            bool on = tl.IsVisibleInTree() && tl.Modulate.A > 0.05f && tl.SelfModulate.A > 0.05f;
            if (!on) { _tearTimer[i] = 0f; continue; }
            _tearTimer[i] -= dt;
            if (_tearTimer[i] > 0f || _tearRings.Count >= TearRingMax) continue;
            _tearTimer[i] = (float)GD.RandRange(TearRingInterval.X, TearRingInterval.Y);

            var parent = tl.GetParent();
            var node = new TearRing { Radius = TearRingRadius, Width = TearRingWidth, Alpha = TearRingAlpha, MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, TearRingAlpha) };
            parent.AddChild(node);
            parent.MoveChild(node, tl.GetIndex() + 1);   // 疊在眼淚圖層上面、前髮下面
            var origin = tl.Position + new Vector2(rc.Position.X + (float)GD.RandRange(0.15, 0.85) * rc.Size.X, rc.End.Y);
            node.Position = origin;
            _tearRings.Add(new TearRingState { Node = node, Origin = origin, Phase = (float)GD.RandRange(0.0, Mathf.Tau) });
        }

        // 2) 更新：往下＋左右擺動；離開臉就淡掉消失
        for (int i = _tearRings.Count - 1; i >= 0; i--)
        {
            var r = _tearRings[i];
            if (r.Node == null || !IsInstanceValid(r.Node)) { _tearRings.RemoveAt(i); continue; }
            r.T += dt;
            float y = r.Origin.Y + TearRingFall * r.T + 0.5f * TearRingAccel * r.T * r.T;
            float x = r.Origin.X + TearRingSway * Mathf.Sin(r.T * TearRingSwayHz * Mathf.Tau + r.Phase);
            var pos = new Vector2(x, y);
            r.Node.Position = pos;

            if (r.Dead < 0f)
            {
                // 圓圈最下緣離開臉就開始淡出
                if (!TearFaceInside(pos + new Vector2(0f, TearRingRadius))) r.Dead = 0f;
            }
            if (r.Dead >= 0f)
            {
                r.Dead += dt;
                float a = 1f - r.Dead / Mathf.Max(0.01f, TearRingFadeOut);
                if (a <= 0f) { r.Node.QueueFree(); _tearRings.RemoveAt(i); continue; }
                r.Node.Modulate = new Color(1f, 1f, 1f, TearRingAlpha * a);
            }
        }
    }

    void ClearTearRings()
    {
        foreach (var r in _tearRings) if (r.Node != null && IsInstanceValid(r.Node)) r.Node.QueueFree();
        _tearRings.Clear();
    }
}
