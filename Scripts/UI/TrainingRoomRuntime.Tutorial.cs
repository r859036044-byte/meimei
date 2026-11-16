using Godot;

/// <summary>
/// 新手教學（全年齡，開新遊戲後第一次進調教室，設定裡可關）：
/// 畫面暗下去只留一塊亮區聚焦，手把手帶你：摸頭 → 捏臉 → 莉奈說一句普通話（摸頭）→ 莉奈說一句雜魚話（捏臉）→ 提示離開房間。
/// 可隨時跳過。教學中不動任何真實數值（心情／雌小鬼值每幀還原成進教學前的值），台詞是固定的。
/// 面板上的「開新遊戲時顯示教學」勾選框跟設定頁同一個開關。
/// </summary>
public partial class TrainingRoomRuntime
{
    public static bool TutorialPending { get; set; }

    bool  _tutActive, _tutWaiting;
    float _tutWaitT, _tutPatT, _tutAdvanceT = -1f;
    int   _tutStep;
    float _tutMood, _tutBrat;
    CanvasLayer _tutLayer;
    ColorRect[] _tutDim; ColorRect _tutBlock;
    Panel _tutRing; PanelContainer _tutBox; Label _tutMsg; Button _tutNext, _tutSkip;
    Rect2? _tutHole;

    void SetupTutorial()
    {
        if (!TutorialPending) return;
        TutorialPending = false;
        if (BratR18 || DemoMode.Enabled) return;   // 只有全年齡版有教學
        _tutWaiting = true;
    }

    void TutorialStart()
    {
        _tutWaiting = false;
        if (!_bratLoaded) BratLoad();
        _tutMood = Mood; _tutBrat = BratValue;
        _tutActive = true; _tutStep = 0; _tutPatT = 0f; _tutAdvanceT = -1f;
        BuildTutorialUi();
        TutorialGoto(0);
    }

