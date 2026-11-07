using Godot;
using System;

/// <summary>
/// 右上角「?」圓形按鈕 → 操作說明面板（附 UI 按鈕的實際圖示）。
/// 說明文字（共 3 頁）全部在下面 HelpPages() 裡，要改字直接改那裡。
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

    // ── 操作說明：3 頁 ──────────────────────────────────────────
    //   第 1 頁：按鈕的功能　第 2 頁：按住向上／向下調整速度　第 3 頁：射精
    // 圖示種類："L"/"R"＝手按鈕圖、"emoji:🍆"＝圓形按鈕、"tex:cursor"／"tex:dress"＝滑鼠／衣服按鈕的圖、
    //          "jiji"＝雞雞造型按鈕、"key:文字"＝按鍵、"bar:色碼"＝直條。要改字直接改下面。
    static (string title, (string icon, string title, string desc)[] rows)[] HelpPages() => new[]
    {
        ("第 1 頁　按鈕的功能", new[]
        {
            ("tex:cursor", "滑鼠",         "預設的操作方式。選了其他工具時，點這顆回到滑鼠。"),
            ("tex:dress",  "衣服模式",     "只拖衣服／被子，忽略身上的綠點。再點一次取消。"),
            ("L",          "左手",         "指定要用左手（紅圈＝已選），再點一次取消。"),
            ("R",          "右手",         "指定要用右手（紅圈＝已選），再點一次取消。"),
            ("emoji:👅",   "舌頭",         "嘴巴直接點就會用舌頭；選取後，點胸部、乳頭或下方也會改用舌頭。再點一次取消。"),
            ("jiji",       "雞雞",         "男主說「硬了」之後才會出現。點一下叫出雞雞，再點一下收起；叫出後，裡面的溫度計會顯示男主的精力。"),
            ("emoji:🧘",   "忍耐",         "在雞雞按鈕旁邊，一樣硬了才出現。精力快滿時按，可以忍回去。"),
            ("emoji:🚪",   "離開房間",     "結束今天。"),
            ("key:☰",      "選單",         "保存／讀檔／設定。"),
            ("key:F12",    "截圖",         "按 F12 把目前畫面存成圖片；Shift＋F12 存成不含介面的版本；Ctrl＋F12 開啟存放截圖的資料夾。"),
        }),
        ("第 2 頁　調整速度", new[]
        {
            ("key:左鍵",   "點擊物件",     "點角色身上的部位開始互動；在正在互動的部位上按右鍵取消。"),
            ("key:↑",      "按住向上",     "互動中按住左鍵往上拖＝加速。"),
            ("key:↓",      "按住向下",     "互動中按住左鍵往下拖＝減速。"),
            ("key:滾輪",   "縮放鏡頭",     "滾輪放大／縮小，按住中鍵或右鍵拖曳移動畫面。"),
        }),
        ("第 3 頁　射精", new[]
        {
            ("jiji",       "溫度計",       "雞雞按鈕中間的溫度計，白色會隨男主的精力慢慢升高，滿格＝精力 100。"),
            ("emoji:💦",   "滿了會怎樣",   "滿格時雞雞按鈕會放大並震動，同時男主說「不行了，要射了」。不處理的話，不動約 5 秒、抽插時約 3 秒就會射出來。"),
            ("emoji:💬",   "兩個選項",     "「慢下來」：降低抽插速度、爭取一點時間。「射進去」：直接射精。"),
            ("emoji:🧘",   "忍耐",         "滿了的時候可以按忍耐忍回去。但抽插太快會忍不住，這時只會漏出一部分（約 30～50%）。"),
            ("bar:ffffff", "精液量",       "總量約 300%，一次滿量射掉 100%。用完就會軟，要等回復一些才能再硬。"),
        }),
    };

    int _helpPage;
    Label _helpTitleLbl, _helpPageLbl;
    VBoxContainer _helpList;
    Button _helpPrev, _helpNext;

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
        _helpTitleLbl = new Label { Text = "操作說明", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _helpTitleLbl.AddThemeFontSizeOverride("font_size", 28);
        _helpTitleLbl.AddThemeColorOverride("font_color", UiAccent);
        head.AddChild(_helpTitleLbl);
        var close = new Button { Text = "✕", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(40, 40) };
        close.Pressed += () => ShowHelp(false);
        head.AddChild(close);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        col.AddChild(scroll);
        _helpList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _helpList.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_helpList);

        // 換頁列：◀ 上一頁　1 / 3　下一頁 ▶
        var nav = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        nav.AddThemeConstantOverride("separation", 16);
        col.AddChild(nav);
        _helpPrev = new Button { Text = "◀ 上一頁", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 40) };
        _helpNext = new Button { Text = "下一頁 ▶", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(120, 40) };
        _helpPageLbl = new Label { HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(80, 0) };
        _helpPageLbl.AddThemeFontSizeOverride("font_size", 20);
        _helpPageLbl.AddThemeColorOverride("font_color", UiText);
        _helpPrev.Pressed += () => ShowHelpPage(_helpPage - 1);
        _helpNext.Pressed += () => ShowHelpPage(_helpPage + 1);
        nav.AddChild(_helpPrev); nav.AddChild(_helpPageLbl); nav.AddChild(_helpNext);

        ShowHelpPage(0);
    }

    void ShowHelpPage(int page)
    {
        var pages = HelpPages();
        _helpPage = Mathf.Clamp(page, 0, pages.Length - 1);
        var (pageTitle, rows) = pages[_helpPage];
        _helpTitleLbl.Text = "操作說明　" + pageTitle;
        _helpPageLbl.Text = $"{_helpPage + 1} / {pages.Length}";
        _helpPrev.Disabled = _helpPage == 0;
        _helpNext.Disabled = _helpPage == pages.Length - 1;

        foreach (var c in _helpList.GetChildren()) { _helpList.RemoveChild(c); c.QueueFree(); }

        // 試玩版沒有射精：第 3 頁最上面加一行提醒
        if (_helpPage == 2 && DemoMode.Enabled)
        {
            var note = new Label { Text = "※ 試玩版：射精的瞬間，試玩就結束了（黑屏顯示「試玩版已結束」）。", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            note.AddThemeFontSizeOverride("font_size", 17);
            note.AddThemeColorOverride("font_color", UiAccent);
            _helpList.AddChild(note);
        }

        foreach (var (icon, t, d) in rows)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 14);
            _helpList.AddChild(row);
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
        if (icon == "tex:cursor" || icon == "tex:dress")
        {
            box.AddThemeStyleboxOverride("panel", Round(Colors.White, (int)(S / 2f)));
            var tr = new TextureRect { Texture = icon == "tex:cursor" ? MkCursorIcon() : MkDressIcon(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
            tr.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            tr.OffsetLeft = 6; tr.OffsetTop = 6; tr.OffsetRight = -6; tr.OffsetBottom = -6;
            box.AddChild(tr);
            return box;
        }
        if (icon == "jiji")
        {   // 雞雞造型按鈕（縮小版，顯示溫度計）：用同一個 JijiGauge 控制項畫
            box.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
            var g = new JijiGauge { Position = new Vector2(S / 2f - 14f, -2f), Size = new Vector2(28f, S + 4f), ShowGauge = true, Value = 0.6f, MouseFilter = Control.MouseFilterEnum.Ignore };
            box.AddChild(g);
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
