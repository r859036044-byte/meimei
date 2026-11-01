using Godot;

/// <summary>將操作音效與彈性互動套用到各場景的文字按鈕。</summary>
public partial class CuteUIEffects : Node
{
    public override void _Ready()
    {
        GetTree().NodeAdded += OnNodeAdded;
        Visit(GetTree().Root);
    }
    public override void _ExitTree() => GetTree().NodeAdded -= OnNodeAdded;
    void Visit(Node node)
    {
        OnNodeAdded(node);
        foreach (Node child in node.GetChildren()) Visit(child);
    }
    void OnNodeAdded(Node node)
    {
        if (node is Control portrait && (portrait.Name == "CharRoot" || portrait.Name == "CharacterRoot" || portrait.Name == "PortraitFloatRoot"))
        {
            bool hasExistingBreathing = false;
            for (Node ancestor = portrait.GetParent(); ancestor != null; ancestor = ancestor.GetParent())
                if (ancestor is TrainingRoomRuntime) hasExistingBreathing = true;
            if (!hasExistingBreathing) Callable.From(() => {
                if (IsInstanceValid(portrait) && portrait.IsInsideTree()) PortraitFloat.Attach(portrait);
            }).CallDeferred();
        }
        if (node is not Button button) return;
        Callable.From(() => {
            if (!IsInstanceValid(button) || !button.IsInsideTree()) return;
            var label = button.GetNodeOrNull<Label>("T") ?? button.GetNodeOrNull<Label>("Text");
            bool labeled = !string.IsNullOrEmpty(button.Text) || label != null || button.Name == "Hit";
            if (labeled) CuteButtonMotion.Attach(button);
            if (labeled && button.Name != "Hit" && button is not CheckBox && button is not CheckButton)
            {
                if (!button.HasMeta("preserve_button_style")) UIStyle.ApplyButtonStyle(button);
                if (label != null)
                {
                    label.AddThemeColorOverride("font_color", button.Disabled ? new Color("978993") : UIStyle.BtnTextColor);
                    label.AddThemeConstantOverride("outline_size", 0);
                }
                var background = button.GetNodeOrNull<Control>("BG");
                if (label != null && background != null) background.Hide();
            }
        }).CallDeferred();
    }
}
