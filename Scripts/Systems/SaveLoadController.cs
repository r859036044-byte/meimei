using Godot;
using System;
using System.IO;

/// <summary>
/// 存讀檔控制器
/// 程式化生成 UI，風格與 SettingsController 一致（羊皮紙棕色 + 菱格紋）
/// 在 Godot 中使用 Control 節點樹建構 UI
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
    //  面板尺寸設定
    // ═══════════════════════════════════════════════════════════
    public float PanelWidth       = 1100f;
    public float PanelHeight      = 720f;
    public float BorderWidth      = 4f;
    public float TitleBarHeight   = 55f;
    public int   TitleFontSize    = 34;
    public int   TabFontSize      = 24;
    public float TabHeight        = 42f;
    public float TabWidth         = 140f;
    public float TabGap           = 6f;
    public float TabTopMargin     = 15f;
    public int   SlotCount        = 3;
    public float SlotHeight       = 130f;
    public float SlotSpacing      = 12f;
    public float SlotPadding      = 30f;
    public float ThumbnailWidth   = 200f;
    public float ThumbnailHeight  = 112f;
    public float DeleteButtonSize = 32f;
    public int   SlotNameFontSize = 24;
    public int   SlotTimeFontSize = 20;
    public int   EmptySlotFontSize = 22;

    // ═══════════════════════════════════════════════════════════
    //  顏色主題
    // ═══════════════════════════════════════════════════════════
    public Color OverlayColor         = new Color(0f, 0f, 0f, 0.55f);
    public Color BorderColor          = new Color(0.22f, 0.13f, 0.07f, 1f);
    public Color PanelBgColor         = new Color(0.91f, 0.86f, 0.77f, 1f);
    public Color TitleBarColor        = new Color(0.30f, 0.18f, 0.10f, 1f);
    public Color TitleTextColor       = new Color(0.95f, 0.90f, 0.80f, 1f);
    public Color CloseButtonBgColor   = new Color(0.55f, 0.20f, 0.15f, 1f);
    public Color CloseButtonTextColor = Colors.White;
    public Color TabActiveColor       = new Color(0.91f, 0.86f, 0.77f, 1f);
    public Color TabInactiveColor     = new Color(0.55f, 0.40f, 0.25f, 1f);
    public Color TabActiveTextColor   = new Color(0.25f, 0.15f, 0.08f, 1f);
    public Color TabInactiveTextColor = new Color(0.90f, 0.85f, 0.75f, 1f);
    public Color SlotBgColor          = new Color(0.85f, 0.79f, 0.68f, 1f);
    public Color SlotTextColor        = new Color(0.25f, 0.15f, 0.08f, 1f);
    public Color SlotTimeColor        = new Color(0.45f, 0.35f, 0.22f, 1f);
    public Color EmptySlotTextColor   = new Color(0.55f, 0.45f, 0.35f, 1f);
    public Color DeleteButtonColor    = new Color(0.70f, 0.18f, 0.15f, 1f);

    // ═══════════════════════════════════════════════════════════
    //  動畫
    // ═══════════════════════════════════════════════════════════
    public float PopInDuration  = 0.3f;
    public float PopOutDuration = 0.2f;

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
    private Mode _currentMode = Mode.Save;
    private CanvasLayer _canvasLayer;
    private Control _rootPanel;
    private Action _onCloseCallback;

    // UI 引用
    private ColorRect _saveTabBg, _loadTabBg;
    private Label _saveTabLabel, _loadTabLabel;
    private SlotUI[] _slots;
    private Image _pendingScreenshot;

    // 存檔路徑
    private string SaveDir => Path.Combine(OS.GetUserDataDir(), "Saves");

    struct SlotUI
    {
        public Control root;
        public TextureRect thumbnail;
        public Label nameLabel;
        public Label timeLabel;
        public Label emptyLabel;
        public Control deleteBtn;
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
        RefreshAllSlots();
        UpdateTabVisuals();
        _rootPanel.Visible = true;
        AnimatePopIn();
        _isOpen = true;
    }

    /// <summary>截取螢幕後開啟存檔面板</summary>
    public async void CaptureAndOpen(Action onClose = null)
    {
        // 等待一幀確保畫面渲染完成
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        // 截取螢幕
        _pendingScreenshot = GetViewport().GetTexture().GetImage();

        Open(Mode.Save, onClose);
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        AnimatePopOut();
    }

    public bool IsOpen() => _isOpen;

    // ═══════════════════════════════════════════════════════════
    //  輸入處理 — ESC 關閉
    // ═══════════════════════════════════════════════════════════
    public override void _Input(InputEvent @event)
    {
        if (!_isOpen) return;
        if (@event is InputEventKey key && key.Keycode == Key.Escape && key.Pressed)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  BuildUI
    // ═══════════════════════════════════════════════════════════
    void BuildUI()
    {
        _isBuilt = true;

        // --- CanvasLayer（高排序值確保在最上層）---
        _canvasLayer = new CanvasLayer();
        _canvasLayer.Layer = 140;
        AddChild(_canvasLayer);

        // --- 根面板（全螢幕）---
        _rootPanel = new Control();
        _rootPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _canvasLayer.AddChild(_rootPanel);

        // --- 半透明遮罩 ---
        var overlay = new ColorRect();
        overlay.Color = OverlayColor;
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // 點擊遮罩不做任何事（阻擋背景互動）
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;
        _rootPanel.AddChild(overlay);

        // --- 外框 ---
        var border = new ColorRect();
        border.Color = BorderColor;
        border.CustomMinimumSize = new Vector2(PanelWidth + BorderWidth * 2, PanelHeight + BorderWidth * 2);
        border.Size = border.CustomMinimumSize;
        border.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        border.Position = new Vector2(
            (1920f - border.Size.X) / 2f,
            (1080f - border.Size.Y) / 2f
        );
        _rootPanel.AddChild(border);

        // --- 面板底色 ---
        var panelBg = new ColorRect();
        panelBg.Color = PanelBgColor;
        panelBg.Size = new Vector2(PanelWidth, PanelHeight);
        panelBg.Position = new Vector2(BorderWidth, BorderWidth);
        border.AddChild(panelBg);

        // --- 標題列 ---
        var titleBar = new ColorRect();
        titleBar.Color = TitleBarColor;
        titleBar.Size = new Vector2(PanelWidth, TitleBarHeight);
        titleBar.Position = Vector2.Zero;
        panelBg.AddChild(titleBar);

        var titleLabel = new Label();
        titleLabel.Text = "保存 / 讀檔";
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.VerticalAlignment = VerticalAlignment.Center;
        titleLabel.Size = new Vector2(PanelWidth, TitleBarHeight);
        titleLabel.AddThemeFontSizeOverride("font_size", TitleFontSize);
        titleLabel.AddThemeColorOverride("font_color", TitleTextColor);
        titleBar.AddChild(titleLabel);

        // 關閉按鈕
        var closeBtn = new Button();
        closeBtn.Text = "✕";
        closeBtn.Size = new Vector2(44, 44);
        closeBtn.Position = new Vector2(PanelWidth - 54, (TitleBarHeight - 44) / 2f);
        closeBtn.AddThemeFontSizeOverride("font_size", 28);
        closeBtn.Pressed += () => Close();
        titleBar.AddChild(closeBtn);

        // --- 標籤頁 ---
        float tabY = TitleBarHeight + TabTopMargin;
        float tabTotalWidth = TabWidth * 2 + TabGap;
        float tabStartX = (PanelWidth - tabTotalWidth) / 2f;

        // 保存標籤
        _saveTabBg = new ColorRect();
        _saveTabBg.Color = TabActiveColor;
        _saveTabBg.Size = new Vector2(TabWidth, TabHeight);
        _saveTabBg.Position = new Vector2(tabStartX, tabY);
        panelBg.AddChild(_saveTabBg);

        _saveTabLabel = new Label();
        _saveTabLabel.Text = "保存";
        _saveTabLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _saveTabLabel.VerticalAlignment = VerticalAlignment.Center;
        _saveTabLabel.Size = new Vector2(TabWidth, TabHeight);
        _saveTabLabel.AddThemeFontSizeOverride("font_size", TabFontSize);
        _saveTabLabel.AddThemeColorOverride("font_color", TabActiveTextColor);
        _saveTabBg.AddChild(_saveTabLabel);

        // 保存標籤點擊
        var saveTabBtn = new Button();
        saveTabBtn.Flat = true;
        saveTabBtn.Size = new Vector2(TabWidth, TabHeight);
        saveTabBtn.Pressed += () => SwitchMode(Mode.Save);
        _saveTabBg.AddChild(saveTabBtn);

        // 讀檔標籤
        _loadTabBg = new ColorRect();
        _loadTabBg.Color = TabInactiveColor;
        _loadTabBg.Size = new Vector2(TabWidth, TabHeight);
        _loadTabBg.Position = new Vector2(tabStartX + TabWidth + TabGap, tabY);
        panelBg.AddChild(_loadTabBg);

        _loadTabLabel = new Label();
        _loadTabLabel.Text = "讀檔";
        _loadTabLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _loadTabLabel.VerticalAlignment = VerticalAlignment.Center;
        _loadTabLabel.Size = new Vector2(TabWidth, TabHeight);
        _loadTabLabel.AddThemeFontSizeOverride("font_size", TabFontSize);
        _loadTabLabel.AddThemeColorOverride("font_color", TabInactiveTextColor);
        _loadTabBg.AddChild(_loadTabLabel);

        var loadTabBtn = new Button();
        loadTabBtn.Flat = true;
        loadTabBtn.Size = new Vector2(TabWidth, TabHeight);
        loadTabBtn.Pressed += () => SwitchMode(Mode.Load);
        _loadTabBg.AddChild(loadTabBtn);

        // --- 存檔槽 ---
        _slots = new SlotUI[SlotCount];
        float slotAreaTop = tabY + TabHeight + SlotSpacing;
        float slotW = PanelWidth - SlotPadding * 2;

        for (int i = 0; i < SlotCount; i++)
        {
            float slotY = slotAreaTop + i * (SlotHeight + SlotSpacing);
            _slots[i] = CreateSlot(panelBg, i, slotW, slotY);
        }

        _rootPanel.Visible = false;
    }

    SlotUI CreateSlot(Control parent, int index, float width, float yPos)
    {
        var slot = new SlotUI();

        // 底色
        var slotBg = new ColorRect();
        slotBg.Color = SlotBgColor;
        slotBg.Size = new Vector2(width, SlotHeight);
        slotBg.Position = new Vector2(SlotPadding, yPos);
        parent.AddChild(slotBg);
        slot.root = slotBg;

        // 整個槽位可點擊
        int idx = index;
        var slotBtn = new Button();
        slotBtn.Flat = true;
        slotBtn.Size = new Vector2(width, SlotHeight);
        slotBtn.Pressed += () => OnSlotClicked(idx);
        slotBg.AddChild(slotBtn);

        // 縮略圖
        float thumbX = 15f;
        var thumbRect = new TextureRect();
        thumbRect.Size = new Vector2(ThumbnailWidth, ThumbnailHeight);
        thumbRect.Position = new Vector2(thumbX, (SlotHeight - ThumbnailHeight) / 2f);
        thumbRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
        slotBg.AddChild(thumbRect);
        slot.thumbnail = thumbRect;

        // 存檔名稱
        float textX = thumbX + ThumbnailWidth + 20f;
        var nameLabel = new Label();
        nameLabel.Text = $"存檔 {index + 1}";
        nameLabel.Size = new Vector2(width - ThumbnailWidth - 80f, 30f);
        nameLabel.Position = new Vector2(textX, SlotHeight / 2f - 25f);
        nameLabel.AddThemeFontSizeOverride("font_size", SlotNameFontSize);
        nameLabel.AddThemeColorOverride("font_color", SlotTextColor);
        slotBg.AddChild(nameLabel);
        slot.nameLabel = nameLabel;

        // 時間戳
        var timeLabel = new Label();
        timeLabel.Text = "";
        timeLabel.Size = new Vector2(width - ThumbnailWidth - 80f, 26f);
        timeLabel.Position = new Vector2(textX, SlotHeight / 2f + 5f);
        timeLabel.AddThemeFontSizeOverride("font_size", SlotTimeFontSize);
        timeLabel.AddThemeColorOverride("font_color", SlotTimeColor);
        slotBg.AddChild(timeLabel);
        slot.timeLabel = timeLabel;

        // 空槽位提示
        var emptyLabel = new Label();
        emptyLabel.Text = "— 空 —";
        emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        emptyLabel.VerticalAlignment = VerticalAlignment.Center;
        emptyLabel.Size = new Vector2(width, SlotHeight);
        emptyLabel.AddThemeFontSizeOverride("font_size", EmptySlotFontSize);
        emptyLabel.AddThemeColorOverride("font_color", EmptySlotTextColor);
        slotBg.AddChild(emptyLabel);
        slot.emptyLabel = emptyLabel;

        // 刪除按鈕
        var delBtn = new Button();
        delBtn.Text = "✕";
        delBtn.Size = new Vector2(DeleteButtonSize, DeleteButtonSize);
        delBtn.Position = new Vector2(width - 15f - DeleteButtonSize, 15f);
        delBtn.AddThemeFontSizeOverride("font_size", 18);
        delBtn.Pressed += () => DeleteSlot(idx);
        slotBg.AddChild(delBtn);
        slot.deleteBtn = delBtn;

        return slot;
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
        _saveTabBg.Color    = isSave ? TabActiveColor    : TabInactiveColor;
        _saveTabLabel.AddThemeColorOverride("font_color", isSave ? TabActiveTextColor : TabInactiveTextColor);
        _loadTabBg.Color    = !isSave ? TabActiveColor   : TabInactiveColor;
        _loadTabLabel.AddThemeColorOverride("font_color", !isSave ? TabActiveTextColor : TabInactiveTextColor);
    }

    // ═══════════════════════════════════════════════════════════
    //  槽位操作
    // ═══════════════════════════════════════════════════════════
    void OnSlotClicked(int index)
    {
        if (_currentMode == Mode.Save)
            SaveToSlot(index);
        else
            LoadFromSlot(index);
    }

    void SaveToSlot(int index)
    {
        EnsureSaveDir();

        // 取得遊戲狀態
        SaveData data = OnGetSaveData?.Invoke();
        if (data == null)
        {
            data = new SaveData();
            data.sceneName = GetTree().CurrentScene.SceneFilePath;
        }
        data.saveTime = DateTime.Now.ToString("yyyy/MM/dd  HH:mm:ss");
        data.dialogueVars ??= PlayerDataManager.Instance?.GetAllDialogueVars();

        // 儲存 JSON
        string json = System.Text.Json.JsonSerializer.Serialize(data,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(GetSavePath(index), json);

        // 儲存截圖
        if (_pendingScreenshot != null)
        {
            _pendingScreenshot.SavePng(GetScreenshotPath(index));
        }

        GD.Print($"[SaveLoad] 已儲存到槽位 {index + 1}: {data.saveTime}");
        RefreshSlot(index);
    }

    void LoadFromSlot(int index)
    {
        string path = GetSavePath(index);
        if (!System.IO.File.Exists(path))
        {
            GD.Print($"[SaveLoad] 槽位 {index + 1} 是空的");
            return;
        }

        string json = System.IO.File.ReadAllText(path);
        SaveData data;
        try
        {
            data = System.Text.Json.JsonSerializer.Deserialize<SaveData>(json);
        }
        catch (Exception e)
        {
            GD.PrintErr($"[SaveLoad] 讀取槽位 {index + 1} 失敗: {e.Message}");
            return;
        }

        GD.Print($"[SaveLoad] 讀取槽位 {index + 1}: {data.saveTime}");
        PlayerDataManager.Instance?.SetAllDialogueVars(data.dialogueVars);
        Close();

        // 呼叫讀檔回調
        OnApplyLoadData?.Invoke(data);
    }

    void DeleteSlot(int index)
    {
        string savePath = GetSavePath(index);
        string ssPath   = GetScreenshotPath(index);
        if (System.IO.File.Exists(savePath)) System.IO.File.Delete(savePath);
        if (System.IO.File.Exists(ssPath))   System.IO.File.Delete(ssPath);
        GD.Print($"[SaveLoad] 已刪除槽位 {index + 1}");
        RefreshSlot(index);
    }

    // ═══════════════════════════════════════════════════════════
    //  槽位顯示刷新
    // ═══════════════════════════════════════════════════════════
    void RefreshAllSlots()
    {
        for (int i = 0; i < SlotCount; i++)
            RefreshSlot(i);
    }

    void RefreshSlot(int index)
    {
        if (index < 0 || index >= _slots.Length) return;
        var slot = _slots[index];

        string savePath = GetSavePath(index);
        bool hasData = System.IO.File.Exists(savePath);

        if (hasData)
        {
            string json = System.IO.File.ReadAllText(savePath);
            SaveData data = null;
            try { data = System.Text.Json.JsonSerializer.Deserialize<SaveData>(json); }
            catch { /* 忽略解析錯誤 */ }

            slot.nameLabel.Visible  = true;
            slot.timeLabel.Visible  = true;
            slot.emptyLabel.Visible = false;
            slot.deleteBtn.Visible  = true;

            slot.nameLabel.Text = $"存檔 {index + 1}";
            slot.timeLabel.Text = data?.saveTime ?? "";

            // 載入截圖
            string ssPath = GetScreenshotPath(index);
            if (System.IO.File.Exists(ssPath))
            {
                var img = new Image();
                img.Load(ssPath);
                var tex = ImageTexture.CreateFromImage(img);
                slot.thumbnail.Texture = tex;
            }
            else
            {
                slot.thumbnail.Texture = null;
            }
        }
        else
        {
            slot.nameLabel.Visible  = false;
            slot.timeLabel.Visible  = false;
            slot.emptyLabel.Visible = true;
            slot.deleteBtn.Visible  = false;
            slot.thumbnail.Texture  = null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  檔案路徑
    // ═══════════════════════════════════════════════════════════
    void EnsureSaveDir()
    {
        if (!System.IO.Directory.Exists(SaveDir))
            System.IO.Directory.CreateDirectory(SaveDir);
    }

    string GetSavePath(int index)       => Path.Combine(SaveDir, $"save_{index}.json");
    string GetScreenshotPath(int index) => Path.Combine(SaveDir, $"save_{index}.png");

    // ═══════════════════════════════════════════════════════════
    //  彈出 / 收回動畫
    // ═══════════════════════════════════════════════════════════
    void AnimatePopIn()
    {
        _rootPanel.Modulate = new Color(1, 1, 1, 0);
        _rootPanel.Scale = new Vector2(0.8f, 0.8f);
        _rootPanel.PivotOffset = _rootPanel.Size / 2f;

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_rootPanel, "modulate:a", 1.0f, PopInDuration);
        tween.TweenProperty(_rootPanel, "scale", Vector2.One, PopInDuration)
             .SetTrans(Tween.TransitionType.Back)
             .SetEase(Tween.EaseType.Out);
    }

    async void AnimatePopOut()
    {
        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_rootPanel, "modulate:a", 0.0f, PopOutDuration);
        tween.TweenProperty(_rootPanel, "scale", new Vector2(0.8f, 0.8f), PopOutDuration)
             .SetTrans(Tween.TransitionType.Quad)
             .SetEase(Tween.EaseType.In);

        await ToSignal(tween, Tween.SignalName.Finished);
        _rootPanel.Visible = false;
        _onCloseCallback?.Invoke();
    }
}
