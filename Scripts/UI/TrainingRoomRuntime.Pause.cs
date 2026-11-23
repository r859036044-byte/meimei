using Godot;

/// <summary>
/// TrainingRoom 右上角「⏸」暫停按鈕（放在「?」左邊）。
/// 按下 → 整個遊戲暫停（節點、語音、動畫都停），畫面變暗，出現「繼續」和「回標題畫面」兩個按鈕。
/// 暫停用 SceneTree.Paused ＋ Engine.TimeScale=0 兩層：節點本身停住，靠 delta 計時的非同步動畫也停住。
/// 暫停選單放在獨立的 CanvasLayer（ProcessMode＝Always），暫停中才點得到。
/// </summary>
public partial class TrainingRoomRuntime
{
    Button      _pauseBtn;
    CanvasLayer _pauseLayer;
    bool        _paused;
    double      _timeScaleBeforePause = 1.0;
    bool        _pauseQuitArmed;

    void SetupPauseButton()
    {
        _pauseBtn = new Button
        {
            Name = "PauseButton", Text = "", TooltipText = "暫停",   // 不用 ⏸ 字元：Windows 會把它畫成藍色方塊表情符號
            FocusMode = Control.FocusModeEnum.None,
        };
        _pauseBtn.AnchorLeft = 1f; _pauseBtn.AnchorRight = 1f; _pauseBtn.AnchorTop = 0f; _pauseBtn.AnchorBottom = 0f;
        _pauseBtn.OffsetLeft = -280f; _pauseBtn.OffsetRight = -234f;   // 放在「?」左邊（? 在 -224~-178）
        _pauseBtn.OffsetTop = 20f;    _pauseBtn.OffsetBottom = 66f;
        _pauseBtn.AddThemeFontSizeOverride("font_size", 24);
        _pauseBtn.AddThemeColorOverride("font_color", UiAccent);
        _pauseBtn.AddThemeColorOverride("font_hover_color", UiAccent.Darkened(0.2f));
        StyleBoxFlat Circle(Color bg)
        {
            var sb = new StyleBoxFlat { BgColor = bg, BorderColor = BarOutline };
            sb.SetBorderWidthAll(3); sb.SetCornerRadiusAll(23);
            return sb;
        }
        _pauseBtn.AddThemeStyleboxOverride("normal",  Circle(Colors.White));
        _pauseBtn.AddThemeStyleboxOverride("hover",   Circle(new Color(1f, 0.92f, 0.96f)));
        _pauseBtn.AddThemeStyleboxOverride("pressed", Circle(new Color(0.95f, 0.85f, 0.90f)));
        _pauseBtn.AddThemeStyleboxOverride("focus",   new StyleBoxEmpty());
        _pauseBtn.Pressed += () => { if (!_endingDay) SetPaused(true); };
        // 暫停圖示：兩條粉色直槓，跟旁邊「?」同色（用方塊畫，不依賴字型）
        foreach (float x in new[] { -9f, 3f })
        {
            var bar = new ColorRect { Color = UiAccent, MouseFilter = Control.MouseFilterEnum.Ignore };
            bar.AnchorLeft = 0.5f; bar.AnchorRight = 0.5f; bar.AnchorTop = 0.5f; bar.AnchorBottom = 0.5f;
            bar.OffsetLeft = x; bar.OffsetRight = x + 6f; bar.OffsetTop = -10f; bar.OffsetBottom = 10f;
            _pauseBtn.AddChild(bar);
        }
        UiRoot.AddChild(_pauseBtn);
    }

    void SetPaused(bool on)
    {
        if (on == _paused) return;
        _paused = on;
        _pauseQuitArmed = false;
        if (on)
        {
            _timeScaleBeforePause = Engine.TimeScale;
            Engine.TimeScale = 0.0;
            GetTree().Paused = true;
            BuildPauseMenu();
        }
        else
        {
            GetTree().Paused = false;
            Engine.TimeScale = _timeScaleBeforePause <= 0.0 ? 1.0 : _timeScaleBeforePause;
            if (_pauseLayer != null && IsInstanceValid(_pauseLayer)) { _pauseLayer.QueueFree(); _pauseLayer = null; }
        }
    }

    void BuildPauseMenu()
    {
        _pauseLayer = new CanvasLayer { Name = "PauseLayer", Layer = 3000, ProcessMode = ProcessModeEnum.Always };
        AddChild(_pauseLayer);

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop, ProcessMode = ProcessModeEnum.Always };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _pauseLayer.AddChild(dim);

        var box = new VBoxContainer { ProcessMode = ProcessModeEnum.Always };
        box.AddThemeConstantOverride("separation", 18);
        box.SetAnchorsPreset(Control.LayoutPreset.Center);
        box.CustomMinimumSize = new Vector2(360f, 0f);
        box.Position = new Vector2(-180f, -120f);
        dim.AddChild(box);

        var title = new Label { Text = "暫停中", HorizontalAlignment = HorizontalAlignment.Center, ProcessMode = ProcessModeEnum.Always };
        title.AddThemeFontSizeOverride("font_size", 40);
        title.AddThemeColorOverride("font_color", Colors.White);
        box.AddChild(title);

        Button Mk(string text, System.Action act)
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(360f, 64f), FocusMode = Control.FocusModeEnum.None, ProcessMode = ProcessModeEnum.Always };
            b.AddThemeFontSizeOverride("font_size", 26);
            b.Pressed += act;
            box.AddChild(b);
            return b;
        }
        Mk("繼續遊戲", () => SetPaused(false));
        Button quit = null;
        quit = Mk("回標題畫面", () =>
        {
            if (!_pauseQuitArmed) { _pauseQuitArmed = true; quit.Text = "再按一次確認回標題"; return; }
            SetPaused(false);
            GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
        });
    }

    /// <summary>離開場景時一定要把暫停還原，不然標題畫面會是停住的（由 TrainingRoomRuntime._ExitTree 呼叫）。</summary>
    void RestorePauseOnExit()
    {
        if (!_paused) return;
        GetTree().Paused = false;
        Engine.TimeScale = _timeScaleBeforePause <= 0.0 ? 1.0 : _timeScaleBeforePause;
        _paused = false;
    }
}
