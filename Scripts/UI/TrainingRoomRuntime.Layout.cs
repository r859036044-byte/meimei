using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

/// <summary>
/// TrainingRoomRuntime — 版面編輯器（只調整「背景」和「UI 面板」，完全不碰人物相關的任何東西）。
///
/// 操作（僅開發版有效，匯出的正式版不會出現編輯器）：
///   • F2 開／關編輯器。開啟時所有滑鼠輸入都被這裡吞掉，不會觸發人物熱點、鏡頭縮放、HUD 按鈕。
///   • 在畫面上按住左鍵拖曳：點到 UI 面板就移動該面板；點到空白處就移動背景。
///   • 方向鍵微調 1px（Shift＝10px）。下方數值欄也可以直接輸入，全部即時生效。
///   • 「儲存」寫入專案根目錄的 troom_layout.json，之後每次進調教房會自動套用。
///   • 「複製數值」把目前所有數值複製到剪貼簿（也會印在輸出面板），方便貼回程式碼當預設值。
///   • 「刪除存檔」刪掉 troom_layout.json，下次進入回到程式碼裡的預設值。
///
/// 可編輯對象：背景（BackgroundOffset／BackgroundSize）＋ _uiPanelNames 列出的 UI 面板
/// （TopHUD、StatsPanel、VertBars、BottomBar、SidePanel）。UI 面板用 Offset 四邊＋Scale，
/// 面板裡面的子元件位置不變，所以整個面板可以自由搬移、縮放。
/// </summary>
public partial class TrainingRoomRuntime
{
    const string LayoutFilePath = "res://troom_layout.json";

    sealed class LayoutTarget
    {
        public string Name;                 // 下拉選單顯示名稱
        public string Key;                  // 存檔用 key（背景="BG"，UI 面板=節點名稱）
        public string[] Labels;             // 5 個欄位的標籤，空字串＝該欄隱藏
        public Func<float[]> Get;           // 讀出目前 5 個數值
        public Action<float[]> Set;         // 寫入 5 個數值並立即套用
        public Action<Vector2> Drag;        // 螢幕像素位移 → 套用
    }

    readonly List<LayoutTarget> _layoutTargets = new();
    CanvasLayer _layoutLayer;
    PanelContainer _layoutWin;
    OptionButton _layoutPick;
    readonly SpinBox[] _layoutSpins = new SpinBox[5];
    readonly Label[] _layoutLabels = new Label[5];
    readonly Control[] _layoutCells = new Control[5];
    Label _layoutMsg;
    LayoutTarget _layoutSel;
    bool _layoutActive, _layoutDragging, _layoutRefreshing;

    string LayoutFileGlobal => ProjectSettings.GlobalizePath(LayoutFilePath);

    /// <summary>_Ready 在 SetupCameraLayers() 之後呼叫（UI 面板已搬到 UiRoot）。</summary>
    void SetupLayoutEditor()
    {
        // 開局就先把 F1 專用面板（含右側選單的射精鈕）藏起來，不等第一次 _Process，避免開場被點到
        foreach (var n in _f1OnlyPanelNames)
        {
            var c = UiRoot.GetNodeOrNull<Control>(n);
            if (c != null) c.Visible = false;
        }
        _f1UiShown = false;
        BuildLayoutTargets();
        LoadLayoutFile();                       // 已儲存的版面每個版本都會套用
        if (!OS.IsDebugBuild()) return;         // 編輯器本身只在開發版出現
        BuildLayoutWindow();
        AddChild(new InputTap { Name = "LayoutInputTap", Handler = HandleLayoutInput });
    }

