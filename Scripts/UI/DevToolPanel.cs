using Godot;
using System;

/// <summary>
/// 開發者工具視窗（僅供內部除錯測試使用，非玩家正式功能）。
/// 由 DevModeManager 建立／管理（密技召喚，解鎖後可按 F9 開關）。
///
/// 【設計】
/// - 全解鎖模式：純顯示層開關，只影響 GalleryDataManager 的查詢結果，
///   不會動到玩家真實存檔，可以隨時開關。
/// - R18/全年齡：原本在主選單上的按鈕搬進這裡，邏輯還是呼叫
///   PlayerDataManager.SetR18()，行為完全不變，只是換了個入口。
/// - 視窗本身不是全螢幕遮罩，而是一個可以自由拖動的浮動小視窗：
///   拖動標題列即可移動位置，不會擋住底下遊戲畫面其他地方的操作。
/// </summary>
public partial class DevToolPanel : Control
{
    static Vector2 s_lastWindowPos = new(-1, -1); // 記住上次拖動的位置（僅本次遊戲執行期間）

    Control _window;
    Label _unlockStatusLabel;
    Label _r18StatusLabel;
    Button _unlockToggleBtn;
    Button _r18ToggleBtn;
    Label _msgLabel;

    bool _dragging;
    Vector2 _dragGrabOffset;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore; // 視窗以外的地方點擊可以穿透到遊戲本身

        const float winWidth = 420f;

        _window = new PanelContainer
        {
            CustomMinimumSize = new Vector2(winWidth, 0),
            MouseFilter = MouseFilterEnum.Stop,
        };
        var winSty = new StyleBoxFlat
        {
            BgColor = new Color("120b1cf0"),
            BorderColor = new Color("6a3888"),
        };
        winSty.SetBorderWidthAll(2);
        winSty.SetCornerRadiusAll(14);
        winSty.ContentMarginLeft = winSty.ContentMarginRight = 0;
        winSty.ContentMarginTop = winSty.ContentMarginBottom = 0;
        ((PanelContainer)_window).AddThemeStyleboxOverride("panel", winSty);
        AddChild(_window);

        var vbOuter = new VBoxContainer();
        vbOuter.AddThemeConstantOverride("separation", 0);
        _window.AddChild(vbOuter);

        vbOuter.AddChild(BuildTitleBar());

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        margin.AddChild(content);
        vbOuter.AddChild(margin);

        var hint = new Label
        {
            Text = "僅供除錯測試使用。標題列可拖動視窗位置。",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color("a080c0"));
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(hint);

        content.AddChild(new HSeparator());

        // ── 全解鎖模式（圖鑒顯示層開關，不動真實存檔）──────────────
        _unlockToggleBtn = MakeButton("", () =>
        {
            var gdm = GalleryDataManager.Instance;
            if (gdm == null) { SetMsg("找不到 GalleryDataManager", false); return; }
            gdm.DebugUnlockAllOverride = !gdm.DebugUnlockAllOverride;
            RefreshUnlockDisplay();
            // 如果圖鑒正開著，直接刷新它的內容，不用使用者自己關掉再打開才會生效
            RefreshOpenGalleryIfAny();
        });
        content.AddChild(_unlockToggleBtn);

        _unlockStatusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _unlockStatusLabel.AddThemeFontSizeOverride("font_size", 14);
        content.AddChild(_unlockStatusLabel);

        content.AddChild(MakeButton("立即打開圖鑒預覽", () =>
        {
            var gallery = GetTree().CurrentScene?.GetNodeOrNull<GalleryScreen>("GalleryScreen");
            if (gallery == null) { SetMsg("目前畫面沒有圖鑒（只有主選單才有）", false); return; }
            gallery.Open();
        }, "1e2a3a", "5a90c0"));

        content.AddChild(new HSeparator());

