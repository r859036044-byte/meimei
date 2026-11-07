using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 截圖功能（Autoload 單例，全遊戲任何畫面都能用）。
///   F12          把目前畫面存成 PNG（包含所有介面、對話框）
///   Shift + F12  存成「不含介面」的版本（只留角色和背景；介面／對話框／HUD 暫時藏起來再截）
///   Ctrl + F12   開啟存放截圖的資料夾
/// 存檔位置：user://screenshots/（檔名 meimei_年月日_時分秒.png，不含介面的會加 _noUI）。
/// 實際路徑（Windows 通常在 %APPDATA%\Godot\app_userdata\專案名稱\screenshots）會顯示在截圖後的提示裡。
/// </summary>
public partial class ScreenshotManager : Node
{
    const string Dir = "user://screenshots";
    const int UiLayerMin = 10;   // CanvasLayer 的 Layer ≥ 這個值就當成「介面」（調教室的 UI 層是 11，開發面板更高）

    CanvasLayer _toastLayer;
    PanelContainer _toastBox;
    Label _toastLabel;
    Tween _toastTween;
    bool _busy;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;   // 就算遊戲暫停也能截圖

        _toastLayer = new CanvasLayer { Name = "ScreenshotToastLayer", Layer = 4000 };
        AddChild(_toastLayer);
        _toastBox = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _toastBox.AnchorLeft = 0.5f; _toastBox.AnchorRight = 0.5f; _toastBox.AnchorTop = 0f; _toastBox.AnchorBottom = 0f;
        _toastBox.OffsetLeft = -380f; _toastBox.OffsetRight = 380f; _toastBox.OffsetTop = 18f; _toastBox.OffsetBottom = 70f;
        var sb = new StyleBoxFlat { BgColor = new Color(0.12f, 0.08f, 0.14f, 0.88f), BorderColor = new Color(1f, 0.75f, 0.88f) };
        sb.SetBorderWidthAll(2); sb.SetCornerRadiusAll(14); sb.SetContentMarginAll(12);
        _toastBox.AddThemeStyleboxOverride("panel", sb);
        _toastLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _toastLabel.AddThemeFontSizeOverride("font_size", 18);
        _toastLabel.AddThemeColorOverride("font_color", Colors.White);
        _toastBox.AddChild(_toastLabel);
        _toastLayer.AddChild(_toastBox);
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F12 } k)
        {
            if (k.CtrlPressed) OpenFolder();
            else _ = TakeScreenshotAsync(withoutUi: k.ShiftPressed);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>截圖。withoutUi＝true：先把介面層藏起來再截。</summary>
    public async System.Threading.Tasks.Task TakeScreenshotAsync(bool withoutUi = false)
    {
        if (_busy) return;
        _busy = true;
        var hidden = new List<CanvasLayer>();
        try
        {
            _toastLayer.Visible = false;   // 提示本身不要拍進去
            if (withoutUi)
                foreach (var cl in FindCanvasLayers(GetTree().Root))
                    if (cl != _toastLayer && cl.Layer >= UiLayerMin && cl.Visible) { cl.Visible = false; hidden.Add(cl); }

            // 等畫面真的畫完（藏起來的介面不會出現在這一幀）
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            var img = GetViewport().GetTexture().GetImage();
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Dir));
            string file = $"meimei_{DateTime.Now:yyyyMMdd_HHmmss}{(withoutUi ? "_noUI" : "")}.png";
            string path = $"{Dir}/{file}";
            var err = img.SavePng(path);

            foreach (var cl in hidden) if (IsInstanceValid(cl)) cl.Visible = true;
            hidden.Clear();
            _toastLayer.Visible = true;

            if (err == Error.Ok) Toast($"📷 截圖已儲存（{(withoutUi ? "不含介面" : "含介面")}）：{file}\n資料夾：{ProjectSettings.GlobalizePath(Dir)}\n（Ctrl＋F12 開啟資料夾）");
            else Toast($"截圖失敗（錯誤碼 {err}）");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Screenshot] 失敗：{ex}");
            Toast("截圖失敗，詳細原因看輸出面板");
        }
        finally
        {
            foreach (var cl in hidden) if (IsInstanceValid(cl)) cl.Visible = true;   // 不管怎樣都把介面還回來
            if (IsInstanceValid(_toastLayer)) _toastLayer.Visible = true;
            _busy = false;
        }
    }

    void OpenFolder()
    {
        string abs = ProjectSettings.GlobalizePath(Dir);
        DirAccess.MakeDirRecursiveAbsolute(abs);
        OS.ShellShowInFileManager(abs);
    }

    void Toast(string text)
    {
        _toastLabel.Text = text;
        _toastBox.Visible = true;
        _toastBox.Modulate = Colors.White;
        _toastBox.OffsetBottom = 18f + 28f + _toastLabel.GetLineCount() * 26f;
        _toastTween?.Kill();
        _toastTween = CreateTween();
        _toastTween.TweenInterval(3.0);
        _toastTween.TweenProperty(_toastBox, "modulate:a", 0f, 0.5);
        _toastTween.TweenCallback(Callable.From(() => _toastBox.Visible = false));
    }

    static IEnumerable<CanvasLayer> FindCanvasLayers(Node root)
    {
        foreach (var c in root.GetChildren())
        {
            if (c is CanvasLayer cl) yield return cl;
            foreach (var d in FindCanvasLayers(c)) yield return d;
        }
    }
}