    // ── 編輯對象 ─────────────────────────────────────────────
    void BuildLayoutTargets()
    {
        _layoutTargets.Clear();
        _layoutTargets.Add(new LayoutTarget
        {
            Name = "背景", Key = "BG",
            Labels = new[] { "位移X(正=右)", "位移Y(正=上)", "寬", "高", "" },
            Get = () => new[] { BackgroundOffset.X, BackgroundOffset.Y, BackgroundSize.X, BackgroundSize.Y, 0f },
            Set = v =>
            {
                BackgroundOffset = new Vector2(v[0], v[1]);
                BackgroundSize   = new Vector2(v[2], v[3]);
                ApplyBackgroundLayout();
            },
            // 螢幕像素 → 場景像素（背景在會縮放的 _canvas 裡，要除以鏡頭倍率）；Y 軸 ApplyPointAnchor 內會反轉
            Drag = d =>
            {
                BackgroundOffset += new Vector2(d.X, -d.Y) / Mathf.Max(_camZoom, 0.01f);
                ApplyBackgroundLayout();
            },
        });

        foreach (var n in _uiPanelNames)
        {
            if (UiRoot.GetNodeOrNull<Control>(n) == null) continue;
            string key = n;
            _layoutTargets.Add(new LayoutTarget
            {
                Name = n, Key = key,
                Labels = new[] { "Left", "Top", "Right", "Bottom", "Scale" },
                Get = () =>
                {
                    var p = UiRoot.GetNodeOrNull<Control>(key);
                    return p == null ? new float[5]
                        : new[] { p.OffsetLeft, p.OffsetTop, p.OffsetRight, p.OffsetBottom, p.Scale.X };
                },
                Set = v =>
                {
                    var p = UiRoot.GetNodeOrNull<Control>(key);
                    if (p == null) return;
                    p.OffsetLeft = v[0]; p.OffsetTop = v[1]; p.OffsetRight = v[2]; p.OffsetBottom = v[3];
                    float s = Mathf.Max(v[4], 0.1f);
                    p.Scale = new Vector2(s, s);
                },
                Drag = d =>
                {
                    var p = UiRoot.GetNodeOrNull<Control>(key);
                    if (p == null) return;
                    p.OffsetLeft += d.X; p.OffsetRight += d.X;
                    p.OffsetTop  += d.Y; p.OffsetBottom += d.Y;
                },
            });
        }
    }

    /// <summary>把 BackgroundOffset／BackgroundSize 套到兩層背景（BG_Base 與 BG 保持完全一致）。</summary>
    void ApplyBackgroundLayout()
    {
        foreach (var n in new[] { "BG_Base", "BG" })
        {
            var c = _canvas?.GetNodeOrNull<Control>(n);
            if (c != null)
                MainMenuRuntime.ApplyPointAnchor(c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    BackgroundOffset, BackgroundSize);
        }
    }

    // ── 編輯視窗 ─────────────────────────────────────────────
    void BuildLayoutWindow()
    {
        _layoutLayer = new CanvasLayer { Name = "LayoutEditorLayer", Layer = 60, Visible = false };
        AddChild(_layoutLayer);

        _layoutWin = new PanelContainer { Name = "LayoutWin", MouseFilter = Control.MouseFilterEnum.Stop };
        _layoutWin.AnchorLeft = 0.5f; _layoutWin.AnchorRight = 0.5f;
        _layoutWin.AnchorTop = 1f;    _layoutWin.AnchorBottom = 1f;
        _layoutWin.OffsetLeft = -380; _layoutWin.OffsetRight = 380;
        _layoutWin.OffsetTop = -10;   _layoutWin.OffsetBottom = -10;
        _layoutWin.GrowVertical = Control.GrowDirection.Begin;   // 內容多高就往上長多高
        var sty = new StyleBoxFlat { BgColor = new Color(0.06f, 0.03f, 0.10f, 0.94f), BorderColor = new Color(0.9f, 0.5f, 0.7f) };
        sty.SetBorderWidthAll(2);
        sty.SetCornerRadiusAll(10);
        sty.SetContentMarginAll(10);
        _layoutWin.AddThemeStyleboxOverride("panel", sty);
        _layoutLayer.AddChild(_layoutWin);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 8);
        _layoutWin.AddChild(vb);

