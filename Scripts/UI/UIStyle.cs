using Godot;

/// <summary>奶白底、玫瑰粉細框的共用按鈕樣式；由 Godot 原生繪製。</summary>
public static class UIStyle
{
    public static readonly Color BtnTextColor = new Color("634553");
    public static readonly Color BtnLabelColor = new Color("ffe6f2");
    public const float BtnBorderThickness = 2f;
    public const float BtnWhiteBorder = 2f;
    public const int BtnCornerRadius = 16;

    static StyleBoxFlat ButtonFace(string fill, string border, bool withBorder, int shadow = 3)
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(fill), BorderColor = new Color(border),
            BorderWidthTop = withBorder ? 2 : 0, BorderWidthBottom = withBorder ? 4 : 0,
            BorderWidthLeft = withBorder ? 2 : 0, BorderWidthRight = withBorder ? 2 : 0,
            CornerRadiusTopLeft = 24, CornerRadiusTopRight = 24,
            CornerRadiusBottomLeft = 24, CornerRadiusBottomRight = 24,
            CornerDetail = 8, AntiAliasing = true,
            ContentMarginLeft = 8, ContentMarginRight = 8,
            ContentMarginTop = 4, ContentMarginBottom = 4,
            ShadowColor = new Color(0.40f, 0.22f, 0.30f, 0.16f),
            ShadowSize = shadow, ShadowOffset = new Vector2(0, shadow)
        };
    }

    public static void ApplyButtonStyle(Button btn, bool withBorder = true)
    {
        if (btn == null) return;
        btn.Flat = false;
        CuteButtonMotion.Attach(btn);
        btn.AddThemeStyleboxOverride("normal", ButtonFace("fffafb", "e8bccd", withBorder));
        btn.AddThemeStyleboxOverride("hover", ButtonFace("fff0f6", "cf809f", withBorder));
        btn.AddThemeStyleboxOverride("pressed", ButtonFace("f6dce7", "bd6d8d", withBorder, 0));
        btn.AddThemeStyleboxOverride("hover_pressed", ButtonFace("f6dce7", "bd6d8d", withBorder, 0));
        btn.AddThemeStyleboxOverride("disabled", ButtonFace("eee8eb", "d7ccd1", withBorder, 0));
        var focus = ButtonFace("ffffff", "b76185", true, 0);
        focus.DrawCenter = false;
        btn.AddThemeStyleboxOverride("focus", focus);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color" })
            btn.AddThemeColorOverride(state, BtnTextColor);
        btn.AddThemeColorOverride("font_disabled_color", new Color("978993"));
        btn.AddThemeConstantOverride("outline_size", 0);
        btn.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
    }

    /// <summary>
    /// 快速建立已套用樣式的 Button 節點
    /// </summary>
    public static Button CreateStyledButton(string text, int fontSize = 28)
    {
        var btn = new Button();
        btn.Text = text;
        btn.AddThemeFontSizeOverride("font_size", fontSize);

        // 自適應大小
        btn.CustomMinimumSize = new Vector2(200, 50);
        btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

        ApplyButtonStyle(btn);
        return btn;
    }

    /// <summary>
    /// 建立帶白色最外框效果的按鈕容器
    /// 回傳外層 Panel（需加入場景樹），按鈕已作為子節點
    /// </summary>
    public static PanelContainer CreateBorderedButton(string text, int fontSize = 28)
    {
        // 外層白框容器
        var outerPanel = new PanelContainer();
        var outerStyle = new StyleBoxFlat();
        outerStyle.BgColor = new Color(1f, 1f, 1f, 0f);  // 透明背景
        outerStyle.BorderColor = new Color(1f, 1f, 1f, 0.95f);
        outerStyle.BorderWidthTop     = (int)BtnWhiteBorder;
        outerStyle.BorderWidthBottom  = (int)BtnWhiteBorder;
        outerStyle.BorderWidthLeft    = (int)BtnWhiteBorder;
        outerStyle.BorderWidthRight   = (int)BtnWhiteBorder;
        outerStyle.CornerRadiusTopLeft     = BtnCornerRadius + (int)BtnBorderThickness + (int)BtnWhiteBorder;
        outerStyle.CornerRadiusTopRight    = BtnCornerRadius + (int)BtnBorderThickness + (int)BtnWhiteBorder;
        outerStyle.CornerRadiusBottomLeft  = BtnCornerRadius + (int)BtnBorderThickness + (int)BtnWhiteBorder;
        outerStyle.CornerRadiusBottomRight = BtnCornerRadius + (int)BtnBorderThickness + (int)BtnWhiteBorder;
        outerStyle.AntiAliasing     = true;
        outerStyle.AntiAliasingSize = 1.5f;
        outerStyle.ContentMarginTop    = (int)BtnWhiteBorder;
        outerStyle.ContentMarginBottom = (int)BtnWhiteBorder;
        outerStyle.ContentMarginLeft   = (int)BtnWhiteBorder;
        outerStyle.ContentMarginRight  = (int)BtnWhiteBorder;
        outerPanel.AddThemeStyleboxOverride("panel", outerStyle);

        // 內部按鈕
        var btn = CreateStyledButton(text, fontSize);
        outerPanel.AddChild(btn);

        return outerPanel;
    }

    // ═══════════════════════════════════════════════════════════
    //  Label 樣式輔助
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 套用標準文字描邊樣式到 Label
    /// </summary>
    public static void ApplyLabelOutline(Label label, Color? textColor = null,
        int outlineSize = 2, Color? outlineColor = null)
    {
        if (label == null) return;

        label.AddThemeColorOverride("font_color",
            textColor ?? BtnLabelColor);
        label.AddThemeConstantOverride("outline_size", outlineSize);
        label.AddThemeColorOverride("font_outline_color",
            outlineColor ?? new Color(0f, 0f, 0f, 0.8f));
    }

    /// <summary>
    /// 套用陰影效果到 Label
    /// </summary>
    public static void ApplyLabelShadow(Label label,
        Color? shadowColor = null, Vector2? offset = null)
    {
        if (label == null) return;

        var sColor = shadowColor ?? new Color(0f, 0f, 0f, 0.5f);
        var sOffset = offset ?? new Vector2(2, 2);

        label.AddThemeColorOverride("font_shadow_color", sColor);
        label.AddThemeConstantOverride("shadow_offset_x", (int)sOffset.X);
        label.AddThemeConstantOverride("shadow_offset_y", (int)sOffset.Y);
    }

    // ═══════════════════════════════════════════════════════════
    //  Panel 樣式輔助
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 建立羊皮紙風格面板 StyleBox（供存檔畫面等使用）
    /// </summary>
    public static StyleBoxFlat CreateParchmentStyle()
    {
        var sb = new StyleBoxFlat();
        sb.BgColor = new Color(0.96f, 0.87f, 0.70f, 0.95f); // 羊皮紙黃褐色

        sb.CornerRadiusTopLeft     = 12;
        sb.CornerRadiusTopRight    = 12;
        sb.CornerRadiusBottomLeft  = 12;
        sb.CornerRadiusBottomRight = 12;

        sb.BorderColor       = new Color(0.55f, 0.35f, 0.17f, 0.8f); // 深棕色邊框
        sb.BorderWidthTop    = 4;
        sb.BorderWidthBottom = 4;
        sb.BorderWidthLeft   = 4;
        sb.BorderWidthRight  = 4;

        sb.AntiAliasing     = true;
        sb.AntiAliasingSize = 1.5f;

        sb.ContentMarginTop    = 20;
        sb.ContentMarginBottom = 20;
        sb.ContentMarginLeft   = 20;
        sb.ContentMarginRight  = 20;

        return sb;
    }

    /// <summary>
    /// 建立半透明暗色覆蓋 StyleBox（供彈窗背景等使用）
    /// </summary>
    public static StyleBoxFlat CreateDimOverlayStyle(float alpha = 0.5f)
    {
        var sb = new StyleBoxFlat();
        sb.BgColor = new Color(0f, 0f, 0f, alpha);
        return sb;
    }
}
