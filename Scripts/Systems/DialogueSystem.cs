using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// 對話系統 — 打字機效果，支援立繪、背景、CG 圖層淡入/淡出
/// Godot 4 C# port of Unity DialogueSystem.cs
/// </summary>
public partial class DialogueSystem : Node
{
    public static DialogueSystem Instance { get; private set; }

    [Export] public NodePath DialogueBoxPath;
    [Export] public NodePath SpeakerLabelPath;
    [Export] public NodePath ContentLabelPath;
    [Export] public NodePath CharacterImagePath;
    [Export] public NodePath BackgroundImagePath;
    [Export] public NodePath CgImagePath;

    [Export] public float TypeSpeed    = 0.03f;
    [Export] public float CgFadeDuration = 0.6f;

    private Control     _dialogueBox;
    private Label       _speakerLabel;
    private RichTextLabel _contentLabel;
    private TextureRect _charImage;
    private TextureRect _bgImage;
    private TextureRect _cgImage;

    private bool _isTyping;
    private bool _skip;
    private Action _onComplete;

    // ── 資料結構 ────────────────────────────────────────────────

    public class DialogueLine
    {
        [JsonPropertyName("speaker")]    public string speaker    { get; set; }
        [JsonPropertyName("text")]       public string text       { get; set; }
        [JsonPropertyName("spriteName")] public string spriteName { get; set; }
        [JsonPropertyName("bgName")]     public string bgName     { get; set; }
        [JsonPropertyName("bgScale")]    public float  bgScale    { get; set; }
        [JsonPropertyName("bgX")]        public string bgX        { get; set; }
        [JsonPropertyName("bgY")]        public string bgY        { get; set; }

        // CG 單層
        [JsonPropertyName("showCG")]  public string showCG  { get; set; }
        [JsonPropertyName("hideCG")]  public string hideCG  { get; set; }
        [JsonPropertyName("cgScale")] public string cgScale { get; set; }
        [JsonPropertyName("cgX")]     public string cgX     { get; set; }
        [JsonPropertyName("cgY")]     public string cgY     { get; set; }

        // CG 多層
        [JsonPropertyName("showCG1")]  public string showCG1  { get; set; } [JsonPropertyName("hideCG1")]  public string hideCG1  { get; set; } [JsonPropertyName("cgScale1")] public string cgScale1 { get; set; } [JsonPropertyName("cgX1")] public string cgX1 { get; set; } [JsonPropertyName("cgY1")] public string cgY1 { get; set; }
        [JsonPropertyName("showCG2")]  public string showCG2  { get; set; } [JsonPropertyName("hideCG2")]  public string hideCG2  { get; set; } [JsonPropertyName("cgScale2")] public string cgScale2 { get; set; } [JsonPropertyName("cgX2")] public string cgX2 { get; set; } [JsonPropertyName("cgY2")] public string cgY2 { get; set; }
        [JsonPropertyName("showCG3")]  public string showCG3  { get; set; } [JsonPropertyName("hideCG3")]  public string hideCG3  { get; set; } [JsonPropertyName("cgScale3")] public string cgScale3 { get; set; } [JsonPropertyName("cgX3")] public string cgX3 { get; set; } [JsonPropertyName("cgY3")] public string cgY3 { get; set; }
        [JsonPropertyName("showCG4")]  public string showCG4  { get; set; } [JsonPropertyName("hideCG4")]  public string hideCG4  { get; set; } [JsonPropertyName("cgScale4")] public string cgScale4 { get; set; } [JsonPropertyName("cgX4")] public string cgX4 { get; set; } [JsonPropertyName("cgY4")] public string cgY4 { get; set; }
        [JsonPropertyName("showCG5")]  public string showCG5  { get; set; } [JsonPropertyName("hideCG5")]  public string hideCG5  { get; set; } [JsonPropertyName("cgScale5")] public string cgScale5 { get; set; } [JsonPropertyName("cgX5")] public string cgX5 { get; set; } [JsonPropertyName("cgY5")] public string cgY5 { get; set; }
        [JsonPropertyName("showCG6")]  public string showCG6  { get; set; } [JsonPropertyName("hideCG6")]  public string hideCG6  { get; set; } [JsonPropertyName("cgScale6")] public string cgScale6 { get; set; } [JsonPropertyName("cgX6")] public string cgX6 { get; set; } [JsonPropertyName("cgY6")] public string cgY6 { get; set; }
        [JsonPropertyName("showCG7")]  public string showCG7  { get; set; } [JsonPropertyName("hideCG7")]  public string hideCG7  { get; set; } [JsonPropertyName("cgScale7")] public string cgScale7 { get; set; } [JsonPropertyName("cgX7")] public string cgX7 { get; set; } [JsonPropertyName("cgY7")] public string cgY7 { get; set; }
        [JsonPropertyName("showCG8")]  public string showCG8  { get; set; } [JsonPropertyName("hideCG8")]  public string hideCG8  { get; set; } [JsonPropertyName("cgScale8")] public string cgScale8 { get; set; } [JsonPropertyName("cgX8")] public string cgX8 { get; set; } [JsonPropertyName("cgY8")] public string cgY8 { get; set; }
        [JsonPropertyName("showCG9")]  public string showCG9  { get; set; } [JsonPropertyName("hideCG9")]  public string hideCG9  { get; set; } [JsonPropertyName("cgScale9")] public string cgScale9 { get; set; } [JsonPropertyName("cgX9")] public string cgX9 { get; set; } [JsonPropertyName("cgY9")] public string cgY9 { get; set; }
        [JsonPropertyName("showCG10")] public string showCG10 { get; set; } [JsonPropertyName("hideCG10")] public string hideCG10 { get; set; } [JsonPropertyName("cgScale10")] public string cgScale10 { get; set; } [JsonPropertyName("cgX10")] public string cgX10 { get; set; } [JsonPropertyName("cgY10")] public string cgY10 { get; set; }