        var title = new Label
        {
            Text = "版面編輯（F2 關閉）｜拖曳面板或空白處(背景)｜方向鍵微調，Shift=10px",
            Modulate = new Color(1f, 0.85f, 0.92f),
        };
        vb.AddChild(title);

        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 8);
        vb.AddChild(row1);

        _layoutPick = new OptionButton { FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(160, 0) };
        foreach (var t in _layoutTargets) _layoutPick.AddItem(t.Name);
        _layoutPick.ItemSelected += idx => SelectLayoutTarget((int)idx);
        row1.AddChild(_layoutPick);

        AddLayoutButton(row1, "儲存", SaveLayoutFile);
        AddLayoutButton(row1, "複製數值", CopyLayoutCode);
        AddLayoutButton(row1, "刪除存檔", DeleteLayoutFile);

        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", 8);
        vb.AddChild(row2);
        for (int i = 0; i < 5; i++)
        {
            var cell = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _layoutLabels[i] = new Label();
            var sp = new SpinBox
            {
                MinValue = -6000, MaxValue = 6000,
                Step = i == 4 ? 0.01 : 0.1,
                AllowGreater = true, AllowLesser = true,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            sp.ValueChanged += _ => OnLayoutSpinChanged();
            _layoutSpins[i] = sp;
            _layoutCells[i] = cell;
            cell.AddChild(_layoutLabels[i]);
            cell.AddChild(sp);
            row2.AddChild(cell);
        }

        _layoutMsg = new Label { Text = "", Modulate = new Color(1f, 0.9f, 0.5f) };
        vb.AddChild(_layoutMsg);
    }

    void AddLayoutButton(Container parent, string text, Action onPressed)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        b.Pressed += onPressed;
        parent.AddChild(b);
    }

    void SelectLayoutTarget(int i)
    {
        if (i < 0 || i >= _layoutTargets.Count) return;
        _layoutSel = _layoutTargets[i];
        _layoutPick.Select(i);
        for (int j = 0; j < 5; j++)
        {
            string lb = _layoutSel.Labels[j];
            _layoutLabels[j].Text = lb;
            _layoutCells[j].Visible = lb != "";
        }
        RefreshLayoutSpins();
    }

    void RefreshLayoutSpins()
    {
        if (_layoutSel == null) return;
        _layoutRefreshing = true;
        var v = _layoutSel.Get();
        for (int i = 0; i < 5; i++) _layoutSpins[i].Value = v[i];
        _layoutRefreshing = false;
    }

    void OnLayoutSpinChanged()
    {
        if (_layoutRefreshing || _layoutSel == null) return;
        var v = new float[5];
        for (int i = 0; i < 5; i++) v[i] = (float)_layoutSpins[i].Value;
        _layoutSel.Set(v);
    }

    void ToggleLayoutEditor()
    {
        _layoutActive = !_layoutActive;
        _layoutDragging = false;
        _layoutLayer.Visible = _layoutActive;
        if (_layoutActive) SelectLayoutTarget(_layoutSel != null ? _layoutTargets.IndexOf(_layoutSel) : 0);
    }

    // ── 輸入（搶在人物互動／鏡頭之前）────────────────────────────
    bool HandleLayoutInput(InputEvent e)
    {
        if (e is InputEventKey fk && fk.Pressed && !fk.Echo && fk.Keycode == Key.F2)
        {
            ToggleLayoutEditor();
            return true;
        }
        if (!_layoutActive) return false;

        if (e is InputEventKey k)
        {
            if (!k.Pressed || _layoutSel == null) return false;
            if (GetViewport().GuiGetFocusOwner() is LineEdit) return false;   // 正在輸入數字時不當成微調
            float step = k.ShiftPressed ? 10f : 1f;
            Vector2 d = k.Keycode switch
            {
                Key.Left  => new Vector2(-step, 0f),
                Key.Right => new Vector2(step, 0f),
                Key.Up    => new Vector2(0f, -step),
                Key.Down  => new Vector2(0f, step),
                _         => Vector2.Zero,
            };
            if (d == Vector2.Zero) return false;
            // 背景的 Drag 吃螢幕像素，要乘回鏡頭倍率才會剛好是 1 個場景像素
            _layoutSel.Drag(_layoutSel.Key == "BG" ? d * _camZoom : d);
            RefreshLayoutSpins();
            return true;
        }

        if (e is InputEventMouse me)
        {
            if (_layoutWin.GetGlobalRect().HasPoint(me.Position))
            {
                _layoutDragging = false;
                return false;                    // 滑鼠在編輯視窗上：交給 GUI 處理
            }
            if (me is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed) { PickLayoutTargetAt(mb.Position); _layoutDragging = true; }
                    else _layoutDragging = false;
                }
                return true;                     // 吞掉所有按鍵／滾輪：不觸發熱點、縮放、HUD 按鈕
            }
            if (me is InputEventMouseMotion mm)
            {
                if (_layoutDragging && _layoutSel != null)
                {
                    _layoutSel.Drag(mm.Relative);
                    RefreshLayoutSpins();
                }
                return true;
            }
        }
        return false;
    }

    /// <summary>按下的位置有 UI 面板就選它（後加入的在上層，優先），否則選背景。</summary>
    void PickLayoutTargetAt(Vector2 pos)
    {
        for (int i = _layoutTargets.Count - 1; i >= 1; i--)
        {
            var c = UiRoot.GetNodeOrNull<Control>(_layoutTargets[i].Key);
            if (c == null || !c.IsVisibleInTree()) continue;
            Vector2 local = c.GetGlobalTransform().AffineInverse() * pos;   // 含 Scale 的命中判定
            if (new Rect2(Vector2.Zero, c.Size).HasPoint(local)) { SelectLayoutTarget(i); return; }
        }
        SelectLayoutTarget(0);
    }

    // ── 儲存／讀取／複製 ───────────────────────────────────────
    void SaveLayoutFile()
    {
        try
        {
            var d = new Dictionary<string, float[]>();
            foreach (var t in _layoutTargets) d[t.Key] = t.Get();
            System.IO.File.WriteAllText(LayoutFileGlobal,
                JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true }));
            ShowLayoutMsg("已儲存到 troom_layout.json（之後進調教房自動套用）");
        }
        catch (Exception ex) { ShowLayoutMsg("儲存失敗：" + ex.Message); }
    }

    void LoadLayoutFile()
    {
        try
        {
            string path = LayoutFileGlobal;
            if (!System.IO.File.Exists(path)) return;
            var d = JsonSerializer.Deserialize<Dictionary<string, float[]>>(System.IO.File.ReadAllText(path));
            if (d == null) return;
            foreach (var t in _layoutTargets)
                if (d.TryGetValue(t.Key, out var v) && v != null && v.Length >= 5) t.Set(v);
            GD.Print("[版面] 已套用 troom_layout.json（要回到程式預設請用編輯器的「刪除存檔」）");
        }
        catch (Exception ex) { GD.PushWarning("[版面] 讀取 troom_layout.json 失敗：" + ex.Message); }
    }

    void DeleteLayoutFile()
    {
        try
        {
            string path = LayoutFileGlobal;
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            ShowLayoutMsg("已刪除存檔，下次進入回到程式碼預設值");
        }
        catch (Exception ex) { ShowLayoutMsg("刪除失敗：" + ex.Message); }
    }

    void CopyLayoutCode()
    {
        var sb = new StringBuilder();
        foreach (var t in _layoutTargets)
        {
            var v = t.Get();
            if (t.Key == "BG")
                sb.AppendLine($"BackgroundOffset = new Vector2({LayoutNum(v[0])}f, {LayoutNum(v[1])}f);  BackgroundSize = new Vector2({LayoutNum(v[2])}f, {LayoutNum(v[3])}f);");
            else
                sb.AppendLine($"{t.Key}: OffsetLeft={LayoutNum(v[0])} OffsetTop={LayoutNum(v[1])} OffsetRight={LayoutNum(v[2])} OffsetBottom={LayoutNum(v[3])} Scale={LayoutNum(v[4])}");
        }
        DisplayServer.ClipboardSet(sb.ToString());
        GD.Print("[版面]\n" + sb);
        ShowLayoutMsg("已複製到剪貼簿（也印在輸出面板）");
    }

    static string LayoutNum(float x) => x.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    void ShowLayoutMsg(string s)
    {
        if (_layoutMsg != null) _layoutMsg.Text = s;
        GD.Print("[版面] " + s);
    }

    // ── F1 才顯示的開發用 UI ───────────────────────────────────
    //  使用者要求：一般畫面只保留左下（手／雞雞按鈕、精力、精液），
    //  高潮度(TopHUD)、右側選單(SidePanel) 都跟著 F1 面板一起開關。
    static readonly string[] _f1OnlyPanelNames = { "TopHUD", "SidePanel" };   // 狀態卡（StatsPanel）改成一般畫面常駐顯示，不再跟著 F1
    ExpressionTester _f1Tester;
    bool? _f1UiShown;

    /// <summary>每幀呼叫：F1 面板開/關狀態有變才切換顯示，不會每幀重設。</summary>
    void UpdateF1OnlyUi()
    {
        if (_f1Tester == null || !IsInstanceValid(_f1Tester))
            _f1Tester = GetNodeOrNull<ExpressionTester>("ExpressionTesterLayer/ExpressionTester");
        bool show = _f1Tester != null && _f1Tester.IsPanelOpen;
        if (_f1UiShown == show) return;
        _f1UiShown = show;
        foreach (var n in _f1OnlyPanelNames)
        {
            var c = UiRoot.GetNodeOrNull<Control>(n);
            if (c != null) c.Visible = show;
        }
    }

    // ── 人物夜晚濾鏡 ──────────────────────────────────────────
    //  只套在人物根節點 CharacterRoot（_charRoot）上，背景和 UI 不受影響。
    //  做法：Modulate 乘色——整個人物（含所有子圖層）一起染成偏藍、偏暗的夜晚色調，
    //  不動任何人物圖層本身的設定。每幀套用，Play 中改 Inspector 立即生效。
    [ExportGroup("人物夜晚濾鏡")]
    [Export] public bool  NightFilterEnabled  = true;
    [Export] public Color NightFilterColor    = new Color(0.70f, 0.72f, 0.92f);   // 乘色：R/G 壓低、B 保留＝偏藍
    [Export(PropertyHint.Range, "0,1")] public float NightFilterStrength = 0.5f;   // 0＝原色，1＝完整套用

    void UpdateNightFilter()
    {
        if (_charRoot == null) return;
        Color c = NightFilterEnabled ? Colors.White.Lerp(NightFilterColor, NightFilterStrength) : Colors.White;
        if (_charRoot.Modulate != c) _charRoot.Modulate = c;
        // 掛在人物外面的精液（噴出／落地／水滴）和床上的血斑：RGB 也套同一個夜晚乘色（保留各自的透明度，不影響淡出動畫）
        foreach (var d in _ejacDebris)
        {
            if (d == null || !IsInstanceValid(d) || d.GetParent() != _canvas) continue;
            var m = d.Modulate;
            if (m.R != c.R || m.G != c.G || m.B != c.B) d.Modulate = new Color(c.R, c.G, c.B, m.A);
        }
        // 手／舌頭：夜晚色＋設定裡的「手・舌透明度」
        Color hc = new Color(c.R, c.G, c.B, SettingsManager.Instance?.InteractAlpha ?? 0.85f);
        if (_touchHandLayer != null && IsInstanceValid(_touchHandLayer) && _touchHandLayer.Modulate != hc) _touchHandLayer.Modulate = hc;   // 摸胸的手一起套夜晚色
        SyncHandShade();
    }

    // ── 人物光影濾鏡（頭頂加深／右側柔和加深）─────────────────────
    //  做法：把 CharacterRoot 包進一個 CanvasGroup（整個人物先合成成一張，再套 shader），
    //  所以濾鏡只會作用在人物的不透明像素上，背景不會被染到。
    //  漸層範圍用「目前可見人物圖層的螢幕外框」計算（每幀更新，跟著鏡頭縮放/平移），不讀取圖片內容。
    //  兩個效果和上面的夜晚濾鏡各自獨立開關，方便比較。
    [ExportGroup("人物光影濾鏡")]
    [Export] public bool  CharTopShadeEnabled  = true;
    [Export(PropertyHint.Range, "0,1")] public float CharTopShadeHeight   = 0.10f;   // 從人物外框頂端往下的比例
    [Export(PropertyHint.Range, "0,0.3")] public float CharTopShadeSoftness = 0.03f;   // 只在分界線邊緣做漸層（原 0.01 二分硬邊） // 邊緣柔和度：越小越像二分硬邊
    [Export] public Color CharTopShadeColor    = new Color(0.936f, 0.927f, 0.968f); // 現在的 50% 強度
    [Export(PropertyHint.Range, "-0.5,0.5")] public float CharTopShadeTilt = -0.12f;   // 斜度：負值＝左低右高（左邊加深範圍較大），正值反過來，0＝水平
    [Export(PropertyHint.Range, "-30,30")] public float CharTopShadeExtraAngle = 3f;   // 在上面的斜度上再加斜幾度（螢幕上的實際角度）
    [Export] public float CharTopShadeOffsetPx = 3f;                                    // 分界線整體往下移幾個螢幕像素    // 加深的乘色
    [Export] public bool  CharRightShadeEnabled = true;
    [Export(PropertyHint.Range, "0,1")] public float CharRightShadeStart    = 0.65f;  // 分界：左邊這個比例以內加深，右邊不加深（預設左 65%／右 35%）
    [Export(PropertyHint.Range, "0,1")] public float CharRightShadeSoftness = 0.12f;  // 分界的模糊寬度（越大越柔）
    [Export] public Color CharRightShadeColor  = new Color(0.928f, 0.92f, 0.968f);  // 現在的 50% 強度     // 輕微加深（不要太強）

    CanvasGroup _charShadeGroup;
    ShaderMaterial _charShadeMat;
    readonly List<CanvasItem> _charShadeLayers = new();

    /// <summary>人物在 _canvas 底下的最外層節點（有光影群組時是群組，否則是 _charRoot）。
    /// 其他程式要把東西排在人物前/後面時，用這個找索引。</summary>
    Node CharCanvasChild => _charShadeGroup != null ? _charShadeGroup : _charRoot;

    const string CharShadeShaderCode = @"
