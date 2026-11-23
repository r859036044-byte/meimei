using Godot;

/// <summary>
/// 視窗拖曳縮放時鎖住 16:9：視窗永遠維持遊戲畫面的比例，畫面等比放大縮小，不會出現上下／左右黑邊。
/// （全螢幕或最大化時視窗大小由系統決定，螢幕不是 16:9 的話仍會有黑邊，那是無法避免的。）
/// </summary>
public partial class WindowAspectLock : Node
{
    const float Aspect = 16f / 9f;
    Vector2I _last;
    bool _busy;

    public static void Install(Node host)
    {
        var root = host.GetTree().Root;
        if (root.GetNodeOrNull("WindowAspectLock") != null) return;
        root.CallDeferred(Node.MethodName.AddChild, new WindowAspectLock { Name = "WindowAspectLock" });
    }

    public override void _Ready()
    {
        var w = GetWindow();
        _last = w.Size;
        w.SizeChanged += OnSizeChanged;
    }

    public override void _ExitTree()
    {
        var w = GetWindow();
        if (w != null) w.SizeChanged -= OnSizeChanged;
    }

    void OnSizeChanged()
    {
        if (_busy) return;
        var w = GetWindow();
        var s = w.Size;
        if (w.Mode != Window.ModeEnum.Windowed || s.X <= 0 || s.Y <= 0) { _last = s; return; }

        int tw = s.X, th = s.Y;
        // 使用者拖的是寬還是高？變動比較大的那一邊當基準，另一邊跟著算
        if (Mathf.Abs(s.X - _last.X) >= Mathf.Abs(s.Y - _last.Y)) th = Mathf.RoundToInt(s.X / Aspect);
        else tw = Mathf.RoundToInt(s.Y * Aspect);

        if (Mathf.Abs(tw - s.X) <= 1 && Mathf.Abs(th - s.Y) <= 1) { _last = s; return; }

        _busy = true;
        var t = new Vector2I(tw, th);
        w.Size = t;
        _last = t;
        CallDeferred(nameof(ReleaseBusy));
    }

    void ReleaseBusy() => _busy = false;
}