        // 立繪
        [JsonPropertyName("showCH")]  public string showCH  { get; set; }
        [JsonPropertyName("hideCH")]  public string hideCH  { get; set; }
        [JsonPropertyName("chScale")] public string chScale { get; set; }
        [JsonPropertyName("chX")]     public string chX     { get; set; }
        [JsonPropertyName("chY")]     public string chY     { get; set; }
        // Spine 動態立繪（跟序章同格式）：有任何一個欄位就改用 Spine 顯示
        [JsonPropertyName("chOutfit")] public string chOutfit { get; set; }   // school / school2 / shirt / sleep / sleep2 / nude
        [JsonPropertyName("chSkin")]   public string chSkin   { get; set; }   // chOutfit 的別名
        [JsonPropertyName("chHair")]   public string chHair   { get; set; }   // twin / long / none
        [JsonPropertyName("chSocks")]  public string chSocks  { get; set; }   // white / black / none
        [JsonPropertyName("chFace")]   public string chFace   { get; set; }   // 表情動畫（舊圖片名稱也可以）
        [JsonPropertyName("chAnim")]   public string chAnim   { get; set; }   // 身體動畫

        // 第二角色
        [JsonPropertyName("showCH2")]  public string showCH2  { get; set; }
        [JsonPropertyName("hideCH2")]  public string hideCH2  { get; set; }
        [JsonPropertyName("chScale2")] public string chScale2 { get; set; }
        [JsonPropertyName("chX2")]     public string chX2     { get; set; }
        [JsonPropertyName("chY2")]     public string chY2     { get; set; }

        [JsonPropertyName("bgm")]      public string bgm      { get; set; }
        [JsonPropertyName("se")]       public string se       { get; set; }
        [JsonPropertyName("me")]       public string me       { get; set; }
        [JsonPropertyName("bgAnim")]   public string bgAnim   { get; set; }
        [JsonPropertyName("autoNext")] public float  autoNext { get; set; }

        [JsonPropertyName("fadeout")]      public string fadeout      { get; set; }
        [JsonPropertyName("fadeoutBlack")] public string fadeoutBlack { get; set; }
        [JsonPropertyName("fadeoutWhite")] public string fadeoutWhite { get; set; }
        [JsonPropertyName("fadein")]       public string fadein       { get; set; }
        [JsonPropertyName("wait")]         public float  wait         { get; set; }

