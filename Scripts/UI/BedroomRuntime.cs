using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 寢室場景 — 分層立繪顯示系統
/// Godot 4 C# port of Unity BedroomRuntime.cs
/// </summary>
public partial class BedroomRuntime : Node
{
    // ── 圖層順序（底→頂）─────────────────────────────────────────
    static readonly string[] LayerOrder = new[]
    {
        "後髮1", "後髮2", "後髮3",
        "身體",
        "腿/腿1", "腿/腿2", "腿/腳1", "腿/腳2", "腿/足1", "腿/足2",
        "m腿/腿1","m腿/腿2","m腿/腳1","m腿/腳2","m腿/足1","m腿/足2",
        "男人腿1","男人腿2","男人肚子",
        "臉的陰影","臉",
        "表情/眼白","表情/眼白2",
        "表情/眼球1","表情/眼球2",
        "表情/眼皮","表情/眼皮2",
        "表情/睫毛1","表情/睫毛2",
        "表情/下睫毛1","表情/下睫毛2",
        "眉毛1","眉毛2",
        "表情/鼻子","表情/嘴巴","表情/腮紅",
        "胸1","胸2","奶頭1","奶頭2",
        "手臂1","手臂2","手1","手2",
        "前髮",
        "汗1","汗2","汗3","汗4","腿/汗1",
        "m腿/小穴","m腿/肛門","腿/小穴",
        "表情/眼淚1","表情/眼淚2","表情/眼淚3","表情/眼淚4",
        "表情/小穴水","表情/高潮水",
        "雞雞","精液",
    };

    [Export] public float CharScale    = 0.2f;
    [Export] public Vector2 CharPosition = Vector2.Zero;
    [Export] public float ClimaxLevel  = 0f;

    // 公開圖層字典，讓其他腳本控制顯示/隱藏
    public readonly Dictionary<string, Control> Layers = new Dictionary<string, Control>();

    private Label   _climaxValueLabel;
    private HSlider _climaxSlider;
    private CanvasLayer _canvas;

    public override void _Ready()
    {
        NovelReader.ResetPlayback();
        SceneBgm.Install(this, SceneBgm.Night); // 進入自由操作場景時停止自動／快進。
        _canvas = new CanvasLayer();
        _canvas.Layer = 10;
        AddChild(_canvas);

        Build();
        TaskLogger.Run(FadeInAsync(), "Bedroom.FadeInAsync");
    }

    public override void _Process(double delta)
    {
        if (_climaxSlider    != null) _climaxSlider.Value      = ClimaxLevel / 100.0;
        if (_climaxValueLabel!= null) _climaxValueLabel.Text   = Mathf.RoundToInt(ClimaxLevel) + " / 100";
    }

