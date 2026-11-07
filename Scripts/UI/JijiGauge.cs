using Godot;
using System;

/// <summary>
/// 程式畫出來的雞雞造型直條按鈕（不用貼圖）：
///   下面兩個圓（睪丸）、中間一條細長橢圓（莖）、上面蘑菇頭（龜頭）。
///   整個造型合成同一個輪廓（外框深色、內部填色），可以點擊（Pressed）。
///   ShowGauge＝true 時，內部由下往上填綠色（Value 0~1），當成精力條；false 時是空的造型按鈕。
/// 放在原本黃色「勃起度」直條的位置，硬了之後才出現（見 TrainingRoomRuntime.HudGate.cs）。
/// </summary>
public partial class JijiGauge : Control
{
    public event Action Pressed;
    public bool  ShowGauge;                    // 顯示內部的綠色精力條
    public float Value;                        // 0~1
    public Color OutlineColor = new Color(0.28f, 0.14f, 0.24f);
    public Color BodyColor    = new Color(1.00f, 0.80f, 0.66f);   // 還沒顯示綠條時的底色（跟原本 🍆 按鈕同色）
    public Color EmptyColor   = new Color(0.97f, 0.93f, 0.94f);   // 顯示綠條時，空的部分
    public Color GaugeColor   = new Color(0.30f, 0.72f, 0.48f);
    public float Border = 3f;

    bool _hover, _down;
    float _lastValue = -1f; bool _lastShow; Vector2 _lastSize; bool _lastHover, _lastDown;

    public override void _Ready()
    {
        MouseEntered += () => _hover = true;
        MouseExited  += () => { _hover = false; _down = false; };
    }

    public override void _Process(double delta)
    {
        if (!Mathf.IsEqualApprox(Value, _lastValue) || ShowGauge != _lastShow || Size != _lastSize || _hover != _lastHover || _down != _lastDown)
        {
            _lastValue = Value; _lastShow = ShowGauge; _lastSize = Size; _lastHover = _hover; _lastDown = _down;
            QueueRedraw();
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed) _down = true;
            else
            {
                bool wasDown = _down; _down = false;
                if (wasDown && GetRect().Size.X > 0f && new Rect2(Vector2.Zero, Size).HasPoint(mb.Position)) Pressed?.Invoke();
            }
            AcceptEvent();
        }
    }

    // ── 造型 ─────────────────────────────────────────────
    static Vector2[] Ellipse(float cx, float cy, float rx, float ry, int n = 40, float a0 = 0f, float a1 = Mathf.Tau)
    {
        var pts = new Vector2[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)n);
            pts[i] = new Vector2(cx + rx * Mathf.Cos(a), cy + ry * Mathf.Sin(a));
        }
        return pts;
    }

    /// <summary>蘑菇頭：上半是大圓頂，下半是比較扁的邊緣（比莖寬）。</summary>
    static Vector2[] Cap(float cx, float cy, float rx, float ryTop, float ryBottom)
    {
        var top = Ellipse(cx, cy, rx, ryTop, 24, Mathf.Pi, Mathf.Tau);        // 左 → 上 → 右
        var bot = Ellipse(cx, cy, rx, ryBottom, 16, 0f, Mathf.Pi);            // 右 → 下 → 左
        var all = new Vector2[top.Length + bot.Length];
        top.CopyTo(all, 0); bot.CopyTo(all, top.Length);
        return all;
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

    Vector2[] BuildSilhouette()
    {
        float w = Size.X, h = Size.Y, cx = w * 0.5f;
        float ballR = w * 0.235f, ballY = h - ballR - 2f;
        float capRx = w * 0.40f, capTop = capRx * 1.05f, capBot = capRx * 0.32f, capCy = capTop + 2f;
        float shaftRx = w * 0.25f;
        float shaftTop = capCy + capBot * 0.4f, shaftBot = ballY - ballR * 0.2f;
        float shaftCy = (shaftTop + shaftBot) * 0.5f, shaftRy = (shaftBot - shaftTop) * 0.5f + 8f;   // 兩端各多蓋 8px，和頭、球接在一起

        var parts = new[]
        {
            Ellipse(cx - ballR * 0.95f, ballY, ballR, ballR),   // 左球
            Ellipse(cx + ballR * 0.95f, ballY, ballR, ballR),   // 右球
            Ellipse(cx, shaftCy, shaftRx, shaftRy),             // 莖（細長橢圓）
            Cap(cx, capCy, capRx, capTop, capBot),              // 蘑菇頭
        };
        var sil = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var merged = Geometry2D.MergePolygons(sil, parts[i]);
            if (merged.Count > 0) sil = Largest(merged);
        }
        return sil;
    }

    public override void _Draw()
    {
        if (Size.X < 8f || Size.Y < 8f) return;
        var sil = BuildSilhouette();
        DrawColoredPolygon(sil, OutlineColor);

        var shrunk = Geometry2D.OffsetPolygon(sil, -Border);
        if (shrunk.Count == 0) return;
        var inner = Largest(shrunk);

        Color baseCol = ShowGauge ? EmptyColor : BodyColor;
        if (_down) baseCol = baseCol.Darkened(0.12f); else if (_hover) baseCol = baseCol.Lightened(0.12f);
        DrawColoredPolygon(inner, baseCol);

        if (ShowGauge && Value > 0.001f)
        {
            float v = Mathf.Clamp(Value, 0f, 1f);
            float top = Size.Y - Size.Y * v;
            var rect = new[] { new Vector2(-4f, top), new Vector2(Size.X + 4f, top), new Vector2(Size.X + 4f, Size.Y + 4f), new Vector2(-4f, Size.Y + 4f) };
            var fill = Geometry2D.IntersectPolygons(inner, rect);
            foreach (var poly in fill) if (poly.Length >= 3) DrawColoredPolygon(poly, GaugeColor);
        }
    }
}
