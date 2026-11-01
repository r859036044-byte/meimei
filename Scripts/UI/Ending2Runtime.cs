using Godot;
using System.Threading.Tasks;

/// <summary>
/// Ending2 場景：Day4(bad end) / NothingRoute 小遊戲
/// 若 UseNothingRoute 為 true，則先執行互動小遊戲再播放對話。
/// </summary>
public partial class Ending2Runtime : EndingBaseRuntime
{
    // ── NothingRoute 靜態旗標 ────────────────────────────────────────
    public static bool UseNothingRoute { get; set; }

    // ── 實例旗標 ─────────────────────────────────────────────────────
    bool _isNothingRoute;

    // ── 抽象屬性覆寫 ────────────────────────────────────────────────
    protected override string DialoguePath => _isNothingRoute
        ? "res://Resources/Data/Dialogues/NothingRoute/Ending3_SecretGame.json"
        : "res://Resources/Data/Dialogues/Endings/Day4(bad end).json";

    protected override string PlayedKey => "Day4_bad_end";

    // 對應圖鑒 E3（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 3;

    // ── 小遊戲狀態 ──────────────────────────────────────────────────
    bool          _minigameActive;
    CanvasLayer   _mgCanvas;          // 小遊戲專用的 CanvasLayer（Layer=101）
    Control       _minigameRoot;
    TextureRect   _jijiLayer;         // 可移動的雞雞圖層
    float         _jijiMGY          = -300f;
    const float   _jijiMGMinY       = -450f;
    const float   _jijiMGMaxY       =  250f;
    const float   _jijiMGSens       =  1.0f;
    const float   _jijiMGOffX       =  0f;
    readonly Vector2 _gPointCenter  = new(0f,  -30f);
    readonly Vector2 _gPointHalf    = new(50f,  40f);
    readonly Vector2 _uterusCenter  = new(0f,  170f);
    readonly Vector2 _uterusHalf    = new(60f,  50f);
    bool          _gPointHit;
    bool          _uterusHit;
    bool          _dev2Visible;
    ColorRect     _dbgGPoint;
    ColorRect     _dbgUterus;
    ColorRect     _dbgJijiTip;
    float         _mgMouseDeltaY;    // 累積自 _Input 的滑鼠 Y delta

    // ────────────────────────────────────────────────────────────────
    //  OnBeforeDialogueAsync：初始化 NothingRoute 旗標，若需要先跑小遊戲
    //  此方法在 BuildUI() 之前被呼叫（透過 StartAsync → OnBefore → PlayDialogue）
    // ────────────────────────────────────────────────────────────────
    protected override async Task OnBeforeDialogueAsync()
    {
        _isNothingRoute  = UseNothingRoute;
        UseNothingRoute  = false;   // 用完即重設

        if (_isNothingRoute)
            await RunMinigameAsync();
    }

    // ────────────────────────────────────────────────────────────────
    //  _Process 覆寫：在小遊戲期間呼叫 UpdateMinigame
    // ────────────────────────────────────────────────────────────────
    public override void _Process(double delta)
    {
        base._Process(delta);           // 基底類別點擊 / skipAll 邏輯

        if (_minigameActive)
        {
            UpdateMinigame();
            _mgMouseDeltaY = 0f;        // 每幀清零，避免累積
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  _Input：收集滑鼠移動量（供 UpdateMinigame 使用）
    // ────────────────────────────────────────────────────────────────
    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventMouseMotion mm)
            _mgMouseDeltaY += mm.Relative.Y;
    }

