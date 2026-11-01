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
        b.Visible = MiniGameEntryVisible;
        b.Pressed += () => TaskLogger.Run(StartMiniGameFromMenu(), "MainMenu.MiniGame");
        _btnMiniGame = b;
    }

    async Task StartMiniGameFromMenu()
    {
        if (_miniGameRunning) return;
        _miniGameRunning = true;
        SetProcessInput(false);                 // 避免主選單 _Input 吃到小遊戲的點擊
        _mainPanel.Visible = false;
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
            _mainPanel.Visible = true;
            SetProcessInput(true);
            _miniGameRunning = false;
        }
    }
}
