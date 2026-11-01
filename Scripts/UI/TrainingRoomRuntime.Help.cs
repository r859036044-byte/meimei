using Godot;
using System;

/// <summary>
/// 右上角「?」圓形按鈕 → 操作說明面板（附 UI 按鈕的實際圖示）。
/// 說明文字全部在下面 HelpRows() 裡，要改字直接改那裡。
/// </summary>
public partial class TrainingRoomRuntime
{
    Button  _helpBtn;
    Control _helpPanel;

    void SetupHelpButton()
    {
        _helpBtn = new Button
        {
            Name = "HelpButton", Text = "?", TooltipText = "操作說明",
            FocusMode = Control.FocusModeEnum.None,
        };
        _helpBtn.AnchorLeft = 1f; _helpBtn.AnchorRight = 1f; _helpBtn.AnchorTop = 0f; _helpBtn.AnchorBottom = 0f;
        _helpBtn.OffsetLeft = -224f; _helpBtn.OffsetRight = -178f;   // 放在「☰ 選單」左邊
        _helpBtn.OffsetTop = 20f;    _helpBtn.OffsetBottom = 66f;
        _helpBtn.AddThemeFontSizeOverride("font_size", 28);
        _helpBtn.AddThemeColorOverride("font_color", UiAccent);
        _helpBtn.AddThemeColorOverride("font_hover_color", UiAccent.Darkened(0.2f));
        StyleBoxFlat Circle(Color bg)
        {
            var sb = new StyleBoxFlat { BgColor = bg, BorderColor = BarOutline };
            sb.SetBorderWidthAll(3); sb.SetCornerRadiusAll(23);
            return sb;
        }
        _helpBtn.AddThemeStyleboxOverride("normal",  Circle(Colors.White));
        _helpBtn.AddThemeStyleboxOverride("hover",   Circle(new Color(1f, 0.92f, 0.96f)));
        _helpBtn.AddThemeStyleboxOverride("pressed", Circle(new Color(0.95f, 0.85f, 0.90f)));
        _helpBtn.AddThemeStyleboxOverride("focus",   new StyleBoxEmpty());
        _helpBtn.Pressed += () => ShowHelp(!(_helpPanel?.Visible ?? false));
        UiRoot.AddChild(_helpBtn);
    }

    void ShowHelp(bool show)
    {
        if (show && _helpPanel == null) BuildHelpPanel();
        if (_helpPanel != null) _helpPanel.Visible = show;
    }

    /// <summary>(圖示種類, 標題, 說明)。圖示："L"/"R"＝手按鈕圖、"emoji:🍆"＝圓形按鈕、"dot"＝綠點、"bar:色碼"＝直條、"key:文字"＝按鍵。</summary>
    static (string icon, string title, string desc)[] HelpRows() => new[]
    {
        ("key:左鍵",   "點擊物件",       "點擊角色身上的部位開始互動。"),
        ("key:右鍵",   "取消",           "在正在互動的部位上按右鍵取消。"),
        ("key:拖曳",   "調整速度",       "互動中按住左鍵往上拖＝加速，往下拖＝減速。"),
        ("key:滾輪",   "縮放鏡頭",       "滾輪放大／縮小，按住中鍵或右鍵拖曳移動畫面。"),
        ("L",          "左手／右手",     "指定要用哪一隻手（紅圈＝已選），再點一次取消。"),
        ("emoji:🍆",   "🍆",             "出現／收起。"),
        ("emoji:👅",   "👅",             "嘴巴直接點就會用舌頭；選取 👅 後，點胸部、乳頭或下方也會改成用舌頭。再點一次按鈕取消選取。"),
        ("bar:5cb87a", "精力",           "左下角綠色直條。"),
        ("bar:e0609a", "精液",           "左下角粉色直條。"),
        ("emoji:🚪",   "離開房間",       "結束今天。"),
        ("key:☰",      "選單",           "保存／讀檔／設定。"),
    };