    // ════════════════════════════════════════════════════════════════
    //  RunMinigameAsync
    // ════════════════════════════════════════════════════════════════
    async Task RunMinigameAsync()
    {
        // 小遊戲使用獨立的 CanvasLayer（Layer=101 > 對話的 100）
        _mgCanvas = new CanvasLayer { Layer = 101, Name = "MG_Canvas" };
        AddChild(_mgCanvas);

        BuildMinigameLayers();
        BuildDev2Panel();

        _minigameActive = true;

        // ── 「繼續」按鈕 ─────────────────────────────────────────
        bool done = false;

        var btn = new Button { Name = "MG_ContinueBtn" };
        btn.Text = "▶ 繼續";
        var btnSty = new StyleBoxFlat { BgColor = new Color(0.15f, 0.55f, 0.15f, 0.92f) };
        btn.AddThemeStyleboxOverride("normal",  btnSty);
        btn.AddThemeStyleboxOverride("hover",   new StyleBoxFlat { BgColor = new Color(0.2f, 0.7f, 0.2f, 0.95f) });
        btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color(0.1f, 0.45f, 0.1f, 1f) });
        btn.AddThemeFontSizeOverride("font_size", 22);
        btn.AnchorLeft   = 0.5f; btn.AnchorRight  = 0.5f;
        btn.AnchorTop    = 1f;   btn.AnchorBottom = 1f;
        btn.OffsetLeft   = -100f; btn.OffsetRight  =  100f;
        btn.OffsetTop    = -80f;  btn.OffsetBottom =  -30f;
        _minigameRoot.AddChild(btn);

        btn.Pressed += () => done = true;

        // 等待玩家點擊繼續
        while (!done)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        _minigameActive = false;

        // 清除小遊戲 Canvas
        if (_mgCanvas != null)
        {
            _mgCanvas.QueueFree();
            _mgCanvas = null;
        }
        _minigameRoot = null;
        _jijiLayer    = null;
    }

    // ════════════════════════════════════════════════════════════════
    //  BuildMinigameLayers
    //  圖層順序（由下到上）：底 → 雞雞 → 子宮 → 外框
    //  Resources 路徑：
    //    MG/mg_bottom  → 底層（陰道內部背景）
    //    MG/mg_jiji    → 雞雞（可移動層）
    //    MG/mg_uterus  → 子宮（判定位置）
    //    MG/mg_frame   → 外框（最上層）
    // ════════════════════════════════════════════════════════════════
    void BuildMinigameLayers()
    {
        // 根容器：填滿 Canvas
        _minigameRoot = new Control { Name = "MinigameRoot" };
        _minigameRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _mgCanvas.AddChild(_minigameRoot);

        // 4 個圖層（由下到上）
        MakeMGLayer("MG_底",   "MG/mg_bottom", Vector2.Zero,
                    out _);
        MakeMGLayer("MG_雞雞", "MG/mg_jiji",
                    new Vector2(_jijiMGOffX, -_jijiMGY),   // Godot Y 軸翻轉
                    out var jijiTR);
        _jijiLayer = jijiTR;
        MakeMGLayer("MG_子宮", "MG/mg_uterus", Vector2.Zero, out _);
        MakeMGLayer("MG_外框", "MG/mg_frame",  Vector2.Zero, out _);

        // 判定色塊（初始隱藏，DEV2 開關控制）
        // 注意：中心座標需 Godot Y 翻轉（Y → -Y）
        _dbgGPoint  = MakeMGColorBlock("DBG_GPoint",
            new Vector2(_gPointCenter.X, -_gPointCenter.Y),
            _gPointHalf * 2f,
            new Color(0f, 1f, 0.2f, 0.40f));
        _dbgUterus  = MakeMGColorBlock("DBG_Uterus",
            new Vector2(_uterusCenter.X, -_uterusCenter.Y),
            _uterusHalf * 2f,
            new Color(1f, 0.1f, 0.1f, 0.40f));
        _dbgJijiTip = MakeMGColorBlock("DBG_JijiTip",
            Vector2.Zero, new Vector2(20f, 8f),
            new Color(1f, 1f, 0f, 0.55f));

        _dbgGPoint.Visible  = false;
        _dbgUterus.Visible  = false;
        _dbgJijiTip.Visible = false;
    }

    // ─── 輔助：建立一個以中心錨點定位的 TextureRect 圖層 ────────────
    void MakeMGLayer(string name, string resPath, Vector2 centerPos,
                     out TextureRect tr)
    {
        tr             = new TextureRect { Name = name };
        tr.StretchMode = TextureRect.StretchModeEnum.KeepCentered;
        tr.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        tr.MouseFilter = Control.MouseFilterEnum.Ignore;

        var tex = ResourceLoader.Load<Texture2D>(
            "res://Resources/" + resPath + ".png");
        if (tex != null)
        {
            tr.Texture = tex;
            float hw = tex.GetWidth()  / 2f;
            float hh = tex.GetHeight() / 2f;
            tr.AnchorLeft   = 0.5f; tr.AnchorRight  = 0.5f;
            tr.AnchorTop    = 0.5f; tr.AnchorBottom = 0.5f;
            tr.OffsetLeft   = -hw + centerPos.X;
            tr.OffsetRight  =  hw + centerPos.X;
            tr.OffsetTop    = -hh + centerPos.Y;
            tr.OffsetBottom =  hh + centerPos.Y;
        }
        else
        {
            // 找不到圖片：顯示灰色佔位區塊
            GD.PushWarning(
                $"[Ending3] 找不到小遊戲圖片：{resPath}  " +
                $"請在 res://Resources/{resPath}.png 放入圖片。");
            tr.AnchorLeft   = 0.5f; tr.AnchorRight  = 0.5f;
            tr.AnchorTop    = 0.5f; tr.AnchorBottom = 0.5f;
            tr.OffsetLeft   = -200f + centerPos.X;
            tr.OffsetRight  =  200f + centerPos.X;
            tr.OffsetTop    = -300f + centerPos.Y;
            tr.OffsetBottom =  300f + centerPos.Y;
            tr.Modulate     = new Color(0.4f, 0.4f, 0.4f, 0.25f);
        }

        _minigameRoot.AddChild(tr);
    }

    // ─── 輔助：建立判定用的彩色矩形色塊 ────────────────────────────
    ColorRect MakeMGColorBlock(string name, Vector2 centerPos, Vector2 size, Color col)
    {
        var cr = new ColorRect { Name = name, Color = col };
        cr.MouseFilter  = Control.MouseFilterEnum.Ignore;
        cr.AnchorLeft   = 0.5f; cr.AnchorRight  = 0.5f;
        cr.AnchorTop    = 0.5f; cr.AnchorBottom = 0.5f;
        cr.OffsetLeft   = -size.X / 2f + centerPos.X;
        cr.OffsetRight  =  size.X / 2f + centerPos.X;
        cr.OffsetTop    = -size.Y / 2f + centerPos.Y;
        cr.OffsetBottom =  size.Y / 2f + centerPos.Y;
        _minigameRoot.AddChild(cr);
        return cr;
    }

    // ════════════════════════════════════════════════════════════════
    //  BuildDev2Panel：右上角開發者面板
    // ════════════════════════════════════════════════════════════════
    void BuildDev2Panel()
    {
        // DEV2 面板背景（Panel + StyleBox）
        var panel = new Panel { Name = "DEV2Panel" };
        panel.AnchorLeft   = 1f; panel.AnchorRight  = 1f;
        panel.AnchorTop    = 0f; panel.AnchorBottom = 0f;
        panel.OffsetLeft   = -180f; panel.OffsetRight  = -10f;
        panel.OffsetTop    =   10f; panel.OffsetBottom = 105f;
        panel.MouseFilter  = Control.MouseFilterEnum.Ignore;
        var sty = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.72f) };
        panel.AddThemeStyleboxOverride("panel", sty);
        _mgCanvas.AddChild(panel);

        // 標題
        MgAddLabel("DEV2", panel, new Vector2(0f, -30f), 13,
            new Color(0.4f, 1f, 0.8f));

        // 色塊開關按鈕
        var togBtn = new Button { Name = "Btn_ToggleDev2", Text = "色塊提示: OFF" };
        togBtn.AddThemeFontSizeOverride("font_size", 12);
        var togNormal = new StyleBoxFlat { BgColor = new Color(0.15f, 0.55f, 0.15f, 1f) };
        togBtn.AddThemeStyleboxOverride("normal",  togNormal);
        togBtn.AddThemeStyleboxOverride("hover",   new StyleBoxFlat { BgColor = new Color(0.2f, 0.65f, 0.2f, 1f) });
        togBtn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat { BgColor = new Color(0.1f, 0.4f, 0.1f, 1f) });
        togBtn.AnchorLeft   = 0.5f; togBtn.AnchorRight  = 0.5f;
        togBtn.AnchorTop    = 0.5f; togBtn.AnchorBottom = 0.5f;
        togBtn.OffsetLeft   = -74f; togBtn.OffsetRight  =  74f;
        togBtn.OffsetTop    = -15f; togBtn.OffsetBottom =  15f;
        panel.AddChild(togBtn);

        togBtn.Pressed += () =>
        {
            _dev2Visible = !_dev2Visible;
            if (_dbgGPoint  != null) _dbgGPoint.Visible  = _dev2Visible;
            if (_dbgUterus  != null) _dbgUterus.Visible  = _dev2Visible;
            if (_dbgJijiTip != null) _dbgJijiTip.Visible = _dev2Visible;
            togBtn.Text = _dev2Visible ? "色塊提示: ON" : "色塊提示: OFF";
            var newSty = new StyleBoxFlat
            {
                BgColor = _dev2Visible
                    ? new Color(0.15f, 0.80f, 0.15f, 1f)
                    : new Color(0.55f, 0.15f, 0.15f, 1f)
            };
            togBtn.AddThemeStyleboxOverride("normal", newSty);
        };

        // 判定狀態提示文字
        MgAddLabel("G點 ■  子宮 ■  雞雞頂 ■", panel, new Vector2(0f, 30f), 10,
            Colors.Gray);
    }

    // ─── 輔助：在 Control 上加一個純文字 Label ──────────────────────
    static void MgAddLabel(string text, Control parent, Vector2 centerOffset,
                           int fontSize, Color col)
    {
        var lbl = new Label
        {
            Name                = "MgLbl",
            Text                = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            MouseFilter         = Control.MouseFilterEnum.Ignore,
        };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", col);
        lbl.AnchorLeft   = 0.5f; lbl.AnchorRight  = 0.5f;
        lbl.AnchorTop    = 0.5f; lbl.AnchorBottom = 0.5f;
        float hw = 82.5f, hh = 13f;
        lbl.OffsetLeft   = -hw + centerOffset.X;
        lbl.OffsetRight  =  hw + centerOffset.X;
        lbl.OffsetTop    = -hh + centerOffset.Y;
        lbl.OffsetBottom =  hh + centerOffset.Y;
        parent.AddChild(lbl);
    }

    // ════════════════════════════════════════════════════════════════
    //  UpdateMinigame：每幀更新雞雞位置與判定（由 _Process 呼叫）
    // ════════════════════════════════════════════════════════════════
    void UpdateMinigame()
    {
        if (_jijiLayer == null) return;

        // Godot 滑鼠向上 → Relative.Y 為負；Unity 向上 → y 為正
        // 轉換：dy(Unity) = -_mgMouseDeltaY
        float dy = -_mgMouseDeltaY * _jijiMGSens;
        _jijiMGY = Mathf.Clamp(_jijiMGY + dy, _jijiMGMinY, _jijiMGMaxY);

        // Godot Y 軸翻轉後更新雞雞層位置
        float godotY = -_jijiMGY;
        float texW   = _jijiLayer.Texture != null ? _jijiLayer.Texture.GetWidth()  : 400f;
        float texH   = _jijiLayer.Texture != null ? _jijiLayer.Texture.GetHeight() : 600f;
        float hw     = texW / 2f;
        float hh     = texH / 2f;
        _jijiLayer.OffsetLeft   = -hw + _jijiMGOffX;
        _jijiLayer.OffsetRight  =  hw + _jijiMGOffX;
        _jijiLayer.OffsetTop    = -hh + godotY;
        _jijiLayer.OffsetBottom =  hh + godotY;

        // 雞雞頂端在 Unity 座標（Y+ = 上）
        float tipY = _jijiMGY + texH * 0.5f;
        var   tip  = new Vector2(_jijiMGOffX, tipY);

        _gPointHit = InMGRect(tip, _gPointCenter, _gPointHalf);
        _uterusHit = InMGRect(tip, _uterusCenter, _uterusHalf);

        // DEV2 雞雞頂端指示器跟隨移動
        if (_dev2Visible && _dbgJijiTip != null)
        {
            float tipGodotY = -tipY;
            float dw = 10f, dh = 4f;
            _dbgJijiTip.OffsetLeft   = -dw + _jijiMGOffX;
            _dbgJijiTip.OffsetRight  =  dw + _jijiMGOffX;
            _dbgJijiTip.OffsetTop    = -dh + tipGodotY;
            _dbgJijiTip.OffsetBottom =  dh + tipGodotY;
            _dbgJijiTip.Color = _uterusHit
                ? new Color(1f, 0.1f, 0.1f, 0.8f)
                : _gPointHit
                    ? new Color(1f, 0.6f, 0f, 0.8f)
                    : new Color(1f, 1f, 0f, 0.55f);
        }
    }

    // ─── 輔助：矩形碰撞判定（Unity 座標空間）────────────────────────
    static bool InMGRect(Vector2 pt, Vector2 center, Vector2 half)
        => Mathf.Abs(pt.X - center.X) <= half.X
        && Mathf.Abs(pt.Y - center.Y) <= half.Y;
}
