using Godot;

/// <summary>
/// 滑鼠停在熱點上時，游標上方出現提示氣泡（摸摸胸、捏乳頭…）；設置面板「懸停提示氣泡」可關。
/// 另外：左右手游標／摸胸的手大小統一（依手圖實際內容範圍正規化，左右手看起來一樣大）。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("懸停提示")]
    [Export] public bool HoverTipEnabled = true;
    [Export] public string[] HoverTipTexts = { "摸頭", "舌頭", "摸摸胸", "摸摸胸", "摸小穴", "摸尻", "摸腿", "摸腿" };   // 依熱點編號 0~7
    [Export] public string HoverTipNipple = "捏乳頭";
    [Export] public string HoverTipJiji = "插入";            // 雞雞出現後的 B 熱點
    [Export] public Vector2 HoverTipOffset = new Vector2(0f, -34f);   // 相對游標（氣泡底部中心）
    [Export] public int HoverTipFontSize = 16;
    [Export] public bool HandSizeNormalize = true;           // 左右手大小統一

    PanelContainer _hoverTip; Label _hoverTipLbl;
    string _hoverTipText; bool _hoverTipOn;

    public void HoverTipShow(string text) { _hoverTipText = text; _hoverTipOn = !string.IsNullOrEmpty(text); }
    public void HoverTipHide() { _hoverTipOn = false; }
    void HoverTipShowIdx(int idx)
    {
        if (idx < 0 || idx >= HoverTipTexts.Length) return;
        string t = HoverTipTexts[idx];
        if (idx == 4 && _jijiPhase > 0 && !string.IsNullOrEmpty(HoverTipJiji)) t = HoverTipJiji;
        HoverTipShow(t);
    }

    void UpdateHoverTip()
    {
        bool want = HoverTipEnabled && _hoverTipOn && !Input.IsMouseButtonPressed(MouseButton.Left) && !ClothDragActive;
        if (_hoverTip == null || !IsInstanceValid(_hoverTip))
        {
            if (!want || UiRoot == null) return;
            _hoverTip = new PanelContainer { Name = "HoverTip", MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 100 };
            var sb = new StyleBoxFlat { BgColor = new Color(1f, 1f, 1f, 0.95f), BorderColor = new Color(0.2f, 0.12f, 0.2f), CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10 };
            sb.SetBorderWidthAll(2); sb.ContentMarginLeft = 10; sb.ContentMarginRight = 10; sb.ContentMarginTop = 4; sb.ContentMarginBottom = 4;
            _hoverTip.AddThemeStyleboxOverride("panel", sb);
            _hoverTipLbl = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
            _hoverTipLbl.AddThemeColorOverride("font_color", new Color(0.2f, 0.1f, 0.2f));
            _hoverTip.AddChild(_hoverTipLbl);
            UiRoot.AddChild(_hoverTip);
        }
        _hoverTip.Visible = want;
        if (!want) return;
        _hoverTipLbl.AddThemeFontSizeOverride("font_size", HoverTipFontSize);
        _hoverTipLbl.Text = _hoverTipText;
        _hoverTip.ResetSize();
        Vector2 m = GetViewport().GetMousePosition();
        _hoverTip.Position = m + HoverTipOffset - new Vector2(_hoverTip.Size.X * 0.5f, _hoverTip.Size.Y);
    }

    // ── 條上的具體數值（勃起度／精力）──
    Label _semenNumLbl, _stamNumLbl;
    void UpdateBarNumbers()
    {
        if (_semenBar == null || _staminaBar == null) return;
        if (_semenNumLbl == null || !IsInstanceValid(_semenNumLbl)) _semenNumLbl = MakeBarNum(_semenBar);
        if (_stamNumLbl == null || !IsInstanceValid(_stamNumLbl)) _stamNumLbl = MakeBarNum(_staminaBar);
        _semenNumLbl.Text = $"{_semenLevel * 100f:F0}";
        _stamNumLbl.Text = $"{Stamina:F0}";
    }
    Label MakeBarNum(ProgressBar bar)
    {
        var l = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.Position = new Vector2(-8f, bar.Size.Y - 2f);
        l.Size = new Vector2(bar.Size.X + 16f, 16f);
        l.AddThemeFontSizeOverride("font_size", 12);
        l.AddThemeConstantOverride("outline_size", 4);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        bar.AddChild(l);
        return l;
    }

    // ── 左右手大小統一 ──
    readonly System.Collections.Generic.Dictionary<bool, float> _handContentMax = new();
    float HandContentMax(bool isLeft)
    {
        if (_handContentMax.TryGetValue(isLeft, out var v)) return v;
        float r = 0f;
        var tex = ResourceHelper.LoadTexAny(isLeft ? "Sprites/左手" : "Sprites/右手");
        if (tex != null)
        {
            var img = tex.GetImage();
            if (img != null)
            {
                if (img.IsCompressed()) img.Decompress();
                var u = img.GetUsedRect();
                r = Mathf.Max(u.Size.X, u.Size.Y);
            }
        }
        _handContentMax[isLeft] = r;
        return r;
    }

    /// <summary>手大小正規化係數：讓左右手內容的長邊一樣（保持兩隻的平均大小不變）。</summary>
    float HandNormK(bool isLeft)
    {
        if (!HandSizeNormalize) return 1f;
        float a = HandContentMax(true), b = HandContentMax(false);
        float me = isLeft ? a : b;
        if (a <= 0f || b <= 0f || me <= 0f) return 1f;
        return (a + b) * 0.5f / me;
    }
}
