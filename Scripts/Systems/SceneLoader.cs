using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 切換到較重的場景（如 TrainingRoom）時使用：
/// 1) 先蓋上「載入中」畫面（掛在 root，不會隨場景切換消失）
/// 2) 在背景執行緒預載貼圖，畫面保持可動（點點動畫）
/// 3) 預載完成才切場景，新場景起來後再淡出載入畫面
/// 這樣不會長時間純黑屏，場景 _Ready 時的同步載入也大多命中快取。
/// </summary>
public static class SceneLoader
{
    const float TimeoutSec = 12f;

    public static async Task GoToAsync(Node from, string scenePath)
    {
        var tree = from.GetTree();
        var layer = BuildOverlay(tree, out var label, out var overlay); // overlay = 底色 Control，用它做淡入淡出

        // 淡入載入畫面
        for (float t = 0f; t < 0.25f; t += (float)tree.Root.GetProcessDeltaTime())
        {
            overlay.Modulate = new Color(1, 1, 1, t / 0.25f);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        overlay.Modulate = Colors.White;

        // 背景預載
        var pending = new List<string>();
        if (scenePath.EndsWith("TrainingRoom.tscn"))
        {
            foreach (var rel in TrainingRoomRuntime.GetPreloadSpritePaths())
            {
                string full = FindTexPath(rel);
                if (full != null && ResourceLoader.LoadThreadedRequest(full) == Error.Ok)
                    pending.Add(full);
            }
        }

        double elapsed = 0, dotT = 0; int dots = 0;
        while (pending.Count > 0 && elapsed < TimeoutSec)
        {
            pending.RemoveAll(p => ResourceLoader.LoadThreadedGetStatus(p) != ResourceLoader.ThreadLoadStatus.InProgress);
            double dt = tree.Root.GetProcessDeltaTime();
            elapsed += dt; dotT += dt;
            if (dotT > 0.4) { dotT = 0; dots = (dots + 1) % 4; label.Text = "載入中" + new string('.', dots); }
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        // 確保「載入中」至少畫出一幀再切換
        label.Text = "載入中...";
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        tree.ChangeSceneToFile(scenePath);

        // 新場景 _Ready 的同步建立會卡幾幀；等它穩定後再淡出
        for (int i = 0; i < 5; i++) await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        for (float t = 0f; t < 0.3f; t += (float)tree.Root.GetProcessDeltaTime())
        {
            if (!GodotObject.IsInstanceValid(layer)) return;
            overlay.Modulate = new Color(1, 1, 1, 1f - t / 0.3f);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        if (GodotObject.IsInstanceValid(layer)) layer.QueueFree();
    }

    static string FindTexPath(string rel)
    {
        string b = "res://Resources/" + rel;
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp" })
            if (ResourceLoader.Exists(b + ext)) return b + ext;
        return null;
    }

    static CanvasLayer BuildOverlay(SceneTree tree, out Label label, out ColorRect fade)
    {
        var layer = new CanvasLayer { Layer = 200 };
        var bg = new ColorRect { Color = new Color(0.05f, 0.03f, 0.06f, 1f), MouseFilter = Control.MouseFilterEnum.Stop };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(bg);
        fade = bg;
        label = new Label { Text = "載入中", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        label.AddThemeFontSizeOverride("font_size", 36);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.92f));
        bg.AddChild(label);
        bg.Modulate = new Color(1, 1, 1, 0);
        tree.Root.AddChild(layer);
        return layer;
    }
}
