using Godot;
using System.Threading.Tasks;

/// <summary>
/// 場景開始時從黑色淡入（Unity FadeInOnStart 的 Godot 移植版）
/// 掛在全螢幕 ColorRect 上，_Ready 時自動執行淡入並移除自身。
/// </summary>
public partial class FadeInOnStart : ColorRect
{
    [Export] public float FadeDuration = 1f;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        Color = new Color(0, 0, 0, 1);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 100;
        _ = DoFadeIn();
    }

    private async Task DoFadeIn()
    {
        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            elapsed += (float)GetProcessDeltaTime();
            float a = 1f - Mathf.Clamp(elapsed / FadeDuration, 0f, 1f);
            Color = new Color(0, 0, 0, a);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Color = new Color(0, 0, 0, 0);
        QueueFree();
    }
}
