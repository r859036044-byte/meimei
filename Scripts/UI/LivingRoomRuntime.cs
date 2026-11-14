using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


/// <summary>
/// LivingRoom 場景的主控腳本（Godot 4 port）
/// </summary>
public partial class LivingRoomRuntime : Node
{
    NovelReader _reader;
    void BeginReadingLine(string text)
    {
        _reader ??= NovelReader.Install(this, _dialogueBox, _contentText, _btnBarGO,
            () => _isTyping, () => _waitingClick, () => _lockClick,
            () => _skipTyping = true, () => _waitingClick = false, null);
        _reader.SetSpeakerBadge(_nameBarGO, _speakerText);
        _reader.BeginLine(_speakerText?.Text, text);
    }

    // ── 初次進入判斷 ─────────────────────────────────────────────
    static readonly HashSet<string> _playedDialogues = new();
    static bool HasDialoguePlayed(string id) => _playedDialogues.Contains(id);
    /// <summary>PrologueLast 還沒播過（第一次從 TrainingRoom 出來時要先接這段，再進 Day2）。</summary>
    public static bool PrologueLastPending => !HasDialoguePlayed("PrologueLast");
    static void MarkDialoguePlayed(string id) => _playedDialogues.Add(id);
    /// <summary>開新遊戲時清掉「已播過」紀錄與待播旗標（NewGameReset 呼叫）</summary>
    public static void ResetForNewGame()
    {
        _playedDialogues.Clear();
        PendingDay3 = false;
        PendingMesugaki = false;
        ReplayAAEnding = 0;
    }

    public static bool PendingDay3 = false;
    /// <summary>雌小鬼值滿 100（心情沒歸 0）→ 離開 TrainingRoom 後的隔天早上，直接播雌小鬼大小姐結局（Day3_Start 開頭那段）。</summary>
    public static bool PendingMesugaki = false;
    bool _mesugakiRun;

    /// <summary>圖鑒重播全年齡結局用：1~5（由 GalleryScreen 在開啟重播前設定）</summary>
    public static int ReplayAAEnding = 0;
    bool _aaEndingDone;          // 全年齡結局標題已播完 → 這條路線結束
    bool _aaReplay;              // 目前是圖鑒重播
    int  _forcedChoice = -1;     // 重播時自動選的選項
    System.Collections.Generic.Dictionary<string, int> _varsBeforeReplay;

    // ── UI 節點 ───────────────────────────────────────────────────
    Label _speakerText;
    Label _contentText;
    Control _panel;
    TextureRect _charBodyImage;
    SpineChar _spineChar;   // Spine 動態立繪（掛在身體圖底下，跟著它的位置／縮放／淡入淡出）
    TextureRect _charFaceImage;
    TextureRect _char2Image;
    Panel _nameBarPanel;
    StyleBoxFlat _nameBarStyle;
    TextureRect _bgImage;
    ColorRect _fadeOverlay;
    Control _dialogueBox;
    Control _nameBarGO;
    Control _btnBarGO;
    bool _screenBlack;
    Color _screenFadeColor = Colors.Black;

    // ── 選項 ─────────────────────────────────────────────────────
    Control _choicePanel;
    int _choiceResult = -1;
    string _gotoAllAges; // 文本裡的 gotoIfAllAges 目標（全年齡模式用）

    // ── 音訊 ─────────────────────────────────────────────────────
    AudioStreamPlayer _bgmPlayer;
    AudioStreamPlayer _sePlayer;
    AudioStreamPlayer _mePlayer;
    float _bgmLinearVol = 1f;
    float _seLinearVol  = 1f;
    float _meLinearVol  = 1f;

    // ── 打字機 / 點擊控制 ────────────────────────────────────────
    bool _waitingClick;
    bool _lockClick;
    bool _isTyping;
    bool _skipTyping;
    bool _fastForward;
    bool _mouseHoldActive;
    float _mouseHoldTime;
    int  _autoPlaySpeed;
    bool _autoAdvance;
    Label _fastFwdBtnTxt;
    Label _autoPlayBtnTxt;
    bool _wasMousePressed;

    // ── 角色位置狀態 ─────────────────────────────────────────────
    Vector2 _charBodySize   = new(650f, 1338f);
    Vector2 _charBodyOffset = Vector2.Zero;
    Vector2 _char2Size      = new(650f, 1338f);
    Vector2 _char2Offset    = Vector2.Zero;

    // ── CG 圖層 ──────────────────────────────────────────────────
    float[]       _cgScale  = new float[DialogueSystem.CG_LAYER_COUNT];
    Vector2[]     _cgOffset = new Vector2[DialogueSystem.CG_LAYER_COUNT];
    TextureRect[] _cgImages = new TextureRect[DialogueSystem.CG_LAYER_COUNT];

    // ── CanvasLayer ──────────────────────────────────────────────
    CanvasLayer _canvas;

    // ── 迷你遊戲 ─────────────────────────────────────────────────
    bool _mgRunning;
    Control _mgJijiCtrl;
    Control _mgMkCtrl;
    Control _mgTipCtrl;
    Control _mgBottomCtrl;
    Control _mgUterusCtrl;
    Control _mgFrameCtrl;
    MiniGameNight3Config _mgCfg;

    // ── 迷你遊戲點擊 ─────────────────────────────────────────────
    bool _mgClicked;
    int  _mgActionResult = -1;

    // ─────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        _canvas = new CanvasLayer { Layer = 10 };
        AddChild(_canvas);

        _bgmPlayer = new AudioStreamPlayer(); AddChild(_bgmPlayer);
        _sePlayer  = new AudioStreamPlayer(); AddChild(_sePlayer);
        _mePlayer  = new AudioStreamPlayer(); AddChild(_mePlayer);
        _bgmPlayer.VolumeDb = Mathf.LinearToDb(1f);
        _sePlayer.VolumeDb  = Mathf.LinearToDb(1f);
        _mePlayer.VolumeDb  = Mathf.LinearToDb(1f);
        SceneBgm.Install(this, SceneBgm.Morning, _bgmPlayer);

        for (int i = 0; i < DialogueSystem.CG_LAYER_COUNT; i++)
            _cgScale[i] = 1f;