    // ── 淡入 ──────────────────────────────────────────────────────
    async Task FadeInAsync()
    {
        var overlay = new ColorRect();
        overlay.Color = Colors.Black;
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.AddChild(overlay);

        float t = 1f;
        while (t > 0f)
        {
            t -= (float)GetProcessDeltaTime() * 1.5f;
            overlay.Color = new Color(0, 0, 0, Mathf.Clamp(t, 0f, 1f));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        overlay.QueueFree();
    }

    // ── 建立全場景 ────────────────────────────────────────────────
    void Build()
    {
        BuildBackground();
        BuildCharacter();
        BuildTopHUD();
        BuildBottomButtons();
        BuildSideMenu();
    }

    // ── 1. 背景 ───────────────────────────────────────────────────
    void BuildBackground()
    {
        var bg = new TextureRect();
        bg.Name = "BG";
        bg.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        bg.StretchMode = TextureRect.StretchModeEnum.Scale;
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var tex = ResourceLoader.Load<Texture2D>("res://Resources/Backgrounds/bg_bedroom.png");
        if (tex != null) { bg.Texture = tex; bg.Modulate = Colors.White; }
        else             { bg.Modulate = new Color(0.05f, 0.02f, 0.08f); }

        _canvas.AddChild(bg);
        _canvas.MoveChild(bg, 0); // 強制待在最底層
    }

    // ── 2. 立繪 ──────────────────────────────────────────────────
    void BuildCharacter()
    {
        var existing = _canvas.FindChild("CharacterRoot", false, false) as Control;
        if (existing != null)
        {
            UseExistingCharacterRoot(existing);
            return;
        }

        var charRoot = new Control();
        charRoot.Name = "CharacterRoot";
        charRoot.SetAnchorsPreset(Control.LayoutPreset.Center);
        charRoot.Size = new Vector2(1920f, 1080f);
        charRoot.PivotOffset = charRoot.Size / 2f;
        charRoot.Position = CharPosition - charRoot.Size / 2f;
        charRoot.Scale = new Vector2(CharScale, CharScale);
        _canvas.AddChild(charRoot);

        foreach (var layerName in LayerOrder)
        {
            string goName = layerName.Replace("/", "_");
            var img = new TextureRect();
            img.Name = goName;
            img.MouseFilter = Control.MouseFilterEnum.Ignore;
            img.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            img.StretchMode = TextureRect.StretchModeEnum.Scale;

            // 實際美術檔案攤平放在 Sprites/ 底下（無 Meimei/ 及分組子資料夾），故取 basename 載入
            string baseName1 = layerName.Contains("/") ? layerName[(layerName.LastIndexOf('/') + 1)..] : layerName;
            var tex = ResourceLoader.Load<Texture2D>(DlMode.ResolvePath(baseName1));   // DL 版：雞雞等改載入馬賽克版
            if (tex != null)
            {
                img.Texture = tex;
                img.Size = new Vector2(tex.GetWidth(), tex.GetHeight());
                // Centre within the 1920×1080 root
                img.Position = (new Vector2(1920f, 1080f) - img.Size) / 2f;
                img.Visible = true;
            }
            else
            {
                img.Modulate = Colors.Transparent;
                img.Visible = false;
            }
            charRoot.AddChild(img);
            Layers[layerName] = img;
        }
    }

    void UseExistingCharacterRoot(Control charRoot)
    {
        GD.Print("[Bedroom] 使用場景中的 CharacterRoot 版面");
        // Move in front of BG
        _canvas.MoveChild(charRoot, _canvas.GetChildCount() - 1);

        foreach (var layerName in LayerOrder)
        {
            string goName = layerName.Replace("/", "_");
            var child = charRoot.FindChild(goName, false, false) as Control;
            if (child != null)
            {
                Layers[layerName] = child;
                if (child is TextureRect tr && tr.Texture == null)
                {
                    string baseName2 = layerName.Contains("/") ? layerName[(layerName.LastIndexOf('/') + 1)..] : layerName;
                    var tex = ResourceLoader.Load<Texture2D>(DlMode.ResolvePath(baseName2));
                    if (tex != null) { tr.Texture = tex; tr.Modulate = Colors.White; }
                }
            }
        }
        GD.Print($"[Bedroom] 已掛載 {Layers.Count} 個圖層");
    }

    // ── 3. 頂部 HUD（高潮度）──────────────────────────────────────
    void BuildTopHUD()
    {
        var bar = new Panel();
        bar.Name = "TopHUD";
        bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        bar.Size = new Vector2(0, 80); // width driven by anchor
        var barStyle = new StyleBoxFlat();
        barStyle.BgColor = new Color(0.03f, 0.01f, 0.06f, 0.85f);
        bar.AddThemeStyleboxOverride("panel", barStyle);
        _canvas.AddChild(bar);

        // 標籤
        var lbl = MkLabel(bar, "ClimaxLabel", "💗 高潮度", 22, new Color(1f, 0.65f, 0.8f));
        lbl.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
        lbl.Position = new Vector2(20, -20);
        lbl.Size = new Vector2(130, 40);

        // 數值文字
        _climaxValueLabel = MkLabel(bar, "ClimaxValue", "0 / 100", 20, new Color(1f, 0.85f, 0.9f));
        _climaxValueLabel.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
        _climaxValueLabel.Position = new Vector2(160, -18);
        _climaxValueLabel.Size = new Vector2(110, 36);

        // HSlider (readonly climax bar)
        _climaxSlider = new HSlider();
        _climaxSlider.Name = "ClimaxSlider";
        _climaxSlider.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
        _climaxSlider.Position = new Vector2(280, -9);
        _climaxSlider.Size = new Vector2(320, 18);
        _climaxSlider.MinValue = 0; _climaxSlider.MaxValue = 1; _climaxSlider.Value = 0;
        _climaxSlider.Editable = false;
        bar.AddChild(_climaxSlider);
    }

    // ── 4. 底部操作按鈕 ──────────────────────────────────────────
    void BuildBottomButtons()
    {
        var bar = new Panel();
        bar.Name = "BottomBar";
        bar.AnchorLeft = 0; bar.AnchorRight = 0.72f;
        bar.AnchorTop  = 1; bar.AnchorBottom = 1;
        bar.OffsetTop  = -90; bar.OffsetBottom = 0;
        var barStyle = new StyleBoxFlat();
        barStyle.BgColor = new Color(0.03f, 0.01f, 0.06f, 0.85f);
        bar.AddThemeStyleboxOverride("panel", barStyle);
        _canvas.AddChild(bar);

        string[] lbl = { "撫摸", "親吻", "挑逗", "脫衣", "更多…" };
        Color[]  col = {
            new Color(0.55f,0.12f,0.25f),
            new Color(0.45f,0.10f,0.35f),
            new Color(0.35f,0.10f,0.40f),
            new Color(0.25f,0.12f,0.40f),
            new Color(0.15f,0.10f,0.30f),
        };

        float totalW = 5 * 160f + 4 * 12f;
        float startX = -totalW / 2f + 80f;

        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            var btn = MkActionBtn(bar, "Btn" + i, lbl[i], col[i],
                new Vector2(0.5f, 0.5f), new Vector2(startX + i * 172f, 0));
            btn.Pressed += () => GD.Print($"[Bedroom] 點擊了：{lbl[idx]}（待連接）");
        }
    }

