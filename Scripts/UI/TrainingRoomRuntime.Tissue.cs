using Godot;

/// <summary>
/// TrainingRoomRuntime — 衛生紙模式：
/// 身上／床上還有精液時出現「🧻 衛生紙」按鈕，按下進入擦拭模式（滑鼠變衛生紙）。
/// 按住左鍵在精液上擦，精液會慢慢變淡消失；全部擦乾淨之前，「離開房間」會被擋下。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("衛生紙")]
    [Export] public float TissueRadius   = 70f;    // 擦拭半徑（螢幕 px）
    [Export] public float TissueWipeRate = 1.6f;   // 每秒擦掉多少透明度（1＝0.6 秒擦掉）
    [Export] public string[] TissueNeedLines = { "（還沒擦乾淨…先用衛生紙擦乾淨再走。）", "（精液還留著，要擦乾淨才行。）" };
    [Export] public string[] TissueDoneLines = { "（擦乾淨了。）" };

    bool   _tissueOn;
    Button _tissueBtn;
    Label  _tissueCursor;
    bool   _tissueHadDebris;

    static bool DebrisAlive(Control d) => d != null && GodotObject.IsInstanceValid(d) && !d.IsQueuedForDeletion() && d.Visible;

    int TissueRemaining()
    {
        int n = 0;
        foreach (var d in _ejacDebris) if (DebrisAlive(d) && d.Modulate.A > 0.05f && d.SelfModulate.A > 0.05f) n++;
        return n;
    }

    /// <summary>離開房間前呼叫：還有精液沒擦乾淨就擋下並進入衛生紙模式。回傳 true＝可以離開。</summary>
    bool TissueAllowLeave()
    {
        if (TissueRemaining() <= 0) return true;
        _tissueOn = true;
        SayMale(TissueNeedLines);
        return false;
    }

    bool TissueOverUi(Vector2 p)
    {
        if (_tissueBtn != null && IsInstanceValid(_tissueBtn) && _tissueBtn.GetGlobalRect().HasPoint(p)) return true;
        var leave = UiRoot?.GetNodeOrNull<Control>("LeavePanel");
        return leave != null && leave.GetGlobalRect().HasPoint(p);
    }

    /// <summary>_Input 最前面呼叫：擦拭模式下左鍵不要傳給其他互動（按鈕除外）。</summary>
    bool TissueInput(InputEvent e)
    {
        if (!_tissueOn) return false;
        if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !TissueOverUi(mb.Position)) return true;
        return false;
    }

    void UpdateTissue(float dt)
    {
        var parent = UiRoot;
        if (parent == null) return;
        if (_tissueBtn == null || !IsInstanceValid(_tissueBtn))
        {
            _tissueBtn = MakeCornerBtn(parent, "BtnTissue", "🧻 衛生紙", -556f, -416f);
            _tissueBtn.Visible = false;
            _tissueBtn.Pressed += () => { _tissueOn = !_tissueOn; };
        }
        if (_tissueCursor == null || !IsInstanceValid(_tissueCursor))
        {
            _tissueCursor = new Label { Name = "TissueCursor", Text = "🧻", MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 4000, Visible = false };
            _tissueCursor.AddThemeFontSizeOverride("font_size", 56);
            parent.AddChild(_tissueCursor);
        }

        int remain = TissueRemaining();
        bool any = remain > 0;
        if (any) _tissueHadDebris = true;
        _tissueBtn.Visible = any || _tissueOn;
        _tissueBtn.Text = _tissueOn ? "🧻 收起" : "🧻 衛生紙";

        if (!any && _tissueOn)
        {   // 擦乾淨了
            _tissueOn = false;
            HideJijiSemen();
            if (_tissueHadDebris) { _tissueHadDebris = false; SayMale(TissueDoneLines); }
        }

        _tissueCursor.Visible = _tissueOn;
        if (!_tissueOn) return;

        Vector2 m = GetViewport().GetMousePosition();
        _tissueCursor.Position = m - new Vector2(28f, 28f);
        if (!Input.IsMouseButtonPressed(MouseButton.Left) || TissueOverUi(m)) return;

        foreach (var d in _ejacDebris.ToArray())
        {
            if (!DebrisAlive(d)) continue;
            Vector2 c = d.GetGlobalTransform() * (d.Size * 0.5f);
            if (c.DistanceTo(m) > TissueRadius) continue;
            var mod = d.Modulate;
            mod.A -= TissueWipeRate * dt;
            d.Modulate = mod;
            if (mod.A <= 0.05f) { _ejacDebris.Remove(d); d.QueueFree(); }
        }
    }
}
