using Godot;

/// <summary>
/// 全年齡：莉奈主動說話滿 AAMaxTalks 次 → 她累了，提示後自動離開房間（走 EndToday，不扣心情）。
/// 沒滿就自己按「離開房間」→ 心情大幅下降（做得越少扣越多），並顯示謎語人式提示。R18 不套用。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("對話次數上限（全年齡）")]
    [Export] public int   AAMaxTalks = 8;                 // 莉奈主動說話幾次後強制結束
    [Export] public float AAEarlyLeaveMoodDown = 40f;     // 一句都沒互動就離開：心情扣多少（做得越多扣越少：× (1 − 次數/上限)）
    [Export] public float AATalkEndNoticeSec = 3.5f;      // 提示停留秒數
    [Export] public string AATalkEndNotice = "夜深了。\n她的呼吸，漸漸平穩下來……";
    [Export] public string AAEarlyLeaveNotice = "現實裡，你和莉奈之間的距離，\n或許不會因此好轉……";

    int  _aaTalkCount;
    bool _aaAutoLeaving;

    /// <summary>莉奈每主動說一句呼叫（BratProcess）。</summary>
    void AATalkCounted() { if (!BratR18) _aaTalkCount++; }

    /// <summary>每次一輪對話結算完呼叫（BratResolve）：滿了就收工。</summary>
    void AACheckTalkLimit()
    {
        if (BratR18 || _aaAutoLeaving || _endingDay || _aaTalkCount < AAMaxTalks) return;
        _aaAutoLeaving = true;
        TaskLogger.Run(AAAutoLeaveAsync(), "TrainingRoom.AAAutoLeave");
    }

    async System.Threading.Tasks.Task AAAutoLeaveAsync()
    {
        AAShowNotice(AATalkEndNotice, AATalkEndNoticeSec);
        await ToSignal(GetTree().CreateTimer(AATalkEndNoticeSec), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this)) return;
        BratSave();
        EndToday();
        if (!_endingDay) _aaAutoLeaving = false;   // 被擋下（例如還沒擦乾淨）：之後可再試
    }

    /// <summary>玩家自己按離開／結束今晚。</summary>
    void LeaveByPlayer()
    {
        if (_endingDay || _aaAutoLeaving || _timingChallengeActive) return;
        if (!BratR18 && BratEnabled && _aaTalkCount > 0 && _aaTalkCount < AAMaxTalks && TissueAllowLeave())
        {
            float loss = AAEarlyLeaveMoodDown * (1f - (float)_aaTalkCount / AAMaxTalks);
            Mood = Mathf.Max(1f, Mood - loss);   // 不會因為離開直接觸發心情 0 結局
            BratSave();
            GD.Print($"[Brat] 主動離開（只互動 {_aaTalkCount}/{AAMaxTalks} 次）→ 心情 -{loss:F0}");
            AAShowNotice(AAEarlyLeaveNotice, 2.5f);
            _aaAutoLeaving = true;
            TaskLogger.Run(AALeaveDelayed(2.5f), "TrainingRoom.AALeaveDelayed");
            return;
        }
        EndToday();
    }

    async System.Threading.Tasks.Task AALeaveDelayed(float sec)
    {
        await ToSignal(GetTree().CreateTimer(sec), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this)) return;
        EndToday();
        if (!_endingDay) _aaAutoLeaving = false;
    }

    /// <summary>畫面中央的淡入淡出提示字。</summary>
    void AAShowNotice(string text, float sec)
    {
        var layer = new CanvasLayer { Layer = 2500, Name = "AANotice" };
        var lbl = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Modulate = new Color(1, 1, 1, 0) };
        lbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        lbl.AddThemeFontSizeOverride("font_size", 30);
        lbl.AddThemeColorOverride("font_color", new Color(1f, 0.93f, 0.96f));
        lbl.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.03f, 0.08f));
        lbl.AddThemeConstantOverride("outline_size", 8);
        layer.AddChild(lbl);
        AddChild(layer);
        var tw = lbl.CreateTween();
        tw.TweenProperty(lbl, "modulate:a", 1f, 0.6f);
        tw.TweenInterval(Mathf.Max(0.1f, sec - 1.4f));
        tw.TweenProperty(lbl, "modulate:a", 0f, 0.8f);
        tw.TweenCallback(Callable.From(layer.QueueFree));
    }
}