        TaskLogger.Run(StartAsync(), "LivingRoom.StartAsync");
    }

    public override void _Process(double delta)
    {
        // NovelReader 統一處理點擊、自動及快進。
    }

    // ─────────────────────────────────────────────────────────────
    // 入場路由
    // ─────────────────────────────────────────────────────────────
    async Task StartAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        BuildUI();
        _panel.Visible = true;

        int replayN = ReplayAAEnding;
        ReplayAAEnding = 0;
        if (replayN > 0 && GalleryReplaySession.Active != null)
        {
            await ReplayAAEndingAsync(replayN);
            return;
        }

        var pdm       = PlayerDataManager.Instance;
        int night     = pdm.CurrentNight;
        int idleCount = pdm.IdleNightCount;
        bool noInsert = pdm.VaginaUseCount == 0;

        if (PendingMesugaki)
        {   // 雌小鬼大小姐結局：隔天早上直接播，播完回標題
            PendingMesugaki = false;
            _mesugakiRun = true;
            await PlayDialogueAsync("Day3/Day3_Start");
            await FinishAAEndingAsync();
            return;
        }

        if (PendingDay3)
        {
            PendingDay3 = false;
            if (TimingChallenge.Eligible(night, idleCount))
            {
                pdm.CurrentNight = 3;
                await MiniGameNight3Async();
            }
            else if (night >= 2)
            {
                // 有摸過→播「魚夢早晨」，否則播「道歉早晨」
                bool hadTouchMorning = TrainingRoomRuntime.HadBreastTouch;
                NormalizeAAEndingVars();   // 全年齡：結局 4 已拿掉，先把會落到結局 4 的變數組合改掉
                if (hadTouchMorning)
                    await PlayDialogueAsync("Day3/Day3_Morning_Touch");
                else
                    await PlayDialogueAsync("Day3/Day3_Start");
                if (_aaEndingDone) { await FinishAAEndingAsync(); return; }
                await ScreenFadeAsync(true, 1f, Colors.Black);
                await DetermineEndingAsync();
            }
            else
            {
                if (noInsert && TrainingRoomRuntime.HadBreastTouch && TrainingRoomRuntime.BreastComfortRoute)
                    await PlayDialogueAsync("NothingRoute/Breast_Good");
                else if (noInsert && TrainingRoomRuntime.HadBreastTouch)
                    await PlayDialogueAsync("NothingRoute/Breast_Bad");

                if (idleCount >= 1)
                    await PlayDialogueAsync("NothingRoute/Day2startday");

                await PlayDialogueAsync("NothingRoute/Day2_Start3");
                if (PlayerDataManager.Instance?.R18ContentEnabled == true)
                {
                    TrainingRoomRuntime.PendingDay3First = true;
                    await ScreenFadeAsync(true, 1f, Colors.Black);
                    await SceneLoader.GoToAsync(this, "res://Scenes/TrainingRoom.tscn");
                }
                else
                {
                    GD.Print("[LivingRoom] 全年齡模式：略過 TrainingRoom，顯示蓋被子選擇");
                    if (pdm != null) { pdm.ResetTodayTracking(); pdm.CurrentNight = 2; }
                    await AllAgesChoiceHelper.ShowBlanketChoiceAsync(this);
                    PendingDay3 = true;
                    await ScreenFadeAsync(true, 1f, Colors.Black);
                    GetTree().ChangeSceneToFile("res://Scenes/LivingRoom.tscn");
                }
            }
        }
        else if (!HasDialoguePlayed("PrologueLast"))
        {
            await PlayDialogueAsync("Prologue/PrologueLast");
            MarkDialoguePlayed("PrologueLast");
            _gotoAllAges = null;
            // 全年齡：第 2 晚蓋不蓋被子不再讓玩家選，由心情／雌小鬼值決定（有數值紀錄才自動選；沒有就照原本讓玩家選）
            if (!(PlayerDataManager.Instance?.R18ContentEnabled ?? false) && (PlayerDataManager.Instance?.GetDialogueVar("BratInit") ?? 0) != 0)
            {
                var pd = PlayerDataManager.Instance;
                _forcedChoice = StoryTreeData.AAEndingPath(StoryTreeData.PickAAEndingByStats(pd.GetDialogueVar("Mood"), pd.GetDialogueVar("Brat"))).d2Cover ? 0 : 1;   // 0＝蓋被子、1＝不蓋
            }
            await PlayDialogueAsync("Day2/Day2_Start2", startBlack: true);
            if (!string.IsNullOrEmpty(_gotoAllAges))
            {
                await PlayAllAgesGotoAsync();
                return;
            }
            await ScreenFadeAsync(true, 1f, Colors.Black);
            GetTree().ChangeSceneToFile("res://Scenes/Apartment.tscn");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 全年齡結局：結束路線 / 圖鑒重播
    // ─────────────────────────────────────────────────────────────
    async Task FinishAAEndingAsync()
    {
        _aaEndingDone = false;
        SetDialogueUIVisible(false);
        if (_aaReplay || GalleryReplaySession.Active != null)
        {
            GalleryReplaySession.Active?.Close();
            return;
        }
        if (!_screenBlack) await ScreenFadeAsync(true, 1f, Colors.Black);
        MainMenuRuntime.SkipIntroOnce = true;   // 結局播完回「標題畫面」，不要重播 LOGO 和開頭動畫
        GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
    }

    async Task ReplayAAEndingAsync(int n)
    {
        if (n < 1 || n >= StoryTreeData.AAEndings.Length) { GalleryReplaySession.Active?.Close(); return; }
        var e = StoryTreeData.AAEndings[n];
        var pdm = PlayerDataManager.Instance;
        _aaReplay = true;
        // 暫存玩家目前的劇情變數，重播結束（_ExitTree）時還原，不影響正式進度
        _varsBeforeReplay = pdm?.GetAllDialogueVars();
        pdm?.SetAllDialogueVars(new System.Collections.Generic.Dictionary<string, int> { [e.Day1Var] = 1, [e.Day2Var] = 1 });
        _forcedChoice = e.Pick;
        if (e.Day1Var == StoryTreeData.AAEndingBadEndVar) await PlayDialogueAsync("Endings/Day2_Start(bad end)");   // 結局7：斷線的風箏
        else await PlayDialogueAsync("Day3/Day3_Start");
        await FinishAAEndingAsync();
    }

    public override void _ExitTree()
    {
        if (_aaReplay && _varsBeforeReplay != null)
            PlayerDataManager.Instance?.SetAllDialogueVars(_varsBeforeReplay);
    }

    // ─────────────────────────────────────────────────────────────
    // 結局標題（移植自 EndingBaseRuntime，逐字交錯淡入 + 副標題）
    // ─────────────────────────────────────────────────────────────
    Control _endTitleContainer;

    async Task ShowEndTitleAsync(string title, string subtitle)
    {
        if (_endTitleContainer != null) { _endTitleContainer.QueueFree(); _endTitleContainer = null; }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var container = new Control { Name = "EndTitle", MouseFilter = Control.MouseFilterEnum.Ignore };
        container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _panel.AddChild(container);
        _panel.MoveChild(container, _panel.GetChildCount() - 1); // 最上層（黑屏之上）
        _endTitleContainer = container;

        var tmpLabel = new Label();
        container.AddChild(tmpLabel);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        int fontSize = 90;
        var font = tmpLabel.GetThemeFont("font");
        tmpLabel.QueueFree();

        title ??= "";
        int n = title.Length;
        float[] widths = new float[n];
        float totalW = 0f;
        for (int i = 0; i < n; i++)
        {
            widths[i] = font.GetStringSize(title[i].ToString(), HorizontalAlignment.Left, -1, fontSize).X;
            totalW += widths[i];
        }
        float accum = -totalW / 2f;
        var chars = new Label[n];
        for (int i = 0; i < n; i++)
        {
            float cx = accum + widths[i] / 2f;
            accum += widths[i];
            var lbl = new Label
            {
                Text = title[i].ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                AutowrapMode        = TextServer.AutowrapMode.Off,
                MouseFilter         = Control.MouseFilterEnum.Ignore,
            };
            lbl.AddThemeFontSizeOverride("font_size", fontSize);
            lbl.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0));
            lbl.AnchorLeft = lbl.AnchorRight = 0.5f;
            lbl.AnchorTop  = lbl.AnchorBottom = 0.5f;
            float hw = (widths[i] + 10f) / 2f;
            lbl.OffsetLeft = cx - hw; lbl.OffsetRight = cx + hw;
            lbl.OffsetTop  = -50f - 65f; lbl.OffsetBottom = -50f + 65f;
            container.AddChild(lbl);
            chars[i] = lbl;
        }

        float perCharDur = 0.6f, stagger = 0.12f;
        float total = perCharDur + stagger * Mathf.Max(n - 1, 0);
        float elapsed = 0f;
        while (elapsed < total)
        {
            elapsed += (float)GetProcessDeltaTime();
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Clamp((elapsed - stagger * i) / perCharDur, 0f, 1f);
                chars[i].AddThemeColorOverride("font_color", new Color(1, 1, 1, a));
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        foreach (var c in chars) c.AddThemeColorOverride("font_color", Colors.White);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var sub = new Label
            {
                Text = subtitle,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                AutowrapMode        = TextServer.AutowrapMode.Off,
                MouseFilter         = Control.MouseFilterEnum.Ignore,
            };
            sub.AddThemeFontSizeOverride("font_size", 42);
            sub.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, 0));
            sub.AnchorLeft = sub.AnchorRight = 0.5f;
            sub.AnchorTop  = sub.AnchorBottom = 0.5f;
            sub.OffsetLeft = -400f; sub.OffsetRight = 400f;
            sub.OffsetTop  = 25f;   sub.OffsetBottom = 95f;
            sub.GrowHorizontal = Control.GrowDirection.Both;
            container.AddChild(sub);
            elapsed = 0f;
            while (elapsed < 0.8f)
            {
                elapsed += (float)GetProcessDeltaTime();
                sub.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, Mathf.Clamp(elapsed / 0.8f, 0f, 1f)));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            sub.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, 1f));
        }
    }

    // 結局標題顯示時台詞框是關的，NovelReader 不會接收點擊，這裡自己等點擊／空白鍵／Enter
    async Task WaitTitleClickAsync(float autoNext)
    {
        if (autoNext > 1f) { await TimerAsync(autoNext / 60f); return; }
        bool wasDown = true; // 進來時若還按著，要先放開才算一次新的點擊
        while (true)
        {
            bool down = Input.IsMouseButtonPressed(MouseButton.Left)
                     || Input.IsKeyPressed(Key.Space) || Input.IsKeyPressed(Key.Enter);
            if (down && !wasDown) break;
            wasDown = down;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    async Task FadeOutEndTitleAsync(float duration = 2.5f)   // 結局標題顯示完之後慢慢淡出的秒數
    {
        if (_endTitleContainer == null) return;
        var labels = new List<Label>();
        foreach (Node child in _endTitleContainer.GetChildren())
            if (child is Label lbl) labels.Add(lbl);
        float[] start = new float[labels.Count];
        for (int i = 0; i < labels.Count; i++) start[i] = labels[i].GetThemeColor("font_color").A;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += (float)GetProcessDeltaTime();
            float k = Mathf.Clamp(elapsed / duration, 0f, 1f);
            for (int i = 0; i < labels.Count; i++)
            {
                var c = labels[i].GetThemeColor("font_color");
                labels[i].AddThemeColorOverride("font_color", new Color(c.R, c.G, c.B, Mathf.Lerp(start[i], 0f, k)));
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _endTitleContainer.QueueFree();
        _endTitleContainer = null;
    }

    // ─────────────────────────────────────────────────────────────
    // 全年齡：文本裡 gotoIfAllAges 指定的跳轉
    // - 值是對話檔（Resources/Data/Dialogues/<值>.json）→ 直接接著播放，
    //   旗標比照原本全年齡流程（蓋被子按鈕會設的高潮路線、第2晚），播完走一般第三天結局判定
    // - 值是場景名（Scenes/<值>.tscn，跟序章一樣）→ 切換場景
    // ─────────────────────────────────────────────────────────────
    async Task PlayAllAgesGotoAsync()
    {
        string target = _gotoAllAges;
        _gotoAllAges = null;
        GD.Print($"[LivingRoom] 全年齡跳轉 → {target}");

        if (target == "TrainingRoom")
        {   // Day2 晚上（原本「蓋被子／不蓋被子」選擇的位置）：全年齡也進調教室做第 2 晚的互動（互動受 AllAgesLocked 限制），做完回客廳播隔天早上的結局
            var pd2 = PlayerDataManager.Instance;
            if (pd2 != null) { pd2.ResetTodayTracking(); pd2.CurrentNight = 2; }
            TrainingRoomRuntime.PendingDay3First = true;   // 第 2 晚首次進場（跟 R18 進第 2 晚調教室同一個旗標）
            await ScreenFadeAsync(true, 1f, Colors.Black);
            await SceneLoader.GoToAsync(this, "res://Scenes/TrainingRoom.tscn");
            return;
        }

        if (FileAccess.FileExists($"res://Resources/Data/Dialogues/{target}.json"))
        {
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) { pdm.ResetTodayTracking(); pdm.CurrentNight = 2; }
            AllAgesChoiceHelper.SetClimaxFlags();
            await PlayDialogueAsync(target);
            if (_aaEndingDone) { await FinishAAEndingAsync(); return; }
            await ScreenFadeAsync(true, 1f, Colors.Black);
            await DetermineEndingAsync();
            return;
        }

        string scene = $"res://Scenes/{target}.tscn";
        await ScreenFadeAsync(true, 1f, Colors.Black);
        if (ResourceLoader.Exists(scene)) GetTree().ChangeSceneToFile(scene);
        else
        {
            GD.PrintErr($"[LivingRoom] gotoIfAllAges 找不到：{target}（對話檔或場景都不存在），改走原本流程");
            GetTree().ChangeSceneToFile("res://Scenes/Apartment.tscn");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 通用對話播放
    // ─────────────────────────────────────────────────────────────
    /// <summary>全年齡：結局 4（無聲的拒絕）已拿掉。只有「略過第 2 晚調教室」這條邊角路徑才可能出現「第 1 晚蓋被＋第 2 晚不蓋」這組合（落到結局 4），
    /// 這裡把它改成「兩晚都不蓋」（結局 1），保證不會觸發結局 4。正常有進調教室的路徑，第 2 晚結束時已經由數值把兩個變數一起寫好了，不受影響。</summary>
    static void NormalizeAAEndingVars()
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm == null || pdm.R18ContentEnabled) return;
        if (pdm.GetDialogueVar("day1_blanket") > 0 && pdm.GetDialogueVar("day2_noBlanket") > 0)
        {
            pdm.AddDialogueVar("day1_noBlanket", 1 - pdm.GetDialogueVar("day1_noBlanket"));
            pdm.AddDialogueVar("day1_blanket", -pdm.GetDialogueVar("day1_blanket"));
            GD.Print("[結局] 結局 4 已拿掉：第1晚蓋被＋第2晚不蓋 → 改走兩晚都不蓋（結局 1）");
        }
    }

    async Task PlayDialogueAsync(string dialogueName, bool startBlack = false)
    {
        string path = "res://Resources/Data/Dialogues/" + dialogueName + ".json";
        string json = FileAccess.GetFileAsString(path);
        if (string.IsNullOrEmpty(json)) { GD.PrintErr("[LivingRoom] 找不到：" + path); return; }

        var data = DialogueSystem.ParseDialogueData(DialogueSystem.PreprocessJson(json));
        if (data == null || data.Lines == null || data.Lines.Count == 0) return;

        // 若要求從黑屏開始：先隱藏台詞框，再以 60 幀淡出到黑（已經是黑屏則略過）
        if (startBlack)
        {
            SetDialogueUIVisible(false);
            if (!_screenBlack)
            {
                _screenFadeColor = Colors.Black;
                await ScreenFadeAsync(true, 60f / 60f, Colors.Black);
                _screenBlack = true;
            }
            else if (_fadeOverlay != null)
            {
                MoveToFront(_fadeOverlay);
                _fadeOverlay.Color = new Color(0f, 0f, 0f, 1f);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        bool isR18 = PlayerDataManager.Instance?.R18ContentEnabled ?? false;
        int activeChoice = -1; // 最近一次選擇框的結果（0 起算）

        for (int _i = 0; _i < data.Lines.Count; _i++)
        {
            var line = data.Lines[_i];
            if (line == null) continue;

            // ── 分歧跳轉：全年齡模式跳過 r18 段落，R18 模式跳過 allAges 段落 ──
            // 其他名稱（例如 "coverChoice"）只當作標籤，裡面的內容照常播放
            if (!string.IsNullOrEmpty(line.branchStart))
            {
                string b = line.branchStart.Trim();
                bool shouldSkip = (!isR18 && b == "r18") || (isR18 && b == "allAges");
                if (shouldSkip) _i = DialogueSystem.FindBranchEnd(data.Lines, _i);
                continue; // branchStart 本身是純控制標記，不渲染台詞
            }

            // branchEnd 本身也是純控制標記，不渲染台詞
            if (line.branchEnd)
                continue;

            // ── 劇情變數：addVar +1 / varStart 條件段落（支援巢狀）──
            if (!string.IsNullOrEmpty(line.addVar))
            {
                PlayerDataManager.Instance?.AddDialogueVar(line.addVar.Trim());
                if (string.IsNullOrEmpty(line.text)) continue;
            }
            if (!string.IsNullOrEmpty(line.varStart))
            {
                if ((PlayerDataManager.Instance?.GetDialogueVar(line.varStart.Trim()) ?? 0) <= 0)
                    _i = DialogueSystem.FindVarEnd(data.Lines, _i);
                continue;
            }
            if (line.varEnd) continue;

            // ── 選擇框分歧：{ "choice": ["A","B"] } → 顯示選擇框並等待玩家選擇 ──
            if (line.choice != null && line.choice.Length > 0)
            {
                activeChoice = await ShowChoiceAsync(line.choice);
                continue;
            }

            // { "optionStart": n } → 不是玩家選的那個選項就跳到對應的 optionEnd
            if (line.optionStart >= 0)
            {
                if (line.optionStart != activeChoice)
                    _i = DialogueSystem.FindOptionEnd(data.Lines, _i);
                continue;
            }
            if (line.optionEnd) continue;

            // { "endTitle": "...", "endSubtitle": "..." } → 結局標題（與結局場景相同的逐字淡入）
            if (!string.IsNullOrEmpty(line.endTitle))
            {
                SetDialogueUIVisible(false);
                await ShowEndTitleAsync(line.endTitle, line.endSubtitle);
                await WaitTitleClickAsync(line.autoNext);
                await FadeOutEndTitleAsync();
                // 全年齡結局（有 endNo）：解鎖全年齡圖鑒，這條路線到此結束
                if ((!isR18 || _mesugakiRun) && line.endNo > 0)
                {
                    if (!_aaReplay && !isR18) GalleryDataManager.Instance?.UnlockAAEnding(line.endNo);   // R18 不解鎖全年齡圖鑒
                    _aaEndingDone = true;
                    break;
                }
                continue;
            }

            // { "gotoIfAllAges": "Day3/Day3_Start" } → 全年齡模式：結束目前文本，之後改播指定文本
            if (!string.IsNullOrEmpty(line.gotoIfAllAges))
            {
                if (!isR18) { _gotoAllAges = line.gotoIfAllAges.Trim(); break; }
                continue;
            }


            await HandleAudioAsync(line);

            var (foVal, foColor, foPriority, foFrames) = ParseFadeout(line);
            var (hideCHVal, hideCHPriority) = ParseHideCH(line.hideCH);
            bool hideCHHandled = false;

            if (!foPriority && hideCHPriority && !string.IsNullOrEmpty(hideCHVal))
            { await HandleHideCHAsync(hideCHVal); hideCHHandled = true; }

            if (!string.IsNullOrEmpty(line.bgName))
                await HandleBGAsync(line.bgName);

            if (line.wait > 0f) await TimerAsync(line.wait / 60f);

            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!foPriority && cg.hasHide && DialogueSystem.IsHideCGPriority(cg.hide))
                    await FadeCGAsync(li, "", false);
            }
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!foPriority && cg.hasShow) await FadeCGAsync(li, cg.show, true);
            }
            if (!foPriority) await AnimateCGTransformAsync(line);

            bool chScaleDeferred = false;
            string csRaw = line.chScale;
            if (!string.IsNullOrEmpty(csRaw) && csRaw.Contains(","))
            {
                if (!float.TryParse(csRaw.Split(',')[0].Trim(), out _))
                { chScaleDeferred = true; csRaw = csRaw.Split(',')[^1].Trim(); }
            }
            if (!foPriority && !chScaleDeferred)
                await AnimateCharTransformAsync(csRaw, line.chX, line.chY);

            if (!foPriority && !hideCHHandled && line.hideCH != null && !string.IsNullOrEmpty(line.showCH))
            { await HandleHideCHAsync(hideCHVal); hideCHHandled = true; }

            if (!foPriority && chScaleDeferred)
            {
                var chSP = DialogueSystem.ParseValueWithDuration(csRaw);
                if (chSP.value > 0f)
                { _charBodySize = new Vector2(650f * chSP.value, 1338f * chSP.value); ApplyCharBodyTransform(); }
            }
            if (!foPriority) await AnimateChar2TransformAsync(line.chScale2, line.chX2, line.chY2);
            if (!foPriority && line.hideCH2 != null && !string.IsNullOrEmpty(line.showCH2))
                await FadeChar2Async(false);

            if (!foPriority && HasSpine(line)) await SpineApplyAsync(line, false);
            if (!foPriority)
            {
                bool hasCH  = !string.IsNullOrEmpty(line.showCH);
                bool hasCH2 = !string.IsNullOrEmpty(line.showCH2);
                if (hasCH && hasCH2) await Task.WhenAll(FadeCharAsync(line.showCH, true), FadeChar2ShowAsync(line.showCH2));
                else if (hasCH)  await FadeCharAsync(line.showCH, true);
                else if (hasCH2) await FadeChar2ShowAsync(line.showCH2);
            }
            if (!foPriority && line.hideCH2 != null && string.IsNullOrEmpty(line.showCH2))
                await FadeChar2Async(false);

            if (!string.IsNullOrEmpty(line.fadein))
            {
                float.TryParse(line.fadein.Trim(), out float fiFrames);
                float fiDur = fiFrames > 0f ? fiFrames / 60f : 0.5f;
                await ScreenFadeAsync(false, fiDur, _screenFadeColor);
                _screenBlack = false;
                if (Mathf.IsEqualApprox(line.autoNext, 1f)) continue;
            }

            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!foPriority && cg.hasHide && !DialogueSystem.IsHideCGPriority(cg.hide))
                    await FadeCGAsync(li, "", false);
            }

            string spk = (line.speaker ?? "").Trim();
            if (_speakerText != null) _speakerText.Text = spk;
            // nameBar 可見性與樣式由 NovelReader.ApplySpeakerStyle 統一控制，這裡不碰

            if (_screenBlack)
            {
                if (!string.IsNullOrEmpty(line.text))
                {
                    MoveToFront(_dialogueBox); _dialogueBox.Visible = true;
                    if (_reader != null && !string.IsNullOrEmpty(spk)) MoveToFront(_nameBarGO);
                    _btnBarGO.Visible = true;
                    _btnBarGO.Visible = true;
                    await TypeLineAsync(line.text);
                    await WaitClickAsync(line.autoNext);
                    SetDialogueUIVisible(false);
                    MoveToFront(_fadeOverlay);
                }
                else await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                if (!hideCHHandled && line.hideCH != null)
                    await HandleHideCHAsync(hideCHVal);
                continue;
            }

            if (Mathf.IsEqualApprox(line.autoNext, 1f) || string.IsNullOrEmpty(line.text))
            {
                SetDialogueUIVisible(false);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            else
            {
                SetDialogueUIVisible(true);
                // nameBar 由 NovelReader.BeginLine 控制，不在這裡設定
                await TypeLineAsync(line.text);
                await WaitClickAsync(line.autoNext);
            }

            if (foVal != null)
            {
                SetDialogueUIVisible(false);
                float foDur = string.IsNullOrEmpty(foFrames) ? 0.5f
                    : (float.TryParse(foFrames, out float ff) ? (ff > 0f ? ff / 60f : 0.5f) : 0.5f);
                _screenFadeColor = foColor;
                await ScreenFadeAsync(true, foDur, foColor);
                _screenBlack = true;

                if (foPriority)
                {
                    if (hideCHPriority && !hideCHHandled && !string.IsNullOrEmpty(hideCHVal))
                    { await HandleHideCHAsync(hideCHVal); hideCHHandled = true; }
                    for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
                    {
                        var cg = DialogueSystem.GetCGLayer(line, li);
                        if (cg.hasHide && DialogueSystem.IsHideCGPriority(cg.hide))
                            await FadeCGAsync(li, "", false);
                    }
                    for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
                    {
                        var cg = DialogueSystem.GetCGLayer(line, li);
                        if (cg.hasShow) await FadeCGAsync(li, cg.show, true);
                    }
                    ApplyCGTransformImmediate(line);
                    ApplyCharTransformImmediate(line);
                    if (HasSpine(line)) await SpineApplyAsync(line, true);
                    if (!string.IsNullOrEmpty(line.showCH)) await FadeCharAsync(line.showCH, true);
                    for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
                    {
                        var cg = DialogueSystem.GetCGLayer(line, li);
                        if (cg.hasHide && !DialogueSystem.IsHideCGPriority(cg.hide))
                            await FadeCGAsync(li, "", false);
                    }
                }
                if (!hideCHHandled && line.hideCH != null)
                    await HandleHideCHAsync(hideCHVal);
                continue;
            }

            if (!hideCHHandled && line.hideCH != null && string.IsNullOrEmpty(line.showCH))
                await HandleHideCHAsync(hideCHVal);
        }

        SetDialogueUIVisible(false);
    }

    // ─────────────────────────────────────────────────────────────
    // 帶選項的對話（Day3_First3: 算了不吵 / 教訓妹妹）
    // ─────────────────────────────────────────────────────────────
    async Task PlayDialogueArrayWithChoiceAsync(string dialogueName)
    {
        // 先播放主對話段落
        await PlayDialogueAsync(dialogueName);

        // 顯示選項
        int result = await ShowTwoChoiceAsync("算了，不跟你吵", "教訓妹妹");
        _choiceResult = result;

        if (_choiceResult == 0)
        {
            // 選「算了」→ Ending1
            await ScreenFadeAsync(true, 1f, Colors.Black);
            GetTree().ChangeSceneToFile("res://Scenes/Ending4.tscn");
        }
        else
        {
            // 選「教訓妹妹」→ 迷你遊戲
            await MiniGameNight3Async();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 判定結局
    // ─────────────────────────────────────────────────────────────
    async Task DetermineEndingAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var pdm       = PlayerDataManager.Instance;
        bool hadTouch  = TrainingRoomRuntime.HadBreastTouch;
        bool hadOrgasm = pdm?.MeimeiHasOrgasmed ?? false;
        bool hadInsert = (pdm?.VaginaUseCount ?? 0) > 0;

        // ── 插入優先：只要有插入（不論有沒有摸），或有摸且有高潮 → 客廳路線 ─
        if (hadInsert || (hadTouch && hadOrgasm))
        {
            await PlayDialogueAsync("Day3/Day3_Evening_Touch2");

            // 浴後客廳選擇：教訓雌小鬼 / 算了懶得理
            int lr = await ShowTwoChoiceAsync("……算了，我懶得理妳。", "教訓雌小鬼");
            if (lr == 0)
            {
                // 選B：放棄 → 錯失的信號（Ending5）
                await ScreenFadeAsync(true, 1f, Colors.Black);
                GetTree().ChangeSceneToFile("res://Scenes/Ending5.tscn");
            }
            else
            {
                // 選A：教訓雌小鬼 → 客廳小遊戲（TODO: 接後續結局 6~9）
                await MiniGameLivingRoomAsync();
            }
            return;
        }

        // ── 有摸、無高潮、無插入 → Ending3 ─────────────────────
        if (hadTouch)
        {
            GetTree().ChangeSceneToFile("res://Scenes/Ending3.tscn");
            return;
        }

        // ── 預設回退（其他結局待實作）────────────────────────
        GetTree().ChangeSceneToFile("res://Scenes/Ending1.tscn");
    }

    // ─────────────────────────────────────────────────────────────
    // 迷你遊戲 Night 3
    // ─────────────────────────────────────────────────────────────
    async Task MiniGameNight3Async()
    {
        _reader?.Stop();
        SetDialogueUIVisible(false);
        bool won = await TimingChallenge.Play(this);
        await ScreenFadeAsync(true, 0.4f, Colors.Black);
        if (won && (PlayerDataManager.Instance?.IdleNightCount ?? 0) >= 2)
        {
            Ending2Runtime.UseNothingRoute = true;
            GetTree().ChangeSceneToFile("res://Scenes/Ending2.tscn");
        }
        else GetTree().ChangeSceneToFile((PlayerDataManager.Instance?.IdleNightCount ?? 0) >= 2 ? "res://Scenes/Ending1.tscn" : "res://Scenes/Ending4.tscn");
    }


    // ─────────────────────────────────────────────────────────────
    // 客廳小遊戲（浴後，貼圖待換）
    // 遊戲邏輯與 Night3 相同；之後依勝負路由到 Ending 6~9（TODO）
    // ─────────────────────────────────────────────────────────────
    async System.Threading.Tasks.Task MiniGameLivingRoomAsync()
    {
        await PlayDialogueAsync("Day3/Day3_LivingRoom_Intro");
        _reader?.Stop();
        SetDialogueUIVisible(false);
        bool won = await TimingChallenge.Play(this);
        await ScreenFadeAsync(true, 0.4f, Colors.Black);
        if (won)
        {
            GetTree().ChangeSceneToFile("res://Scenes/Ending6.tscn");
        }
        else
        {
            // TODO: 路由到 Ending 7~9（根據次數與是否高潮）
            GetTree().ChangeSceneToFile("res://Scenes/Ending1.tscn");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 迷你遊戲 UI helpers
    // ─────────────────────────────────────────────────────────────
    Control MgMakeImgLayer(Control parent, string imgName, Vector2 screenPos, Vector2 size, float anchorX, float anchorY)
    {
        var tr = new TextureRect();
        tr.Name           = imgName;
        tr.StretchMode    = TextureRect.StretchModeEnum.Scale;
        tr.ExpandMode     = TextureRect.ExpandModeEnum.IgnoreSize;
        tr.MouseFilter    = Control.MouseFilterEnum.Ignore;
        tr.AnchorLeft = tr.AnchorRight = anchorX;
        tr.AnchorTop  = tr.AnchorBottom = anchorY;
        tr.OffsetLeft   = -size.X / 2f; tr.OffsetRight  = size.X / 2f;
        tr.OffsetTop    = -size.Y / 2f; tr.OffsetBottom = size.Y / 2f;
        // screenPos = absolute screen coordinate of center
        // parent is FullRect → convert to anchor-based offset
        tr.Position = screenPos - new Vector2(1920f * anchorX, 1080f * anchorY);
        var tex = ResourceLoader.Load<Texture2D>("res://Resources/MiniGame/" + imgName + ".png");
        if (tex != null) tr.Texture = tex;
        parent.AddChild(tr);
        return tr;
    }

    ColorRect MkColorBox(Control parent, Color color, Vector2 pos, Vector2 size)
    {
        var cr = new ColorRect();
        cr.Color    = color;
        cr.Position = pos;
        cr.Size     = size;
        parent.AddChild(cr);
        return cr;
    }

    Label MkLabel(Control parent, string text, int fontSize, Color color, Vector2 pos, Vector2 size)
    {
        var lbl = new Label();
        lbl.Text     = text;
        lbl.Position = pos;
        lbl.Size     = size;
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        parent.AddChild(lbl);
        return lbl;
    }

    Button MkMgButton(Control parent, string text, float w, float h)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(w, h);
        btn.AddThemeFontSizeOverride("font_size", 26);
        btn.AddThemeColorOverride("font_color", Colors.White);
        var sb = new StyleBoxFlat();
        sb.BgColor = new Color(0.2f, 0.2f, 0.5f, 0.9f);
        btn.AddThemeStyleboxOverride("normal", sb);
        var sbH = new StyleBoxFlat();
        sbH.BgColor = new Color(0.4f, 0.4f, 0.8f, 0.9f);
        btn.AddThemeStyleboxOverride("hover",   sbH);
        btn.AddThemeStyleboxOverride("pressed", sbH);
        parent.AddChild(btn);
        return btn;
    }

    // ─────────────────────────────────────────────────────────────
    // 兩選項 UI
    // ─────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────
    // 通用選擇 helper（重建面板→等待→回傳 0 or 1）
    // ─────────────────────────────────────────────────────────────
    async System.Threading.Tasks.Task<int> ShowTwoChoiceAsync(string label0, string label1)
        => await ShowChoiceAsync(new[] { label0, label1 });

    /// <summary>任意數量選項的選擇框（劇本 JSON 的 "choice" 使用）</summary>
    async System.Threading.Tasks.Task<int> ShowChoiceAsync(string[] labels)
    {
        if (_forcedChoice >= 0) { int r = _forcedChoice; _forcedChoice = -1; return r; }
        BuildChoiceUI(labels);
        _choicePanel.Visible = true;
        _reader?.Stop();
        _choiceResult = -1;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        while (_choiceResult < 0)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _choicePanel.Visible = false;
        return _choiceResult;
    }

    // 劇本 JSON 選擇框的版面（預設值與序章 MainMenu 場景的設定相同，Play 中改 Inspector 即時生效）
    [Export] public Vector2 ChoiceBtnSize  = new Vector2(650f, 100f); // 單顆按鈕寬、高
    [Export] public float   ChoiceBtnGap   = 100f;                     // 按鈕之間的間距
    [Export] public Vector2 ChoicePos      = new Vector2(0f, 100f);    // 整組按鈕相對畫面中央的位移（正 Y = 往上，同序章）
    [Export] public int     ChoiceFontSize = 35;                       // 按鈕文字大小

    void BuildChoiceUI(string[] labels)
    {
        _choicePanel?.QueueFree();
        _choicePanel = null;
        var container = new VBoxContainer();
        container.SetAnchorsPreset(Control.LayoutPreset.Center);
        _canvas.AddChild(container);
        _choicePanel = container;
        var buttons = new System.Collections.Generic.List<Button>();

        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            var btn = new Button();
            btn.Text = labels[i];
            btn.AddThemeColorOverride("font_color", Colors.White);
            var sbN = new StyleBoxFlat();
            sbN.BgColor = new Color(0, 0, 0, 0.9f);
            sbN.CornerRadiusTopLeft = sbN.CornerRadiusTopRight =
            sbN.CornerRadiusBottomLeft = sbN.CornerRadiusBottomRight = 4;
            var sbH = new StyleBoxFlat();
            sbH.BgColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);
            sbH.CornerRadiusTopLeft = sbH.CornerRadiusTopRight =
            sbH.CornerRadiusBottomLeft = sbH.CornerRadiusBottomRight = 4;
            btn.AddThemeStyleboxOverride("normal",  sbN);
            btn.AddThemeStyleboxOverride("hover",   sbH);
            btn.AddThemeStyleboxOverride("pressed", sbH);
            btn.Pressed += () => { _choiceResult = idx; };
            container.AddChild(btn);
            buttons.Add(btn);
        }

        // 位置／大小與序章的 BuildButtonOnlyChoice 相同算法；每幀同步，Play 中改 Inspector 即時生效
        void ApplyLayout()
        {
            if (!IsInstanceValid(container)) return;
            int n = buttons.Count;
            float totalH = n * ChoiceBtnSize.Y + Mathf.Max(n - 1, 0) * ChoiceBtnGap;
            container.AddThemeConstantOverride("separation", (int)ChoiceBtnGap);
            container.AnchorLeft = container.AnchorRight = 0.5f;
            container.AnchorTop  = container.AnchorBottom = 0.5f;
            float cx = ChoicePos.X, cy = -ChoicePos.Y;   // 正 Y 往上（與序章一致）
            container.OffsetLeft   = cx - ChoiceBtnSize.X * 0.5f;
            container.OffsetRight  = cx + ChoiceBtnSize.X * 0.5f;
            container.OffsetTop    = cy - totalH * 0.5f;
            container.OffsetBottom = cy + totalH * 0.5f;
            foreach (var b in buttons)
            {
                b.CustomMinimumSize = ChoiceBtnSize;
                b.AddThemeFontSizeOverride("font_size", ChoiceFontSize);
            }
        }
        ApplyLayout();
        var tree = GetTree();
        System.Action onFrame = ApplyLayout;
        tree.ProcessFrame += onFrame;
        container.TreeExiting += () => tree.ProcessFrame -= onFrame;

        container.Visible = false;
    }

    // ─────────────────────────────────────────────────────────────
    // 打字機
    // ─────────────────────────────────────────────────────────────
    async Task TypeLineAsync(string text)
    {
        BeginReadingLine(text);
        if (_contentText == null) return;
        _contentText.Text = "";
        _isTyping = true; _skipTyping = false;
        foreach (char c in text ?? "")
        {
            while (_reader.Paused) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_skipTyping) { _contentText.Text = text; break; }
            _contentText.Text += c;
            await TimerAsync(_fastForward ? 0.005f : 0.03f / NovelReader.AutoTypeMul);
        }
        _isTyping = false;
    }

    // ─────────────────────────────────────────────────────────────
    // 螢幕淡入/淡出
    // ─────────────────────────────────────────────────────────────
    async Task ScreenFadeAsync(bool toBlack, float duration, Color color)
    {
        if (_fadeOverlay == null) return;
        MoveToFront(_fadeOverlay);
        float startA = toBlack ? 0f : 1f, endA = toBlack ? 1f : 0f;
        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            float a = Mathf.Lerp(startA, endA, Mathf.Clamp(t / duration, 0, 1));
            _fadeOverlay.Color = new Color(color.R, color.G, color.B, a);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _fadeOverlay.Color = new Color(color.R, color.G, color.B, endA);
    }

    // ─────────────────────────────────────────────────────────────
    // CG 淡入/淡出
    // ─────────────────────────────────────────────────────────────
    async Task FadeCGAsync(int layer, string cgName, bool fadeIn, float duration = 0.6f)
    {
        if (fadeIn)
        {
            if (_cgImages[layer] == null)
            {
                var cg = new TextureRect();
                cg.StretchMode = TextureRect.StretchModeEnum.Scale;
                cg.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
                cg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                cg.MouseFilter = Control.MouseFilterEnum.Ignore;
                _panel.AddChild(cg);
                _cgImages[layer] = cg;
                _cgScale[layer]  = 1f; _cgOffset[layer] = Vector2.Zero;
                int bgIdx = _bgImage?.GetIndex() + 1 ?? 0;
                _panel.MoveChild(cg, bgIdx + layer);
            }
            var tex = ResourceLoader.Load<Texture2D>("res://Resources/CG/" + cgName.Trim() + ".png");
            if (tex == null) { GD.PushWarning("[LivingRoom] 找不到 CG：" + cgName); return; }
            _cgImages[layer].Texture = tex; _cgImages[layer].Visible = true;
            _cgScale[layer] = 1f; _cgOffset[layer] = Vector2.Zero; ApplyCGTransform(layer);
            _cgImages[layer].Modulate = new Color(1, 1, 1, 0);
            float t = 0f;
            while (t < duration)
            {
                t += (float)GetProcessDeltaTime();
                _cgImages[layer].Modulate = new Color(1, 1, 1, Mathf.Clamp(t / duration, 0, 1));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            _cgImages[layer].Modulate = Colors.White;
        }
        else
        {
            if (_cgImages[layer] == null || !_cgImages[layer].Visible) return;
            float t = 0f;
            while (t < duration)
            {
                t += (float)GetProcessDeltaTime();
                _cgImages[layer].Modulate = new Color(1, 1, 1, 1f - Mathf.Clamp(t / duration, 0, 1));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            _cgImages[layer].Modulate = new Color(1, 1, 1, 0);
            _cgImages[layer].Texture  = null;
            _cgScale[layer] = 1f; _cgOffset[layer] = Vector2.Zero; ApplyCGTransform(layer);
            _cgImages[layer].Visible = false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 立繪
    // ─────────────────────────────────────────────────────────────
    // ── Spine 動態立繪（跟序章 MainMenuRuntime.Character.cs 同一套做法）──
    [Export] public bool Spine18PlusOutfit = true;   // true＝R18 時劇本的 sleep 自動換成 sleep2（18+ 睡衣）；false＝不換
    static bool HasSpine(DialogueSystem.DialogueLine l)
        => !string.IsNullOrWhiteSpace(l.chOutfit) || !string.IsNullOrWhiteSpace(l.chSkin)
        || !string.IsNullOrWhiteSpace(l.chHair)   || !string.IsNullOrWhiteSpace(l.chSocks)
        || !string.IsNullOrWhiteSpace(l.chFace)   || !string.IsNullOrWhiteSpace(l.chAnim);

    /// <summary>套用這一行的 Spine 設定；還沒顯示時淡入（instant＝true 直接顯示）。</summary>
    async Task SpineApplyAsync(DialogueSystem.DialogueLine l, bool instant)
    {
        if (_spineChar == null || !_spineChar.EnsureLoaded()) return;
        string outfit = !string.IsNullOrWhiteSpace(l.chOutfit) ? l.chOutfit : l.chSkin;
        if (Spine18PlusOutfit && outfit != null && outfit.Trim() == "sleep" && (PlayerDataManager.Instance?.R18ContentEnabled ?? false)) outfit = "sleep2";
        _spineChar.Apply(outfit, l.chHair, l.chSocks, l.chFace, l.chAnim);

        if (_spineChar.Visible && _charBodyImage.Modulate.A > 0.01f) return;   // 已經在畫面上：只換外觀，不重新淡入

        if (_charBodyImage.Texture != null || _charFaceImage.Texture != null)
        {   // 圖片立繪還在 → 先清掉
            _charBodyImage.Texture = null; _charFaceImage.Texture = null;
            _charFaceImage.Modulate = new Color(1, 1, 1, 0); _charBodyImage.Modulate = new Color(1, 1, 1, 0);
        }
        _spineChar.Visible = true;
        if (instant) { _charBodyImage.Modulate = Colors.White; return; }
        float startA = _charBodyImage.Modulate.A, t = 0f;
        while (t < 0.5f)
        {
            t += (float)GetProcessDeltaTime();
            _charBodyImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 1f, Mathf.Clamp(t / 0.5f, 0f, 1f)));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _charBodyImage.Modulate = Colors.White;
    }

    async Task FadeCharAsync(string name, bool fadeIn)
    {
        float duration = 0.5f;
        if (fadeIn)
        {
            string bodyName = null, faceName = null, char2Name = null;
            bool forceFade = false;
            if (_spineChar != null && _spineChar.Visible && !name.Contains(" ")) _spineChar.Visible = false;   // 圖片立繪要出來了：Spine 先收起來
            if (name.Contains(","))
            {
                var parts = name.Split(',');
                string last = parts[^1].Trim().ToLower();
                if (last == "fade")
                {
                    forceFade = true;
                    string imgName = parts[0].Trim();
                    if (imgName.ToLower().Contains("linai")) bodyName = imgName;
                    else if (imgName.Contains(" ")) char2Name = imgName;
                    else faceName = imgName;
                }
                else { bodyName = parts[0].Trim(); faceName = parts.Length > 1 ? parts[1].Trim() : null; }
            }
            else if (name.ToLower().Contains("linai")) bodyName = name.Trim();
            else if (name.Contains(" ")) char2Name = name.Trim();
            else faceName = name.Trim();

            bool hasBody = false, hasFace = false, hasChar2 = false;
            if (bodyName != null)
            {
                var tex = ResourceLoader.Load<Texture2D>("res://Resources/Character/" + bodyName + ".png");
                if (tex != null) { _charBodyImage.Texture = tex; _charBodyImage.Visible = true; hasBody = true; }
            }
            if (faceName != null)
            {
                var tex = ResourceLoader.Load<Texture2D>("res://Resources/Character/" + faceName + ".png");
                if (tex != null) { _charFaceImage.Texture = tex; _charFaceImage.Visible = true; hasFace = true; }
            }
            else if (bodyName != null) _charFaceImage.Modulate = new Color(1, 1, 1, 0);
            if (char2Name != null)
            {
                var tex = ResourceLoader.Load<Texture2D>("res://Resources/Character/" + char2Name + ".png");
                if (tex != null) { _char2Image.Texture = tex; _char2Image.Visible = true; hasChar2 = true; }
            }
            if (hasBody && (_charBodyImage.Modulate.A < 0.9f || forceFade))
            {
                float startA = _charBodyImage.Modulate.A; float t = 0f;
                while (t < duration)
                {
                    t += (float)GetProcessDeltaTime();
                    float a = Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0, 1));
                    _charBodyImage.Modulate = new Color(1, 1, 1, a);
                    if (hasFace) _charFaceImage.Modulate = new Color(1, 1, 1, a);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                _charBodyImage.Modulate = Colors.White;
                if (hasFace) _charFaceImage.Modulate = Colors.White;
            }
            else if (hasBody) { _charBodyImage.Modulate = Colors.White; if (hasFace) _charFaceImage.Modulate = Colors.White; }
            if (!hasBody && hasFace && forceFade)
            {
                float startA = _charFaceImage.Modulate.A; float t = 0f;
                while (t < duration)
                {
                    t += (float)GetProcessDeltaTime();
                    _charFaceImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0, 1)));
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                _charFaceImage.Modulate = Colors.White;
            }
            else if (!hasBody && hasFace) _charFaceImage.Modulate = Colors.White;
            if (hasChar2 && forceFade)
            {
                float startA = _char2Image.Modulate.A; float t = 0f;
                while (t < duration)
                {
                    t += (float)GetProcessDeltaTime();
                    _char2Image.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0, 1)));
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                _char2Image.Modulate = Colors.White;
            }
            else if (hasChar2) _char2Image.Modulate = Colors.White;
        }
        else
        {
            float bodyA = _charBodyImage.Modulate.A, faceA = _charFaceImage.Modulate.A, c2A = _char2Image.Modulate.A;
            float t = 0f;
            while (t < duration)
            {
                t += (float)GetProcessDeltaTime();
                float p = Mathf.Clamp(t / duration, 0, 1);
                _charBodyImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(bodyA, 0, p));
                _charFaceImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(faceA, 0, p));
                _char2Image.Modulate    = new Color(1, 1, 1, Mathf.Lerp(c2A,   0, p));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            _charBodyImage.Modulate = new Color(1,1,1,0); _charFaceImage.Modulate = new Color(1,1,1,0);
            _char2Image.Modulate    = new Color(1,1,1,0);
            _charBodyImage.Texture  = null; _charFaceImage.Texture = null; _char2Image.Texture = null;
            if (_spineChar != null) _spineChar.Visible = false;
            _charBodyOffset = Vector2.Zero; _charBodySize = new Vector2(650, 1338); ApplyCharBodyTransform();
            _char2Offset    = Vector2.Zero; _char2Size    = new Vector2(650, 1338); ApplyChar2Transform();
        }
    }

    async Task FadeCharSelectiveAsync(string names)
    {
        float duration = 0.5f;
        var parts = names.Split(',');
        bool hideBody = false, hideFace = false, hideChar2 = false;
        foreach (var p in parts)
        {
            string n = p.Trim();
            if (_charBodyImage.Texture is Texture2D bt  && bt.ResourcePath.GetFile().GetBaseName()  == n) hideBody  = true;
            if (_charFaceImage.Texture is Texture2D ft  && ft.ResourcePath.GetFile().GetBaseName()  == n) hideFace  = true;
            if (_char2Image.Texture    is Texture2D c2t && c2t.ResourcePath.GetFile().GetBaseName() == n) hideChar2 = true;
        }
        if (!hideBody && !hideFace && !hideChar2) return;
        float bodyA = _charBodyImage.Modulate.A, faceA = _charFaceImage.Modulate.A, c2A = _char2Image.Modulate.A;
        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            float p = Mathf.Clamp(t / duration, 0, 1);
            if (hideBody)  _charBodyImage.Modulate = new Color(1,1,1, Mathf.Lerp(bodyA, 0, p));
            if (hideFace)  _charFaceImage.Modulate = new Color(1,1,1, Mathf.Lerp(faceA, 0, p));
            if (hideChar2) _char2Image.Modulate    = new Color(1,1,1, Mathf.Lerp(c2A,   0, p));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (hideBody)  { _charBodyImage.Modulate = new Color(1,1,1,0); _charBodyImage.Texture = null; }
        if (hideFace)  { _charFaceImage.Modulate = new Color(1,1,1,0); _charFaceImage.Texture = null; }
        if (hideChar2) { _char2Image.Modulate    = new Color(1,1,1,0); _char2Image.Texture    = null; }
        if (_charBodyImage.Texture == null && _charFaceImage.Texture == null)
        { _charBodyOffset = Vector2.Zero; _charBodySize = new Vector2(650,1338); ApplyCharBodyTransform(); }
        if (_char2Image.Texture == null)
        { _char2Offset = Vector2.Zero; _char2Size = new Vector2(650,1338); ApplyChar2Transform(); }
    }

    async Task HandleHideCHAsync(string hideCH)
    {
        string val = (hideCH ?? "").Trim();
        if (val == "" || val == "0" || val == "1") await FadeCharAsync("", false);
        else await FadeCharSelectiveAsync(val);
    }

    async Task FadeChar2ShowAsync(string name)
    {
        float duration = 0.5f; bool forceFade = false; string imgName = name.Trim();
        if (imgName.Contains(","))
        {
            var parts = imgName.Split(',');
            if (parts[^1].Trim().ToLower() == "fade") { forceFade = true; imgName = parts[0].Trim(); }
        }
        var tex = ResourceLoader.Load<Texture2D>("res://Resources/Character/" + imgName + ".png");
        if (tex == null) return;
        _char2Image.Texture = tex; _char2Image.Visible = true;
        if (forceFade)
        {
            float startA = _char2Image.Modulate.A; float t = 0f;
            while (t < duration)
            {
                t += (float)GetProcessDeltaTime();
                _char2Image.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0, 1)));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
        _char2Image.Modulate = Colors.White;
    }

    async Task FadeChar2Async(bool fadeIn)
    {
        float duration = 0.5f; float startA = _char2Image.Modulate.A; float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            _char2Image.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 0f, Mathf.Clamp(t / duration, 0, 1)));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _char2Image.Modulate = new Color(1,1,1,0); _char2Image.Texture = null;
        _char2Offset = Vector2.Zero; _char2Size = new Vector2(650,1338); ApplyChar2Transform();
    }

    // ─────────────────────────────────────────────────────────────
    // 背景切換
    // ─────────────────────────────────────────────────────────────
    async Task CrossfadeBGAsync(Texture2D newTex, float duration = 0.6f)
    {
        var overlay = new TextureRect();
        overlay.Texture = newTex; overlay.StretchMode = TextureRect.StretchModeEnum.Scale;
        overlay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        overlay.Modulate = new Color(1, 1, 1, 0);
        _panel.AddChild(overlay); _panel.MoveChild(overlay, _bgImage.GetIndex() + 1);
        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            overlay.Modulate = new Color(1, 1, 1, Mathf.Clamp(t / duration, 0, 1));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _bgImage.Texture = newTex; _bgImage.Modulate = Colors.White; overlay.QueueFree();
    }

    async Task FadeBGColorAsync(Color targetColor, float duration = 0.6f)
    {
        Color startColor = _bgImage.Modulate; float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            _bgImage.Modulate = startColor.Lerp(targetColor, Mathf.Clamp(t / duration, 0, 1));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _bgImage.Texture = null; _bgImage.Modulate = targetColor;
    }

    async Task HandleBGAsync(string bgName)
    {
        string name = bgName.Trim(); bool crossfade = false;
        if (name.Contains(","))
        {
            var parts = name.Split(','); name = parts[0].Trim();
            crossfade = parts.Length > 1 && parts[1].Trim().ToLower() == "fade";
        }
        if (name.ToLower() == "black")
        {
            if (crossfade) await FadeBGColorAsync(Colors.Black);
            else { _bgImage.Texture = null; _bgImage.Modulate = Colors.Black; }
        }
        else if (name.ToLower() == "white")
        {
            if (crossfade) await FadeBGColorAsync(Colors.White);
            else { _bgImage.Texture = null; _bgImage.Modulate = Colors.White; }
        }
        else
        {
            var tex = ResourceHelper.LoadTexAny("Backgrounds/" + name);
            if (tex == null) GD.PrintErr($"[LivingRoom] 找不到背景圖：Backgrounds/{name}（背景沒有切換）");
            if (tex != null && _bgImage != null)
            {
                if (crossfade && _screenBlack) crossfade = false;   // 畫面是黑的（例如一段劇情從黑屏開始）時看不到交叉淡入，直接換成新背景最保險，不會卡在舊背景
                GD.Print($"[LivingRoom] 切換背景 → {name}{(crossfade ? "（淡入）" : "")}");
                if (crossfade) await CrossfadeBGAsync(tex);
                else { _bgImage.Texture = tex; _bgImage.Modulate = Colors.White; }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 音訊
    // ─────────────────────────────────────────────────────────────
    async Task HandleAudioAsync(DialogueSystem.DialogueLine line)
    {
        if (!string.IsNullOrEmpty(line.bgm))
        {
            if (line.bgm.Trim().ToLower() == "stop") _ = FadeOutAudioAsync(_bgmPlayer, 0.8f);
            else
            {
                var stream = ResourceLoader.Load<AudioStream>("res://Resources/Audio/bgm/" + line.bgm.Trim() + ".ogg");
                if (stream != null) { _bgmPlayer.Stream = stream; _bgmPlayer.Play(); }
            }
        }
        if (!string.IsNullOrEmpty(line.se))
        {
            string seName = line.se; float seDelay = 0f;
            if (line.se.Contains(","))
            {
                var parts = line.se.Split(','); seName = parts[0].Trim();
                if (float.TryParse(parts[1].Trim(), out float frames)) seDelay = frames / 60f;
            }
            var clip = ResourceLoader.Load<AudioStream>("res://Resources/Audio/se/" + seName.Trim() + ".ogg");
            if (clip != null)
            {
                if (seDelay > 0f) _ = PlaySEDelayedAsync(clip, seDelay);
                else { _sePlayer.Stream = clip; _sePlayer.Play(); }
            }
        }
        if (!string.IsNullOrEmpty(line.me))
        {
            var clip = ResourceLoader.Load<AudioStream>("res://Resources/Audio/me/" + line.me.Trim() + ".ogg");
            if (clip != null)
            {
                float bgmVol = Mathf.DbToLinear(_bgmPlayer.VolumeDb);
                _bgmPlayer.VolumeDb = Mathf.LinearToDb(0.2f);
                _mePlayer.Stream = clip; _mePlayer.Play();
                _ = RestoreBGMAfterMEAsync(bgmVol);
            }
        }
        await Task.CompletedTask;
    }

    async Task PlaySEDelayedAsync(AudioStream stream, float delay)
    {
        await TimerAsync(delay); _sePlayer.Stream = stream; _sePlayer.Play();
    }

    async Task FadeOutAudioAsync(AudioStreamPlayer player, float duration)
    {
        float startVol = Mathf.DbToLinear(player.VolumeDb); float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            player.VolumeDb = Mathf.LinearToDb(Mathf.Lerp(startVol, 0f, t / duration));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        player.Stop(); player.VolumeDb = Mathf.LinearToDb(startVol);
    }

    async Task RestoreBGMAfterMEAsync(float targetVol)
    {
        while (_mePlayer.Playing) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        float t = 0f;
        while (t < 0.5f)
        {
            t += (float)GetProcessDeltaTime();
            _bgmPlayer.VolumeDb = Mathf.LinearToDb(Mathf.Lerp(0.2f, targetVol, t / 0.5f));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _bgmPlayer.VolumeDb = Mathf.LinearToDb(targetVol);
    }

    // ─────────────────────────────────────────────────────────────
    // 計時 / 等待點擊
    // ─────────────────────────────────────────────────────────────
    async Task TimerAsync(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds, false), SceneTreeTimer.SignalName.Timeout);
    }

    async Task WaitClickAsync(float autoNext)
    {
        if (autoNext > 1f)
        {
            _waitingClick = true; _lockClick = true; _autoAdvance = false;
            _ = AutoNextTimerAsync(autoNext / 60f);
            while (_waitingClick) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _lockClick = false;
        }
        else
        {
            _waitingClick = true; _autoAdvance = false;
            if (_autoPlaySpeed > 0) _ = AutoAdvanceTimerAsync();
            while (_waitingClick) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    async Task AutoNextTimerAsync(float seconds)
    {
        if (_reader != null && await _reader.WaitScriptDelay(seconds))
            _waitingClick = false;
    }

    async Task AutoAdvanceTimerAsync()
    {
        float[] delays = { 0f, 2.5f, 1.5f, 0.8f, 0.2f };
        await TimerAsync(delays[Mathf.Clamp(_autoPlaySpeed, 0, 4)]);
        _autoAdvance = true; if (_waitingClick) _waitingClick = false;
    }

    // ─────────────────────────────────────────────────────────────
    // 角色 / CG 位移動畫
    // ─────────────────────────────────────────────────────────────
    async Task AnimateCharTransformAsync(string csRaw, string chX, string chY)
    {
        var chSP = DialogueSystem.ParseValueWithDuration(csRaw);
        var chXP = DialogueSystem.ParseValueWithDuration(chX);
        var chYP = DialogueSystem.ParseValueWithDuration(chY);
        Vector2 targetSize = !string.IsNullOrEmpty(csRaw) && chSP.value > 0f
            ? new Vector2(650f * chSP.value, 1338f * chSP.value) : _charBodySize;
        bool hasX = !string.IsNullOrEmpty(chX), hasY = !string.IsNullOrEmpty(chY);
        Vector2 targetPos = new(hasX ? chXP.value : _charBodyOffset.X, hasY ? chYP.value : _charBodyOffset.Y);
        float dur = 0.4f;
        if (!string.IsNullOrEmpty(csRaw)) dur = Mathf.Max(dur, chSP.durationSec);
        if (hasX) dur = Mathf.Max(dur, chXP.durationSec);
        if (hasY) dur = Mathf.Max(dur, chYP.durationSec);
        if (targetSize == _charBodySize && targetPos == _charBodyOffset) return;
        Vector2 startSize = _charBodySize, startPos = _charBodyOffset; float t = 0f;
        while (t < dur)
        {
            t += (float)GetProcessDeltaTime();
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t / dur, 0, 1));
            _charBodySize = startSize.Lerp(targetSize, p); _charBodyOffset = startPos.Lerp(targetPos, p);
            ApplyCharBodyTransform();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _charBodySize = targetSize; _charBodyOffset = targetPos; ApplyCharBodyTransform();
    }

    async Task AnimateChar2TransformAsync(string csRaw2, string chX2, string chY2)
    {
        if (string.IsNullOrEmpty(csRaw2) && string.IsNullOrEmpty(chX2) && string.IsNullOrEmpty(chY2)) return;
        var c2SP = DialogueSystem.ParseValueWithDuration(csRaw2);
        var c2XP = DialogueSystem.ParseValueWithDuration(chX2);
        var c2YP = DialogueSystem.ParseValueWithDuration(chY2);
        Vector2 tgtSize = !string.IsNullOrEmpty(csRaw2) && c2SP.value > 0f
            ? new Vector2(650f * c2SP.value, 1338f * c2SP.value) : _char2Size;
        bool hasX2 = !string.IsNullOrEmpty(chX2), hasY2 = !string.IsNullOrEmpty(chY2);
        Vector2 tgtPos = new(hasX2 ? c2XP.value : _char2Offset.X, hasY2 ? c2YP.value : _char2Offset.Y);
        float dur = 0.4f;
        if (!string.IsNullOrEmpty(csRaw2)) dur = Mathf.Max(dur, c2SP.durationSec);
        if (hasX2) dur = Mathf.Max(dur, c2XP.durationSec);
        if (hasY2) dur = Mathf.Max(dur, c2YP.durationSec);
        if (tgtSize == _char2Size && tgtPos == _char2Offset) return;
        Vector2 sSize = _char2Size, sPos = _char2Offset; float t = 0f;
        while (t < dur)
        {
            t += (float)GetProcessDeltaTime();
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t / dur, 0, 1));
            _char2Size = sSize.Lerp(tgtSize, p); _char2Offset = sPos.Lerp(tgtPos, p);
            ApplyChar2Transform();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _char2Size = tgtSize; _char2Offset = tgtPos; ApplyChar2Transform();
    }

    async Task AnimateCGTransformAsync(DialogueSystem.DialogueLine line)
    {
        for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
        {
            if (_cgImages[li] == null) continue;
            var cg = DialogueSystem.GetCGLayer(line, li);
            var cgSP = DialogueSystem.ParseValueWithDuration(cg.scale);
            var cgXP = DialogueSystem.ParseValueWithDuration(cg.x);
            var cgYP = DialogueSystem.ParseValueWithDuration(cg.y);
            float tgtScale = !string.IsNullOrEmpty(cg.scale) && cgSP.value > 0f ? cgSP.value : _cgScale[li];
            bool hasCgX = !string.IsNullOrEmpty(cg.x), hasCgY = !string.IsNullOrEmpty(cg.y);
            float tgtOX = hasCgX ? cgXP.value : _cgOffset[li].X;
            float tgtOY = hasCgY ? cgYP.value : _cgOffset[li].Y;
            float dur = 0.4f;
            if (!string.IsNullOrEmpty(cg.scale)) dur = Mathf.Max(dur, cgSP.durationSec);
            if (hasCgX) dur = Mathf.Max(dur, cgXP.durationSec);
            if (hasCgY) dur = Mathf.Max(dur, cgYP.durationSec);
            bool needMove = Mathf.Abs(tgtScale - _cgScale[li]) > 0.001f
                         || Mathf.Abs(tgtOX - _cgOffset[li].X) > 0.1f
                         || Mathf.Abs(tgtOY - _cgOffset[li].Y) > 0.1f;
            if (!needMove) continue;
            float startScale = _cgScale[li]; Vector2 startOff = _cgOffset[li]; float t = 0f;
            while (t < dur)
            {
                t += (float)GetProcessDeltaTime();
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t / dur, 0, 1));
                _cgScale[li]  = Mathf.Lerp(startScale, tgtScale, p);
                _cgOffset[li] = startOff.Lerp(new Vector2(tgtOX, tgtOY), p);
                ApplyCGTransform(li);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            _cgScale[li] = tgtScale; _cgOffset[li] = new Vector2(tgtOX, tgtOY); ApplyCGTransform(li);
        }
    }

    void ApplyCGTransformImmediate(DialogueSystem.DialogueLine line)
    {
        for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
        {
            if (_cgImages[li] == null) continue;
            var cg = DialogueSystem.GetCGLayer(line, li);
            var cgSP = DialogueSystem.ParseValueWithDuration(cg.scale);
            if (!string.IsNullOrEmpty(cg.scale) && cgSP.value > 0f) _cgScale[li] = cgSP.value;
            bool hasCgX = !string.IsNullOrEmpty(cg.x), hasCgY = !string.IsNullOrEmpty(cg.y);
            if (hasCgX || hasCgY)
            {
                float ox = hasCgX ? DialogueSystem.ParseValueWithDuration(cg.x).value : _cgOffset[li].X;
                float oy = hasCgY ? DialogueSystem.ParseValueWithDuration(cg.y).value : _cgOffset[li].Y;
                _cgOffset[li] = new Vector2(ox, oy);
            }
            ApplyCGTransform(li);
        }
    }

    void ApplyCharTransformImmediate(DialogueSystem.DialogueLine line)
    {
        string csRaw2 = line.chScale;
        if (!string.IsNullOrEmpty(csRaw2) && csRaw2.Contains(","))
        {
            if (!float.TryParse(csRaw2.Split(',')[0].Trim(), out _))
                csRaw2 = csRaw2.Split(',')[^1].Trim();
        }
        var chSP2 = DialogueSystem.ParseValueWithDuration(csRaw2);
        if (!string.IsNullOrEmpty(line.chScale) && chSP2.value > 0f)
            _charBodySize = new Vector2(650f * chSP2.value, 1338f * chSP2.value);
        bool hasX = !string.IsNullOrEmpty(line.chX), hasY = !string.IsNullOrEmpty(line.chY);
        if (hasX || hasY)
        {
            float posX = hasX ? DialogueSystem.ParseValueWithDuration(line.chX).value : _charBodyOffset.X;
            float posY = hasY ? DialogueSystem.ParseValueWithDuration(line.chY).value : _charBodyOffset.Y;
            _charBodyOffset = new Vector2(posX, posY);
        }
        ApplyCharBodyTransform();
    }

    // ─────────────────────────────────────────────────────────────
    // Transform 應用
    // ─────────────────────────────────────────────────────────────
    void ApplyCharBodyTransform()
    {
        float hw = _charBodySize.X / 2f, h = _charBodySize.Y;
        float ox = _charBodyOffset.X, oy = _charBodyOffset.Y;
        _charBodyImage.OffsetLeft  = -hw + ox; _charBodyImage.OffsetRight  =  hw + ox;
        _charBodyImage.OffsetTop   = -h  - oy; _charBodyImage.OffsetBottom = -oy;
        _charFaceImage.OffsetLeft  = -hw + ox; _charFaceImage.OffsetRight  =  hw + ox;
        _charFaceImage.OffsetTop   = -h  - oy; _charFaceImage.OffsetBottom = -oy;
    }

    void ApplyChar2Transform()
    {
        float hw = _char2Size.X / 2f, h = _char2Size.Y;
        float ox = _char2Offset.X, oy = _char2Offset.Y;
        _char2Image.OffsetLeft  = -hw + ox; _char2Image.OffsetRight  =  hw + ox;
        _char2Image.OffsetTop   = -h  - oy; _char2Image.OffsetBottom = -oy;
    }

    void ApplyCGTransform(int layer)
    {
        float s = _cgScale[layer];
        _cgImages[layer].Scale    = new Vector2(s, s);
        _cgImages[layer].Position = new Vector2(_cgOffset[layer].X, -_cgOffset[layer].Y);
    }

    // ─────────────────────────────────────────────────────────────
    // 解析 helpers
    // ─────────────────────────────────────────────────────────────
    (string val, Color color, bool priority, string frames) ParseFadeout(DialogueSystem.DialogueLine line)
    {
        string foVal = null; Color foColor = Colors.Black; bool foPriority = false; string foFrames = "";
        if (!string.IsNullOrEmpty(line.fadeoutWhite)) { foVal = line.fadeoutWhite; foColor = Colors.White; }
        else if (!string.IsNullOrEmpty(line.fadeoutBlack)) { foVal = line.fadeoutBlack; foColor = Colors.Black; }
        else if (!string.IsNullOrEmpty(line.fadeout)) { foVal = line.fadeout; foColor = Colors.Black; }
        if (foVal != null) { var parsed = DialogueSystem.ParseFadeoutValue(foVal); foFrames = parsed.framesStr; foPriority = parsed.priority; }
        return (foVal, foColor, foPriority, foFrames);
    }

    (string val, bool priority) ParseHideCH(string raw)
    {
        string val = (raw ?? "").Trim(); bool priority = false;
        if (!string.IsNullOrEmpty(val) && val.Contains(","))
        { var parts = val.Split(','); val = parts[0].Trim(); priority = parts.Length > 1 && parts[1].Trim() == "1"; }
        return (val, priority);
    }

    void ApplyNameBarColor(string spk)
    {
        if (_nameBarStyle == null || string.IsNullOrEmpty(spk)) return;
        if (spk.Contains("莉奈") || spk.Contains("Linai"))
            _nameBarStyle.BgColor = new Color(0.85f, 0.65f, 0.75f, 0.95f);
        else if (spk.Contains("男主") || spk.Contains("主角"))
            _nameBarStyle.BgColor = new Color(0.55f, 0.70f, 0.90f, 0.95f);
        else
            _nameBarStyle.BgColor = new Color(0.65f, 0.12f, 0.25f, 0.95f);
    }

    void SetDialogueUIVisible(bool v)
    {
        if (_dialogueBox != null) _dialogueBox.Visible = v;
        // _nameBarGO.Visible 由 NovelReader.BeginLine 統一控制，這裡不碰
        if (_btnBarGO    != null) _btnBarGO.Visible     = v;
    }

    void MoveToFront(Control node)
    {
        if (node?.GetParent() is Control parent)
            parent.MoveChild(node, parent.GetChildCount() - 1);
    }

    // ─────────────────────────────────────────────────────────────
    // 建立對話 UI
    // ─────────────────────────────────────────────────────────────
    void BuildUI()
    {
        var panel = new Control();
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.AddChild(panel);
        _panel = panel;

        var bg = new TextureRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.StretchMode = TextureRect.StretchModeEnum.Scale;
        bg.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        bg.Modulate    = new Color(0.08f, 0.04f, 0.12f);
        panel.AddChild(bg);
        _bgImage = bg;

        var portraitLayer = new Control { Name = "PortraitFloatRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(portraitLayer);
        portraitLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        void SetupChar(out TextureRect tr, string goName)
        {
            tr = new TextureRect(); tr.Name = goName;
            tr.MouseFilter  = Control.MouseFilterEnum.Ignore;
            tr.StretchMode  = TextureRect.StretchModeEnum.Scale;
            tr.ExpandMode   = TextureRect.ExpandModeEnum.IgnoreSize;
            tr.AnchorLeft   = 0.5f; tr.AnchorRight  = 0.5f;
            tr.AnchorTop    = 1.0f; tr.AnchorBottom = 1.0f;
            tr.Modulate     = new Color(1, 1, 1, 0);
            portraitLayer.AddChild(tr);
        }
        SetupChar(out _charBodyImage, "CharBody");
        _spineChar = new SpineChar { Name = "CharSpine", Visible = false };
        _charBodyImage.AddChild(_spineChar);
        _spineChar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        SetupChar(out _charFaceImage, "CharFace");
        SetupChar(out _char2Image,    "Char2");
        ApplyCharBodyTransform(); ApplyChar2Transform();

        float boxH = 260f;
        var box = new Panel();
        box.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        box.OffsetTop = -boxH; box.OffsetBottom = 0;
        var boxStyle = new StyleBoxFlat(); boxStyle.BgColor = new Color(0, 0, 0, 0.88f);
        box.AddThemeStyleboxOverride("panel", boxStyle);
        panel.AddChild(box);
        _dialogueBox = box;

        var contentLabel = new Label();
        contentLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        contentLabel.OffsetLeft = 60; contentLabel.OffsetRight = -60;
        contentLabel.OffsetTop  = 20; contentLabel.OffsetBottom = -20;
        contentLabel.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        contentLabel.AddThemeFontSizeOverride("font_size", 28);
        contentLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.96f, 1f));
        box.AddChild(contentLabel);
        _contentText = contentLabel;

        var hint = new Label(); hint.Text = "▼ 點擊繼續";
        hint.AddThemeFontSizeOverride("font_size", 15);
        hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.4f));
        hint.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        hint.OffsetLeft = -180; hint.OffsetBottom = -10;
        box.AddChild(hint);

        var nameBar = new Panel();
        nameBar.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        nameBar.OffsetLeft = 50f; nameBar.OffsetRight = 350f;
        nameBar.OffsetBottom = -(boxH + 4f); nameBar.OffsetTop = -(boxH + 52f);
        _nameBarStyle = new StyleBoxFlat();
        _nameBarStyle.BgColor = new Color(0f, 0f, 0f, 0f); // 透明初始，防閃爍
        _nameBarStyle.CornerRadiusTopLeft = _nameBarStyle.CornerRadiusTopRight =
        _nameBarStyle.CornerRadiusBottomLeft = _nameBarStyle.CornerRadiusBottomRight = 4;
        nameBar.AddThemeStyleboxOverride("panel", _nameBarStyle);
        nameBar.Modulate = new Color(1f, 1f, 1f, 0f); // 完全透明，等 NovelReader 接管後才顯示
        panel.AddChild(nameBar);
        _nameBarGO = nameBar; _nameBarPanel = nameBar;

        var spkLabel = new Label();
        spkLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        spkLabel.HorizontalAlignment = HorizontalAlignment.Center;
        spkLabel.VerticalAlignment   = VerticalAlignment.Center;
        spkLabel.AddThemeFontSizeOverride("font_size", 26);
        spkLabel.AddThemeColorOverride("font_color", Colors.White);
        nameBar.AddChild(spkLabel);
        _speakerText = spkLabel;

        var btnBar = new HBoxContainer();
        btnBar.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        btnBar.OffsetRight = -20f; btnBar.OffsetBottom = -(boxH + 6f); btnBar.OffsetTop = -(boxH + 46f);
        panel.AddChild(btnBar);
        _btnBarGO = btnBar;
        // 初始隱藏：避免開場淡入時先出現空白對話框，等第一句台詞才顯示
        btnBar.Visible = false;
        if (_dialogueBox != null) _dialogueBox.Visible = false;

        var btnFF   = MkCtrlBtn(btnBar, "快速播放", 140f);
        var btnAuto = MkCtrlBtn(btnBar, "自動播放", 140f);
        MkCtrlBtn(btnBar, "💾 保存", 140f);
        MkCtrlBtn(btnBar, "📂 讀檔", 140f);

        _fastFwdBtnTxt  = btnFF.GetNode<Label>("T");
        _autoPlayBtnTxt = btnAuto.GetNode<Label>("T");

        btnFF.Pressed += () => {
            _fastForward = !_fastForward;
            _fastFwdBtnTxt.AddThemeColorOverride("font_color",
                _fastForward ? new Color(1f, 0.85f, 0.25f) : new Color(1, 1, 1, 0.55f));
        };
        btnAuto.Pressed += () => {
            _autoPlaySpeed = (_autoPlaySpeed + 1) % 5;
            _autoPlayBtnTxt.Text = _autoPlaySpeed == 0 ? "自動播放" : "自動 X" + _autoPlaySpeed;
            _autoPlayBtnTxt.AddThemeColorOverride("font_color",
                _autoPlaySpeed == 0 ? new Color(1, 1, 1, 0.55f) : new Color(1f, 0.85f, 0.25f));
        };

        var fade = new ColorRect();
        fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        fade.Color = new Color(0, 0, 0, 0);
        fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(fade);
        _fadeOverlay = fade;
    }

    Button MkCtrlBtn(Control parent, string label, float w)
    {
        var btn = new Button();
        btn.Name = "Btn_" + label; btn.CustomMinimumSize = new Vector2(w, 36f);
        var sb = new StyleBoxFlat(); sb.BgColor = new Color(0, 0, 0, 0.45f);
        btn.AddThemeStyleboxOverride("normal", sb);
        var sbH = new StyleBoxFlat(); sbH.BgColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        btn.AddThemeStyleboxOverride("hover", sbH);
        btn.AddThemeStyleboxOverride("pressed", sbH);
        var lbl = new Label(); lbl.Name = "T"; lbl.Text = label;
        lbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        lbl.VerticalAlignment   = VerticalAlignment.Center;
        lbl.AddThemeFontSizeOverride("font_size", 16);
        lbl.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.85f));
        lbl.MouseFilter = Control.MouseFilterEnum.Ignore;
        btn.AddChild(lbl);
        parent.AddChild(btn);
        return btn;
    }
}
