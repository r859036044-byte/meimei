using Godot;

/// <summary>
/// 視窗鎖 16:9：視窗（非全螢幕）永遠維持遊戲畫面的比例，畫面等比放大縮小，不變形、不出現黑邊。
/// 作法：視窗大小停止變動一小段時間（使用者放開滑鼠）之後，才把視窗修正成 16:9。
/// （以前是在 SizeChanged 裡立刻改大小，拖曳中會被系統蓋回去；而且開遊戲時視窗不是 16:9 也不會被修正。）
/// 全螢幕／最大化時，視窗大小由系統決定，螢幕不是 16:9 的話仍會有黑邊，無法避免。
/// </summary>
public partial class WindowAspectLock : Node
{
    const float Aspect     = 16f / 9f;
    const float SettleSec  = 0.15f;   // 視窗大小穩定多久之後才修正
    const int   MinWidth   = 960;

    Vector2I _seen;        // 上一幀看到的視窗大小
    Vector2I _stable;      // 上一次「已經是 16:9」的大小（判斷使用者主要拖的是寬還是高）
    float    _still;       // 視窗大小已經沒變動的秒數
    bool     _startupChecked;

    public static void Install(Node host)
    {
        var root = host.GetTree().Root;
        if (root.GetNodeOrNull("WindowAspectLock") != null) return;
        root.CallDeferred(Node.MethodName.AddChild, new WindowAspectLock { Name = "WindowAspectLock" });
    }

    public override void _Ready()
    {
        var w = GetWindow();
        _seen = _stable = w.Size;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        var w = GetWindow();
        if (w == null) return;
        var s = w.Size;
        if (s.X <= 0 || s.Y <= 0) return;

        if (s != _seen) { _seen = s; _still = 0f; return; }
        _still += (float)delta;
        if (_still < SettleSec && _startupChecked) return;
        _startupChecked = true;

        if (w.Mode != Window.ModeEnum.Windowed) { _stable = s; return; }   // 全螢幕／最大化：交給系統
        if (IsAspectOk(s)) { _stable = s; return; }

        Fix(w, s);
        _still = 0f;
    }

    static bool IsAspectOk(Vector2I s) => Mathf.Abs(s.X / (float)s.Y - Aspect) < 0.008f;

    void Fix(Window w, Vector2I s)
    {
        // 使用者拖的是寬還是高？變動比較大的那一邊當基準，另一邊跟著算（開遊戲時 _stable == s，預設以寬為準）
        int tw = s.X, th = s.Y;
        if (Mathf.Abs(s.Y - _stable.Y) > Mathf.Abs(s.X - _stable.X)) tw = Mathf.RoundToInt(s.Y * Aspect);
        else th = Mathf.RoundToInt(s.X / Aspect);

        // 不要超過螢幕可用範圍（扣掉標題列／邊框的餘量）
        int screen = w.CurrentScreen;
        var usable = DisplayServer.ScreenGetUsableRect(screen);
        int maxW = usable.Size.X - 16;
        int maxH = usable.Size.Y - 48;
        if (tw > maxW) { tw = maxW; th = Mathf.RoundToInt(tw / Aspect); }
        if (th > maxH) { th = maxH; tw = Mathf.RoundToInt(th * Aspect); }
        if (tw < MinWidth) { tw = MinWidth; th = Mathf.RoundToInt(tw / Aspect); }

        var t = new Vector2I(tw, th);
        w.Size = t;
        _stable = t;
        _seen = t;

        // 如果放大後超出螢幕下緣／右緣，把視窗往回挪
        var pos = w.Position;
        int dx = Mathf.Min(0, usable.End.X - (pos.X + tw + 8));
        int dy = Mathf.Min(0, usable.End.Y - (pos.Y + th + 8));
        if (dx != 0 || dy != 0)
            w.Position = new Vector2I(Mathf.Max(usable.Position.X, pos.X + dx), Mathf.Max(usable.Position.Y, pos.Y + dy));
    }
}
