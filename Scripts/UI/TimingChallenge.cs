using Godot;
using System;
using System.Threading.Tasks;

/// <summary>非露骨第三天時機挑戰：15 回合累積三顆星，與原有路線結果分開。</summary>
public partial class TimingChallenge : CanvasLayer
{
    readonly TaskCompletionSource<bool> _result = new();
    Label _roundLabel, _starLabel, _feedback;
    ProgressBar _meter;
    Control _track;
    HeartDecoration _cursor;
    Button _action;
    int _round, _stars, _points;
    float _phase, _position, _cooldown;
    bool _running, _finished, _won;
    public static bool Eligible(int completedNight, int consecutiveIdle) => completedNight >= 2 && consecutiveIdle >= 2;
    public static int Gain(float position) => Mathf.Abs(position - 0.5f) <= 0.055f ? 50 : Mathf.Abs(position - 0.5f) <= 0.13f ? 35 : 0;
    public static async Task<bool> Play(Node owner)
    {
        NovelReader.ResetPlayback();
        var game = new TimingChallenge { Layer = 200, Name = "TimingChallenge" };
        owner.AddChild(game);
        try { return await game._result.Task; }
        finally { if (IsInstanceValid(game)) game.QueueFree(); }
    }
    public override void _ExitTree() => _result.TrySetCanceled();
    public override void _Ready()
    {
        var veil = new ColorRect { Color = new Color("fff4fa"), MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(veil); veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var card = new Panel { MouseFilter = Control.MouseFilterEnum.Stop };
        veil.AddChild(card);
        card.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        card.OffsetLeft = -450; card.OffsetRight = 450; card.OffsetTop = -325; card.OffsetBottom = 325;
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new Color("fffafd"), BorderColor = new Color("edb8cf"),
            BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusTopLeft = 32, CornerRadiusTopRight = 32, CornerRadiusBottomLeft = 32, CornerRadiusBottomRight = 32,
            ShadowColor = new Color(0.4f, 0.15f, 0.28f, 0.14f), ShadowSize = 12, ShadowOffset = new Vector2(0, 6)
        });
        Text(card, "第三天 · 心拍挑戰", 34, 38, "994b71");
        Text(card, "在愛心進入粉色區域時按下按鈕，15 回合內收集 3 顆星。", 22, 102, "956d81");
        _roundLabel = Text(card, "準備好了嗎？", 24, 164, "805168");
        _starLabel = Text(card, "☆  ☆  ☆", 36, 213, "d774a0");
        _meter = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = false, Position = new Vector2(100, 281), Size = new Vector2(700, 20) };
        card.AddChild(_meter);
        _meter.AddThemeStyleboxOverride("background", Round("f6e4ed", 10));
        _meter.AddThemeStyleboxOverride("fill", Round("eba1c2", 10));
        _track = new Panel { Position = new Vector2(100, 350), Size = new Vector2(700, 28), MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddChild(_track); ((Panel)_track).AddThemeStyleboxOverride("panel", Round("f4e6ef", 14));
        var zone = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        _track.AddChild(zone); zone.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        zone.AnchorLeft = 0.37f; zone.AnchorRight = 0.63f; zone.AddThemeStyleboxOverride("panel", Round("f2b2ce", 12));
        var perfect = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        _track.AddChild(perfect); perfect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        perfect.AnchorLeft = 0.445f; perfect.AnchorRight = 0.555f; perfect.AddThemeStyleboxOverride("panel", Round("e576a8", 10));
        _cursor = new HeartDecoration { Size = new Vector2(32, 32), Position = new Vector2(334, -2) }; _track.AddChild(_cursor);
        _feedback = Text(card, "深粉中心：+50　｜　淺粉區域：+35　｜　每 100 分獲得一顆星", 20, 410, "a16b85");
        _action = Button(card, "開始挑戰", new Vector2(285, 487), new Vector2(330, 64));
        _action.Pressed += Act;
        var leave = Button(card, "放棄挑戰", new Vector2(675, 565), new Vector2(165, 42));
        leave.AddThemeFontSizeOverride("font_size", 18);
        leave.Pressed += () => { if (_finished) _result.TrySetResult(_won); else Finish(false); };
        Text(card, "滑鼠點擊按鈕 / 空白鍵　·　每次按下只計算一回合", 17, 570, "b08b9d").Size = new Vector2(580, 38);
    }
    static StyleBoxFlat Round(string color, int radius) => new StyleBoxFlat { BgColor = new Color(color),
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };
    static Label Text(Control parent, string text, int size, float y, string color)
    {
        var label = new Label { Text = text, Position = new Vector2(30, y), Size = new Vector2(840, 46),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(color)); parent.AddChild(label); return label;
    }
    static Button Button(Control parent, string text, Vector2 position, Vector2 size)
    {
        var button = UIStyle.CreateStyledButton(text, 25); parent.AddChild(button);
        button.Position = position; button.CustomMinimumSize = size; button.Size = size; return button;
    }
    public override void _Process(double delta)
    {
        if (!_running || _finished) return;
        if (_cooldown > 0) { _cooldown -= (float)delta; return; }
        _phase += (float)delta * (2.3f + _stars * 0.25f);
        _position = 0.5f + 0.48f * Mathf.Sin(_phase);
        _cursor.Position = new Vector2(_position * _track.Size.X - 16, -2);
    }
    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is InputEventKey { Keycode: Key.Space, Pressed: true, Echo: false }) { Act(); GetViewport().SetInputAsHandled(); }
    }
    void Act()
    {
        if (_finished) { _result.TrySetResult(_won); return; }
        if (!_running) { _running = true; _position = 0.5f; _action.Text = "抓住時機！"; UpdateScore(); return; }
        if (_cooldown > 0) return;
        int gain = Gain(_position); _points += gain; _round++;
        if (_points >= 100) { _stars++; _points -= 100; }
        _feedback.Text = gain == 50 ? "完美命中！ +50" : gain == 35 ? "抓到節奏了！ +35" : "差一點點，再試一次！";
        _cooldown = 0.55f;
        UpdateScore();
        if (_stars >= 3 || _round >= 15) Finish(_stars >= 3);
    }
    void UpdateScore()
    {
        _roundLabel.Text = $"回合 {Math.Min(_round + 1, 15)} / 15";
        _starLabel.Text = new string('★', _stars) + new string('☆', 3 - _stars);
        _meter.Value = _points;
    }
    void Finish(bool won)
    {
        _finished = true; _running = false; _won = won;
        _roundLabel.Text = won ? "挑戰成功" : "挑戰結束";
        _feedback.Text = won ? "三顆星都收集到了，繼續接下來的故事吧。" : "這次還差一點，看看接下來的故事吧。";
        if (won) _meter.Value = 100;
        _action.Text = "繼續故事";
    }
}
