using Godot;

/// <summary>只移動整個立繪父容器，身體與表情保持同步，不改動劇情定位。</summary>
public partial class PortraitFloat : Node
{
    Control _portrait;
    Vector2 _origin;
    float _time;
    bool _initialized;
    public static void Attach(Control portrait)
    {
        if (!portrait.HasNode("PortraitFloat")) portrait.AddChild(new PortraitFloat { Name = "PortraitFloat" });
    }
    public override void _Ready() => _portrait = GetParent<Control>();
    public override void _Process(double delta)
    {
        if (!_initialized) { _origin = _portrait.Position; _initialized = true; }
        if (!_portrait.IsVisibleInTree()) return;
        _time += (float)delta;
        _portrait.Position = _origin + new Vector2(0, 2f * Mathf.Sin(_time * Mathf.Tau / 10f));
    }
}