    void BuildHelpPanel()
    {
        // 半透明遮罩：點空白處關閉
        var dim = new ColorRect { Name = "HelpPanel", Color = new Color(0f, 0f, 0f, 0.35f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) ShowHelp(false); };
        UiRoot.AddChild(dim);
        _helpPanel = dim;

        var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        card.AnchorLeft = 0.5f; card.AnchorRight = 0.5f; card.AnchorTop = 0.5f; card.AnchorBottom = 0.5f;
        card.OffsetLeft = -380f; card.OffsetRight = 380f; card.OffsetTop = -330f; card.OffsetBottom = 330f;
        var cs = MkCuteCardStyle(20); cs.SetContentMarginAll(18);
        card.AddThemeStyleboxOverride("panel", cs);
        dim.AddChild(card);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        card.AddChild(col);

        var head = new HBoxContainer();
        col.AddChild(head);
        var title = new Label { Text = "操作說明", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", UiAccent);
        head.AddChild(title);
        var close = new Button { Text = "✕", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(40, 40) };
        close.Pressed += () => ShowHelp(false);
        head.AddChild(close);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        col.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(list);

        foreach (var (icon, t, d) in HelpRows())
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 14);
            list.AddChild(row);
            row.AddChild(MakeHelpIcon(icon));

            var txt = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            txt.AddThemeConstantOverride("separation", 0);
            row.AddChild(txt);
            var tl = new Label { Text = t };
            tl.AddThemeFontSizeOverride("font_size", 20);
            tl.AddThemeColorOverride("font_color", UiText);
            txt.AddChild(tl);
            var dl = new Label { Text = d, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            dl.AddThemeFontSizeOverride("font_size", 16);
            dl.AddThemeColorOverride("font_color", UiMuted.Darkened(0.2f));
            txt.AddChild(dl);
        }
    }

    /// <summary>說明列左邊的小圖示（盡量用跟畫面上一樣的樣式／圖）。</summary>
    Control MakeHelpIcon(string icon)
    {
        const float S = 48f;
        var box = new Panel { CustomMinimumSize = new Vector2(S, S), MouseFilter = Control.MouseFilterEnum.Ignore };
        StyleBoxFlat Round(Color bg, int r)
        {
            var sb = new StyleBoxFlat { BgColor = bg, BorderColor = BarOutline };
            sb.SetBorderWidthAll(2); sb.SetCornerRadiusAll(r);
            return sb;
        }

        if (icon == "L" || icon == "R")
        {
            box.AddThemeStyleboxOverride("panel", Round(Colors.White, (int)(S / 2f)));
            var tex = icon == "L" ? _handIconLeftTex : _handIconRightTex;
            if (tex != null)
            {
                var tr = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
                tr.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                tr.OffsetLeft = 8; tr.OffsetTop = 8; tr.OffsetRight = -8; tr.OffsetBottom = -8;
                box.AddChild(tr);
            }
            return box;
        }
        if (icon == "dot")
        {
            box.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            var dot = new TextureRect { Texture = MakeCircleTexture(14, new Color(0.4f, 1f, 0.5f, 0.95f)),
                Position = new Vector2(S / 2f - 14f, S / 2f - 14f), Size = new Vector2(28, 28), MouseFilter = Control.MouseFilterEnum.Ignore };
            box.AddChild(dot);
            return box;
        }
        if (icon.StartsWith("bar:"))
        {
            box.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            var bar = new Panel { Position = new Vector2(S / 2f - 9f, 2f), Size = new Vector2(18, S - 4f), MouseFilter = Control.MouseFilterEnum.Ignore };
            bar.AddThemeStyleboxOverride("panel", Round(new Color(icon.Substring(4)), 9));
            box.AddChild(bar);
            return box;
        }

        bool emoji = icon.StartsWith("emoji:");
        string text = icon.Substring(icon.IndexOf(':') + 1);
        box.AddThemeStyleboxOverride("panel", Round(emoji ? new Color(1f, 0.85f, 0.75f) : Colors.White, emoji ? (int)(S / 2f) : 10));
        var lbl = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        lbl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        lbl.AddThemeFontSizeOverride("font_size", emoji ? 26 : (text.Length > 2 ? 13 : 18));
        lbl.AddThemeColorOverride("font_color", UiText);
        box.AddChild(lbl);
        return box;
    }
}