    // ── 5. 右側返回選單 ──────────────────────────────────────────
    void BuildSideMenu()
    {
        var side = new Panel();
        side.Name = "SidePanel";
        side.AnchorLeft = 1; side.AnchorRight = 1;
        side.AnchorTop = 0; side.AnchorBottom = 1;
        side.OffsetLeft = -220; side.OffsetRight = 0;
        var sStyle = new StyleBoxFlat();
        sStyle.BgColor = new Color(0.04f, 0.02f, 0.08f, 0.90f);
        side.AddThemeStyleboxOverride("panel", sStyle);
        _canvas.AddChild(side);

        var locName = MkLabel(side, "LocName", "寢　室", 36, new Color(1f, 0.88f, 0.45f));
        locName.HorizontalAlignment = HorizontalAlignment.Center;
        locName.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        locName.Position = new Vector2(0, 30);
        locName.Size = new Vector2(0, 50);

        float[] ys  = { 130, 196, 262, 328 };  // Y-flipped (down from top)
        string[] lb = { "日　記", "換　裝", "訊　息", "設　定" };
        Color[]  cl = {
            new Color(0.30f,0.18f,0.08f), new Color(0.18f,0.35f,0.14f),
            new Color(0.14f,0.20f,0.46f), new Color(0.20f,0.10f,0.22f)
        };
        for (int i = 0; i < 4; i++)
            MkSideBtn(side, "Btn" + i, lb[i], cl[i], ys[i]);

        var back = MkBtn(side, "BtnBack", "← 返回", new Color(0.12f, 0.06f, 0.14f),
            Control.LayoutPreset.BottomWide, 160, 44, new Vector2(0, -30));
        back.Pressed += () => _ = FadeAndLoadAsync("FoxVillage");

        var ver = MkLabel(side, "Ver", "v0.1", 12, new Color(1, 1, 1, 0.15f));
        ver.HorizontalAlignment = HorizontalAlignment.Center;
        ver.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        ver.Position = new Vector2(0, -30);
        ver.Size = new Vector2(0, 20);
    }

