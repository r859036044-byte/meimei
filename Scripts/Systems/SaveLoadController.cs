using Godot;
using System;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// 存讀檔控制器
/// 程式化生成 UI，風格與遊戲內其他視窗一致（奶白底 + 玫瑰粉細框，按鈕用 UIStyle）。
/// 功能：保存／讀檔分頁、覆蓋／刪除／讀檔確認、損毀存檔提示、縮圖、儲存時自動截圖。
/// </summary>
public partial class SaveLoadController : Node
{
    public enum Mode { Save, Load }

    // ═══════════════════════════════════════════════════════════
    //  存檔資料結構
    // ═══════════════════════════════════════════════════════════
    public class SaveData
    {
        public string sceneName   { get; set; }
        public string scriptName  { get; set; }
        public int    lineIndex   { get; set; }

        // 背景
        public string bgPath  { get; set; }
        public float  bgScale { get; set; }
        public float  bgX     { get; set; }
        public float  bgY     { get; set; }

        // CG
        public string cgPath  { get; set; }
        public float  cgScale { get; set; }
        public float  cgY     { get; set; }

        // 立繪
        public string charPath { get; set; }
        public string charBase { get; set; }
        public int    charSlot { get; set; }
        public string char2Path { get; set; }

        // BGM
        public string bgmName   { get; set; }
        public float  bgmVolume { get; set; }

        // BGS
        public string bgsName   { get; set; }
        public float  bgsVolume { get; set; }

        // 時間戳
        public string saveTime { get; set; }

        // 劇情變數（day1_blanket / day2_blanket…）。舊存檔沒有這欄 = null，讀檔時不動
        public System.Collections.Generic.Dictionary<string, int> dialogueVars { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    //  面板設定
    // ═══════════════════════════════════════════════════════════
    public float PanelWidth       = 1100f;
    public float PanelHeight      = 760f;
    public int   TitleFontSize    = 34;
    public int   TabFontSize      = 24;
    public int   SlotCount        = 6;
    public float SlotHeight       = 124f;
    public float ThumbnailWidth   = 192f;
    public float ThumbnailHeight  = 108f;
    public int   SlotNameFontSize = 24;
    public int   SlotTimeFontSize = 20;
    public int   EmptySlotFontSize = 22;

    // ═══════════════════════════════════════════════════════════
    //  顏色主題（與 NovelReader / UIStyle 同一套玫瑰粉）
    // ═══════════════════════════════════════════════════════════
    static readonly Color OverlayColor   = new Color(0.14f, 0.07f, 0.12f, 0.72f);
    static readonly Color PaperColor     = new Color("fff9fd");
    static readonly Color BorderColor    = new Color("e8b5ce");
    static readonly Color TitleColor     = new Color("79475f");
    static readonly Color TextColor      = new Color("654657");
    static readonly Color SubTextColor   = new Color("a8758e");
    static readonly Color AccentColor    = new Color("b0577f");
    static readonly Color SlotFillColor  = new Color("fff0f6");
    static readonly Color SlotHoverColor = new Color("ffe3ee");

    // ═══════════════════════════════════════════════════════════
    //  動畫
    // ═══════════════════════════════════════════════════════════
    public float PopInDuration  = 0.25f;
    public float PopOutDuration = 0.18f;

    // ═══════════════════════════════════════════════════════════
    //  靜態回調
    // ═══════════════════════════════════════════════════════════
    /// <summary>取得當前遊戲狀態（存檔時呼叫）</summary>
    public static Func<SaveData> OnGetSaveData;
    /// <summary>套用讀檔的遊戲狀態</summary>
    public static Action<SaveData> OnApplyLoadData;

    // ═══════════════════════════════════════════════════════════
    //  內部
    // ═══════════════════════════════════════════════════════════
    private bool _isBuilt = false;
    private bool _isOpen  = false;
    private bool _busy    = false;   // 截圖／動畫期間擋掉重複點擊
    private Mode _currentMode = Mode.Save;
    private CanvasLayer _canvasLayer;
    private Control _rootPanel;
    private Control _card;
    private Action _onCloseCallback;

    private Button _saveTabBtn, _loadTabBtn;
    private Label _hintLabel;
    private SlotUI[] _slots;
    private Image _pendingScreenshot;

    // 確認視窗
    private Control _confirmLayer;
    private Label _confirmTitle, _confirmBody;
    private Action _confirmAction;

    private static string SaveDirStatic => Path.Combine(OS.GetUserDataDir(), "Saves");
    private string SaveDir => SaveDirStatic;

    /// <summary>是否至少有一個存檔（主選單「繼續遊戲」按鈕用）。</summary>
    public static bool HasAnySave()
    {
        try
        {
            if (!Directory.Exists(SaveDirStatic)) return false;
            return Directory.GetFiles(SaveDirStatic, "save_*.json").Length > 0;
        }
        catch { return false; }
    }

    class SlotUI
    {
        public PanelContainer root;
        public TextureRect thumbnail;
        public Label nameLabel, timeLabel, emptyLabel;
        public Button deleteBtn;
        public StyleBoxFlat normalStyle, hoverStyle;
    }

    // ═══════════════════════════════════════════════════════════
    //  公開 API
    // ═══════════════════════════════════════════════════════════

    /// <summary>以指定模式開啟（Save 或 Load）</summary>
    public void Open(Mode mode, Action onClose = null)
    {
        _onCloseCallback = onClose;
        _currentMode = mode;
        if (!_isBuilt) BuildUI();
        HideConfirm();
        RefreshAllSlots();
        UpdateTabVisuals();
        _rootPanel.Visible = true;
        AnimatePopIn();
        _isOpen = true;
    }

    /// <summary>截取螢幕後開啟存檔面板</summary>
    public async void CaptureAndOpen(Action onClose = null)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _pendingScreenshot = GetViewport().GetTexture().GetImage();
        Open(Mode.Save, onClose);
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        HideConfirm();
        AnimatePopOut();
    }

