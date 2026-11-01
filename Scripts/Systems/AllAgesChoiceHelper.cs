using Godot;
using System.Threading.Tasks;

/// <summary>
/// 全年齡模式輔助類別：在不進入 TrainingRoom 的情況下，
/// 顯示「蓋被子」選擇按鈕，並自動套用高潮路線的旗標。
/// </summary>
public static class AllAgesChoiceHelper
{
    /// <summary>
    /// 同步設定「全部走高潮路線」所需的旗標。
    /// 相當於玩家在 TrainingRoom 完成了插入且到達高潮的結果。
    /// </summary>
    public static void SetClimaxFlags()
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm == null)
        {
            GD.PrintErr("[AllAgesChoiceHelper] PlayerDataManager.Instance 為 null，無法設定旗標");
            return;
        }

        pdm.TodayDidNothing       = false;
        pdm.TodayInserted         = true;
        pdm.TodayInsertOrgasm     = true;
        pdm.MeimeiHasOrgasmed     = true;
        pdm.AddVaginaUse(1);
        TrainingRoomRuntime.HadBreastTouch     = true;
        TrainingRoomRuntime.BreastComfortRoute = true;

        GD.Print("[AllAgesChoiceHelper] 高潮路線旗標已設定（全年齡模式）");
    }

    /// <summary>
    /// 顯示半透明遮罩與「蓋被子」按鈕，等待玩家點擊後設定旗標並移除遮罩。
    /// </summary>
    public static async Task ShowBlanketChoiceAsync(Node scene)
    {
        // ── 建立遮罩層 ──────────────────────────────────────
        var overlay = new CanvasLayer { Layer = 100 };
        scene.AddChild(overlay);

        var bg = new ColorRect();
        bg.Color = new Color(0f, 0f, 0f, 0.7f);
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.MouseFilter = Control.MouseFilterEnum.Stop;
        overlay.AddChild(bg);

        // ── 建立「蓋被子」按鈕 ──────────────────────────────
        var btn = new Button
        {
            Text = "蓋被子",
            CustomMinimumSize = new Vector2(300, 80)
        };
        btn.SetAnchorsPreset(Control.LayoutPreset.Center);
        // LayoutPreset.Center 把 offset 歸零；手動偏移讓按鈕真正置中
        btn.Position = new Vector2(-150f, -40f);
        btn.AddThemeFontSizeOverride("font_size", 28);
        bg.AddChild(btn);

        // ── 等待玩家點擊 ────────────────────────────────────
        bool chosen = false;
        btn.Pressed += () => chosen = true;

        while (!chosen)
            await scene.ToSignal(scene.GetTree(), SceneTree.SignalName.ProcessFrame);

        // ── 設定旗標、清除遮罩 ──────────────────────────────
        SetClimaxFlags();
        overlay.QueueFree();
    }

    /// <summary>
    /// 顯示「蓋被子」選擇，完成後切換到指定場景。
    /// 提供給無法使用 await 的 sync lambda（如 skip 按鈕）透過 TaskLogger.Run 呼叫。
    /// </summary>
    public static async Task ShowBlanketChoiceAndLoadAsync(Node scene, string nextScene)
    {
        await ShowBlanketChoiceAsync(scene);
        scene.GetTree().ChangeSceneToFile(nextScene);
    }
}