    // ── 淡場並切換場景 ─────────────────────────────────────────────
    async Task FadeAndLoadAsync(string scene)
    {
        var overlay = new ColorRect();
        overlay.Color = new Color(0, 0, 0, 0);
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.AddChild(overlay);

        float t = 0f;
        while (t < 1f)
        {
            t += (float)GetProcessDeltaTime();
            overlay.Color = new Color(0, 0, 0, Mathf.Clamp(t, 0f, 1f));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GetTree().ChangeSceneToFile($"res://Scenes/{scene}.tscn");
    }

    // ── 元件工廠 ──────────────────────────────────────────────────

    Button MkActionBtn(Control parent, string name, string text, Color bg,
        Vector2 pivot, Vector2 offset)
    {
        var btn = new Button();
        btn.Name = name;
        btn.Text = text;
        btn.Size = new Vector2(155, 62);
        btn.PivotOffset = pivot * btn.Size;
        btn.SetAnchorsPreset(Control.LayoutPreset.Center);
        btn.Position = offset - btn.Size / 2f;

        var style = new StyleBoxFlat();
        style.BgColor = bg;
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight =
        style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 6;
        btn.AddThemeStyleboxOverride("normal", style);

        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BgColor = new Color(
            Mathf.Min(bg.R + 0.2f, 1), Mathf.Min(bg.G + 0.15f, 1), Mathf.Min(bg.B + 0.15f, 1));
        btn.AddThemeStyleboxOverride("hover", hover);

        var pressed = (StyleBoxFlat)style.Duplicate();
        pressed.BgColor = new Color(bg.R * 0.75f, bg.G * 0.75f, bg.B * 0.75f);
        btn.AddThemeStyleboxOverride("pressed", pressed);

        btn.AddThemeColorOverride("font_color", Colors.White);
        parent.AddChild(btn);
        return btn;
    }

    Button MkSideBtn(Control parent, string name, string text, Color bg, float y)
    {
        var btn = new Button();
        btn.Name = name;
        btn.Text = text;
        btn.Size = new Vector2(180, 54);
        btn.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        btn.Position = new Vector2((parent.Size.X - 180) / 2f, y);

        var style = new StyleBoxFlat();
        style.BgColor = bg;
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight =
        style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 6;
        btn.AddThemeStyleboxOverride("normal", style);

        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BgColor = new Color(
            Mathf.Min(bg.R + 0.2f, 1), Mathf.Min(bg.G + 0.15f, 1), Mathf.Min(bg.B + 0.15f, 1));
        btn.AddThemeStyleboxOverride("hover", hover);

        btn.AddThemeColorOverride("font_color", Colors.White);
        parent.AddChild(btn);
        return btn;
    }

    Label MkLabel(Control parent, string name, string text, int size, Color color)
    {
        var lbl = new Label();
        lbl.Name = name;
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", size);
        lbl.AddThemeColorOverride("font_color", color);
        parent.AddChild(lbl);
        return lbl;
    }

    Button MkBtn(Control parent, string name, string text, Color bg,
        Control.LayoutPreset preset, float w, float h, Vector2 offset)
    {
        var btn = new Button();
        btn.Name = name;
        btn.Text = text;
        btn.Size = new Vector2(w, h);
        btn.SetAnchorsPreset(preset);
        btn.Position = offset + (preset == Control.LayoutPreset.BottomWide
            ? new Vector2((parent.Size.X - w) / 2f, -h)
            : Vector2.Zero);

        var style = new StyleBoxFlat();
        style.BgColor = bg;
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight =
        style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 5;
        btn.AddThemeStyleboxOverride("normal", style);

        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BgColor = new Color(
            Mathf.Min(bg.R + 0.18f, 1), Mathf.Min(bg.G + 0.12f, 1), Mathf.Min(bg.B + 0.12f, 1));
        btn.AddThemeStyleboxOverride("hover", hover);

        var pressed = (StyleBoxFlat)style.Duplicate();
        pressed.BgColor = new Color(bg.R * 0.8f, bg.G * 0.8f, bg.B * 0.8f);
        btn.AddThemeStyleboxOverride("pressed", pressed);

        btn.AddThemeColorOverride("font_color", Colors.White);
        parent.AddChild(btn);
        return btn;
    }
}