    public bool IsOpen() => _isOpen;

    // ═══════════════════════════════════════════════════════════
    //  輸入處理 — ESC：先關確認視窗，再關面板
    // ═══════════════════════════════════════════════════════════
    public override void _Input(InputEvent @event)
    {
        if (!_isOpen) return;
        if (@event is InputEventKey key && key.Keycode == Key.Escape && key.Pressed && !key.Echo)
        {
            if (_confirmLayer != null && _confirmLayer.Visible) HideConfirm();
            else Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  樣式輔助
    // ═══════════════════════════════════════════════════════════
    static StyleBoxFlat Paper(Color? fill = null, int radius = 20, int border = 2)
    {
        return new StyleBoxFlat
        {
            BgColor = fill ?? PaperColor, BorderColor = BorderColor,
            BorderWidthTop = border + 1, BorderWidthBottom = border, BorderWidthLeft = border, BorderWidthRight = border,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ShadowColor = new Color(0.30f, 0.13f, 0.23f, 0.18f), ShadowSize = 6, AntiAliasing = true
        };
    }

    static Label MakeLabel(string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var l = new Label { Text = text, HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        l.MouseFilter = Control.MouseFilterEnum.Ignore;
        return l;
    }

    // ═══════════════════════════════════════════════════════════
    //  BuildUI
    // ═══════════════════════════════════════════════════════════
    void BuildUI()
    {
        _isBuilt = true;

        _canvasLayer = new CanvasLayer { Layer = 140 };
        AddChild(_canvasLayer);

        _rootPanel = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _canvasLayer.AddChild(_rootPanel);
        _rootPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var overlay = new ColorRect { Color = OverlayColor, MouseFilter = Control.MouseFilterEnum.Stop };
        _rootPanel.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _rootPanel.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _card = new PanelContainer { CustomMinimumSize = new Vector2(PanelWidth, PanelHeight), MouseFilter = Control.MouseFilterEnum.Stop };
        var cardStyle = Paper(radius: 28);
        cardStyle.ContentMarginLeft = cardStyle.ContentMarginRight = 36;
        cardStyle.ContentMarginTop = 24; cardStyle.ContentMarginBottom = 24;
        _card.AddThemeStyleboxOverride("panel", cardStyle);
        center.AddChild(_card);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        _card.AddChild(vb);

        // --- 標題列 ---
        var titleRow = new HBoxContainer();
        vb.AddChild(titleRow);
        var spacer = new Control { CustomMinimumSize = new Vector2(48, 0) };
        titleRow.AddChild(spacer);
        var title = MakeLabel("✿  保存 / 讀檔  ✿", TitleFontSize, TitleColor, HorizontalAlignment.Center);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        var closeBtn = UIStyle.CreateStyledButton("✕", 24);
        closeBtn.CustomMinimumSize = new Vector2(48, 44);
        closeBtn.Pressed += Close;
        titleRow.AddChild(closeBtn);

        // --- 分頁（用 UIStyle 按鈕，選中的以粗體＋強調色標示）---
        var tabRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        tabRow.AddThemeConstantOverride("separation", 16);
        vb.AddChild(tabRow);
        _saveTabBtn = UIStyle.CreateStyledButton("💾  保存", TabFontSize);
        _saveTabBtn.CustomMinimumSize = new Vector2(180, 48);
        _saveTabBtn.Pressed += () => SwitchMode(Mode.Save);
        tabRow.AddChild(_saveTabBtn);
        _loadTabBtn = UIStyle.CreateStyledButton("📂  讀檔", TabFontSize);
        _loadTabBtn.CustomMinimumSize = new Vector2(180, 48);
        _loadTabBtn.Pressed += () => SwitchMode(Mode.Load);
        tabRow.AddChild(_loadTabBtn);

        _hintLabel = MakeLabel("", 20, SubTextColor, HorizontalAlignment.Center);
        vb.AddChild(_hintLabel);

        // --- 存檔槽（可捲動）---
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        vb.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(list);

        _slots = new SlotUI[SlotCount];
        for (int i = 0; i < SlotCount; i++)
            _slots[i] = CreateSlot(list, i);

        BuildConfirm();
        _rootPanel.Visible = false;
    }

    SlotUI CreateSlot(Control parent, int index)
    {
        var slot = new SlotUI();
        slot.normalStyle = Paper(SlotFillColor, 18, 2);
        slot.normalStyle.ShadowSize = 2;
        slot.hoverStyle = Paper(SlotHoverColor, 18, 2);
        slot.hoverStyle.BorderColor = new Color("cf809f");
        slot.hoverStyle.ShadowSize = 2;

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, SlotHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        slot.normalStyle.ContentMarginLeft = slot.normalStyle.ContentMarginRight = 14;
        slot.normalStyle.ContentMarginTop = slot.normalStyle.ContentMarginBottom = 8;
        slot.hoverStyle.ContentMarginLeft = slot.hoverStyle.ContentMarginRight = 14;
        slot.hoverStyle.ContentMarginTop = slot.hoverStyle.ContentMarginBottom = 8;
        panel.AddThemeStyleboxOverride("panel", slot.normalStyle);
        parent.AddChild(panel);
        slot.root = panel;

        int idx = index;
        panel.MouseEntered += () => panel.AddThemeStyleboxOverride("panel", slot.hoverStyle);
        panel.MouseExited  += () => panel.AddThemeStyleboxOverride("panel", slot.normalStyle);
        panel.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                OnSlotClicked(idx);
                panel.AcceptEvent();
            }
        };

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 20);
        panel.AddChild(row);

        // 縮圖（粉框）
        var thumbFrame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(ThumbnailWidth + 8, ThumbnailHeight + 8),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var thumbStyle = new StyleBoxFlat
        {
            BgColor = new Color("f6dce7"), BorderColor = BorderColor,
            BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 2, ContentMarginBottom = 2,
            AntiAliasing = true
        };
        thumbFrame.AddThemeStyleboxOverride("panel", thumbStyle);
        row.AddChild(thumbFrame);
        var thumb = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        thumbFrame.AddChild(thumb);
        slot.thumbnail = thumb;

        // 文字區
        var textBox = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(textBox);
        slot.nameLabel = MakeLabel($"存檔 {index + 1}", SlotNameFontSize, AccentColor);
        textBox.AddChild(slot.nameLabel);
        slot.timeLabel = MakeLabel("", SlotTimeFontSize, SubTextColor);
        textBox.AddChild(slot.timeLabel);
        slot.emptyLabel = MakeLabel($"— 空　存檔 {index + 1} —", EmptySlotFontSize, SubTextColor);
        textBox.AddChild(slot.emptyLabel);

        // 刪除
        var delBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(delBox);
        slot.deleteBtn = UIStyle.CreateStyledButton("🗑 刪除", 18);
        slot.deleteBtn.CustomMinimumSize = new Vector2(104, 40);
        slot.deleteBtn.Pressed += () => AskDelete(idx);
        delBox.AddChild(slot.deleteBtn);

        return slot;
    }

