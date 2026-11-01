using Godot;

/// <summary>TrainingRoomRuntime — 拖曳模式（🖱）：開啟後只能拖衣服／被子，綠點全部無視（不會點到）。</summary>
public partial class TrainingRoomRuntime
{
    Button _btnDrag, _btnMouse;
    bool _dragTool;

    void ToggleDragTool() => SetDragTool(!_dragTool);

    void SetDragTool(bool on)
    {
        _dragTool = on;
        if (on)
        {
            if (_tongueTool) { _tongueTool = false; if (_tongueOn) StopTongue(); RefreshTongueButton(); }
            if (_handMode != HandMode.None) { _handMode = HandMode.None; RefreshHandModeButtons(); }
        }
        RefreshDragButton();
        if (_hotspotRTs == null) return;
        foreach (var rt in _hotspotRTs)
        {
            if (rt == null || !IsInstanceValid(rt)) continue;
            var btn = rt.GetNodeOrNull<Button>("Btn");
            if (btn != null) btn.MouseFilter = on ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop;
        }
    }

    void RefreshDragButton()
    {
        if (_btnDrag == null || !IsInstanceValid(_btnDrag)) return;
        Color fill = _dragTool ? new Color(0.70f, 0.85f, 1.00f) : Colors.White;
        StyleBoxFlat Mk(Color c)
        {
            var sb = MkRoundBtnStyle(c);
            if (_dragTool) { sb.BorderColor = new Color(0.12f, 0.35f, 0.92f); sb.SetBorderWidthAll(5); }
            return sb;
        }
        _btnDrag.AddThemeStyleboxOverride("normal",  Mk(fill));
        _btnDrag.AddThemeStyleboxOverride("hover",   Mk(_dragTool ? fill.Lightened(0.15f) : fill.Darkened(0.06f)));
        _btnDrag.AddThemeStyleboxOverride("pressed", Mk(fill.Darkened(0.12f)));
        RefreshMouseButton();
    }

    /// <summary>用多邊形畫一張小圖示（填色＋深色描邊），不需要另外準備素材。</summary>
    static Texture2D MkPolyIcon(Vector2[][] polys, Color fill, Color line, int size = 44)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f) * (44f / size);
                bool inside = false; float dmin = 1e9f;
                foreach (var poly in polys)
                {
                    bool ins = false;
                    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                    {
                        Vector2 a = poly[i], b = poly[j];
                        if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) ins = !ins;
                        Vector2 ab = b - a; float t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(ab.LengthSquared(), 0.0001f), 0f, 1f);
                        dmin = Mathf.Min(dmin, (p - (a + ab * t)).Length());
                    }
                    inside |= ins;
                }
                if (inside && dmin > 2.0f) img.SetPixel(x, y, fill);
                else if (dmin <= 2.0f) img.SetPixel(x, y, line);
            }
        return ImageTexture.CreateFromImage(img);
    }

    static Texture2D MkCursorIcon() => MkPolyIcon(new[] { new[] {
        new Vector2(11, 5), new Vector2(11, 34), new Vector2(18, 27), new Vector2(23, 39), new Vector2(28, 37), new Vector2(23, 25), new Vector2(33, 25) } },
        Colors.White, new Color(0.25f, 0.15f, 0.22f));

    static Texture2D MkDressIcon() => MkPolyIcon(new[] { new[] {
        new Vector2(14, 4), new Vector2(18, 4), new Vector2(20, 10), new Vector2(24, 10), new Vector2(26, 4), new Vector2(30, 4),
        new Vector2(28, 17), new Vector2(36, 40), new Vector2(8, 40), new Vector2(16, 17) } },
        Colors.White, new Color(0.25f, 0.15f, 0.22f));

    /// <summary>🐭 滑鼠（預設操作）：沒選手／舌頭／衣服模式時就是紅色選取狀態；選了其他工具就取消選取。</summary>
    bool MouseToolActive => _handMode == HandMode.None && !_tongueTool && !_dragTool;

    void SelectMouseTool()
    {
        if (_dragTool) SetDragTool(false);
        if (_tongueTool) { _tongueTool = false; if (_tongueOn) StopTongue(); RefreshTongueButton(); }
        if (_handMode != HandMode.None) { _handMode = HandMode.None; RefreshHandModeButtons(); }
        RefreshMouseButton();
    }

    void RefreshMouseButton()
    {
        if (_btnMouse == null || !IsInstanceValid(_btnMouse)) return;
        bool sel = MouseToolActive;
        Color fill = sel ? new Color(1.00f, 0.80f, 0.86f) : Colors.White;
        StyleBoxFlat Mk(Color c)
        {
            var sb = MkRoundBtnStyle(c);
            if (sel) { sb.BorderColor = new Color(0.92f, 0.12f, 0.18f); sb.SetBorderWidthAll(5); }   // 紅色外圈
            return sb;
        }
        _btnMouse.AddThemeStyleboxOverride("normal",  Mk(fill));
        _btnMouse.AddThemeStyleboxOverride("hover",   Mk(sel ? fill.Lightened(0.15f) : fill.Darkened(0.06f)));
        _btnMouse.AddThemeStyleboxOverride("pressed", Mk(fill.Darkened(0.12f)));
    }
}