        [JsonPropertyName("halfBlack")]  public string halfBlack  { get; set; }
        [JsonPropertyName("halfBlackX")] public string halfBlackX { get; set; }
        [JsonPropertyName("halfBlackY")] public string halfBlackY { get; set; }
        [JsonPropertyName("endTitle")]    public string endTitle    { get; set; }
        [JsonPropertyName("endSubtitle")] public string endSubtitle { get; set; }
        [JsonPropertyName("choiceId")]    public string choiceId    { get; set; }  // 觸發選擇UI的標識
        [JsonPropertyName("endNo")]       public int    endNo       { get; set; }  // 全年齡結局編號（1~5，圖鑒用；搭配 endTitle）

        // 全年齡 / R18 分歧控制
        [JsonPropertyName("branchStart")] public string branchStart { get; set; }  // "allAges" 或 "r18" → 分歧段落開始
        [JsonPropertyName("branchEnd")]   public bool   branchEnd   { get; set; }  // true → 分歧段落結束
        [JsonPropertyName("addVar")]      public string addVar      { get; set; }  // 非空 → 該劇情變數 +1（純控制行）
        [JsonPropertyName("varStart")]    public string varStart    { get; set; }  // 非空 → 變數 >0 才播放此段，否則跳到 varEnd
        [JsonPropertyName("varEnd")]      public bool   varEnd      { get; set; }  // true → 變數條件段落結束

        // 選擇框分歧（與序章同格式）：{ "choice": ["A","B"] } → 顯示選擇框；
        // 之後用 { "optionStart": 0 } ... { "optionEnd": true } 包住各選項的劇情
        [JsonPropertyName("choice")]      public string[] choice      { get; set; }
        [JsonPropertyName("choiceTitle")] public string   choiceTitle { get; set; }
        [JsonPropertyName("optionStart")] public int      optionStart { get; set; } = -1;
        [JsonPropertyName("optionEnd")]   public bool     optionEnd   { get; set; }
        // 全年齡模式播到這行 → 立刻結束目前文本，改播指定的文本（例如 "Day3/Day3_Start"）
        [JsonPropertyName("gotoIfAllAges")] public string gotoIfAllAges { get; set; }
    }

    public class DialogueData
    {
        [JsonPropertyName("id")]    public string id    { get; set; }
        [JsonPropertyName("lines")] public List<DialogueLine> Lines { get; set; } = new();
    }

    // ── 段落跳躍（支援巢狀）────────────────────────────────────
    // 從 start（區塊開始那行）往後找「對應的」結束行，回傳該結束行的 index。
    // 以前 varStart / optionStart 只會找「下一個」varEnd / optionEnd，
    // 只要段落裡面還有巢狀的 varStart，就會提早停在內層的 varEnd，
    // 後面整段劇情錯位或消失（看起來就是劇情播不出來）。
    public static int FindBlockEnd<T>(System.Collections.Generic.IList<T> lines, int start,
        Func<T, bool> isStart, Func<T, bool> isEnd)
    {
        int depth = 1;
        for (int i = start + 1; i < lines.Count; i++)
        {
            var l = lines[i];
            if (l == null) continue;
            if (isStart(l)) depth++;
            else if (isEnd(l)) { depth--; if (depth <= 0) return i; }
        }
        return lines.Count - 1;
    }

    public static int FindBranchEnd(IList<DialogueLine> lines, int start)
        => FindBlockEnd(lines, start, l => !string.IsNullOrEmpty(l.branchStart), l => l.branchEnd);
    public static int FindVarEnd(IList<DialogueLine> lines, int start)
        => FindBlockEnd(lines, start, l => !string.IsNullOrEmpty(l.varStart), l => l.varEnd);
    public static int FindOptionEnd(IList<DialogueLine> lines, int start)
        => FindBlockEnd(lines, start, l => l.optionStart >= 0, l => l.optionEnd);

    // ── 靜態工具方法 ────────────────────────────────────────────