    // ═══════════════════════════════════════════════════════════
    //  確認視窗
    // ═══════════════════════════════════════════════════════════
    void BuildConfirm()
    {
        _confirmLayer = new Control { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        _rootPanel.AddChild(_confirmLayer);
        _confirmLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0.14f, 0.07f, 0.12f, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop };
        _confirmLayer.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _confirmLayer.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var card = new PanelContainer { CustomMinimumSize = new Vector2(520, 0), MouseFilter = Control.MouseFilterEnum.Stop };
        var sty = Paper(radius: 24);
        sty.ContentMarginLeft = sty.ContentMarginRight = 40;
        sty.ContentMarginTop = sty.ContentMarginBottom = 30;
        card.AddThemeStyleboxOverride("panel", sty);
        center.AddChild(card);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 18);
        card.AddChild(vb);
        _confirmTitle = MakeLabel("", 28, TitleColor, HorizontalAlignment.Center);
        vb.AddChild(_confirmTitle);
        _confirmBody = MakeLabel("", 22, TextColor, HorizontalAlignment.Center);
        _confirmBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vb.AddChild(_confirmBody);

        var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnRow.AddThemeConstantOverride("separation", 24);
        vb.AddChild(btnRow);
        var ok = UIStyle.CreateStyledButton("確定", 24);
        ok.CustomMinimumSize = new Vector2(150, 48);
        ok.Pressed += () => { var a = _confirmAction; HideConfirm(); a?.Invoke(); };
        btnRow.AddChild(ok);
        var cancel = UIStyle.CreateStyledButton("取消", 24);
        cancel.CustomMinimumSize = new Vector2(150, 48);
        cancel.Pressed += HideConfirm;
        btnRow.AddChild(cancel);
    }