        // ── R18 / 全年齡（原主選單按鈕搬過來，邏輯不變）──────────────
        _r18ToggleBtn = MakeButton("", () =>
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm == null) { SetMsg("找不到 PlayerDataManager", false); return; }
            pdm.ToggleR18();
            SettingsManager.Instance?.Save();
            RefreshR18Display();
        });
        content.AddChild(_r18ToggleBtn);

        _r18StatusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _r18StatusLabel.AddThemeFontSizeOverride("font_size", 14);
        content.AddChild(_r18StatusLabel);

        // ── 進入房間指令（略過正常流程，直接切到訓練室場景，並自動打開 F1 表情測試面板，
        //    對應 Unity 版「進去房間就能立刻用 DEV 工具」的除錯體驗）────────────────────
        content.AddChild(MakeButton("進入房間（切到訓練室＋自動開DEV）", () =>
        {
            if (DevModeManager.Instance != null)
                DevModeManager.Instance.PendingAutoOpenExpressionTester = true;
            ClosePanel();
            GetTree().ChangeSceneToFile("res://Scenes/TrainingRoom.tscn");
        }, "1a3a2a", "40a070"));

        content.AddChild(new HSeparator());

        _msgLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        _msgLabel.AddThemeFontSizeOverride("font_size", 13);
        _msgLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_msgLabel);

        content.AddChild(MakeButton("關閉（F9 可再打開）", ClosePanel));

        RefreshUnlockDisplay();
        RefreshR18Display();

        // 視窗定位：記得上次拖動的位置，第一次開則置中
        CallDeferred(nameof(PositionWindow));
    }

    void PositionWindow()
    {
        // 這個視窗掛在 CanvasLayer 底下，不是一般 Control 樹狀結構，
        // 不會自動把自己收縮成內容需要的最小尺寸（不然就是之前那種
        // 下面留一大片空白、視窗被撐到跟畫面一樣高的問題）。
        // GetCombinedMinimumSize() 會即時重新計算，不受時機影響，直接拿來設定 Size 最保險。
        _window.Size = _window.GetCombinedMinimumSize();

        var vpSize = GetViewportRect().Size;
        _window.Position = s_lastWindowPos.X >= 0
            ? s_lastWindowPos
            : new Vector2(340f, 80f);   // 預設放左邊（狀態卡右側），不跟右邊的表情測試面板疊在一起

        ClampWindowToScreen();
    }

    Control BuildTitleBar()
    {
        var bar = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(0, 40) };
        var barSty = new StyleBoxFlat
        {
            BgColor = new Color("2a1848"),
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
        };
        barSty.ContentMarginLeft = 16;
        barSty.ContentMarginRight = 8;
        bar.AddThemeStyleboxOverride("panel", barSty);

        var hb = new HBoxContainer();
        bar.AddChild(hb);

        var title = new Label { Text = "開發者工具", VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", new Color("f0d8ff"));
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hb.AddChild(title);

        var closeBtn = new Button { Text = "✕", Flat = true, CustomMinimumSize = new Vector2(32, 32) };
        closeBtn.AddThemeColorOverride("font_color", new Color("d0a8e0"));
        closeBtn.Pressed += ClosePanel;
        hb.AddChild(closeBtn);

        bar.GuiInput += OnTitleBarGuiInput;
        return bar;
    }

    void OnTitleBarGuiInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed)
            {
                _dragging = true;
                _dragGrabOffset = GetViewport().GetMousePosition() - _window.Position;
            }
            else
            {
                _dragging = false;
                s_lastWindowPos = _window.Position;
            }
        }
        else if (ev is InputEventMouseMotion && _dragging)
        {
            _window.Position = GetViewport().GetMousePosition() - _dragGrabOffset;
            ClampWindowToScreen();
        }
    }

    void ClampWindowToScreen()
    {
        var vp = GetViewportRect().Size;
        var sz = _window.Size;
        float minVisible = 40f;
        float x = Mathf.Clamp(_window.Position.X, minVisible - sz.X, vp.X - minVisible);
        float y = Mathf.Clamp(_window.Position.Y, 0f, vp.Y - minVisible);
        _window.Position = new Vector2(x, y);
    }

    void RefreshOpenGalleryIfAny()
    {
        var gallery = GetTree().CurrentScene?.GetNodeOrNull<GalleryScreen>("GalleryScreen");
        if (gallery != null && gallery.Visible) gallery.RefreshContent();
    }

    void RefreshUnlockDisplay()
    {
        bool on = GalleryDataManager.Instance?.DebugUnlockAllOverride ?? false;
        _unlockToggleBtn.Text = on ? "全解鎖模式：開啟中（點擊關閉）" : "全解鎖模式：關閉中（點擊開啟）";
        StyleToggle(_unlockToggleBtn, on);
        _unlockStatusLabel.Text = on ? "圖鑒目前顯示：全部解鎖" : "圖鑒目前顯示：玩家真實進度";
        _unlockStatusLabel.AddThemeColorOverride("font_color", on ? new Color("9ae6a0") : new Color("a080c0"));
    }

    void RefreshR18Display()
    {
        bool on = PlayerDataManager.Instance?.R18ContentEnabled ?? false;
        _r18ToggleBtn.Text = on ? "R18：開啟中（點擊切換為全年齡）" : "全年齡：開啟中（點擊切換為 R18）";
        StyleToggle(_r18ToggleBtn, on);
        _r18StatusLabel.Text = on ? "目前顯示：R18 成人內容" : "目前顯示：全年齡（已遮蔽成人內容）";
        _r18StatusLabel.AddThemeColorOverride("font_color", on ? new Color("e0a0a0") : new Color("a080c0"));
    }

    static void StyleToggle(Button btn, bool on)
    {
        string bg = on ? "2a5c2a" : "5c2a2a";
        string border = on ? "60c060" : "c06060";

        var normal = new StyleBoxFlat { BgColor = new Color(bg), BorderColor = new Color(border) };
        normal.SetBorderWidthAll(2);
        normal.SetCornerRadiusAll(10);

        var hover = new StyleBoxFlat { BgColor = new Color(bg), BorderColor = new Color(border) };
        hover.BgColor = hover.BgColor.Lightened(0.15f);
        hover.SetBorderWidthAll(2);
        hover.SetCornerRadiusAll(10);

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", normal);
        btn.AddThemeColorOverride("font_color", new Color("f0f0f0"));
    }

    void SetMsg(string msg, bool ok)
    {
        _msgLabel.Text = msg;
        _msgLabel.AddThemeColorOverride("font_color", ok ? new Color("9ae6a0") : new Color("e08080"));
    }

    void ClosePanel()
    {
        s_lastWindowPos = _window.Position;
        DevModeManager.Instance?.ClosePanel();
    }

    static Button MakeButton(string text, Action onPressed, string bg = "261540", string border = "7a48a8")
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(0, 46) };

        var normal = new StyleBoxFlat { BgColor = new Color(bg), BorderColor = new Color(border) };
        normal.SetBorderWidthAll(1);
        normal.SetCornerRadiusAll(10);

        var hover = new StyleBoxFlat { BgColor = new Color("3a1860"), BorderColor = new Color("a060e0") };
        hover.SetBorderWidthAll(1);
        hover.SetCornerRadiusAll(10);

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", normal);
        btn.AddThemeColorOverride("font_color", new Color("f0d8ff"));
        btn.Pressed += onPressed;
        return btn;
    }
}