    /// <summary>預處理 JSON：把帶空格的 key 轉成合法欄位名</summary>
    public static string PreprocessJson(string json)
    {
        json = json.Replace("\"fadeout black\"", "\"fadeoutBlack\"");
        json = json.Replace("\"fadeout white\"", "\"fadeoutWhite\"");
        return json;
    }

    /// <summary>解析 DialogueData from JSON string</summary>
    public static DialogueData ParseDialogueData(string json)
    {
        // 劇本 JSON 裡許多欄位（chX/chScale/bgX/cgScale 等）常被寫成裸數字而非字串，
        // Unity 端的解析很寬容，System.Text.Json 預設會直接丟例外中斷。
        // 設定統一放在 GameJson.Options()，三個解析入口共用同一份。
        var opts = GameJson.Options();
        try
        {
            return JsonSerializer.Deserialize<DialogueData>(json, opts);
        }
        catch (JsonException e)
        {
            Godot.GD.PrintErr($"[DialogueSystem] 對話 JSON 解析失敗：{e.Message}");
            throw;
        }
    }

    /// <summary>從 DialogueLine 取得 fadeout 資訊（值、顏色）</summary>
    public static (string val, Color color) GetFadeoutInfo(DialogueLine line)
    {
        if (!string.IsNullOrEmpty(line.fadeoutWhite)) return (line.fadeoutWhite, Colors.White);
        if (!string.IsNullOrEmpty(line.fadeoutBlack)) return (line.fadeoutBlack, Colors.Black);
        if (!string.IsNullOrEmpty(line.fadeout))      return (line.fadeout,      Colors.Black);
        return (null, Colors.Black);
    }

    /// <summary>解析 fadeout 的值，格式："60" 或 "60, 1"</summary>
    public static (string framesStr, bool priority) ParseFadeoutValue(string val)
    {
        if (string.IsNullOrEmpty(val)) return ("", false);
        var parts = val.Split(',');
        string framesStr = parts[0].Trim();
        bool priority = parts.Length >= 2 && parts[1].Trim() == "1";
        return (framesStr, priority);
    }