    void ShowConfirm(string title, string body, Action onOk)
    {
        _confirmTitle.Text = title;
        _confirmBody.Text = body;
        _confirmAction = onOk;
        _confirmLayer.Visible = true;
    }

    void HideConfirm()
    {
        _confirmAction = null;
        if (_confirmLayer != null) _confirmLayer.Visible = false;
    }

    // ═══════════════════════════════════════════════════════════
    //  標籤頁切換
    // ═══════════════════════════════════════════════════════════
    void SwitchMode(Mode mode)
    {
        _currentMode = mode;
        UpdateTabVisuals();
    }

    void UpdateTabVisuals()
    {
        bool isSave = _currentMode == Mode.Save;
        void Mark(Button b, bool active)
        {
            b.AddThemeColorOverride("font_color", active ? AccentColor : UIStyle.BtnTextColor);
            b.AddThemeColorOverride("font_hover_color", active ? AccentColor : UIStyle.BtnTextColor);
            b.AddThemeColorOverride("font_pressed_color", active ? AccentColor : UIStyle.BtnTextColor);
            b.AddThemeColorOverride("font_focus_color", active ? AccentColor : UIStyle.BtnTextColor);
            b.Modulate = active ? Colors.White : new Color(1f, 1f, 1f, 0.72f);
        }
        Mark(_saveTabBtn, isSave);
        Mark(_loadTabBtn, !isSave);
        _hintLabel.Text = isSave ? "點選欄位保存目前進度（已有存檔會先詢問是否覆蓋）" : "點選有存檔的欄位讀取進度";
    }

    // ═══════════════════════════════════════════════════════════
    //  槽位操作
    // ═══════════════════════════════════════════════════════════
    void OnSlotClicked(int index)
    {
        if (_busy) return;
        bool hasData = File.Exists(GetSavePath(index));
        if (_currentMode == Mode.Save)
        {
            if (hasData)
                ShowConfirm("覆蓋存檔", $"存檔 {index + 1} 已有進度，要覆蓋嗎？", () => SaveToSlot(index));
            else
                SaveToSlot(index);
        }
        else
        {
            if (!hasData) return;
            ShowConfirm("讀取存檔", $"要讀取存檔 {index + 1} 嗎？\n目前未保存的進度會消失。", () => LoadFromSlot(index));
        }
    }

    void AskDelete(int index)
    {
        if (_busy) return;
        ShowConfirm("刪除存檔", $"確定要刪除存檔 {index + 1} 嗎？\n刪除後無法復原。", () => DeleteSlot(index));
    }

    async void SaveToSlot(int index)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            // 從「讀檔」分頁切過來時沒有截圖：暫時隱藏面板，抓當前遊戲畫面
            if (_pendingScreenshot == null)
            {
                _rootPanel.Visible = false;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _pendingScreenshot = GetViewport().GetTexture().GetImage();
                _rootPanel.Visible = true;
            }

            EnsureSaveDir();
            SaveData data = OnGetSaveData?.Invoke();
            if (data == null)
            {
                data = new SaveData();
                data.sceneName = GetTree().CurrentScene?.SceneFilePath ?? "";
            }
            data.saveTime = DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss");
            data.dialogueVars ??= PlayerDataManager.Instance?.GetAllDialogueVars();

            string json = System.Text.Json.JsonSerializer.Serialize(data,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            // 先寫暫存檔再替換，避免寫到一半失敗把舊存檔弄壞
            string path = GetSavePath(index), tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, true);

            if (_pendingScreenshot != null)
                _pendingScreenshot.SavePng(GetScreenshotPath(index));

