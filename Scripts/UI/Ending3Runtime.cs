using Godot;
using System.Threading.Tasks;

/// <summary>
/// Ending3 場景：第一晚摸了、第二晚沒做 → 走廊門未鎖
/// 在走廊選擇點出現二選一：
///   「進去看看」→ 進入第三晚 TrainingRoom（PendingNight3）
///   「回去房間睡覺」→ 繼續播放「恰如其分的距離」結局獨白
/// </summary>
public partial class Ending3Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/Day3(normal end - distance).json";

    protected override string PlayedKey => "Day3_NormalEnd_Distance";

    // 對應圖鑒 E6（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 6;

    // ── 選擇 UI Hook ──────────────────────────────────────────────
    protected override async Task<bool> HandleChoiceAsync(string choiceId)
    {
        if (choiceId != "door_open_choice") return false;

        bool chose = false;        // false = 回去睡覺 (default)
        bool decided = false;

        // 建立選擇 UI
        var canvas = GetNode<CanvasLayer>("CanvasLayer");  // base class 建立的 _canvas
        // 找到 _canvas（CanvasLayer, Layer=100）
        CanvasLayer cl = null;
        foreach (Node child in GetChildren())
            if (child is CanvasLayer ca) { cl = ca; break; }
        if (cl == null) { GD.PushWarning("[Ending3] 找不到 CanvasLayer"); return false; }

        var choiceContainer = new Control { Name = "ChoiceContainer" };
        choiceContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        choiceContainer.MouseFilter = Control.MouseFilterEnum.Stop;
        cl.AddChild(choiceContainer);

        // 暗色半透明遮罩
        var dimRect = new ColorRect { Color = new Color(0, 0, 0, 0.45f) };
        dimRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dimRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        choiceContainer.AddChild(dimRect);

        // 選擇框
        var vbox = new VBoxContainer { Name = "ChoiceBox" };
        vbox.AddThemeConstantOverride("separation", 16);
        vbox.AnchorLeft   = 0.5f; vbox.AnchorRight  = 0.5f;
        vbox.AnchorTop    = 0.5f; vbox.AnchorBottom = 0.5f;
        vbox.OffsetLeft   = -240f; vbox.OffsetRight  = 240f;
        vbox.OffsetTop    = -70f;  vbox.OffsetBottom = 70f;
        choiceContainer.AddChild(vbox);

        // 提示標籤
        var hint = new Label
        {
            Text = "……",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        hint.AddThemeFontSizeOverride("font_size", 22);
        hint.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 1f));
        vbox.AddChild(hint);

        // 選項按鈕
        Button MkChoiceBtn(string label)
        {
            var btn = new Button { Text = label };
            btn.CustomMinimumSize = new Vector2(480, 54);
            btn.AddThemeFontSizeOverride("font_size", 24);
            var sb = new StyleBoxFlat();
            sb.BgColor = new Color(0.12f, 0.06f, 0.18f, 0.92f);
            sb.BorderColor = new Color(0.75f, 0.45f, 0.65f, 0.9f);
            sb.BorderWidthTop = sb.BorderWidthBottom = sb.BorderWidthLeft = sb.BorderWidthRight = 2;
            sb.CornerRadiusTopLeft = sb.CornerRadiusTopRight =
            sb.CornerRadiusBottomLeft = sb.CornerRadiusBottomRight = 6;
            btn.AddThemeStyleboxOverride("normal", sb);
            var sbH = sb.Duplicate() as StyleBoxFlat;
            sbH.BgColor = new Color(0.22f, 0.10f, 0.30f, 0.98f);
            btn.AddThemeStyleboxOverride("hover", sbH);
            btn.AddThemeStyleboxOverride("pressed", sbH);
            btn.AddThemeColorOverride("font_color", Colors.White);
            return btn;
        }

        var btnEnter = MkChoiceBtn("進去看看");
        var btnSleep = MkChoiceBtn("回去房間睡覺");
        vbox.AddChild(btnEnter);
        vbox.AddChild(btnSleep);

        btnEnter.Pressed += () => { chose = true;  decided = true; };
        btnSleep.Pressed += () => { chose = false; decided = true; };

        // 等待玩家選擇
        while (!decided)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        choiceContainer.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (chose)
        {
            // 「進去看看」→ 設定第三晚旗標，跳轉 TrainingRoom
            TrainingRoomRuntime.PendingNight3 = true;
            GetTree().ChangeSceneToFile("res://Scenes/TrainingRoom.tscn");
            return true;  // 中止剩餘對話
        }

        // 「回去房間睡覺」→ 繼續播放後續獨白
        return false;
    }
}