shader_type canvas_item;
render_mode unshaded;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
uniform vec4 char_rect = vec4(0.0, 0.0, 1.0, 1.0);   // 人物外框（螢幕 UV）：xy=左上 zw=右下
uniform bool top_enabled = true;
uniform float top_height = 0.10;
uniform float top_softness = 0.01;
uniform float top_tilt = -0.12;
uniform float top_offset = 0.0;   // 往下位移（人物外框高度的比例）
uniform vec4 top_color : source_color = vec4(0.62, 0.56, 0.76, 1.0);
uniform bool right_enabled = true;
uniform float right_start = 0.5;
uniform float right_softness = 0.45;
uniform vec4 right_color : source_color = vec4(0.84, 0.82, 0.93, 1.0);
void fragment() {
    vec4 c = textureLod(screen_texture, SCREEN_UV, 0.0);
    if (c.a > 0.0001) c.rgb /= c.a;
    vec2 cuv = (SCREEN_UV - char_rect.xy) / max(char_rect.zw - char_rect.xy, vec2(0.0001));
    vec3 mul = vec3(1.0);
    if (top_enabled) {
        float edge = top_height + top_offset + top_tilt * (cuv.x - 0.5);   // 斜的分界線
        float t = 1.0 - smoothstep(edge - top_softness, edge + top_softness, cuv.y);
        mul *= mix(vec3(1.0), top_color.rgb, t);
    }
    if (right_enabled) {
        // 左邊（分界以內）加深，右邊不加深；分界附近模糊過渡
        float r = 1.0 - smoothstep(right_start - max(right_softness, 0.0001), right_start, cuv.x);
        mul *= mix(vec3(1.0), right_color.rgb, r);
    }
    c.rgb *= mul;
    COLOR *= c;
}";

    /// <summary>_Ready：Build() 之後呼叫。把 CharacterRoot 搬進 CanvasGroup，位置/大小維持不變。</summary>
    void SetupCharShade()
    {
        if (_charRoot == null || _charRoot.GetParent() != _canvas) return;

        // CanvasGroup 是 Node2D，沒有矩形，Control 錨點會失效——先記下目前實際位置/大小，搬過去後改成左上錨點直接放回原位
        Vector2 pos = _charRoot.Position, size = _charRoot.Size;
        int idx = _charRoot.GetIndex();

        _charShadeMat = new ShaderMaterial { Shader = new Shader { Code = CharShadeShaderCode } };
        _charShadeGroup = new CanvasGroup { Name = "CharShadeGroup", Material = _charShadeMat };
        _canvas.AddChild(_charShadeGroup);
        _canvas.MoveChild(_charShadeGroup, idx);

        _charRoot.Reparent(_charShadeGroup, false);
        _charRoot.AnchorLeft = _charRoot.AnchorTop = _charRoot.AnchorRight = _charRoot.AnchorBottom = 0f;
        _charRoot.Position = pos;
        _charRoot.Size = size;

        foreach (var n in _charRoot.FindChildren("*", "TextureRect", true, false))
            if (n is CanvasItem ci) _charShadeLayers.Add(ci);

        _charShadeLocalRect = ComputeCharShadeLocalRect();
    }

    // 人物光影的範圍：進房間時用「預設姿勢」顯示中的圖層算一次，存成人物本地座標並固定下來。
    // 之前每幀依目前顯示的圖層重算，切到 M 腿模式（顯示另一組腿部圖層）外框會變，光影就跟著位移。
    Rect2 _charShadeLocalRect;

    Rect2 ComputeCharShadeLocalRect()
    {
        Transform2D toLocal = _charRoot.GetGlobalTransform().AffineInverse();
        bool any = false;
        Vector2 mn = Vector2.Zero, mx = Vector2.Zero;
        foreach (var ci in _charShadeLayers)
        {
            if (!IsInstanceValid(ci) || !ci.IsVisibleInTree() || ci is not TextureRect tr || tr.Texture == null) continue;
            Rect2 used = GetOpaqueLocalRect(tr);
            if (used.Size.X <= 0f || used.Size.Y <= 0f) continue;
            Transform2D xf = toLocal * tr.GetGlobalTransform();
            Vector2 a = used.Position, b = used.End;
            foreach (var p in new[] { a, new Vector2(b.X, a.Y), b, new Vector2(a.X, b.Y) })
            {
                Vector2 lp = xf * p;
                if (!any) { mn = mx = lp; any = true; }
                else { mn = mn.Min(lp); mx = mx.Max(lp); }
            }
        }
        return any ? new Rect2(mn, mx - mn) : new Rect2(Vector2.Zero, _charRoot.Size);
    }

    /// <summary>每幀：更新人物外框（螢幕 UV）與 Inspector 參數。</summary>
    void UpdateCharShade()
    {
        if (_charShadeMat == null) return;
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        Transform2D cam = _canvas.Transform;
        // 固定範圍（人物本地座標）→ 螢幕座標：只跟著人物整體位置與鏡頭走，不受姿勢／圖層切換影響
        Transform2D xfr = cam * _charRoot.GetGlobalTransform();
        Vector2 ra = _charShadeLocalRect.Position, rb = _charShadeLocalRect.End;
        bool any = false;
        Vector2 mn = Vector2.Zero, mx = Vector2.Zero;
        foreach (var p in new[] { ra, new Vector2(rb.X, ra.Y), rb, new Vector2(ra.X, rb.Y) })
        {
            Vector2 sp = xfr * p;
            if (!any) { mn = mx = sp; any = true; }
            else { mn = mn.Min(sp); mx = mx.Max(sp); }
        }
        if (any) _charShadeMat.SetShaderParameter("char_rect", new Vector4(mn.X / vp.X, mn.Y / vp.Y, mx.X / vp.X, mx.Y / vp.Y));

        // 斜度：CharTopShadeTilt 是「外框比例」單位，換算成螢幕上的實際角度後再加 CharTopShadeExtraAngle 度，
        // 往同一個方向（更斜）。位移：螢幕像素 ÷ 外框高度像素。
        float wPx = Mathf.Max(mx.X - mn.X, 1f), hPx = Mathf.Max(mx.Y - mn.Y, 1f);
        float baseDeg = Mathf.RadToDeg(Mathf.Atan(CharTopShadeTilt * hPx / wPx));
        float dir = CharTopShadeTilt < 0f ? -1f : 1f;
        float totalDeg = Mathf.Clamp(baseDeg + dir * CharTopShadeExtraAngle, -80f, 80f);
        float tilt = Mathf.Tan(Mathf.DegToRad(totalDeg)) * wPx / hPx;
        _charShadeMat.SetShaderParameter("top_offset", CharTopShadeOffsetPx / hPx);
        _charShadeMat.SetShaderParameter("top_enabled",    CharTopShadeEnabled);
        _charShadeMat.SetShaderParameter("top_height",     CharTopShadeHeight);
        _charShadeMat.SetShaderParameter("top_softness",   CharTopShadeSoftness);
        _charShadeMat.SetShaderParameter("top_tilt",       tilt);
        _charShadeMat.SetShaderParameter("top_color",      CharTopShadeColor);
        _charShadeMat.SetShaderParameter("right_enabled",  CharRightShadeEnabled);
        _charShadeMat.SetShaderParameter("right_start",    CharRightShadeStart);
        _charShadeMat.SetShaderParameter("right_softness", CharRightShadeSoftness);
        _charShadeMat.SetShaderParameter("right_color",    CharRightShadeColor);
    }

    // ── F1 面板用：兩個濾鏡各自獨立開關 ─────────────────────────
    //  直接改 Export 欄位，Inspector 勾選會同步。
    public void ToggleNightFilter() => NightFilterEnabled = !NightFilterEnabled;

    public void ToggleShadeFilter()
    {
        bool on = !(CharTopShadeEnabled || CharRightShadeEnabled);
        CharTopShadeEnabled = CharRightShadeEnabled = on;
    }

    public string NightFilterLabel() => "第一版：夜晚濾鏡 " + (NightFilterEnabled ? "【開】" : "【關】");
    public string ShadeFilterLabel() => "第二版：光影濾鏡 " + ((CharTopShadeEnabled || CharRightShadeEnabled) ? "【開】" : "【關】");

    // ── 圖層「實際有畫東西」的範圍 ─────────────────────────────
    //  圖層貼圖常常是整張畫布大小，四周大片透明；用外框算的話頭頂 10% 會落在空白處、看不到效果。
    //  這裡在執行時用 Image.GetUsedRect() 算出不透明像素的範圍（每張貼圖只算一次並快取），
    //  再換算成 TextureRect 的本地座標。
    readonly Dictionary<Texture2D, Rect2> _opaqueRectCache = new();

    Rect2 GetOpaqueLocalRect(TextureRect tr)
    {
        var tex = tr.Texture;
        Vector2 texSize = tex.GetSize();
        if (texSize.X <= 0f || texSize.Y <= 0f) return new Rect2(Vector2.Zero, tr.Size);
        if (!_opaqueRectCache.TryGetValue(tex, out var used))
        {
            used = new Rect2(Vector2.Zero, texSize);   // 算不出來就退回整張
            try
            {
                var img = tex.GetImage();
                if (img != null)
                {
                    if (img.IsCompressed()) img.Decompress();
                    Rect2I r = img.GetUsedRect();
                    used = new Rect2(r.Position, r.Size);
                }
            }
            catch (Exception ex) { GD.PushWarning("[光影濾鏡] 無法計算圖層不透明範圍：" + ex.Message); }
            _opaqueRectCache[tex] = used;
        }
        Vector2 k = tr.Size / texSize;                  // 貼圖像素 → TextureRect 本地座標
        Vector2 pos = used.Position * k, size = used.Size * k;
        if (tr.FlipH) pos.X = tr.Size.X - pos.X - size.X;
        if (tr.FlipV) pos.Y = tr.Size.Y - pos.Y - size.Y;
        return new Rect2(pos, size);
    }

    /// <summary>F1 面板滑桿用：夜晚濾鏡強度（0～1），直接改 NightFilterStrength。</summary>
    public float NightFilterStrengthValue
    {
        get => NightFilterStrength;
        set => NightFilterStrength = Mathf.Clamp(value, 0f, 1f);
    }
}
