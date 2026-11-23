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

        // 右下角：一鍵解鎖（只在「結局」頁籤顯示；解鎖目前模式的全部結局，否則得一個一個實際玩到才會解鎖）
        _unlockAllBtn = new Button { Name = "UnlockAllBtn", Text = "🔓 一鍵解鎖" };
        card.AddChild(_unlockAllBtn);
        _unlockAllBtn.AddThemeFontSizeOverride("font_size", 18);
        _unlockAllBtn.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        _unlockAllBtn.OffsetLeft = -190; _unlockAllBtn.OffsetRight = -24;
        _unlockAllBtn.OffsetTop = -48;   _unlockAllBtn.OffsetBottom = -10;
        UIStyle.ApplyButtonStyle(_unlockAllBtn);
        _unlockAllBtn.Pressed += () => { GalleryDataManager.Instance?.UnlockAllEndingsOfCurrentMode(); RefreshContent(); };
    }

    Button _unlockAllBtn;

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
        if (_unlockAllBtn != null) _unlockAllBtn.Visible = (idx == 0);
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
            qMark.AddThemeFontSizeOverride("font_size", 72);
            qMark.AddThemeColorOverride("font_color", new Color("bd98aa"));
            panel.AddChild(qMark);
            qMark.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        return panel;
    }

    // ══════════════════════════════════════════════════════════════
    //  CG 頁
    // ══════════════════════════════════════════════════════════════

    // CG 卡片（小窗）大小：靜態／動態共用
    static readonly Vector2 CgCardSize = new Vector2(460, 320);

    // CG 頁篩選：0＝動態（預設，排最前）、1＝靜態、2＝立繪、3＝全部
    int _cgFilter = 0;
    readonly Button[] _cgFilterBtns = new Button[4];
    Control _cgDynSection, _cgStaticSection, _cgPortraitSection;

    Control BuildCGPage(Control parent)
    {
        var scroll = new ScrollContainer { Name = "CGPage", Visible = false };
        parent.AddChild(scroll);
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 20);
        scroll.AddChild(vb);
        vb.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var gdm = GalleryDataManager.Instance;

        // ── 篩選列：動態／靜態／全部 ──
        var filterBar = new HBoxContainer();
        filterBar.AddThemeConstantOverride("separation", 10);
        vb.AddChild(filterBar);
        string[] filterLabels = { "動態", "靜態", "立繪", "全部" };
        for (int i = 0; i < filterLabels.Length; i++)
        {
            int idx = i;
            var b = new Button { Text = filterLabels[i], CustomMinimumSize = new Vector2(130, 44), FocusMode = FocusModeEnum.None };
            b.AddThemeFontSizeOverride("font_size", 22);
            b.Pressed += () => { _cgFilter = idx; ApplyCgFilter(); };
            filterBar.AddChild(b);
            _cgFilterBtns[i] = b;
        }

        // ── 動態 CG（排最前）：真結局 Spine ──
        var dyn = new VBoxContainer();
        dyn.AddThemeConstantOverride("separation", 12);
        dyn.AddChild(BuildCgSectionLabel("動態"));
        var dynGrid = NewCgFlow();
        dyn.AddChild(dynGrid);
        dynGrid.AddChild(BuildSpineCard());      // 真結局 Spine 動態 CG
        vb.AddChild(dyn);
        _cgDynSection = dyn;

        // ── 立繪（獨立一類）──
        var por = new VBoxContainer();
        por.AddThemeConstantOverride("separation", 12);
        por.AddChild(BuildPortraitCard());
        vb.AddChild(por);
        _cgPortraitSection = por;

        // ── 靜態 CG ──
        var st = new VBoxContainer();
        st.AddThemeConstantOverride("separation", 12);
        st.AddChild(BuildCgSectionLabel("靜態"));
        var staticGrid = NewCgFlow();
        st.AddChild(staticGrid);
        // 敲門的 5 張 CG 合成一張卡片：點進去看大圖，左右切換、滾輪縮放
        var unlockedIds = new System.Collections.Generic.List<string>();
        foreach (var (id, _) in CgDefs)
            if (gdm?.IsCGUnlocked(id) ?? false) unlockedIds.Add(id);
        staticGrid.AddChild(BuildKnockCGCard(unlockedIds));
        vb.AddChild(st);
        _cgStaticSection = st;

        ApplyCgFilter();
        return scroll;
    }

    void ApplyCgFilter()
    {
        if (_cgDynSection != null && IsInstanceValid(_cgDynSection)) _cgDynSection.Visible = _cgFilter == 0 || _cgFilter == 3;
        if (_cgPortraitSection != null && IsInstanceValid(_cgPortraitSection)) _cgPortraitSection.Visible = _cgFilter == 2 || _cgFilter == 3;
        if (_cgStaticSection != null && IsInstanceValid(_cgStaticSection)) _cgStaticSection.Visible = _cgFilter == 1 || _cgFilter == 3;
        for (int i = 0; i < _cgFilterBtns.Length; i++)
        {
            var b = _cgFilterBtns[i];
            if (b == null || !IsInstanceValid(b)) continue;
            bool on = i == _cgFilter;
            b.AddThemeStyleboxOverride("normal", on ? TabActiveSty() : TabInactiveSty());
            b.AddThemeStyleboxOverride("hover", on ? TabActiveSty() : TabHoverSty());
            b.AddThemeStyleboxOverride("pressed", TabActiveSty());
            b.AddThemeColorOverride("font_color", on ? new Color("79475f") : new Color("a66d88"));
        }
    }

    static Label BuildCgSectionLabel(string text)
    {
        var lbl = new Label { Text = $"── {text} ──" };
        lbl.AddThemeFontSizeOverride("font_size", 22);
        lbl.AddThemeColorOverride("font_color", new Color("a66d88"));
        return lbl;
    }

    static HFlowContainer NewCgFlow()
    {
        var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        flow.AddThemeConstantOverride("h_separation", 20);
        flow.AddThemeConstantOverride("v_separation", 20);
        return flow;
    }

    static Texture2D LoadCgTexture(string id)
    {
        var texPath = $"res://Resources/CG/{id.Replace('_', ' ')}.png";
        return ResourceLoader.Exists(texPath) ? ResourceLoader.Load<Texture2D>(texPath) : null;
    }

    /// <summary>「敲門」CG 卡片：至少解鎖一張就能點開；沒有任何解鎖時顯示 ？。</summary>
    Control BuildKnockCGCard(System.Collections.Generic.List<string> unlockedIds)
    {
        var panel = new Panel { CustomMinimumSize = CgCardSize };
        if (unlockedIds.Count == 0)
        {
            panel.AddThemeStyleboxOverride("panel", LockedCardSty());
            var qMark = new Label { Text = "？", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            qMark.AddThemeFontSizeOverride("font_size", 72);
            qMark.AddThemeColorOverride("font_color", new Color("bd98aa"));
            panel.AddChild(qMark);
            qMark.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            return panel;
        }

        panel.AddThemeStyleboxOverride("panel", UnlockedCardSty());
        var tex = LoadCgTexture(unlockedIds[0]);
        if (tex != null)
        {
            var img = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
            panel.AddChild(img);
            img.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            img.OffsetBottom = -36;
        }
        var lbl = new Label { Text = $"敲門　{unlockedIds.Count} / {CgDefs.Length}", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        lbl.AddThemeFontSizeOverride("font_size", 20);
        lbl.AddThemeColorOverride("font_color", new Color("79475f"));
        panel.AddChild(lbl);
        lbl.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        lbl.OffsetTop = -34; lbl.OffsetBottom = 0;

        panel.MouseFilter = MouseFilterEnum.Stop;
        panel.MouseDefaultCursorShape = CursorShape.PointingHand;
        var ids = unlockedIds.ToArray();
        panel.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                panel.AcceptEvent();
                OpenCgViewer(ids);
            }
        };
        return panel;
    }

    // ── CG 大圖檢視：◀ ▶（或方向鍵）換圖、滾輪縮放、按住左鍵拖曳、Esc／✕ 關閉 ──
    Control _cgViewer, _cgStage;
    TextureRect _cgViewImg;
    Label _cgViewLbl;
    string[] _cgViewIds;
    int _cgViewIdx;
    float _cgZoom = 1f;
    Vector2 _cgPan = Vector2.Zero;
    bool _cgDragging;

    void OpenCgViewer(string[] ids)
    {
        CloseCgViewer();
        if (ids == null || ids.Length == 0) return;
        _cgViewIds = ids; _cgViewIdx = 0;

        _cgViewer = new Control { Name = "CGViewer", MouseFilter = MouseFilterEnum.Stop };
        AddChild(_cgViewer);
        _cgViewer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var bg = new ColorRect { Color = new Color(0.06f, 0.03f, 0.05f, 0.96f), MouseFilter = MouseFilterEnum.Ignore };
        _cgViewer.AddChild(bg);
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _cgStage = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        _cgViewer.AddChild(_cgStage);
        _cgStage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _cgStage.OffsetTop = 70; _cgStage.OffsetBottom = -20;
        _cgStage.Resized += ApplyCgTransform;
        _cgStage.GuiInput += OnCgStageInput;

        _cgViewImg = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        _cgStage.AddChild(_cgViewImg);

        // 標題列：◀　敲門 1 / 5　▶　　✕
        var bar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bar.AddThemeConstantOverride("separation", 20);
        _cgViewer.AddChild(bar);
        bar.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        bar.OffsetTop = 12; bar.OffsetBottom = 62;
        var prev = UIStyle.CreateStyledButton("◀", 26); prev.CustomMinimumSize = new Vector2(80, 48);
        var next = UIStyle.CreateStyledButton("▶", 26); next.CustomMinimumSize = new Vector2(80, 48);
        prev.Pressed += () => CgViewStep(-1);
        next.Pressed += () => CgViewStep(1);
        _cgViewLbl = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(260, 0) };
        _cgViewLbl.AddThemeFontSizeOverride("font_size", 24);
        _cgViewLbl.AddThemeColorOverride("font_color", new Color("ffe6f2"));
        var hint = new Label { Text = "滾輪縮放　拖曳移動　←→ 換圖", VerticalAlignment = VerticalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 16);
        hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.55f));
        var close = UIStyle.CreateStyledButton("✕", 24); close.CustomMinimumSize = new Vector2(64, 48);
        close.Pressed += CloseCgViewer;
        bar.AddChild(prev); bar.AddChild(_cgViewLbl); bar.AddChild(next); bar.AddChild(hint); bar.AddChild(close);

        ShowCgViewPage(0);
    }

    void CloseCgViewer()
    {
        CloseSpineViewer();
        if (_cgViewer != null && IsInstanceValid(_cgViewer)) _cgViewer.QueueFree();
        _cgViewer = _cgStage = null; _cgViewImg = null; _cgDragging = false;
    }

    void CgViewStep(int d)
    {
        if (_cgViewIds == null || _cgViewIds.Length == 0) return;
        ShowCgViewPage((_cgViewIdx + d + _cgViewIds.Length) % _cgViewIds.Length);
    }

    void ShowCgViewPage(int idx)
    {
        _cgViewIdx = idx;
        _cgViewImg.Texture = LoadCgTexture(_cgViewIds[idx]);
        _cgZoom = 1f; _cgPan = Vector2.Zero;   // 換圖時重設縮放與位置
        _cgViewLbl.Text = $"敲門　{idx + 1} / {_cgViewIds.Length}";
        ApplyCgTransform();
    }

    void ApplyCgTransform()
    {
        if (_cgViewImg == null || _cgStage == null) return;
        var sz = _cgStage.Size;
        _cgViewImg.Size = sz;
        _cgViewImg.PivotOffset = sz / 2f;
        _cgViewImg.Scale = new Vector2(_cgZoom, _cgZoom);
        // 放大後限制拖曳範圍，不要把圖拖出畫面太遠；縮回 1 倍就回到置中
        var lim = sz * (_cgZoom - 1f) / 2f;
        _cgPan = new Vector2(Mathf.Clamp(_cgPan.X, -lim.X, lim.X), Mathf.Clamp(_cgPan.Y, -lim.Y, lim.Y));
        _cgViewImg.Position = _cgPan;
    }

    void OnCgStageInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown)
            {
                float z0 = _cgZoom;
                float z1 = Mathf.Clamp(z0 * (mb.ButtonIndex == MouseButton.WheelUp ? 1.15f : 1f / 1.15f), 1f, 6f);
                if (!Mathf.IsEqualApprox(z0, z1))
                {   // 以滑鼠位置為中心縮放（滑鼠指的那個點維持不動）
                    var c = _cgStage.Size / 2f;
                    var m = mb.Position;
                    _cgPan = m - c - z1 * (m - c - _cgPan) / z0;
                    _cgZoom = z1;
                    ApplyCgTransform();
                }
                _cgStage.AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Left) { _cgDragging = mb.Pressed; _cgStage.AcceptEvent(); }
        }
        else if (ev is InputEventMouseMotion mm && _cgDragging && _cgZoom > 1f)
        {
            _cgPan += mm.Relative;
            ApplyCgTransform();
            _cgStage.AcceptEvent();
        }
    }

    public override void _Input(InputEvent e)
    {
        if (_spineViewer != null && IsInstanceValid(_spineViewer))
        {
            if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
            { CloseSpineViewer(); GetViewport().SetInputAsHandled(); }
            return;
        }
        if (_cgViewer == null || !IsInstanceValid(_cgViewer)) return;
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode == Key.Left) { CgViewStep(-1); GetViewport().SetInputAsHandled(); }
            else if (k.Keycode == Key.Right) { CgViewStep(1); GetViewport().SetInputAsHandled(); }
            else if (k.Keycode == Key.Escape) { CloseCgViewer(); GetViewport().SetInputAsHandled(); }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  真結局 Spine 動態 CG（卡片＋檢視器：可切換表情）
    // ══════════════════════════════════════════════════════════════

    public const string SpineCgId = "spine_trueend";   // 圖鑒 CG 解鎖 id（真結局播到 spine_trueend 背景時解鎖）

    static readonly (string key, string label)[] SpineFaces =
    {
        ("idle", "預設"), ("smile", "笑"), ("shy", "害羞"),   // 雌小鬼笑、生氣的嘴型不自然，已拿掉
    };

    // 立繪檢視器：服裝（含襪子）、髮型、表情
    static readonly (string key, string label, string socks)[] PortraitOutfits =
    {
        ("school", "制服", "black"), ("sleep", "睡衣", "none"),
    };
    static readonly (string key, string label)[] PortraitHairs = { ("twin", "雙馬尾"), ("long", "披髮") };
    static readonly (string key, string label)[] PortraitFaces =
    {
        ("face1", "表情1"), ("face2", "表情2"), ("laugh", "笑"), ("cry_laugh", "笑哭"), ("cry", "哭"),
        ("angry1", "生氣"), ("eye_roll", "白眼"), ("scorn", "鄙視"), ("side_scorn", "斜眼"),
        ("star_eyes", "星星眼"), ("gentle_laugh", "溫柔笑"),   // 「想睡」表情看起來不自然，先拿掉
    };

    Control _spineViewer;
    SpineChar _spineViewChar;

    static SpineChar NewTrueEndSpine(bool cover) => new SpineChar
    {
        Name = "TrueEndSpineView",
        CustomDir = "res://Resources/Spine/trueend/",
        CustomName = "trueend",
        CoverMode = cover,
        MouseFilter = MouseFilterEnum.Ignore,
    };

    Control BuildSpineCard()
    {
        var gdm = GalleryDataManager.Instance;
        // 真結局解鎖（全年齡真結局＝結局 1）或播過 spine_trueend 背景就算解鎖
        bool unlocked = (gdm?.IsCGUnlocked(SpineCgId) ?? false) || (gdm?.IsAAEndingUnlocked(1) ?? false);
        var panel = new Panel { CustomMinimumSize = CgCardSize };
        if (!unlocked)
        {
            panel.AddThemeStyleboxOverride("panel", LockedCardSty());
            var qMark = new Label { Text = "？", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            qMark.AddThemeFontSizeOverride("font_size", 72);
            qMark.AddThemeColorOverride("font_color", new Color("bd98aa"));
            panel.AddChild(qMark);
            qMark.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            return panel;
        }

        panel.AddThemeStyleboxOverride("panel", UnlockedCardSty());
        var clip = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(clip);
        clip.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        clip.OffsetBottom = -36;
        var sp = NewTrueEndSpine(false);   // 小窗顯示整張 CG（不裁切）
        clip.AddChild(sp);
        sp.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (sp.EnsureLoaded()) sp.Apply(null, null, null, "idle", "idle");

        var lbl = new Label { Text = "真結局　動態 CG", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        lbl.AddThemeFontSizeOverride("font_size", 20);
        lbl.AddThemeColorOverride("font_color", new Color("79475f"));
        panel.AddChild(lbl);
        lbl.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        lbl.OffsetTop = -34; lbl.OffsetBottom = 0;

        panel.MouseFilter = MouseFilterEnum.Stop;
        panel.MouseDefaultCursorShape = CursorShape.PointingHand;
        panel.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                panel.AcceptEvent();
                OpenSpineViewer();
            }
        };
        return panel;
    }

    static SpineChar NewPortraitSpine() => new SpineChar
    {
        Name = "PortraitSpineView",
        CoverMode = false,
        MouseFilter = MouseFilterEnum.Ignore,
    };

    /// <summary>立繪區：所有立繪不加外框、並排置中。第一張是 Spine（可換裝／表情），其餘為靜態圖。</summary>
    Control BuildPortraitCard()
    {
        var flow = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        flow.AddThemeConstantOverride("h_separation", 40);
        flow.AddThemeConstantOverride("v_separation", 24);

        // Spine 立繪
        var sp = NewPortraitSpine();
        sp.CustomMinimumSize = new Vector2(560, 640);
        sp.MouseFilter = MouseFilterEnum.Stop;
        sp.MouseDefaultCursorShape = CursorShape.PointingHand;
        if (sp.EnsureLoaded()) sp.Apply("school", "twin", "black", "face1", null);
        sp.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                sp.AcceptEvent();
                OpenPortraitViewer();
            }
        };
        flow.AddChild(WrapPortrait(sp, "莉奈"));

        // 靜態立繪：小時候
        AddStaticPortrait(flow, "res://Resources/Character/linai_child.png", "莉奈（小時候）");
        return flow;
    }

    Control WrapPortrait(Control content, string name)
    {
        var vb = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        vb.AddThemeConstantOverride("separation", 6);
        vb.AddChild(content);
        var lbl = new Label { Text = name, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        lbl.AddThemeFontSizeOverride("font_size", 22);
        lbl.AddThemeColorOverride("font_color", new Color("79475f"));
        vb.AddChild(lbl);
        return vb;
    }

    void AddStaticPortrait(Container parent, string path, string name)
    {
        var tex = GD.Load<Texture2D>(path);
        if (tex == null) return;
        var tr = new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(560, 640),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        parent.AddChild(WrapPortrait(tr, name));
    }

    void OpenSpineViewer()
    {
        var sp = NewTrueEndSpine(false);   // 完整顯示整張 CG（不裁切）
        var rows = OpenSpineViewerShell(sp, 70, 1);
        if (sp.EnsureLoaded()) sp.Apply(null, null, null, "idle", "idle");
        foreach (var (key, label) in SpineFaces)
        {
            string k = key;
            var b = UIStyle.CreateStyledButton(label, 22);
            b.CustomMinimumSize = new Vector2(110, 48);
            b.Pressed += () => _spineViewChar?.Apply(null, null, null, k, k);
            rows[0].AddChild(b);
        }
        AddSpineViewerClose(rows[0]);
    }

    void OpenPortraitViewer()
    {
        var sp = NewPortraitSpine();
        var rows = OpenSpineViewerShell(sp, 138, 2);   // 第一排：服裝／髮型　第二排：表情
        if (sp.EnsureLoaded()) sp.Apply("school", "twin", "black", "face1", null);
        bool r18 = PlayerDataManager.Instance?.R18ContentEnabled ?? false;

        rows[0].AddChild(MakeRowLabel("服裝"));
        foreach (var (key, label, sk) in PortraitOutfits)
        {
            string k = key, s = sk;
            var b = UIStyle.CreateStyledButton(label, 20);
            b.CustomMinimumSize = new Vector2(96, 44);
            b.Pressed += () => _spineViewChar?.Apply(r18 && k == "sleep" ? "sleep2" : k, null, s, null, null);
            rows[0].AddChild(b);
        }
        foreach (var (key, label) in PortraitHairs)
        {
            string k = key;
            var b = UIStyle.CreateStyledButton(label, 20);
            b.CustomMinimumSize = new Vector2(96, 44);
            b.Pressed += () => _spineViewChar?.Apply(null, k, null, null, null);
            rows[0].AddChild(b);
        }
        AddSpineViewerClose(rows[0]);

        rows[1].AddChild(MakeRowLabel("表情"));
        foreach (var (key, label) in PortraitFaces)
        {
            string k = key;
            var b = UIStyle.CreateStyledButton(label, 20);
            b.CustomMinimumSize = new Vector2(96, 44);
            b.Pressed += () => _spineViewChar?.Apply(null, null, null, k, null);
            rows[1].AddChild(b);
        }
    }

    static Label MakeRowLabel(string text)
    {
        var l = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(56, 44) };
        l.AddThemeFontSizeOverride("font_size", 20);
        l.AddThemeColorOverride("font_color", new Color("ffe6f2"));
        return l;
    }

    /// <summary>建立全螢幕 Spine 檢視器外殼；回傳上方的按鈕列（每列會自動換行）。stageTop＝Spine 區域離上緣的距離。</summary>
    HFlowContainer[] OpenSpineViewerShell(SpineChar sp, float stageTop, int rowCount)
    {
        CloseSpineViewer();
        _spineViewer = new Control { Name = "SpineViewer", MouseFilter = MouseFilterEnum.Stop };
        AddChild(_spineViewer);
        _spineViewer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var bg = new ColorRect { Color = new Color(0.06f, 0.03f, 0.05f, 0.96f), MouseFilter = MouseFilterEnum.Ignore };
        _spineViewer.AddChild(bg);
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var stage = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        _spineViewer.AddChild(stage);
        stage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stage.OffsetTop = stageTop; stage.OffsetBottom = -20;

        _spineViewChar = sp;
        stage.AddChild(sp);
        sp.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        _spineViewer.AddChild(col);
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        col.OffsetTop = 12; col.OffsetBottom = stageTop - 8;
        col.OffsetLeft = 16; col.OffsetRight = -16;

        var rows = new HFlowContainer[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            var r = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
            r.AddThemeConstantOverride("h_separation", 12);
            r.AddThemeConstantOverride("v_separation", 8);
            col.AddChild(r);
            rows[i] = r;
        }
        return rows;
    }

    void AddSpineViewerClose(HFlowContainer bar)
    {
        var hint = new Label { Text = "Esc 關閉", VerticalAlignment = VerticalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 16);
        hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.55f));
        var close = UIStyle.CreateStyledButton("✕", 24); close.CustomMinimumSize = new Vector2(64, 48);
        close.Pressed += CloseSpineViewer;
        bar.AddChild(hint); bar.AddChild(close);
    }

    void CloseSpineViewer()
    {
        if (_spineViewer != null && IsInstanceValid(_spineViewer)) _spineViewer.QueueFree();
        _spineViewer = null; _spineViewChar = null;
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

            // 通關過一次後：可以從分支開始遊戲（BranchStart.cs 會跳出選路線視窗）
            if (BranchStart.Available())
            {
                var go = new Button { Text = "從這裡開始", CustomMinimumSize = new Vector2(180, 0), FocusMode = FocusModeEnum.None };
                go.AddThemeFontSizeOverride("font_size", 20);
                go.Pressed += () => BranchStart.OpenPicker(this);
                hb.AddChild(go);
            }
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
