using Godot;
using System;

/// <summary>
/// 程式畫出來的雞雞造型直條按鈕（不用貼圖）：
///   下面兩個圓（睪丸）、中間一條細長橢圓（莖）、上面蘑菇頭（龜頭）。
///   整個造型合成同一個輪廓（外框深色、內部填色），可以點擊（Pressed）。
///   ShowGauge＝true 時，莖的中間出現一根溫度計（黑框黑底，白色由下往上慢慢升，Value 0~1）；false 時只有膚色造型。
///   Full＝true（精力滿了）時，整個造型從底部中心放大並震動。
/// 放在原本黃色「勃起度」直條的位置，硬了之後才出現（見 TrainingRoomRuntime.HudGate.cs）。
/// </summary>
public partial class JijiGauge : Control
{
    public event Action Pressed;
    public bool  ShowGauge;                    // 顯示內部的綠色精力條
    public float Value;                        // 0~1
    public Color OutlineColor = new Color(0.28f, 0.14f, 0.24f);
    public Color BodyColor    = new Color(1.00f, 0.80f, 0.66f);   // 膚色（一律；跟原本 🍆 按鈕同色）
    public Color ThermoFrame  = new Color(0f, 0f, 0f);            // 溫度計黑框
    public Color ThermoBack   = new Color(0.08f, 0.08f, 0.10f);   // 溫度計黑底
    public Color ThermoFill   = Colors.White;                     // 升上來的白色
    public float ThermoBorder = 2.2f;                             // 黑框粗細
    public float ThermoTubeR  = 0.075f;                           // 管子半徑（相對整體寬度）
    public float ThermoTopFrac = 0.10f, ThermoBottomFrac = 0.82f; // 溫度計在莖上的範圍（從莖頂往下佔莖長的幾成）
    public bool  Full;                                            // 滿了：整個放大並震動
    public float FullScale = 1.22f;                               // 滿了放大的倍率（從底部中心放大）
    public float ShakeAmp  = 0.07f;                               // 滿了震動的角度（弧度，約 4°）
    public float Border = 3f;
    public float ShaftMaxRx = 0.31f;    // 莖最寬處的半寬（相對整體寬度）
    public float ShaftSplit = 0.30f;    // 最寬的位置（從莖頂往下佔莖長的幾成；0.30＝離頂端 30%，也就是從底往上 70% 高；越大越往下）
    public float CapWidthScale = 0.90f;       // 蘑菇頭的寬度倍率（0.90＝頭窄 10%）；只影響頭，不影響莖
    public float ShaftNeck  = 0.60f;    // 跟蘑菇頭接合處的寬度，佔最寬處的幾成（越小，上面鼓出來的弧度越明顯；1＝沒有弧度）
    public float ShaftBottomKeep = 0.45f;   // 莖最底端的寬度，佔最寬處的幾成（越小下面越瘦；1＝不收細）

    bool _hover, _down;
    float _scaleCur = 1f, _rotCur = 0f, _time;
    float _lastValue = -1f; bool _lastShow; Vector2 _lastSize; bool _lastHover, _lastDown;

