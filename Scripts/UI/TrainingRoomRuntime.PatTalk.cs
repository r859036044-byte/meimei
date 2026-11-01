using Godot;

/// <summary>
/// 摸頭時的台詞氣泡：主角（玩家）一個氣泡在畫面下方，被摸的角色一個氣泡在頭旁邊（跟著頭走）。
/// 台詞都在 Inspector「摸頭台詞」群組（PatPlayerLines／PatGirlLines），隨機挑一句，Play 中可即時改。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("摸頭台詞")]
    [Export] public bool     PatTalkEnabled   = true;
    [Export] public string[] PatPlayerLines   = { "睡覺的時候還是挺可愛的嘛~" };   // 主角台詞（自行增加）
    [Export] public string[] PatGirlLines     = { "嗯……" };                       // 被摸的人台詞（自行增加）
    [Export] public float    PatTalkFirstDelay = 0.8f;   // 開始摸頭後多久出現第一句
    [Export] public float    PatTalkGap        = 1.0f;   // 主角說完到對方回應的間隔
    [Export] public float    PatTalkShowTime   = 3.0f;   // 每個氣泡停留秒數
    [Export] public float    PatTalkRepeat     = 9.0f;   // 一直摸的話，多久再來一輪
    [Export] public Vector2  PatGirlBubbleOffset   = new Vector2(40f, 10f);    // 對方氣泡尾巴尖端相對頭頂的位置（螢幕 px，越小越靠近頭）
    [Export] public float    PatPlayerBubbleBottom = 12f;    // 主角（第一人稱）氣泡：水平置中、貼畫面最底部，尾巴尖端離底部的距離（螢幕 px）

    Control _bubblePlayer, _bubbleGirl;
    Label   _bubblePlayerLbl, _bubbleGirlLbl;
    float   _patTalkT = -1f;
    int     _patTalkStep;   // 0＝等主角 1＝主角說了、等對方 2＝等下一輪
    readonly RandomNumberGenerator _patRng = new();

    void UpdatePatTalk(float dt)
    {
        if (!PatTalkEnabled || !_patOn)
        {
            if (_patTalkT >= 0f) HideBubble(_bubblePlayer);   // 女主氣泡留給點擊反應用（Play.cs），自己會自動收
            _patTalkT = -1f; _patTalkStep = 0;
            LayoutBubbles(dt);
            return;
        }
        if (_patTalkT < 0f) { _patTalkT = 0f; _patTalkStep = 0; }
        _patTalkT += dt;

        if (_patTalkStep == 0 && _patTalkT >= PatTalkFirstDelay)
        {
            ShowBubble(ref _bubblePlayer, ref _bubblePlayerLbl, Pick(PatPlayerLines), true);
            _patTalkStep = 1; _patTalkT = 0f;
        }
        else if (_patTalkStep == 1 && _patTalkT >= PatTalkGap)
        {
            ShowBubble(ref _bubbleGirl, ref _bubbleGirlLbl, Pick(PatGirlLines), false);
            _patTalkStep = 2; _patTalkT = 0f;
        }
        else if (_patTalkStep == 2 && _patTalkT >= PatTalkRepeat)
        {
            _patTalkStep = 0; _patTalkT = PatTalkFirstDelay;   // 下一輪立刻開始
        }

        LayoutBubbles(dt);
    }

    /// <summary>氣泡每幀的排版／自動收起（摸頭台詞和點擊反應共用）。</summary>
    void LayoutBubbles(float dt)
    {
        PlaceTail(_bubblePlayer, true);
        PlaceTail(_bubbleGirl, false);

        // 自動收起
        AutoHide(_bubblePlayer, dt);
        AutoHide(_bubbleGirl, dt);

        // 對方氣泡跟著頭走
        if (_bubbleGirl != null && IsInstanceValid(_bubbleGirl) && Layers.TryGetValue("前髮", out var hair) && hair != null)
        {
            Vector2 world  = hair.GetGlobalTransform() * HeadPatHairAnchor;
            Vector2 screen = _canvas.Transform * world;
            // 尾巴尖端（氣泡左下 x=40-16、y=底+22）對準頭頂＋偏移
            Vector2 pos = screen + PatGirlBubbleOffset - new Vector2(40f - 17f, _bubbleGirl.Size.Y + 24f);
            // 鏡頭放大時頭頂可能在畫面外：氣泡夾在畫面內（留 8px 邊），尾巴超出時藏起來
            Vector2 vpG = GetViewport().GetVisibleRect().Size;
            Vector2 clamped = new Vector2(
                Mathf.Clamp(pos.X, 8f, Mathf.Max(8f, vpG.X - _bubbleGirl.Size.X - 8f)),
                Mathf.Clamp(pos.Y, 8f, Mathf.Max(8f, vpG.Y - _bubbleGirl.Size.Y - 30f)));
            _bubbleGirl.Position = clamped;
            var tailG = _bubbleGirl.GetNodeOrNull<Node2D>("Tail");
            if (tailG != null) tailG.Visible = clamped.IsEqualApprox(pos);
        }
        if (_bubblePlayer != null && IsInstanceValid(_bubblePlayer))
        {
            Vector2 vp = GetViewport().GetVisibleRect().Size;
            _bubblePlayer.Position = new Vector2((vp.X - _bubblePlayer.Size.X) * 0.5f, vp.Y - PatPlayerBubbleBottom - 22f - _bubblePlayer.Size.Y);
        }
    }

    static void PlaceTail(Control b, bool isPlayer)
    {
        if (b == null || !GodotObject.IsInstanceValid(b)) return;
        var t = b.GetNodeOrNull<Node2D>("Tail");
        if (t != null) t.Position = new Vector2(isPlayer ? b.Size.X * 0.5f : 40f, b.Size.Y);
    }

    string Pick(string[] lines)
    {
        if (lines == null || lines.Length == 0) return "";
        return lines[_patRng.RandiRange(0, lines.Length - 1)];
    }

    void ShowBubble(ref Control bubble, ref Label lbl, string text, bool isPlayer)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (bubble == null || !IsInstanceValid(bubble)) (bubble, lbl) = BuildBubble(isPlayer);
        lbl.Text = text;
        bubble.ResetSize();
        bubble.SetMeta("hide_t", PatTalkShowTime);
        bubble.Visible = true;
        bubble.Modulate = new Color(1, 1, 1, 0);
        bubble.Scale = new Vector2(0.85f, 0.85f);
        var tw = bubble.CreateTween().SetParallel(true);
        tw.TweenProperty(bubble, "modulate:a", 1f, 0.18);
        tw.TweenProperty(bubble, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    void AutoHide(Control b, float dt)
    {
        if (b == null || !IsInstanceValid(b) || !b.Visible || !b.HasMeta("hide_t")) return;
        float t = (float)b.GetMeta("hide_t") - dt;
        b.SetMeta("hide_t", t);
        if (t <= 0f) { b.RemoveMeta("hide_t"); HideBubble(b); }
    }

    void HideBubble(Control b)
    {
        if (b == null || !IsInstanceValid(b) || !b.Visible) return;
        var tw = b.CreateTween();
        tw.TweenProperty(b, "modulate:a", 0f, 0.2);
        tw.TweenCallback(Callable.From(() => { if (IsInstanceValid(b)) b.Visible = false; }));
    }

    /// <summary>圓角白底＋深色描邊的對話氣泡，下方一個小尾巴（主角尾巴在下方中間，對方尾巴在左下指向頭）。</summary>
    (Control, Label) BuildBubble(bool isPlayer)
    {
        var box = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        var sb = new StyleBoxFlat
        {
            BgColor = isPlayer ? new Color(1f, 1f, 1f, 0.90f) : new Color(1f, 0.97f, 0.99f, 0.90f),   // 90% 不透明
            BorderColor = BarOutline,
        };
        sb.SetBorderWidthAll(3); sb.SetCornerRadiusAll(22);
        sb.ContentMarginLeft = sb.ContentMarginRight = 22; sb.ContentMarginTop = sb.ContentMarginBottom = 12;
        if (!isPlayer) { sb.ContentMarginLeft = sb.ContentMarginRight = sb.ContentMarginTop = sb.ContentMarginBottom = 0; }   // 粉框緊貼外框，中間不留白
        box.AddThemeStyleboxOverride("panel", sb);

        // 女主：內側一圈粉紅色細框（跟設定／對話框同一種粉色 e8b5ce）
        Container textParent = box;
        if (!isPlayer)
        {
            var inner = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            var isb = new StyleBoxFlat { BgColor = new Color(1f, 1f, 1f, 0f), BorderColor = new Color("e8b5ce") };
            isb.SetBorderWidthAll(3); isb.SetCornerRadiusAll(19);
            isb.ContentMarginLeft = isb.ContentMarginRight = 17; isb.ContentMarginTop = isb.ContentMarginBottom = 7;
            inner.AddThemeStyleboxOverride("panel", isb);
            box.AddChild(inner);
            textParent = inner;
        }

        var lbl = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        lbl.AddThemeFontSizeOverride("font_size", isPlayer ? 24 : 22);
        lbl.AddThemeColorOverride("font_color", UiText);
        textParent.AddChild(lbl);

        // 尾巴：描邊三角＋內層白三角（Node2D，不受容器排版影響；位置每幀依氣泡大小更新）
        var tail = new Node2D { Name = "Tail" };
        box.AddChild(tail);
        if (isPlayer)
        {
            tail.AddChild(new Polygon2D { Color = BarOutline, Polygon = new[] { new Vector2(-14, -3), new Vector2(14, -3), new Vector2(0, 22) } });
            tail.AddChild(new Polygon2D { Color = sb.BgColor, Polygon = new[] { new Vector2(-10, -5), new Vector2(10, -5), new Vector2(0, 16) } });
        }
        else
        {
            // 女主：深色外框 → 粉紅內框 → 底色，三層三角形，粉框一路包到尾巴尖端
            tail.AddChild(new Polygon2D { Color = BarOutline,           Polygon = new[] { new Vector2(-15, -3), new Vector2(15, -3), new Vector2(-17, 24) } });
            tail.AddChild(new Polygon2D { Color = new Color("e8b5ce"), Polygon = new[] { new Vector2(-12, -6), new Vector2(12, -6), new Vector2(-14.5f, 19) } });
            tail.AddChild(new Polygon2D { Color = sb.BgColor,           Polygon = new[] { new Vector2(-8, -8), new Vector2(8, -8), new Vector2(-11, 13) } });
        }

        UiRoot.AddChild(box);
        return (box, lbl);
    }
}
