using Godot;
using System;

/// <summary>
/// 圖鑒房（Gallery）全螢幕面板
/// 含三個頁籤：結局 / CG / 分支劇情
/// 由 MainMenuRuntime 實例化並加入場景樹；呼叫 Open() / Close() 控制顯隱
/// </summary>
public partial class GalleryScreen : Control
{
    // ─── 靜態內容定義 ────────────────────────────────────────────

    public static readonly (string name, string desc)[] EndingInfo =
    {
        ("", ""),                                              // [0] 不使用
        ("斷線的風箏",         "Nothing線 插入後 — N024"),         // E1
        ("衝動的代價",         "Nothing線 挑戰失敗 — N022"),        // E2
        ("雌小鬼的專屬暗號",   "Nothing線 挑戰成功 — N021"),        // E3
        ("雜魚結局",           "Nothing線 什麼都不做 — N017"),      // E4
        ("無聲的拒絕",         "摸摸線 沒高潮路線 — N037"),          // E5
        ("恰如其分的距離",     "高潮線 傍晚不進入 — N045"),          // E6
        ("無盡的榨取",         "高潮線 摸摸沒高潮 — N051"),          // E7
        ("雌小鬼的專屬早餐",   "高潮線 摸摸高潮 — N052"),            // E8
        ("最熟悉的陌生人",     "B線（沒高潮）後續 — N058"),          // E9
        ("雌小鬼妹妹的專屬早餐2", "高潮線 插入高潮 — N060"),         // E10
        ("淪為妹妹的專屬玩具", "B線 插入高潮 — N056"),               // E11
        ("粗暴的代價",         "插入線 傍晚沒插 — N077"),            // E12
        ("沉淪的課後輔導",     "插入線 傍晚插入 — N076"),            // E13
        ("浴室裡的沉淪",       "高潮線 浴室邀請高潮 — N085"),        // E14
        ("笨拙的代價",         "高潮線 浴室邀請沒高潮 — N084"),      // E15
        ("笨拙的做愛",         "高潮線 浴室邀請高潮後 — N086"),      // E16
        ("被榨取的雜魚玩具",   "插入高潮線 傍晚分歧B — N089"),       // E17
        ("沉迷於主從遊戲的兄妹","插入高潮線 傍晚分歧A — N088"),      // E18
    };

    // 樹狀圖 E# → 實際場景檔名對照（null = 尚未製作）
    // E3 使用 Ending2.tscn（NothingRoute 路徑），E6 使用 Ending3.tscn（恰如其分的距離）
    static readonly string[] EndingSceneFile = new string[19]
    {
        null,        // [0] 不使用
        "Ending10",  // E1  斷線的風箏
        "Ending1",   // E2  衝動的代價（舊 Ending1 = Day3 bad end）
        "Ending2",   // E3  雌小鬼的專屬暗號（舊 Ending2 NothingRoute 路徑）
        "Ending4",   // E4  雜魚結局
        "Ending5",   // E5  無聲的拒絕
        "Ending3",   // E6  恰如其分的距離（舊 Ending3）
        "Ending7",   // E7  無盡的榨取
        "Ending8",   // E8  雌小鬼的專屬早餐
        "Ending9",   // E9  最熟悉的陌生人
        "Ending6",   // E10 好結局（沙發）[待確認]
        "Ending11",  // E11 淪為妹妹的專屬玩具
        "Ending12",  // E12 粗暴的代價
        "Ending13",  // E13 沉淪的課後輔導
        "Ending14",  // E14 浴室裡的沉淪
        "Ending15",  // E15 笨拙的代價
        "Ending16",  // E16 笨拙的做愛
        "Ending17",  // E17 被榨取的雜魚玩具
        "Ending18",  // E18 沉迷於主從遊戲的兄妹
    };

    public static readonly (string id, string label, string night)[] BranchDefs =
    {
        ("night1_idle",   "什麼都不做", "第１晚"),
        ("night1_touch",  "摸　　了",   "第１晚"),
        ("night1_orgasm", "高　　潮",   "第１晚"),
        ("night2_idle",   "什麼都不做", "第２晚"),
        ("night2_touch",  "摸　　了",   "第２晚"),
        ("night2_orgasm", "高　　潮",   "第２晚"),
    };

