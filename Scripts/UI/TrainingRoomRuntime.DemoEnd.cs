using Godot;

/// <summary>
/// 試玩版結束畫面：射精的瞬間（MaleEjaculate／TriggerEjaculation 在 DemoMode 開著時）呼叫 ShowDemoEnd()，
/// 畫面快速轉成全黑，顯示「試玩版已結束」。蓋在所有 UI 上面、擋住滑鼠，所以不能再操作遊戲；
/// 過一會兒出現「返回標題」按鈕，回到主選單。完整版（DemoMode 關閉）不會用到。
/// </summary>
public partial class TrainingRoomRuntime
{
    [Export] public string DemoEndTitle    = "試玩版已結束";
    [Export] public string DemoEndSubtitle = "感謝你的遊玩";
    [Export] public float  DemoEndFadeSec  = 0.25f;   // 轉黑的時間（越小越「瞬間」）
    [Export] public float  DemoEndButtonDelay = 1.5f; // 多久之後才出現「返回標題」按鈕

    bool _demoEnded;

    void ShowDemoEnd()
    {
        if (_demoEnded) return;
        _demoEnded = true;
        GD.Print("[Demo] 射精 → 試玩版結束");

        var layer = new CanvasLayer { Name = "DemoEndLayer", Layer = 200 };
        AddChild(layer);
        var bg = new ColorRect { Name = "DemoEndBlack", Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Stop };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(bg);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.AddChild(center);
        var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 20);
        center.AddChild(col);

        var title = new Label { Text = DemoEndTitle, HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1f, 1f, 1f, 0f) };
        title.AddThemeFontSizeOverride("font_size", 56);
        title.AddThemeColorOverride("font_color", Colors.White);
        col.AddChild(title);
        var sub = new Label { Text = DemoEndSubtitle, HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1f, 1f, 1f, 0f) };
        sub.AddThemeFontSizeOverride("font_size", 24);
        sub.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.85f));
        col.AddChild(sub);
        var back = new Button { Text = "返回標題", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(200f, 52f), Visible = false };
        back.AddThemeFontSizeOverride("font_size", 22);
        back.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
        col.AddChild(back);

        var tw = CreateTween().SetParallel(true);
        tw.TweenProperty(bg, "color:a", 1f, Mathf.Max(0.01f, DemoEndFadeSec));
        tw.TweenProperty(title, "modulate:a", 1f, 0.5f).SetDelay(DemoEndFadeSec);
        tw.TweenProperty(sub, "modulate:a", 1f, 0.5f).SetDelay(DemoEndFadeSec + 0.2f);
        GetTree().CreateTimer(DemoEndButtonDelay).Timeout += () => { if (IsInstanceValid(back)) back.Visible = true; };
    }
}
