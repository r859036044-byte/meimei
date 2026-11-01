using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 開發者模式管理器（Autoload 單例，僅供內部除錯使用，非玩家正式功能）。
///
/// 全域常駐監聽鍵盤輸入（掛在 autoload，不隨場景切換被清掉，任何畫面都有效）：
///   - 密技：打字 jitaiyanjiubu，或輸入 上上下下左右左右baba（Konami Code 變體）。
///     第一次命中會把 Unlocked 設為 true（此次執行期間永久解鎖）並打開開發者工具視窗。
///   - 解鎖後可以直接按 F9 開關開發者工具視窗，不用再重打一次密技。
///
/// Unlocked 跟工具視窗本身都只存在記憶體中，不會寫進存檔——
/// 所以「切換場景」不會關掉開發者模式，但只要整個遊戲關掉重開，就會自動恢復成未解鎖狀態。
///
/// 開發者工具視窗（DevToolPanel）用獨立的 CanvasLayer 蓋在最上層顯示，
/// 是一個可以自由拖動的浮動視窗，不會擋住底下遊戲畫面的其他操作。
/// </summary>
public partial class DevModeManager : Node
{
    public static DevModeManager Instance { get; private set; }

    /// <summary>是否已解鎖開發者模式（此次遊戲執行期間有效，重開遊戲會重置為 false）</summary>
    public bool Unlocked { get; private set; } = false;

    /// <summary>由 DevToolPanel 的「進入房間」按鈕設為 true，代表切場景到 TrainingRoom 之後，
    /// 要自動把 F1 表情測試 DEV 面板打開（不用切過去後還要再手動按一次 F1）。
    /// TrainingRoomRuntime.BuildExpressionSystem() 建立好面板後會檢查這個旗標並自動清掉。</summary>
    public bool PendingAutoOpenExpressionTester = false;

    CanvasLayer _layer;
    DevToolPanel _panel;

    // 密技一：直接打字 jitaiyanjiubu
    static readonly string[] SeqTyping = "jitaiyanjiubu".Select(c => c.ToString()).ToArray();

    // 密技二：上上下下左右左右baba
    static readonly string[] SeqKonami =
    {
        "up", "up", "down", "down", "left", "right", "left", "right", "b", "a", "b", "a",
    };

    static int MaxLen => Math.Max(SeqTyping.Length, SeqKonami.Length);
    readonly List<string> _buffer = new();

    public override void _Ready()
    {
        Instance = this;

        _layer = new CanvasLayer { Name = "DevModeLayer", Layer = 4096 };
        AddChild(_layer);

        // 就算遊戲邏輯之後有做暫停(SceneTree.Paused)，開發者工具的輸入監聽也不受影響
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        // 解鎖後可以直接按 F9 開關視窗
        if (Unlocked && key.Keycode == Key.F9)
        {
            TogglePanel();
            return;
        }

        string token = key.Keycode switch
        {
            Key.Up => "up",
            Key.Down => "down",
            Key.Left => "left",
            Key.Right => "right",
            >= Key.A and <= Key.Z => ((char)('a' + (key.Keycode - Key.A))).ToString(),
            _ => null,
        };
        if (token == null) return;

        _buffer.Add(token);
        while (_buffer.Count > MaxLen) _buffer.RemoveAt(0);

        if (EndsWith(SeqTyping) || EndsWith(SeqKonami))
        {
            _buffer.Clear();
            Unlocked = true;
            OpenPanel();
        }
    }

    bool EndsWith(string[] seq)
    {
        if (_buffer.Count < seq.Length) return false;
        int offset = _buffer.Count - seq.Length;
        for (int i = 0; i < seq.Length; i++)
            if (_buffer[offset + i] != seq[i]) return false;
        return true;
    }

    public void OpenPanel()
    {
        if (IsInstanceValid(_panel)) return; // 已經開著，不重複建立
        _panel = new DevToolPanel { Name = "DevToolPanel" };
        _layer.AddChild(_panel);
    }

    public void ClosePanel()
    {
        if (IsInstanceValid(_panel)) _panel.QueueFree();
        _panel = null;
    }

    public void TogglePanel()
    {
        if (IsInstanceValid(_panel)) ClosePanel();
        else OpenPanel();
    }
}