            GD.Print($"[SaveLoad] 已儲存到槽位 {index + 1}: {data.saveTime}");
            RefreshSlot(index);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SaveLoad] 儲存槽位 {index + 1} 失敗: {e.Message}");
            ShowConfirm("保存失敗", "無法寫入存檔，請確認磁碟空間或權限。", null);
        }
        finally { _busy = false; }
    }

    void LoadFromSlot(int index)
    {
        string path = GetSavePath(index);
        if (!File.Exists(path)) return;

        SaveData data;
        try
        {
            data = System.Text.Json.JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path));
            if (data == null) throw new InvalidDataException("空的存檔內容");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SaveLoad] 讀取槽位 {index + 1} 失敗: {e.Message}");
            ShowConfirm("讀取失敗", "這個存檔已損毀，無法讀取。", null);
            return;
        }

        GD.Print($"[SaveLoad] 讀取槽位 {index + 1}: {data.saveTime}");
        PlayerDataManager.Instance?.SetAllDialogueVars(data.dialogueVars);
        Close();
        OnApplyLoadData?.Invoke(data);
    }

    void DeleteSlot(int index)
    {
        try
        {
            string savePath = GetSavePath(index), ssPath = GetScreenshotPath(index);
            if (File.Exists(savePath)) File.Delete(savePath);
            if (File.Exists(ssPath))   File.Delete(ssPath);
            GD.Print($"[SaveLoad] 已刪除槽位 {index + 1}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SaveLoad] 刪除槽位 {index + 1} 失敗: {e.Message}");
        }
        RefreshSlot(index);
    }

    // ═══════════════════════════════════════════════════════════
    //  槽位顯示刷新
    // ═══════════════════════════════════════════════════════════
    void RefreshAllSlots()
    {
        for (int i = 0; i < SlotCount; i++) RefreshSlot(i);
    }

    void RefreshSlot(int index)
    {
        if (_slots == null || index < 0 || index >= _slots.Length) return;
        var slot = _slots[index];
        string savePath = GetSavePath(index);

        if (!File.Exists(savePath))
        {
            slot.nameLabel.Visible = slot.timeLabel.Visible = false;
            slot.emptyLabel.Visible = true;
            slot.deleteBtn.Visible = false;
            slot.thumbnail.Texture = null;
            return;
        }

        SaveData data = null;
        bool corrupt = false;
        try { data = System.Text.Json.JsonSerializer.Deserialize<SaveData>(File.ReadAllText(savePath)); }
        catch { corrupt = true; }

        slot.nameLabel.Visible = slot.timeLabel.Visible = true;
        slot.emptyLabel.Visible = false;
        slot.deleteBtn.Visible = true;
        slot.nameLabel.Text = $"存檔 {index + 1}" + (corrupt ? "（已損毀）" : "");
        slot.timeLabel.Text = data?.saveTime ?? "";

        slot.thumbnail.Texture = null;
        string ssPath = GetScreenshotPath(index);
        if (File.Exists(ssPath))
        {
            var img = Image.LoadFromFile(ssPath);
            if (img != null) slot.thumbnail.Texture = ImageTexture.CreateFromImage(img);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  檔案路徑
    // ═══════════════════════════════════════════════════════════
    void EnsureSaveDir()
    {
        if (!Directory.Exists(SaveDir)) Directory.CreateDirectory(SaveDir);
    }

    string GetSavePath(int index)       => Path.Combine(SaveDir, $"save_{index}.json");
    string GetScreenshotPath(int index) => Path.Combine(SaveDir, $"save_{index}.png");

    // ═══════════════════════════════════════════════════════════
    //  彈出 / 收回動畫
    // ═══════════════════════════════════════════════════════════
    void AnimatePopIn()
    {
        _rootPanel.Modulate = new Color(1, 1, 1, 0);
        _card.PivotOffset = _card.Size / 2f;
        _card.Scale = new Vector2(0.92f, 0.92f);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_rootPanel, "modulate:a", 1.0f, PopInDuration);
        tween.TweenProperty(_card, "scale", Vector2.One, PopInDuration)
             .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    async void AnimatePopOut()
    {
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_rootPanel, "modulate:a", 0.0f, PopOutDuration);
        tween.TweenProperty(_card, "scale", new Vector2(0.92f, 0.92f), PopOutDuration)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

        await ToSignal(tween, Tween.SignalName.Finished);
        _rootPanel.Visible = false;
        _pendingScreenshot = null;   // 下次從「讀檔」進來的保存會重新截圖，不沿用過期畫面
        _onCloseCallback?.Invoke();
    }
}