    /// <summary>解析帶持續時間的數值，格式："200" 或 "-200, 60"（60幀=1秒）</summary>
    public static (float value, float durationSec) ParseValueWithDuration(string raw, float defaultDuration = 0.4f)
    {
        if (string.IsNullOrEmpty(raw)) return (0f, defaultDuration);
        var parts = raw.Split(',');
        float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val);
        float dur = defaultDuration;
        if (parts.Length >= 2)
        {
            float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float frames);
            dur = (frames > 0f) ? frames / 60f : 0f;
        }
        return (val, dur);
    }

    public struct CGLayerCmd
    {
        public string show, hide, scale, x, y;
        public bool hasShow => !string.IsNullOrEmpty(show);
        public bool hasHide => !string.IsNullOrEmpty(hide);
    }

    public static CGLayerCmd GetCGLayer(DialogueLine line, int layer)
    {
        switch (layer)
        {
            case 0:  return new CGLayerCmd { show = line.showCG,   hide = line.hideCG,   scale = line.cgScale,   x = line.cgX,   y = line.cgY  };
            case 1:  return new CGLayerCmd { show = line.showCG1,  hide = line.hideCG1,  scale = line.cgScale1,  x = line.cgX1,  y = line.cgY1 };
            case 2:  return new CGLayerCmd { show = line.showCG2,  hide = line.hideCG2,  scale = line.cgScale2,  x = line.cgX2,  y = line.cgY2 };
            case 3:  return new CGLayerCmd { show = line.showCG3,  hide = line.hideCG3,  scale = line.cgScale3,  x = line.cgX3,  y = line.cgY3 };
            case 4:  return new CGLayerCmd { show = line.showCG4,  hide = line.hideCG4,  scale = line.cgScale4,  x = line.cgX4,  y = line.cgY4 };
            case 5:  return new CGLayerCmd { show = line.showCG5,  hide = line.hideCG5,  scale = line.cgScale5,  x = line.cgX5,  y = line.cgY5 };
            case 6:  return new CGLayerCmd { show = line.showCG6,  hide = line.hideCG6,  scale = line.cgScale6,  x = line.cgX6,  y = line.cgY6 };
            case 7:  return new CGLayerCmd { show = line.showCG7,  hide = line.hideCG7,  scale = line.cgScale7,  x = line.cgX7,  y = line.cgY7 };
            case 8:  return new CGLayerCmd { show = line.showCG8,  hide = line.hideCG8,  scale = line.cgScale8,  x = line.cgX8,  y = line.cgY8 };
            case 9:  return new CGLayerCmd { show = line.showCG9,  hide = line.hideCG9,  scale = line.cgScale9,  x = line.cgX9,  y = line.cgY9 };
            case 10: return new CGLayerCmd { show = line.showCG10, hide = line.hideCG10, scale = line.cgScale10, x = line.cgX10, y = line.cgY10 };
            default: return new CGLayerCmd();
        }
    }

    public const int CG_LAYER_COUNT = 11; // 0(原始) + 1~10

    public static bool IsHideCGPriority(string val)
    {
        if (string.IsNullOrEmpty(val)) return false;
        return val.Trim() == "1";
    }

    // ── Godot Node lifecycle ─────────────────────────────────────

    public override void _Ready()
    {
        if (Instance != null) { QueueFree(); return; }
        Instance = this;

        if (DialogueBoxPath    != null && !DialogueBoxPath.IsEmpty)    _dialogueBox   = GetNode<Control>(DialogueBoxPath);
        if (SpeakerLabelPath   != null && !SpeakerLabelPath.IsEmpty)   _speakerLabel  = GetNode<Label>(SpeakerLabelPath);
        if (ContentLabelPath   != null && !ContentLabelPath.IsEmpty)   _contentLabel  = GetNode<RichTextLabel>(ContentLabelPath);
        if (CharacterImagePath != null && !CharacterImagePath.IsEmpty) _charImage     = GetNode<TextureRect>(CharacterImagePath);
        if (BackgroundImagePath!= null && !BackgroundImagePath.IsEmpty)_bgImage       = GetNode<TextureRect>(BackgroundImagePath);
        if (CgImagePath        != null && !CgImagePath.IsEmpty)        _cgImage       = GetNode<TextureRect>(CgImagePath);

        if (_dialogueBox != null) _dialogueBox.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isTyping) return;
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed) _skip = true;
        if (@event is InputEventKey key && key.Keycode == Key.Space && key.Pressed) _skip = true;
    }

    // ── 載入 JSON ───────────────────────────────────────────────

    public static DialogueData Load(string id)
    {
        string path = $"res://Resources/Data/Dialogues/{id}.json";
        if (!FileAccess.FileExists(path))
        {
            GD.PrintErr($"[Dialogue] 找不到: {path}");
            return null;
        }
        var text = FileAccess.GetFileAsString(path);
        return ParseDialogueData(PreprocessJson(text));
    }

    // ── 播放對話 ────────────────────────────────────────────────

    public void Play(string dialogueId, Action onComplete = null)
    {
        var data = Load(dialogueId);
        Play(data, onComplete);
    }

    public void Play(DialogueData data, Action onComplete = null)
    {
        if (data == null) { onComplete?.Invoke(); return; }
        _onComplete = onComplete;
        if (_dialogueBox != null) _dialogueBox.Visible = true;
        _ = PlayAsync(data);
    }

    private async Task PlayAsync(DialogueData data)
    {
        foreach (var line in data.Lines)
        {
            if (!string.IsNullOrEmpty(line.bgName))     LoadBG(line.bgName);
            if (!string.IsNullOrEmpty(line.spriteName)) LoadSprite(line.spriteName);
            else if (_charImage != null)                 _charImage.Visible = false;

            if (!string.IsNullOrEmpty(line.showCG))
                await FadeCGInAsync(line.showCG);

            if (_speakerLabel != null)
                _speakerLabel.Text = line.speaker ?? "";

            if (!string.IsNullOrEmpty(line.text))
                await TypeTextAsync(line.text);

            // 等待點擊
            bool clicked = false;
            while (!clicked)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (Input.IsActionJustPressed("ui_accept") || Input.IsMouseButtonPressed(MouseButton.Left))
                    clicked = true;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            if (!string.IsNullOrEmpty(line.hideCG))
                await FadeCGOutAsync();
        }

        if (_dialogueBox != null) _dialogueBox.Visible = false;
        _onComplete?.Invoke();
    }

    // ── CG 淡入/淡出 ────────────────────────────────────────────

    private TextureRect GetOrCreateCgImage()
    {
        if (_cgImage != null) return _cgImage;

        Control parent = _bgImage?.GetParent() as Control ?? _dialogueBox?.GetParent() as Control;
        if (parent == null) return null;

        var cgNode = new TextureRect();
        cgNode.Name = "CG_Layer";
        cgNode.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        cgNode.StretchMode = TextureRect.StretchModeEnum.Scale;
        cgNode.MouseFilter = Control.MouseFilterEnum.Ignore;
        cgNode.Modulate = new Color(1, 1, 1, 0);
        cgNode.AnchorsPreset = (int)Control.LayoutPreset.FullRect;
        parent.AddChild(cgNode);

        // Insert just after BG
        if (_bgImage != null)
            parent.MoveChild(cgNode, _bgImage.GetIndex() + 1);

        _cgImage = cgNode;
        return _cgImage;
    }

    private async Task FadeCGInAsync(string cgName)
    {
        GetOrCreateCgImage();
        if (_cgImage == null) return;

        var tex = ResourceLoader.Load<Texture2D>($"res://Resources/CG/{cgName}.png");
        if (tex == null) { GD.PrintErr($"[DialogueSystem] 找不到 CG: {cgName}"); return; }

        _cgImage.Texture = tex;
        _cgImage.Visible = true;

        float elapsed = 0f;
        while (elapsed < CgFadeDuration)
        {
            elapsed += (float)GetProcessDeltaTime();
            float alpha = Mathf.Clamp(elapsed / CgFadeDuration, 0f, 1f);
            _cgImage.Modulate = new Color(1, 1, 1, alpha);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _cgImage.Modulate = Colors.White;
    }

    private async Task FadeCGOutAsync()
    {
        if (_cgImage == null || !_cgImage.Visible) return;

        float elapsed = 0f;
        while (elapsed < CgFadeDuration)
        {
            elapsed += (float)GetProcessDeltaTime();
            float alpha = 1f - Mathf.Clamp(elapsed / CgFadeDuration, 0f, 1f);
            _cgImage.Modulate = new Color(1, 1, 1, alpha);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _cgImage.Modulate = new Color(1, 1, 1, 0);
        _cgImage.Visible = false;
    }

    // ── 打字機 ──────────────────────────────────────────────────

    private async Task TypeTextAsync(string text)
    {
        if (_contentLabel != null) _contentLabel.Text = "";
        _isTyping = true;
        _skip = false;

        foreach (char c in text)
        {
            if (_skip)
            {
                if (_contentLabel != null) _contentLabel.Text = text;
                break;
            }
            if (_contentLabel != null) _contentLabel.Text += c;
            await ToSignal(GetTree().CreateTimer(TypeSpeed, false), SceneTreeTimer.SignalName.Timeout);
        }
        _isTyping = false;
    }

    // ── 私有載入 ─────────────────────────────────────────────────

    private void LoadBG(string bgName)
    {
        if (_bgImage == null) return;
        var tex = ResourceHelper.LoadTexAny($"Backgrounds/{bgName}");
        if (tex != null) { _bgImage.Texture = tex; _bgImage.Modulate = Colors.White; }
        else             { _bgImage.Texture = null; _bgImage.Modulate = new Color(0.05f, 0.02f, 0.08f); }
    }

    private void LoadSprite(string spriteName)
    {
        if (_charImage == null) return;
        var tex = ResourceLoader.Load<Texture2D>($"res://Resources/Sprites/{spriteName}.png");
        if (tex != null) { _charImage.Texture = tex; _charImage.Visible = true; _charImage.Modulate = Colors.White; }
        else             { _charImage.Visible = false; }
    }
}