    public override void _Ready()
    {
        MouseEntered += () => _hover = true;
        MouseExited  += () => { _hover = false; _down = false; };
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta; _time += dt;
        // 滿了：從底部中心放大並震動；沒滿就回到原樣
        PivotOffset = new Vector2(Size.X * 0.5f, Size.Y);
        float k = 1f - Mathf.Exp(-dt * 10f);
        _scaleCur = Mathf.Lerp(_scaleCur, Full ? FullScale : 1f, k);
        float rot = Full ? (Mathf.Sin(_time * 70f) + 0.5f * Mathf.Sin(_time * 113f)) * ShakeAmp * 0.67f : 0f;
        _rotCur = Mathf.Lerp(_rotCur, rot, Full ? 0.6f : k);
        Scale = new Vector2(_scaleCur, _scaleCur);
        Rotation = _rotCur;

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

    /// <summary>蛋形（連續曲線）：從 yTop 到 yBot，最寬處在 split（0~1）。
    /// 最寬處以上是圓頂（橢圓）；以下平滑收細到最底端剩 bottomKeep 倍寬（不收成尖）。左右對稱。</summary>
    static Vector2[] Egg(float cx, float yTop, float yBot, float split, float rx, float bottomKeep, float neck, int n = 40)
    {
        float len = yBot - yTop;
        float Half(float t)   // t：0＝頂、1＝底，回傳該處半寬
        {
            if (t <= split) { float u = (split - t) / Mathf.Max(0.001f, split); return rx * (neck + (1f - neck) * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u))); }   // 頸（接合處）→ 鼓到最寬
            float v = (t - split) / Mathf.Max(0.001f, 1f - split);
            return rx * (bottomKeep + (1f - bottomKeep) * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(v, 1.6f)), 0.9f));
        }
        var pts = new Vector2[(n + 1) * 2];
        for (int i = 0; i <= n; i++)   // 右邊：頂 → 底
        {
            float t = i / (float)n;
            pts[i] = new Vector2(cx + Half(t), yTop + len * t);
        }
        for (int i = 0; i <= n; i++)   // 左邊：底 → 頂
        {
            float t = 1f - i / (float)n;
            pts[n + 1 + i] = new Vector2(cx - Half(t), yTop + len * t);
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

    /// <summary>莖的上下範圍（跟蘑菇頭、球接起來的位置）。輪廓和溫度計都用它。</summary>
    void ShaftSpan(out float shaftTop, out float shaftBot)
    {
        float w = Size.X, h = Size.Y;
        float ballR = w * 0.235f, ballY = h - ballR - 2f;
        float capBaseRx = w * 0.40f, capTop = capBaseRx * 1.05f, capBot = capBaseRx * 0.32f, capCy = capTop + 2f;
        shaftTop = capCy + capBot * 0.4f - 6f;          // 兩端各多蓋 6px，和頭、球接在一起
        shaftBot = ballY - ballR * 0.2f + 6f;
    }

    Vector2[] BuildSilhouette()
    {
        float w = Size.X, h = Size.Y, cx = w * 0.5f;
        float ballR = w * 0.235f, ballY = h - ballR - 2f;
        float capBaseRx = w * 0.40f;
        float capRx = capBaseRx * CapWidthScale;                                  // 頭的寬度（只有頭會窄）
        float capTop = capBaseRx * 1.05f, capBot = capBaseRx * 0.32f, capCy = capTop + 2f;   // 頭的高度不變
        ShaftSpan(out float shaftTop, out float shaftBot);

        var parts = new[]
        {
            Ellipse(cx - ballR * 0.95f, ballY, ballR, ballR),   // 左球
            Ellipse(cx + ballR * 0.95f, ballY, ballR, ballR),   // 右球
            Egg(cx, shaftTop, shaftBot, ShaftSplit, w * ShaftMaxRx, ShaftBottomKeep, ShaftNeck),   // 莖：蛋形（上胖下瘦）
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

    /// <summary>溫度計外形：上面圓頭的細管＋下面一顆圓球，放在莖的中間。</summary>
    Vector2[] BuildThermo()
    {
        ShaftSpan(out float st, out float sb);
        float len = sb - st, cx = Size.X * 0.5f;
        float tubeR = Size.X * ThermoTubeR, bulbR = tubeR * 1.65f;
        float yTop = st + len * ThermoTopFrac;
        float yBulb = st + len * ThermoBottomFrac - bulbR;                 // 球心
        var cap = Ellipse(cx, yTop + tubeR, tubeR, tubeR, 20, Mathf.Pi, Mathf.Tau);   // 管上端半圓
        var tube = new Vector2[cap.Length + 2];
        cap.CopyTo(tube, 0);
        tube[cap.Length] = new Vector2(cx + tubeR, yBulb); tube[cap.Length + 1] = new Vector2(cx - tubeR, yBulb);
        var merged = Geometry2D.MergePolygons(tube, Ellipse(cx, yBulb, bulbR, bulbR, 32));
        return merged.Count > 0 ? Largest(merged) : tube;
    }

    public override void _Draw()
    {
        if (Size.X < 8f || Size.Y < 8f) return;
        var sil = BuildSilhouette();
        DrawColoredPolygon(sil, OutlineColor);

        var shrunk = Geometry2D.OffsetPolygon(sil, -Border);
        if (shrunk.Count == 0) return;
        var inner = Largest(shrunk);

        Color baseCol = BodyColor;   // 一律膚色
        if (_down) baseCol = baseCol.Darkened(0.12f); else if (_hover) baseCol = baseCol.Lightened(0.12f);
        DrawColoredPolygon(inner, baseCol);

        if (!ShowGauge) return;

        // 溫度計：黑框、黑底，白色液體由下往上慢慢升
        var thermo = BuildThermo();
        DrawColoredPolygon(thermo, ThermoFrame);
        var thIn = Geometry2D.OffsetPolygon(thermo, -ThermoBorder);
        if (thIn.Count == 0) return;
        var tin = Largest(thIn);
        DrawColoredPolygon(tin, ThermoBack);

        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in tin) { minY = Mathf.Min(minY, p.Y); maxY = Mathf.Max(maxY, p.Y); }
        float v = Mathf.Clamp(Value, 0f, 1f);
        if (v > 0.001f)
        {
            float top = maxY - (maxY - minY) * v;
            var rect = new[] { new Vector2(-4f, top), new Vector2(Size.X + 4f, top), new Vector2(Size.X + 4f, Size.Y + 4f), new Vector2(-4f, Size.Y + 4f) };
            foreach (var poly in Geometry2D.IntersectPolygons(tin, rect)) if (poly.Length >= 3) DrawColoredPolygon(poly, ThermoFill);
        }
    }
}