    public static readonly (string id, string label)[] CgDefs =
    {
        ("linai_knock_1", "敲門 1"),
        ("linai_knock_2", "敲門 2"),
        ("linai_knock_3", "敲門 3"),
        ("linai_knock_4", "敲門 4"),
        ("linai_knock_5", "敲門 5"),
    };

    // ─── 節點引用 ────────────────────────────────────────────────

    private Control _endingPage, _cgPage, _branchPage, _treePage;
    private Button[] _tabBtns = new Button[4];
    private int _activeTab = 0;

    // ─── 生命週期 ────────────────────────────────────────────────

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildChrome();

        // 全年齡開關切換 → 圖鑒內容（結局／分支／樹狀圖）立即換成對應版本
        var pdm = PlayerDataManager.Instance;
        if (pdm != null)
        {
            _onR18Toggled = _ => RefreshContent();
            pdm.OnR18Toggled += _onR18Toggled;
        }
    }

    Action<bool> _onR18Toggled;

    public override void _ExitTree()
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm != null && _onR18Toggled != null) pdm.OnR18Toggled -= _onR18Toggled;
    }

    public void Open()
    {
        RebuildPages();
        Visible = true;
        MoveToFront();
        SwitchTab(0);
        _tabBtns[0].GrabFocus();
    }

    public void Close()
    {
        Visible = false;
    }

    /// <summary>
    /// 在圖鑒「已經開著」的狀態下，重新整理內容以反映最新解鎖狀態
    /// （例如開發者工具的全解鎖開關被切換時），不重設目前選中的分頁、
    /// 不搶焦點——跟 Open() 的差別只在於不強制切回第一頁、不做開場動畫相關設定。
    /// </summary>
    public void RefreshContent()
    {
        if (!Visible) return;
        RebuildPages();
        SwitchTab(_activeTab);
    }

    void RebuildPages()
    {
        // 重建各頁內容以反映最新解鎖狀態
        _endingPage?.QueueFree();
        _cgPage?.QueueFree();
        _branchPage?.QueueFree();
        _treePage?.QueueFree();
        _endingPage = _cgPage = _branchPage = _treePage = null;
        BuildPages(_contentArea);
    }

    // ─── 外框（僅建立一次）──────────────────────────────────────

    private Control _contentArea;

    void BuildChrome()
    {
        // 半透明暗色遮罩
        var overlay = new ColorRect
        {
            Name        = "Overlay",
            Color       = new Color(0f, 0f, 0f, 0.72f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // 主卡片
        var card = new Panel { Name = "Card", MouseFilter = MouseFilterEnum.Stop };
        AddChild(card);
        card.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        card.CustomMinimumSize = new Vector2(1360, 800);
        card.Size = new Vector2(1360, 800);
        card.OffsetLeft = -680; card.OffsetRight = 680;
        card.OffsetTop = -400; card.OffsetBottom = 400;
        card.AddThemeStyleboxOverride("panel", CardSty());

        // 標題列
        var titleLbl = new Label
        {
            Name                = "GalleryTitle",
            Text                = "✦  圖鑒房  ✦",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        titleLbl.AddThemeFontSizeOverride("font_size", 34);
        titleLbl.AddThemeColorOverride("font_color", new Color("79475f"));
        titleLbl.AddThemeConstantOverride("outline_size", 0);
        titleLbl.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.5f));
        card.AddChild(titleLbl);
        titleLbl.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        titleLbl.OffsetTop    = 18;
        titleLbl.OffsetBottom = 66;

        // 分隔線
        var divider = new ColorRect
        {
            Name        = "Divider",
            Color       = new Color("efd2df"),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        card.AddChild(divider);
        divider.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        divider.OffsetTop    = 70;
        divider.OffsetBottom = 72;
        divider.OffsetLeft   = 24;
        divider.OffsetRight  = -24;

        // 頁籤列
        var tabRow = new HBoxContainer { Name = "TabRow" };
        card.AddChild(tabRow);
        tabRow.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        tabRow.OffsetTop    = 78;
        tabRow.OffsetBottom = 126;
        tabRow.OffsetLeft   = 24;
        tabRow.OffsetRight  = -24;
        tabRow.AddThemeConstantOverride("separation", 8);

        string[] tabLabels = { "結局", "CG圖鑒", "分支劇情", "樹狀圖" };
        for (int i = 0; i < tabLabels.Length; i++)
        {
            int idx = i;
            var tb = new Button { Name = $"Tab{i}", Text = tabLabels[i] };
            tb.AddThemeFontSizeOverride("font_size", 22);
            tb.CustomMinimumSize = new Vector2(160, 44);
            tb.Pressed += () => SwitchTab(idx);
            tabRow.AddChild(tb);
            UIStyle.ApplyButtonStyle(tb);
            tb.SetMeta("preserve_button_style", true);
            _tabBtns[i] = tb;
        }

        // 內容區域
        var contentHolder = new Control { Name = "ContentHolder", MouseFilter = MouseFilterEnum.Pass };
        card.AddChild(contentHolder);
        contentHolder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        contentHolder.OffsetTop    = 130;
        contentHolder.OffsetBottom = -56;
        contentHolder.OffsetLeft   = 24;
        contentHolder.OffsetRight  = -24;
        _contentArea = contentHolder;

        // 關閉按鈕
        var closeBtn = new Button { Name = "CloseBtn", Text = "✕  關閉" };
        card.AddChild(closeBtn);
        closeBtn.AddThemeFontSizeOverride("font_size", 20);
        closeBtn.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        closeBtn.OffsetTop    = -48;
        closeBtn.OffsetBottom = -10;
        closeBtn.OffsetLeft   = 580;
        closeBtn.OffsetRight  = -580;
        UIStyle.ApplyButtonStyle(closeBtn);
        closeBtn.Pressed += Close;
    }

    // ─── 三個頁面（每次 Open 重建）──────────────────────────────

    void BuildPages(Control parent)
    {
        _endingPage = BuildEndingPage(parent);
        _cgPage     = BuildCGPage(parent);
        _branchPage = BuildBranchPage(parent);
        _treePage   = BuildTreePage(parent);
    }

    // ─── 頁籤切換 ────────────────────────────────────────────────

    void SwitchTab(int idx)
    {
        _activeTab = idx;
        Control[] pages = { _endingPage, _cgPage, _branchPage, _treePage };
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null) pages[i].Visible = (i == idx);
            if (_tabBtns[i] != null)
            {
                _tabBtns[i].AddThemeStyleboxOverride("normal",
                    i == idx ? TabActiveSty() : TabInactiveSty());
                _tabBtns[i].AddThemeStyleboxOverride("hover",
                    i == idx ? TabActiveSty() : TabHoverSty());
                _tabBtns[i].AddThemeColorOverride("font_color",
                    i == idx ? new Color("79475f") : new Color("a66d88"));
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  結局頁
    // ══════════════════════════════════════════════════════════════

    Control BuildEndingPage(Control parent)
    {
        var scroll = new ScrollContainer { Name = "EndingPage" };
        parent.AddChild(scroll);
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 20);
        grid.AddThemeConstantOverride("v_separation", 20);
        scroll.AddChild(grid);
        grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var gdm = GalleryDataManager.Instance;

        if (GalleryDataManager.AllAgesMode)
        {
            // 全年齡版：只有 5 個結局（與 R18 的 18 個分開記錄）
            for (int i = 1; i <= GalleryDataManager.AAEndingCount; i++)
            {
                if (StoryTreeData.AAEndingRemoved(i)) continue;   // 結局 2、4 已拿掉，不顯示
                bool unlocked = gdm?.IsAAEndingUnlocked(i) ?? false;
                grid.AddChild(BuildEndingCard(i, unlocked, true));
            }
            return scroll;
        }

        for (int i = 1; i <= 18; i++)
        {
            bool unlocked = gdm?.IsEndingUnlocked(i) ?? false;
            var card = BuildEndingCard(i, unlocked);
            grid.AddChild(card);
        }

        return scroll;
    }

    Control BuildEndingCard(int n, bool unlocked, bool allAges = false)
    {
        var panel = new Panel { CustomMinimumSize = new Vector2(400, 180) };

        if (unlocked)
        {
            panel.AddThemeStyleboxOverride("panel", UnlockedCardSty());
            var vb = new VBoxContainer();
            vb.AddThemeConstantOverride("separation", 8);
            panel.AddChild(vb);
            vb.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            vb.OffsetLeft = 16; vb.OffsetRight = -16;
            vb.OffsetTop  = 16; vb.OffsetBottom = -16;

            var numLbl = new Label { Text = $"結局 {n}" };
            numLbl.AddThemeFontSizeOverride("font_size", 14);
            numLbl.AddThemeColorOverride("font_color", new Color("a66d88"));
            vb.AddChild(numLbl);

            var nameLbl = new Label
            {
                Text      = allAges ? StoryTreeData.AAEndings[n].Name : EndingInfo[n].name,
                AutowrapMode = TextServer.AutowrapMode.Word,
            };
            nameLbl.AddThemeFontSizeOverride("font_size", 26);
            nameLbl.AddThemeColorOverride("font_color", new Color("79475f"));
            vb.AddChild(nameLbl);

            var descLbl = new Label
            {
                Text      = allAges ? StoryTreeData.AAEndings[n].Desc : EndingInfo[n].desc,
                AutowrapMode = TextServer.AutowrapMode.Word,
            };
            descLbl.AddThemeFontSizeOverride("font_size", 16);
            descLbl.AddThemeColorOverride("font_color", new Color("846777"));
            vb.AddChild(descLbl);

            // 點擊卡片重播該結局——只有場景真的存在（res://Scenes/EndingN.tscn）才綁定，
            // 還沒製作場景的結局維持「已解鎖但不能點」，不會噴錯也不會誤觸未完成的內容。
            // 全年齡結局都在 Day3_Start 裡，用客廳場景重播（LivingRoomRuntime.ReplayAAEnding）
            string sceneFile = allAges ? "LivingRoom" : (n < EndingSceneFile.Length) ? EndingSceneFile[n] : null;
            bool hasScene = sceneFile != null && ResourceLoader.Exists($"res://Scenes/{sceneFile}.tscn");
            if (hasScene)
            {
                panel.MouseFilter = MouseFilterEnum.Stop;
                panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
                panel.GuiInput += (InputEvent ev) =>
                {
                    if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                    {
                        panel.AcceptEvent();
                        if (allAges) LivingRoomRuntime.ReplayAAEnding = n;
                        GalleryReplaySession.Open(this, $"res://Scenes/{sceneFile}.tscn");
                    }
                };

                var playHint = new Label
                {
                    Text = "▶ 點擊重新播放",
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                playHint.AddThemeFontSizeOverride("font_size", 13);
                playHint.AddThemeColorOverride("font_color", new Color("9a5a7a"));
                vb.AddChild(playHint);
            }
        }
        else
        {
            panel.AddThemeStyleboxOverride("panel", LockedCardSty());
            var qMark = new Label
            {
                Text                = "？",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            };
            qMark.AddThemeFontSizeOverride("font_size", 52);
            qMark.AddThemeColorOverride("font_color", new Color("bd98aa"));
            panel.AddChild(qMark);
            qMark.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        return panel;
    }

    // ══════════════════════════════════════════════════════════════
    //  CG 頁
    // ══════════════════════════════════════════════════════════════

    Control BuildCGPage(Control parent)
    {
        var scroll = new ScrollContainer { Name = "CGPage", Visible = false };
        parent.AddChild(scroll);
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 16);
        scroll.AddChild(grid);
        grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var gdm = GalleryDataManager.Instance;

        foreach (var (id, label) in CgDefs)
        {
            bool unlocked = gdm?.IsCGUnlocked(id) ?? false;
            grid.AddChild(BuildCGCard(id, label, unlocked));
        }

        return scroll;
    }

    Control BuildCGCard(string id, string label, bool unlocked)
    {
        var panel = new Panel { CustomMinimumSize = new Vector2(290, 200) };

        if (unlocked)
        {
            panel.AddThemeStyleboxOverride("panel", UnlockedCardSty());

            // 嘗試載入 CG 圖片
            var texPath = $"res://Resources/CG/{id.Replace('_', ' ')}.png";
            var tex = ResourceLoader.Exists(texPath)
                ? ResourceLoader.Load<Texture2D>(texPath)
                : null;

            if (tex != null)
            {
                var img = new TextureRect
                {
                    Texture     = tex,
                    ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                };
                panel.AddChild(img);
                img.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                img.OffsetBottom = -30;

                var lbl = new Label
                {
                    Text                = label,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                lbl.AddThemeFontSizeOverride("font_size", 16);
                lbl.AddThemeColorOverride("font_color", new Color("79475f"));
                panel.AddChild(lbl);
                lbl.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
                lbl.OffsetTop = -28; lbl.OffsetBottom = 0;
            }
            else
            {
                var lbl = new Label
                {
                    Text                = label,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                };
                lbl.AddThemeFontSizeOverride("font_size", 20);
                lbl.AddThemeColorOverride("font_color", new Color("79475f"));
                panel.AddChild(lbl);
                lbl.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            }
        }
        else
        {
            panel.AddThemeStyleboxOverride("panel", LockedCardSty());
            var qMark = new Label
            {
                Text                = "？",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            };
            qMark.AddThemeFontSizeOverride("font_size", 52);
            qMark.AddThemeColorOverride("font_color", new Color("bd98aa"));
            panel.AddChild(qMark);
            qMark.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        return panel;
    }

    // ══════════════════════════════════════════════════════════════
    //  分支劇情頁
    // ══════════════════════════════════════════════════════════════

    Control BuildBranchPage(Control parent)
    {
        var scroll = new ScrollContainer { Name = "BranchPage", Visible = false };
        parent.AddChild(scroll);
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 24);
        scroll.AddChild(vb);
        vb.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var gdm = GalleryDataManager.Instance;

        // 依晚次分組
        string currentNight = "";
        var defs = GalleryDataManager.AllAgesMode ? StoryTreeData.AABranchDefs : BranchDefs;
        foreach (var (id, label, night) in defs)
        {
            if (night != currentNight)
            {
                currentNight = night;
                var nightLbl = new Label { Text = $"── {night} ──" };
                nightLbl.AddThemeFontSizeOverride("font_size", 22);
                nightLbl.AddThemeColorOverride("font_color", new Color("a66d88"));
                vb.AddChild(nightLbl);
            }

            bool unlocked = gdm?.IsBranchUnlocked(id) ?? false;
            vb.AddChild(BuildBranchCard(id, label, unlocked));
        }

        return scroll;
    }

    Control BuildBranchCard(string id, string label, bool unlocked)
    {
        var panel = new Panel
        {
            CustomMinimumSize = new Vector2(0, 80),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        if (unlocked)
        {
            panel.AddThemeStyleboxOverride("panel", UnlockedCardSty());

            var hb = new HBoxContainer();
            hb.AddThemeConstantOverride("separation", 16);
            panel.AddChild(hb);
            hb.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            hb.OffsetLeft = 20; hb.OffsetRight = -20;
            hb.OffsetTop = 16; hb.OffsetBottom = -16;

            var nameLbl = new Label
            {
                Text                = label,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                VerticalAlignment   = VerticalAlignment.Center,
            };
            nameLbl.AddThemeFontSizeOverride("font_size", 22);
            nameLbl.AddThemeColorOverride("font_color", new Color("79475f"));
            hb.AddChild(nameLbl);

            var replayBtn = new Button { Text = "▶ 重播" };
            replayBtn.AddThemeFontSizeOverride("font_size", 18);
            replayBtn.CustomMinimumSize = new Vector2(120, 40);
            UIStyle.ApplyButtonStyle(replayBtn);
            // TODO: 串接分支重播邏輯
            replayBtn.Pressed += () => GD.Print($"[Gallery] 重播分支: {id}");
            hb.AddChild(replayBtn);
        }
        else
        {
            panel.AddThemeStyleboxOverride("panel", LockedCardSty());

            var hb = new HBoxContainer();
            hb.AddThemeConstantOverride("separation", 16);
            panel.AddChild(hb);
            hb.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            hb.OffsetLeft = 20; hb.OffsetRight = -20;
            hb.OffsetTop = 16; hb.OffsetBottom = -16;

            var qMark = new Label
            {
                Text              = "？  ？  ？",
                VerticalAlignment = VerticalAlignment.Center,
            };
            qMark.AddThemeFontSizeOverride("font_size", 22);
            qMark.AddThemeColorOverride("font_color", new Color("bd98aa"));
            hb.AddChild(qMark);
        }

        return panel;
    }

    // ══════════════════════════════════════════════════════════════
    //  樹狀圖頁
    // ══════════════════════════════════════════════════════════════

    Control BuildTreePage(Control parent)
    {
        var scroll = new ScrollContainer
        {
            Name                  = "TreePage",
            Visible               = false,
            HorizontalScrollMode  = ScrollContainer.ScrollMode.ShowAlways,
            VerticalScrollMode    = ScrollContainer.ScrollMode.ShowAlways,
        };
        parent.AddChild(scroll);
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var treeCtrl = new StoryTreeControl();
        treeCtrl.OnUnlockChanged = RefreshContent;
        scroll.AddChild(treeCtrl);

        return scroll;
    }

    // ══════════════════════════════════════════════════════════════
    //  StyleBox 工廠
    // ══════════════════════════════════════════════════════════════

    static StyleBoxFlat CardSty()
    {
        var sb = new StyleBoxFlat();
        sb.BgColor = new Color("fffafd");
        sb.BorderColor = new Color("edb8cf");
        sb.BorderWidthTop = sb.BorderWidthBottom = 2;
        sb.BorderWidthLeft = sb.BorderWidthRight = 2;
        sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = 16;
        sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 16;
        sb.AntiAliasing = true;
        sb.ShadowColor = new Color(0f, 0f, 0f, 0.5f);
        sb.ShadowSize = 16;
        sb.ShadowOffset = new Vector2(0, 8);
        sb.ContentMarginLeft = sb.ContentMarginRight = 16;
        sb.ContentMarginTop = sb.ContentMarginBottom = 16;
        return sb;
    }

    static StyleBoxFlat UnlockedCardSty()
    {
        var sb = new StyleBoxFlat();
        sb.BgColor = new Color("ffffff");
        sb.BorderColor = new Color("edb8cf");
        sb.BorderWidthTop = sb.BorderWidthBottom = 2;
        sb.BorderWidthLeft = sb.BorderWidthRight = 2;
        sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = 12;
        sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
        sb.AntiAliasing = true;
        return sb;
    }

    static StyleBoxFlat LockedCardSty()
    {
        var sb = new StyleBoxFlat();
        sb.BgColor = new Color("f4edf1");
        sb.BorderColor = new Color("e4d6df");
        sb.BorderWidthTop = sb.BorderWidthBottom = 2;
        sb.BorderWidthLeft = sb.BorderWidthRight = 2;
        sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = 12;
        sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 12;
        sb.AntiAliasing = true;
        return sb;
    }

    static StyleBoxFlat TabActiveSty()
    {
        var sb = new StyleBoxFlat();
        sb.BgColor = new Color("f8d8e8");
        sb.BorderColor = new Color("e873a5");
        sb.BorderWidthBottom = 3;
        sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = 8;
        sb.AntiAliasing = true;
        return sb;
    }

    static StyleBoxFlat TabInactiveSty()
    {
        var sb = new StyleBoxFlat();
        sb.BgColor = new Color("fffafd");
        sb.BorderColor = new Color("bd98aa");
        sb.BorderWidthTop = sb.BorderWidthBottom = 1;
        sb.BorderWidthLeft = sb.BorderWidthRight = 1;
        sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = 8;
        sb.AntiAliasing = true;
        return sb;
    }

    static StyleBoxFlat TabHoverSty()
    {
        var sb = new StyleBoxFlat();
        sb.BgColor = new Color("fce9f2");
        sb.BorderColor = new Color("edb8cf");
        sb.BorderWidthTop = sb.BorderWidthBottom = 1;
        sb.BorderWidthLeft = sb.BorderWidthRight = 1;
        sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight = 8;
        sb.AntiAliasing = true;
        return sb;
    }
}