    void BuildTutorialUi()
    {
        _tutLayer = new CanvasLayer { Layer = 1500, Name = "TutorialLayer" };
        AddChild(_tutLayer);
        _tutDim = new ColorRect[4];
        for (int i = 0; i < 4; i++)
        {
            _tutDim[i] = new ColorRect { Color = new Color(0f, 0f, 0f, 0.68f), MouseFilter = Control.MouseFilterEnum.Stop };
            _tutLayer.AddChild(_tutDim[i]);
        }
        _tutBlock = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };   // 只讀步驟：擋住亮區裡的操作
        _tutLayer.AddChild(_tutBlock);
        _tutRing = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        var rs = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.0f), BorderColor = new Color(1f, 0.82f, 0.9f) };
        rs.SetBorderWidthAll(4); rs.SetCornerRadiusAll(18);
        _tutRing.AddThemeStyleboxOverride("panel", rs);
        _tutLayer.AddChild(_tutRing);
        var rt = _tutRing.CreateTween().SetLoops();
        rt.TweenProperty(_tutRing, "modulate:a", 0.35f, 0.7f); rt.TweenProperty(_tutRing, "modulate:a", 1f, 0.7f);

        _tutBox = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        var bs = new StyleBoxFlat { BgColor = new Color(1f, 0.97f, 0.99f, 0.97f), BorderColor = BarOutline };
        bs.SetBorderWidthAll(3); bs.SetCornerRadiusAll(18); bs.SetContentMarginAll(18);
        _tutBox.AddThemeStyleboxOverride("panel", bs);
        var vb = new VBoxContainer(); vb.AddThemeConstantOverride("separation", 10);
        _tutBox.AddChild(vb);
        _tutMsg = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
        _tutMsg.AddThemeFontSizeOverride("font_size", 22);
        _tutMsg.AddThemeColorOverride("font_color", new Color(0.22f, 0.10f, 0.18f));
        vb.AddChild(_tutMsg);
        var hb = new HBoxContainer(); hb.AddThemeConstantOverride("separation", 12); hb.Alignment = BoxContainer.AlignmentMode.End;
        _tutSkip = BratMakeBtn("跳過教學", () => TutorialEnd());
        _tutNext = BratMakeBtn("下一步", () => TutorialGoto(_tutStep + 1));
        hb.AddChild(_tutSkip); hb.AddChild(_tutNext);
        vb.AddChild(hb);
        var chk = new CheckBox { Text = "開新遊戲時顯示新手教學", FocusMode = Control.FocusModeEnum.None, ButtonPressed = SettingsManager.Instance?.TutorialEnabled ?? true };
        chk.AddThemeFontSizeOverride("font_size", 18);
        foreach (var cn in new[] { "font_color", "font_pressed_color", "font_hover_color", "font_focus_color", "font_hover_pressed_color" })
            chk.AddThemeColorOverride(cn, new Color(0.22f, 0.10f, 0.18f));
        chk.Toggled += on => { SettingsManager.Instance?.SetTutorialEnabled(on); SettingsManager.Instance?.Save(); };
        vb.AddChild(chk);
        _tutLayer.AddChild(_tutBox);
    }

    void TutorialGoto(int step)
    {
        _tutStep = step; _tutPatT = 0f; _tutAdvanceT = -1f;
        if (step >= 6) { TutorialEnd(); return; }
        HideBubble(_bubbleGirl); HideBubble(_bubblePlayer);
        bool info = step == 0 || step == 5;   // 只讀步驟：要按按鈕才往下
        _tutNext.Visible = info;
        _tutSkip.Visible = step != 5;
        _tutBlock.Visible = info;
        switch (step)
        {
            case 0: _tutMsg.Text = "歡迎來到莉奈的房間！\n要不要先看一下簡單的新手教學？（教學不會影響真正的數值）"; _tutNext.Text = "開始教學"; break;
            case 1: _tutMsg.Text = "① 摸頭\n在莉奈的頭上按住滑鼠左鍵，摸摸看吧。"; break;
            case 2: _tutMsg.Text = "② 捏臉\n在莉奈的臉頰上按住左鍵，往旁邊拖一下再放開。"; break;
            case 3:
                _tutMsg.Text = "③ 莉奈說了普通的話\n這時候摸頭，她會比較開心（心情值上升）。請摸頭。";
                TutorialGirlSay("今天…天氣好像不錯呢。", false); break;
            case 4:
                _tutMsg.Text = "④ 莉奈說了雜魚話\n這時候要捏她的臉，雌小鬼值才會下降。請捏臉。";
                TutorialGirlSay("哼～雜魚哥哥～", true); break;
            case 5:
                _tutMsg.Text = "⑤ 離開房間\n莉奈主動說滿 8 次話，她就累了，會自動結束今晚。\n中途自己按這個鈕離開，莉奈的心情會大幅下降（互動得越少越嚴重）。";
                _tutNext.Text = "我知道了"; break;
        }
    }

    void TutorialGirlSay(string text, bool brat)
    {
        ShowBubble(ref _bubbleGirl, ref _bubbleGirlLbl, text, false);
        if (_bubbleGirl != null && IsInstanceValid(_bubbleGirl)) _bubbleGirl.SetMeta("hide_t", 600f);
        if (brat) PlayTrVoice("talk_brat/brat_zako_2.wav"); else PlayNormalLineVoice();
    }

    /// <summary>捏臉放手時 Brat.cs 呼叫（教學中）。</summary>
    void TutorialPinched()
    {
        if (_tutStep == 2 || _tutStep == 4) _tutAdvanceT = 0.8f;
    }

    void TutorialEnd()
    {
        if (!_tutActive) return;
        _tutActive = false;
        HideBubble(_bubbleGirl); HideBubble(_bubblePlayer);
        if (_tutLayer != null && IsInstanceValid(_tutLayer)) _tutLayer.QueueFree();
        _tutLayer = null;
        Mood = _tutMood; BratValue = _tutBrat;
        _bratTimer = -1f; _bratLineType = 0; _bratDirty = false;
        BratSave();
    }

    Rect2 TutorialLayerRect(string layer, Rect2 localRect)
    {
        if (!Layers.TryGetValue(layer, out var c) || c == null || !c.IsVisibleInTree()) return new Rect2();
        var xf = c.GetGlobalTransformWithCanvas();
        Vector2 a = xf * localRect.Position, b = xf * localRect.End, cc = xf * new Vector2(localRect.Position.X, localRect.End.Y), d = xf * new Vector2(localRect.End.X, localRect.Position.Y);
        float x0 = Mathf.Min(Mathf.Min(a.X, b.X), Mathf.Min(cc.X, d.X)), x1 = Mathf.Max(Mathf.Max(a.X, b.X), Mathf.Max(cc.X, d.X));
        float y0 = Mathf.Min(Mathf.Min(a.Y, b.Y), Mathf.Min(cc.Y, d.Y)), y1 = Mathf.Max(Mathf.Max(a.Y, b.Y), Mathf.Max(cc.Y, d.Y));
        return new Rect2(x0, y0, x1 - x0, y1 - y0);
    }

    Rect2? TutorialHoleForStep()
    {
        switch (_tutStep)
        {
            case 1: case 3: return TutorialLayerRect("前髮", HeadPatZone);
            case 2: case 4:
                PinchScan();
                if (!_pinchScanned || !Layers.TryGetValue("臉", out var f) || f == null) return null;
                var u = _pinchFaceUV;
                return TutorialLayerRect("臉", new Rect2(u.Position.X * f.Size.X, u.Position.Y * f.Size.Y, u.Size.X * f.Size.X, u.Size.Y * f.Size.Y));
            case 5:
                var lp = _canvas?.GetNodeOrNull<Control>("LeavePanel");
                return lp != null ? lp.GetGlobalRect() : null;
        }
        return null;
    }

    void TutorialProcess(float dt)
    {
        Mood = _tutMood; BratValue = _tutBrat;   // 教學不影響真實數值
        if (_tutLayer == null || !IsInstanceValid(_tutLayer)) return;

        // 進度判定
        if (_tutAdvanceT >= 0f) { _tutAdvanceT -= dt; if (_tutAdvanceT < 0f) TutorialGoto(_tutStep + 1); }
        else if (_tutStep == 1 || _tutStep == 3)
        {
            if (_patOn) _tutPatT += dt;
            if (_tutPatT >= 1.2f) _tutAdvanceT = 0.6f;
        }

        // 遮罩：亮區每幀跟著人物（鏡頭可能在動）
        var vp = GetViewport().GetVisibleRect().Size;
        _tutHole = TutorialHoleForStep();
        if (_tutHole is Rect2 h && h.Size.X > 4f)
        {
            h = h.Grow(10f);
            float x0 = Mathf.Clamp(h.Position.X, 0, vp.X), x1 = Mathf.Clamp(h.End.X, 0, vp.X), y0 = Mathf.Clamp(h.Position.Y, 0, vp.Y), y1 = Mathf.Clamp(h.End.Y, 0, vp.Y);
            Place(_tutDim[0], 0, 0, vp.X, y0); Place(_tutDim[1], 0, y1, vp.X, vp.Y - y1);
            Place(_tutDim[2], 0, y0, x0, y1 - y0); Place(_tutDim[3], x1, y0, vp.X - x1, y1 - y0);
            Place(_tutBlock, x0, y0, x1 - x0, y1 - y0);
            Place(_tutRing, x0, y0, x1 - x0, y1 - y0); _tutRing.Visible = true;
        }
        else
        {
            Place(_tutDim[0], 0, 0, vp.X, vp.Y); for (int i = 1; i < 4; i++) Place(_tutDim[i], 0, 0, 0, 0);
            Place(_tutBlock, 0, 0, 0, 0); _tutRing.Visible = false;
        }

        // 說明面板：亮區在上半就放下面，反之放上面
        _tutBox.ResetSize();
        float cy = _tutHole is Rect2 hh ? hh.GetCenter().Y : vp.Y * 0.5f;
        _tutBox.Position = new Vector2((vp.X - _tutBox.Size.X) * 0.5f, cy < vp.Y * 0.5f ? vp.Y - _tutBox.Size.Y - 40f : 40f);
    }

    static void Place(Control c, float x, float y, float w, float h)
    {
        c.Position = new Vector2(x, y); c.Size = new Vector2(Mathf.Max(0, w), Mathf.Max(0, h));
    }
}
