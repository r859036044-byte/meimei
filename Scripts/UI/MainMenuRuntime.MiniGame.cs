using Godot;
using System.Threading.Tasks;

/// <summary>主選單左下角「小遊戲」入口（搜尋：BuildMiniGameEntry）。</summary>
public partial class MainMenuRuntime : Control
{
    [ExportGroup("小遊戲入口")]
    [Export] public bool   MiniGameEntryVisible = true;
    [Export] public string MiniGameEntryText    = "小遊戲";
    [Export] public Vector2 MiniGameEntrySize   = new Vector2(220, 64);
    [Export] public Vector2 MiniGameEntryMargin = new Vector2(24, 24);   // 離左、下邊距

    Button _btnMiniGame;
    bool   _miniGameRunning;

    void BuildMiniGameEntry(Control container)
    {
        var b = UIStyle.CreateStyledButton(MiniGameEntryText, 26);
        b.Name = "BtnMiniGame";
        container.AddChild(b);
        b.AnchorLeft = 0f; b.AnchorRight = 0f; b.AnchorTop = 1f; b.AnchorBottom = 1f;
        b.OffsetLeft   = MiniGameEntryMargin.X;
        b.OffsetRight  = MiniGameEntryMargin.X + MiniGameEntrySize.X;
        b.OffsetBottom = -MiniGameEntryMargin.Y;
        b.OffsetTop    = -MiniGameEntryMargin.Y - MiniGameEntrySize.Y;
        b.Pressed += () => TaskLogger.Run(StartMiniGameFromMenu(), "MainMenu.MiniGame");
        _btnMiniGame = b;

        // 全年齡開啟 → 小遊戲按鈕隱藏；全年齡關閉（R18）→ 顯示。設定裡切換時即時更新
        RefreshMiniGameEntry();
        var pdm = PlayerDataManager.Instance;
        if (pdm != null)
        {
            System.Action<bool> onToggled = _ => RefreshMiniGameEntry();
            pdm.OnR18Toggled += onToggled;
            b.TreeExiting += () => pdm.OnR18Toggled -= onToggled;
        }
    }

    void RefreshMiniGameEntry()
    {
        if (_btnMiniGame == null || !IsInstanceValid(_btnMiniGame)) return;
        bool allAges = !(PlayerDataManager.Instance?.R18ContentEnabled ?? false);
        _btnMiniGame.Visible = MiniGameEntryVisible && !allAges;
    }

    async Task StartMiniGameFromMenu()
    {
        if (_miniGameRunning) return;
        _miniGameRunning = true;
        SetProcessInput(false);                 // 避免主選單 _Input 吃到小遊戲的點擊
        _mainPanel.Visible = false;
        bool wasBgmPlaying = _bgmSource != null && IsInstanceValid(_bgmSource) && _bgmSource.Playing;
        if (wasBgmPlaying) _bgmSource.StreamPaused = true;
        try
        {
            var r = await MiniGameNight3.Play(this);
            // ★ 劇情銜接點：r.Won（是否達成 3 次高潮）、r.Orgasms、r.Rounds
            //   之後在這裡依結果播對話 / 切場景。目前直接回主選單。
            GD.Print($"[MiniGame] won={r.Won} orgasms={r.Orgasms} rounds={r.Rounds}");
        }
        catch (TaskCanceledException) { }
        finally
        {
            if (wasBgmPlaying && _bgmSource != null && IsInstanceValid(_bgmSource))
                _bgmSource.StreamPaused = false;
            _mainPanel.Visible = true;
            SetProcessInput(true);
            _miniGameRunning = false;
        }
    }
}
