using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 所有 Ending 場景的抽象基底類別（Godot 4 port）
/// 提供完整的對話播放、halfBlack、endTitle、skipAll 等功能。
/// 衍生類別只需覆寫 DialoguePath 與 PlayedKey。
/// </summary>
public abstract partial class EndingBaseRuntime : Node
{
    readonly System.Threading.CancellationTokenSource _lifetime = new();
    NovelReader _reader;
    public override void _ExitTree() => _lifetime.Cancel();

    async Task NextPlaybackFrameAsync()
    {
        _lifetime.Token.ThrowIfCancellationRequested();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _lifetime.Token.ThrowIfCancellationRequested();
    }

    // 建立面板時先套用新版介面，不等待第一句文字。
    void EnsureReader()
    {
        _reader ??= NovelReader.Install(this, _dialogueBox, _contentText, _btnBarGO,
            () => _isTyping, () => _waitingClick, () => _lockClick,
            () => _skipTyping = true, () => _waitingClick = false, null);
        _reader.SetSpeakerBadge(_nameBarGO, _speakerText);
    }

    void BeginReadingLine(string text)
    {
        EnsureReader();
        _reader.BeginLine(_speakerText?.Text, text);
    }

    // ── 抽象屬性（衍生類別必須實作）────────────────────────────────
    protected abstract string DialoguePath { get; }
    protected abstract string PlayedKey   { get; }

    /// <summary>
    /// 對應圖鑒（Gallery）的結局編號（E1~E18）。
    /// 衍生類別覆寫此屬性以在正式遊玩抵達結局時自動解鎖圖鑒條目；
    /// 預設為 0 表示「尚未對應到圖鑒編號」，不會觸發解鎖。
    /// </summary>
    protected virtual int GalleryEndingNumber => 0;
    protected virtual int AAEndingNumber => 0;   // 全年齡圖鑒的結局編號（0＝沒有）

    // ── 初次進入判斷 ─────────────────────────────────────────────
    static readonly HashSet<string> _playedDialogues = new();
    static bool HasDialoguePlayed(string id) => _playedDialogues.Contains(id);
    static void MarkDialoguePlayed(string id) => _playedDialogues.Add(id);
    /// <summary>開新遊戲時清掉「已播過」紀錄（NewGameReset 呼叫）</summary>
    public static void ResetForNewGame() => _playedDialogues.Clear();
    public static bool FromGallery = false;  // 圖鑒重播旗標

    // ── UI 節點 ───────────────────────────────────────────────────
    Label      _speakerText;
    Label      _contentText;
    Control    _panel;
    TextureRect _charBodyImage;
    SpineChar _spineChar;   // Spine 動態立繪（掛在身體圖底下，跟著它的位置／縮放／淡入淡出）
    TextureRect _charFaceImage;
    TextureRect _char2Image;
    Panel      _nameBarPanel;
    StyleBoxFlat _nameBarStyle;
    TextureRect _bgImage;
    ColorRect  _fadeOverlay;
    Control    _dialogueBox;
    Control    _nameBarGO;
    Control    _btnBarGO;
    bool       _screenBlack;
    Color      _screenFadeColor = Colors.Black;
    Control    _endTitleContainer;    // 結局標題容器
    TextureRect _halfBlackImage;      // 右半畫面斜邊黑屏
    ImageTexture _halfBlackTex;       // 懶建立的對角漸層紋理
    bool       _hoveringSaveLoad;

    // ── 音樂 ─────────────────────────────────────────────────────
    AudioStreamPlayer _bgmPlayer;
    AudioStreamPlayer _sePlayer;
    AudioStreamPlayer _mePlayer;

    // ── 打字機 / 點擊控制 ────────────────────────────────────────
    bool  _waitingClick;
    bool  _lockClick;
    bool  _isTyping;
    bool  _skipTyping;
    bool  _fastForward;
    bool  _skipAll;
    bool  _mouseHoldActive;
    float _mouseHoldTime;
    int   _autoPlaySpeed;
    bool  _autoAdvance;
    bool  _wasMousePressed;
    Label _fastFwdBtnTxt;
    Label _autoPlayBtnTxt;

    // ── 角色位置狀態（Unity 座標空間）────────────────────────────
    Vector2 _charBodySize   = new(650f, 1338f);
    Vector2 _charBodyOffset = Vector2.Zero;
    Vector2 _char2Size      = new(650f, 1338f);
    Vector2 _char2Offset    = Vector2.Zero;

    // ── 背景 / halfBlack 偏移（Unity 座標空間）────────────────────
    Vector2 _bgOffset        = Vector2.Zero;
    Vector2 _halfBlackOffset = Vector2.Zero;

    // ── CG 圖層 ──────────────────────────────────────────────────
    float[]       _cgScale  = new float[DialogueSystem.CG_LAYER_COUNT];
    Vector2[]     _cgOffset = new Vector2[DialogueSystem.CG_LAYER_COUNT];
    TextureRect[] _cgImages = new TextureRect[DialogueSystem.CG_LAYER_COUNT];

    // ── CanvasLayer ──────────────────────────────────────────────
    CanvasLayer _canvas;

    // ─────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        _canvas = new CanvasLayer { Layer = 100 };
        AddChild(_canvas);

        _bgmPlayer = new AudioStreamPlayer(); AddChild(_bgmPlayer);
        _sePlayer  = new AudioStreamPlayer(); AddChild(_sePlayer);
        _mePlayer  = new AudioStreamPlayer(); AddChild(_mePlayer);
        _bgmPlayer.VolumeDb = Mathf.LinearToDb(1f);
        _sePlayer.VolumeDb  = Mathf.LinearToDb(1f);
        _mePlayer.VolumeDb  = Mathf.LinearToDb(1f);
        SceneBgm.Install(this, DialoguePath.Contains("bad", StringComparison.OrdinalIgnoreCase)
            ? SceneBgm.Night : SceneBgm.Gentle, _bgmPlayer);

        for (int i = 0; i < DialogueSystem.CG_LAYER_COUNT; i++)
            _cgScale[i] = 1f;

        TaskLogger.Run(StartAsync(), "Ending.StartAsync");
    }

    // ── _Process: 點擊推進（含 skipAll）──────────────────────────
    public override void _Process(double delta)
    {
        // NovelReader 統一處理點擊、自動及快進。
    }

    // ─────────────────────────────────────────────────────────────
    // 入口
    // ─────────────────────────────────────────────────────────────
    async Task StartAsync()
    {
        try
        {
            await NextPlaybackFrameAsync();
            bool fromGallery = GalleryReplaySession.Active != null || FromGallery;
            FromGallery = false;
            // 僅在「正式遊玩抵達結局」（非圖鑒重播）時解鎖圖鑒條目
            if (!fromGallery && GalleryEndingNumber > 0)
            {
                if (GalleryDataManager.AllAgesMode && AAEndingNumber > 0) GalleryDataManager.Instance?.UnlockAAEnding(AAEndingNumber);   // 全年齡：解鎖全年齡圖鑒（不解鎖 R18 的）
                else GalleryDataManager.Instance?.UnlockEnding(GalleryEndingNumber);
            }
            if (fromGallery || !HasDialoguePlayed(PlayedKey))
            {
                await OnBeforeDialogueAsync();
                _lifetime.Token.ThrowIfCancellationRequested();
                await PlayDialogueAsync();
            }
            _lifetime.Token.ThrowIfCancellationRequested();
            if (fromGallery)
            {
                if (GalleryReplaySession.Active != null) GalleryReplaySession.Active.Close();
                else GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
            }
            else
            {
                // 結局對話結束後：全螢幕等待點擊，再返回主畫面
                await WaitAnyClickThenReturnAsync();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    /// <summary>
    /// 對話播放前的鉤子。Ending3 覆寫此方法執行 NothingRoute 小遊戲。
    /// </summary>
    protected virtual Task OnBeforeDialogueAsync() => Task.CompletedTask;

    /// <summary>
    /// 選擇 UI 鉤子。當對話行帶有 choiceId 欄位時觸發。
    /// 回傳 true 表示已由衍生類別處理（例如轉場），對話中止；回傳 false 表示繼續播放後續行。
    /// </summary>
    protected virtual Task<bool> HandleChoiceAsync(string choiceId) => Task.FromResult(false);

    // ─────────────────────────────────────────────────────────────
    // 主對話流程
    // ─────────────────────────────────────────────────────────────
    async Task PlayDialogueAsync()
    {
        string json = FileAccess.GetFileAsString(DialoguePath);
        if (string.IsNullOrEmpty(json))
        {
            GD.PrintErr("[Ending] 找不到對話檔：" + DialoguePath);
            return;
        }

        var data = DialogueSystem.ParseDialogueData(DialogueSystem.PreprocessJson(json));
        if (data == null || data.Lines == null || data.Lines.Count == 0) return;

        BuildUI();
        EnsureReader();
        _panel.Visible = true;

        bool isR18 = PlayerDataManager.Instance?.R18ContentEnabled ?? false;
        for (int _i = 0; _i < data.Lines.Count; _i++)
        {
            var line = data.Lines[_i];

            // ── 分歧跳轉：全年齡模式跳過 r18 段落，R18 模式跳過 allAges 段落（跟客廳對話引擎同一套）──
            if (!string.IsNullOrEmpty(line.branchStart))
            {
                string b = line.branchStart.Trim();
                bool shouldSkip = (!isR18 && b == "r18") || (isR18 && b == "allAges");
                if (shouldSkip) _i = DialogueSystem.FindBranchEnd(data.Lines, _i);
                continue;
            }
            if (line.branchEnd) continue;
            // ── 劇情變數：addVar +1 / varStart 條件段落 ──
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

            // ── 音訊 ──────────────────────────────────────────────
            await HandleAudioAsync(line);

            // ── fadeout 解析 ──────────────────────────────────────
            var (foVal, foColor, foPriority, foFrames) = ParseFadeout(line);

            // ── hideCH 解析 ───────────────────────────────────────
            var (hideCHVal, hideCHPriority) = ParseHideCH(line.hideCH);
            bool hideCHHandled = false;

            if (!foPriority && hideCHPriority && !string.IsNullOrEmpty(hideCHVal))
            {
                await HandleHideCHAsync(hideCHVal);
                hideCHHandled = true;
            }

            // ── 背景切換 ──────────────────────────────────────────
            bool bgNameWasSet = !string.IsNullOrEmpty(line.bgName);
            if (bgNameWasSet)
            {
                await HandleBGAsync(line.bgName);
                // bgName 同行的 bgX/bgY：立即套用
                bool hasBgX = !string.IsNullOrEmpty(line.bgX);
                bool hasBgY = !string.IsNullOrEmpty(line.bgY);
                if (hasBgX || hasBgY)
                {
                    if (hasBgX && float.TryParse(line.bgX.Trim(), out float bx)) _bgOffset.X = bx;
                    if (hasBgY && float.TryParse(line.bgY.Trim(), out float by)) _bgOffset.Y = by;
                    ApplyBGOffset();
                }
            }

            // wait
            if (line.wait > 0f)
                await TimerAsync(line.wait / 60f);

            // ── halfBlack 顯示/隱藏 ───────────────────────────────
            if (!string.IsNullOrEmpty(line.halfBlack))
                await HandleHalfBlackShowHideAsync(line);

            // ── bgX/bgY + halfBlackX/Y 同步移動（bgName 不存在時才動畫）
            await AnimateBGAndHalfBlackAsync(line, bgNameWasSet);

            // ── hideCG 優先 ───────────────────────────────────────
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!foPriority && cg.hasHide && DialogueSystem.IsHideCGPriority(cg.hide))
                    await FadeCGAsync(li, "", false);
            }

            // ── showCG ────────────────────────────────────────────
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!foPriority && cg.hasShow)
                    await FadeCGAsync(li, cg.show, true);
            }

            // ── CG 位移/縮放 ──────────────────────────────────────
            if (!foPriority) await AnimateCGTransformAsync(line);

            // ── chScale deferred 判斷 ─────────────────────────────
            bool chScaleDeferred = false;
            string csRaw = line.chScale;
            if (!string.IsNullOrEmpty(csRaw) && csRaw.Contains(","))
            {
                if (!float.TryParse(csRaw.Split(',')[0].Trim(), out _))
                {
                    chScaleDeferred = true;
                    csRaw = csRaw.Split(',')[^1].Trim();
                }
            }
            if (!foPriority && !chScaleDeferred)
                await AnimateCharTransformAsync(csRaw, line.chX, line.chY);

            // ── hideCH + showCH 同行 ──────────────────────────────
            if (!foPriority && !hideCHHandled && line.hideCH != null && !string.IsNullOrEmpty(line.showCH))
            {
                await HandleHideCHAsync(hideCHVal);
                hideCHHandled = true;
            }

            // ── chScale deferred（showCH 前套正確尺寸）────────────
            if (!foPriority && chScaleDeferred)
            {
                var chSP = DialogueSystem.ParseValueWithDuration(csRaw);
                if (chSP.value > 0f)
                {
                    _charBodySize = new Vector2(650f * chSP.value, 1338f * chSP.value);
                    ApplyCharBodyTransform();
                }
            }

            // ── char2 位移/縮放 ───────────────────────────────────
            if (!foPriority) await AnimateChar2TransformAsync(line.chScale2, line.chX2, line.chY2);

            // ── hideCH2 + showCH2 同行 → 先淡出 ──────────────────
            if (!foPriority && line.hideCH2 != null && !string.IsNullOrEmpty(line.showCH2))
                await FadeChar2Async(false);

            if (!foPriority && HasSpine(line)) await SpineApplyAsync(line, false);
            // ── showCH / showCH2（可並行）─────────────────────────
            if (!foPriority)
            {
                bool hasCH  = !string.IsNullOrEmpty(line.showCH);
                bool hasCH2 = !string.IsNullOrEmpty(line.showCH2);
                if (hasCH && hasCH2)
                    await Task.WhenAll(FadeCharAsync(line.showCH, true), FadeChar2ShowAsync(line.showCH2));
                else if (hasCH)
                    await FadeCharAsync(line.showCH, true);
                else if (hasCH2)
                    await FadeChar2ShowAsync(line.showCH2);
            }

            // ── 只隱藏不顯示 char2 ───────────────────────────────
            if (!foPriority && line.hideCH2 != null && string.IsNullOrEmpty(line.showCH2))
                await FadeChar2Async(false);

            // ── fadein ────────────────────────────────────────────
            if (!string.IsNullOrEmpty(line.fadein))
            {
                float.TryParse(line.fadein.Trim(), out float fiFrames);
                float fiDur = fiFrames > 0f ? fiFrames / 60f : 0.5f;
                await ScreenFadeAsync(false, fiDur, _screenFadeColor);
                _screenBlack = false;
                if (Mathf.IsEqualApprox(line.autoNext, 1f)) continue;
            }

            // ── hideCG 非優先 ─────────────────────────────────────
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!foPriority && cg.hasHide && !DialogueSystem.IsHideCGPriority(cg.hide))
                    await FadeCGAsync(li, "", false);
            }

            // ── 說話者 / 名字框 ───────────────────────────────────
            string spk = (line.speaker ?? "").Trim();
            if (_speakerText != null) _speakerText.Text = spk;
            if (_nameBarGO != null) _nameBarGO.Visible = !string.IsNullOrEmpty(spk);
            ApplyNameBarColor(spk);

            // ── 黑屏中處理 ────────────────────────────────────────
            if (_screenBlack)
            {
                if (!string.IsNullOrEmpty(line.text))
                {
                    MoveToFront(_dialogueBox);
                    _dialogueBox.Visible = true;
                    _nameBarGO.Visible = !string.IsNullOrEmpty(spk);
                    if (!string.IsNullOrEmpty(spk)) MoveToFront(_nameBarGO);
                    _btnBarGO.Visible = true;
                    await TypeLineAsync(line.text);
                    await WaitClickAsync(line.autoNext);
                    SetDialogueUIVisible(false);
                    MoveToFront(_fadeOverlay);
                }
                else if (!string.IsNullOrEmpty(line.endTitle))
                {
                    await ShowEndTitleAsync(line.endTitle, line.endSubtitle);
                    await WaitClickAsync(line.autoNext);
                    await FadeOutEndTitleAsync();
                }
                else
                    await NextPlaybackFrameAsync();

                if (!hideCHHandled && line.hideCH != null)
                    await HandleHideCHAsync(hideCHVal);
                continue;
            }

            // ── autoNext == 1 ─────────────────────────────────────
            if (Mathf.IsEqualApprox(line.autoNext, 1f))
            {
                SetDialogueUIVisible(false);
                await NextPlaybackFrameAsync();
            }
            // ── endTitle ──────────────────────────────────────────
            else if (!string.IsNullOrEmpty(line.endTitle))
            {
                SetDialogueUIVisible(false);
                await ShowEndTitleAsync(line.endTitle, line.endSubtitle);
                await WaitClickAsync(line.autoNext);
                await FadeOutEndTitleAsync();
                await NextPlaybackFrameAsync();
            }
            else
            {
                SetDialogueUIVisible(true);
                if (_nameBarGO != null && string.IsNullOrEmpty(spk))
                    _nameBarGO.Visible = false;
                await TypeLineAsync(line.text);
                await WaitClickAsync(line.autoNext);
            }

            // ── choiceId 選擇 UI ─────────────────────────────────────
            if (!string.IsNullOrEmpty(line.choiceId))
            {
                // 回放到需要交還主遊戲的選擇點即結束，避免改寫遊戲進度。
                if (GalleryReplaySession.Active != null) return;
                bool abort = await HandleChoiceAsync(line.choiceId);
                if (abort) return;
            }

            // ── fadeout ───────────────────────────────────────────
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
                    {
                        await HandleHideCHAsync(hideCHVal);
                        hideCHHandled = true;
                    }
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
                    if (!string.IsNullOrEmpty(line.showCH))
                        await FadeCharAsync(line.showCH, true);
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

        if (GalleryReplaySession.Active == null) MarkDialoguePlayed(PlayedKey);
        SetDialogueUIVisible(false);
        GD.Print("[Ending] 對話播放完畢：" + PlayedKey);
    }

    // ─────────────────────────────────────────────────────────────
    // halfBlack 顯示/隱藏
    // ─────────────────────────────────────────────────────────────
    async Task HandleHalfBlackShowHideAsync(DialogueSystem.DialogueLine line)
    {
        string hbRaw = line.halfBlack.Trim().ToLower();
        float hbFrames = 0f;
        string hbVal = hbRaw;
        if (hbRaw.Contains(","))
        {
            var parts = hbRaw.Split(',');
            hbVal = parts[0].Trim();
            float.TryParse(parts[1].Trim(), out hbFrames);
        }
        float hbDur = hbFrames > 0f ? hbFrames / 60f : 0f;

        if (hbVal == "right")
        {
            bool hasInitX = !string.IsNullOrEmpty(line.halfBlackX);
            bool hasInitY = !string.IsNullOrEmpty(line.halfBlackY);
            if (hasInitX || hasInitY)
            {
                // 先建立（alpha=0），設位置，再淡入
                EnsureHalfBlackImage();
                _halfBlackImage.Visible = true;
                _halfBlackImage.Modulate = new Color(1, 1, 1, 0);
                if (hasInitX)
                {
                    var xp = DialogueSystem.ParseValueWithDuration(line.halfBlackX);
                    _halfBlackOffset.X = xp.value;
                }
                if (hasInitY)
                {
                    var yp = DialogueSystem.ParseValueWithDuration(line.halfBlackY);
                    _halfBlackOffset.Y = yp.value;
                }
                ApplyHalfBlackOffset();
                if (hbDur > 0f)
                    await HalfBlackFadeAsync(true, hbDur);
                else
                    _halfBlackImage.Modulate = Colors.White;
            }
            else
                await HalfBlackFadeAsync(true, hbDur);
        }
        else if (hbVal == "hide")
            await HalfBlackFadeAsync(false, hbDur);
    }

    // ── bgX/bgY + halfBlackX/Y 同步移動（animate）───────────────
    async Task AnimateBGAndHalfBlackAsync(DialogueSystem.DialogueLine line, bool bgNameWasSet)
    {
        bool hasBgX = !string.IsNullOrEmpty(line.bgX) && !bgNameWasSet;
        bool hasBgY = !string.IsNullOrEmpty(line.bgY) && !bgNameWasSet;
        bool hasHbX = !string.IsNullOrEmpty(line.halfBlackX);
        bool hasHbY = !string.IsNullOrEmpty(line.halfBlackY);
        // halfBlack move: only when halfBlack "right" is NOT set on same line
        bool hbAlreadySet = !string.IsNullOrEmpty(line.halfBlack) &&
            line.halfBlack.Trim().ToLower().StartsWith("right");
        bool hbAlive = _halfBlackImage != null && _halfBlackImage.Visible;
        bool bgNeedMove = hasBgX || hasBgY;
        bool hbNeedMove = (hasHbX || hasHbY) && hbAlive && !hbAlreadySet;
        if (!bgNeedMove && !hbNeedMove) return;

        var bgXP = DialogueSystem.ParseValueWithDuration(line.bgX);
        var bgYP = DialogueSystem.ParseValueWithDuration(line.bgY);
        var hbXP = DialogueSystem.ParseValueWithDuration(line.halfBlackX);
        var hbYP = DialogueSystem.ParseValueWithDuration(line.halfBlackY);

        Vector2 bgStart = _bgOffset, bgTarget = _bgOffset;
        if (hasBgX) bgTarget.X = bgXP.value;
        if (hasBgY) bgTarget.Y = bgYP.value;
        if (bgStart == bgTarget) bgNeedMove = false;

        Vector2 hbStart = _halfBlackOffset, hbTarget = _halfBlackOffset;
        if (hasHbX) hbTarget.X = hbXP.value;
        if (hasHbY) hbTarget.Y = hbYP.value;
        if (hbStart == hbTarget) hbNeedMove = false;
        if (!bgNeedMove && !hbNeedMove) return;

        float dur = 0.4f;
        if (hasBgX) dur = Mathf.Max(dur, bgXP.durationSec);
        if (hasBgY) dur = Mathf.Max(dur, bgYP.durationSec);
        if (hasHbX) dur = Mathf.Max(dur, hbXP.durationSec);
        if (hasHbY) dur = Mathf.Max(dur, hbYP.durationSec);

        float t = 0f;
        while (t < dur)
        {
            t += (float)GetProcessDeltaTime();
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t / dur, 0, 1));
            if (bgNeedMove) { _bgOffset = bgStart.Lerp(bgTarget, p); ApplyBGOffset(); }
            if (hbNeedMove) { _halfBlackOffset = hbStart.Lerp(hbTarget, p); ApplyHalfBlackOffset(); }
            await NextPlaybackFrameAsync();
        }
        if (bgNeedMove) { _bgOffset = bgTarget; ApplyBGOffset(); }
        if (hbNeedMove) { _halfBlackOffset = hbTarget; ApplyHalfBlackOffset(); }
    }

    // ─────────────────────────────────────────────────────────────
    // halfBlack 紋理 / 淡入淡出
    // ─────────────────────────────────────────────────────────────
    void EnsureHalfBlackImage()
    {
        if (_halfBlackImage != null) return;

        // 建立對角漸層紋理（左透明→斜線→右黑）
        if (_halfBlackTex == null)
        {
            int w = 256, h = 256;
            var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
            // Godot Y=0 是頂部，Unity Y=0 是底部，所以翻轉 y 映射
            for (int y = 0; y < h; y++)
            {
                // 對應 Unity: edgeNorm = Lerp(0.35, 0.55, (float)unityY / h)
                // unityY = h - 1 - y (Godot top → Unity bottom 翻轉)
                float edgeNorm = Mathf.Lerp(0.35f, 0.55f, 1f - (float)y / h);
                int edgePx = Mathf.RoundToInt(edgeNorm * w);
                int blendW = 3;
                for (int x = 0; x < w; x++)
                {
                    float a;
                    if (x >= edgePx + blendW)       a = 1f;
                    else if (x <= edgePx - blendW)  a = 0f;
                    else a = Mathf.InverseLerp(edgePx - blendW, edgePx + blendW, x);
                    img.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            _halfBlackTex = ImageTexture.CreateFromImage(img);
        }

        var hb = new TextureRect();
        hb.Name = "HalfBlack";
        hb.Texture = _halfBlackTex;
        hb.StretchMode = TextureRect.StretchModeEnum.Scale;
        hb.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        hb.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hb.MouseFilter  = Control.MouseFilterEnum.Ignore;
        hb.Modulate     = new Color(1, 1, 1, 0);
        hb.Visible      = false;
        _panel.AddChild(hb);
        _halfBlackImage = hb;

        // 排在背景之後、立繪之前
        int bgIdx = _bgImage?.GetIndex() ?? -1;
        _panel.MoveChild(hb, bgIdx + 1);
        ApplyHalfBlackOffset();
    }

    async Task HalfBlackFadeAsync(bool show, float duration)
    {
        EnsureHalfBlackImage();
        _halfBlackImage.Visible = true;
        float startA = show ? 0f : _halfBlackImage.Modulate.A;
        float endA   = show ? 1f : 0f;

        if (_skipAll || duration <= 0f)
        {
            _halfBlackImage.Modulate = new Color(1, 1, 1, endA);
            if (!show) _halfBlackImage.Visible = false;
            return;
        }

        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            _halfBlackImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, endA, Mathf.Clamp(t / duration, 0, 1)));
            await NextPlaybackFrameAsync();
        }
        _halfBlackImage.Modulate = new Color(1, 1, 1, endA);
        if (!show) _halfBlackImage.Visible = false;
    }

    void ApplyBGOffset()
    {
        if (_bgImage == null) return;
        _bgImage.OffsetLeft   = _bgOffset.X;
        _bgImage.OffsetRight  = _bgOffset.X;
        _bgImage.OffsetTop    = -_bgOffset.Y;
        _bgImage.OffsetBottom = -_bgOffset.Y;
    }

    void ApplyHalfBlackOffset()
    {
        if (_halfBlackImage == null) return;
        _halfBlackImage.OffsetLeft   = _halfBlackOffset.X;
        _halfBlackImage.OffsetRight  = _halfBlackOffset.X;
        _halfBlackImage.OffsetTop    = -_halfBlackOffset.Y;
        _halfBlackImage.OffsetBottom = -_halfBlackOffset.Y;
    }

    // ─────────────────────────────────────────────────────────────
    // 結局標題（逐字交錯淡入）
    // ─────────────────────────────────────────────────────────────
    async Task ShowEndTitleAsync(string title, string subtitle)
    {
        if (_endTitleContainer != null)
        {
            _endTitleContainer.QueueFree();
            _endTitleContainer = null;
        }
        await NextPlaybackFrameAsync();

        var container = new Control();
        container.Name = "EndTitle";
        container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        container.MouseFilter = Control.MouseFilterEnum.Ignore;
        _panel.AddChild(container);
        _panel.MoveChild(container, _panel.GetChildCount() - 1); // 最上層
        _endTitleContainer = container;

        // 取得字型（從暫時 Label）
        var tmpLabel = new Label();
        container.AddChild(tmpLabel);
        await NextPlaybackFrameAsync();
        int fontSize = 90;
        var font = tmpLabel.GetThemeFont("font");
        tmpLabel.QueueFree();

        // 測量每個字的寬度並計算置中 X 座標
        int titleLen = title.Length;
        float[] widths    = new float[titleLen];
        float   totalW    = 0f;
        for (int i = 0; i < titleLen; i++)
        {
            widths[i] = font.GetStringSize(title[i].ToString(),
                HorizontalAlignment.Left, -1, fontSize).X;
            totalW += widths[i];
        }
        // 計算各字中心 X（以螢幕中心為原點）
        float[] centerXs = new float[titleLen];
        float accum = -totalW / 2f;
        for (int i = 0; i < titleLen; i++)
        {
            centerXs[i] = accum + widths[i] / 2f;
            accum += widths[i];
        }

        // 建立每個字的 Label
        var charLabels = new Label[titleLen];
        for (int i = 0; i < titleLen; i++)
        {
            var lbl = new Label();
            lbl.Text = title[i].ToString();
            lbl.AddThemeFontSizeOverride("font_size", fontSize);
            lbl.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0));
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            lbl.VerticalAlignment   = VerticalAlignment.Center;
            lbl.AutowrapMode        = TextServer.AutowrapMode.Off;
            lbl.MouseFilter         = Control.MouseFilterEnum.Ignore;
            // 定位：螢幕中心偏上 50px (Unity Y up → Godot Y down: 正中 - 50px)
            lbl.AnchorLeft   = 0.5f; lbl.AnchorRight  = 0.5f;
            lbl.AnchorTop    = 0.5f; lbl.AnchorBottom = 0.5f;
            float hw = (widths[i] + 10f) / 2f;
            lbl.OffsetLeft   = centerXs[i] - hw;
            lbl.OffsetRight  = centerXs[i] + hw;
            lbl.OffsetTop    = -50f - 65f;   // center - 50 Unity Y up → center - 50 Godot Y down
            lbl.OffsetBottom = -50f + 65f;
            container.AddChild(lbl);
            charLabels[i] = lbl;
        }

        // 逐字交錯淡入
        float perCharDur    = 0.6f;
        float stagger       = 0.12f;
        float titleTotalDur = perCharDur + stagger * (titleLen - 1);
        float elapsed = 0f;
        while (elapsed < titleTotalDur)
        {
            elapsed += (float)GetProcessDeltaTime();
            for (int i = 0; i < titleLen; i++)
            {
                float localT = (elapsed - stagger * i) / perCharDur;
                float a = Mathf.Clamp(localT, 0f, 1f);
                charLabels[i].AddThemeColorOverride("font_color", new Color(1, 1, 1, a));
            }
            await NextPlaybackFrameAsync();
        }
        for (int i = 0; i < titleLen; i++)
            charLabels[i].AddThemeColorOverride("font_color", Colors.White);

        // 副標題淡入
        if (!string.IsNullOrEmpty(subtitle))
        {
            var subLbl = new Label();
            subLbl.Text = subtitle;
            subLbl.AddThemeFontSizeOverride("font_size", 42);
            subLbl.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, 0));
            subLbl.HorizontalAlignment = HorizontalAlignment.Center;
            subLbl.VerticalAlignment   = VerticalAlignment.Center;
            subLbl.AutowrapMode        = TextServer.AutowrapMode.Off;
            subLbl.MouseFilter         = Control.MouseFilterEnum.Ignore;
            subLbl.AnchorLeft = 0.5f; subLbl.AnchorRight  = 0.5f;
            subLbl.AnchorTop  = 0.5f; subLbl.AnchorBottom = 0.5f;
            subLbl.OffsetLeft = -400f; subLbl.OffsetRight  = 400f;
            subLbl.OffsetTop  = 60f - 35f; subLbl.OffsetBottom = 60f + 35f;
            container.AddChild(subLbl);

            float subDur = 0.8f;
            elapsed = 0f;
            while (elapsed < subDur)
            {
                elapsed += (float)GetProcessDeltaTime();
                subLbl.AddThemeColorOverride("font_color",
                    new Color(0.85f, 0.85f, 0.85f, Mathf.Clamp(elapsed / subDur, 0f, 1f)));
                await NextPlaybackFrameAsync();
            }
            subLbl.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f, 1f));
        }
    }

    // ── 結局標題淡出 ─────────────────────────────────────────────
    async Task FadeOutEndTitleAsync(float duration = 1.5f)
    {
        if (_endTitleContainer == null) return;
        var labels = new List<Label>();
        foreach (Node child in _endTitleContainer.GetChildren())
            if (child is Label lbl) labels.Add(lbl);

        float[] startAlphas = new float[labels.Count];
        for (int i = 0; i < labels.Count; i++)
            startAlphas[i] = labels[i].GetThemeColor("font_color").A;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += (float)GetProcessDeltaTime();
            float t = Mathf.Clamp(elapsed / duration, 0f, 1f);
            for (int i = 0; i < labels.Count; i++)
            {
                float a = Mathf.Lerp(startAlphas[i], 0f, t);
                var c = labels[i].GetThemeColor("font_color");
                labels[i].AddThemeColorOverride("font_color", new Color(c.R, c.G, c.B, a));
            }
            await NextPlaybackFrameAsync();
        }

        _endTitleContainer.QueueFree();
        _endTitleContainer = null;
    }

    // ─────────────────────────────────────────────────────────────
    // 打字機
    // ─────────────────────────────────────────────────────────────
    async Task TypeLineAsync(string text)
    {
        BeginReadingLine(text);
        if (_contentText == null) return;
        _contentText.Text = "";
        if (_skipAll) { _contentText.Text = text ?? ""; return; }
        _isTyping = true; _skipTyping = false;
        foreach (char c in text ?? "")
        {
            while (_reader.Paused) await NextPlaybackFrameAsync();
            if (_skipTyping || _skipAll) { _contentText.Text = text; break; }
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
        float endA = toBlack ? 1f : 0f;
        if (_skipAll)
        {
            _fadeOverlay.Color = new Color(color.R, color.G, color.B, endA);
            return;
        }
        float startA = toBlack ? 0f : 1f;
        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            float a = Mathf.Lerp(startA, endA, Mathf.Clamp(t / duration, 0, 1));
            _fadeOverlay.Color = new Color(color.R, color.G, color.B, a);
            await NextPlaybackFrameAsync();
        }
        _fadeOverlay.Color = new Color(color.R, color.G, color.B, endA);
    }

    // ─────────────────────────────────────────────────────────────
    // 等待點擊 / autoNext
    // ─────────────────────────────────────────────────────────────
    /// <summary>結局對話播完後，等待玩家點擊任意位置，然後切回主選單。</summary>
    async Task WaitAnyClickThenReturnAsync()
    {
        // 全螢幕透明按鈕，接收任意點擊
        var overlay = new Button { Name = "EndClickOverlay" };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.FocusMode = Control.FocusModeEnum.None;
        // 透明樣式（無背景無邊框）
        var empty = new StyleBoxEmpty();
        overlay.AddThemeStyleboxOverride("normal",  empty);
        overlay.AddThemeStyleboxOverride("hover",   empty);
        overlay.AddThemeStyleboxOverride("pressed", empty);
        overlay.AddThemeStyleboxOverride("focus",   empty);
        overlay.Text = "";
        _canvas.AddChild(overlay);

        // 可選：顯示「點擊繼續」提示文字
        var hint = new Label
        {
            Text = "▼ 點擊繼續",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Bottom,
        };
        hint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hint.OffsetBottom = -20;
        hint.AddThemeFontSizeOverride("font_size", 16);
        hint.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.5f));
        hint.MouseFilter = Control.MouseFilterEnum.Ignore;
        _canvas.AddChild(hint);

        // 閃爍動畫
        float blinkT = 0f;
        bool clicked = false;
        overlay.Pressed += () => clicked = true;
        while (!clicked)
        {
            blinkT += (float)GetProcessDeltaTime();
            hint.Modulate = new Color(1f, 1f, 1f, 0.35f + 0.35f * Mathf.Sin(blinkT * Mathf.Tau / 1.4f));
            await NextPlaybackFrameAsync();
        }

        hint.QueueFree();
        overlay.QueueFree();
        GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
    }

    async Task WaitClickAsync(float autoNext)
    {
        if (autoNext > 1f)
        {
            _waitingClick = true; _lockClick = true; _autoAdvance = false;
            _ = AutoNextTimerAsync(autoNext / 60f);
            while (_waitingClick) await NextPlaybackFrameAsync();
            _lockClick = false;
        }
        else
        {
            _waitingClick = true; _autoAdvance = false;
            if (_autoPlaySpeed > 0) _ = AutoAdvanceTimerAsync();
            while (_waitingClick) await NextPlaybackFrameAsync();
        }
        await NextPlaybackFrameAsync();
    }

    async Task AutoNextTimerAsync(float seconds)
    {
        if (_reader != null && await _reader.WaitScriptDelay(seconds, _lifetime.Token))
            _waitingClick = false;
    }

    async Task AutoAdvanceTimerAsync()
    {
        float[] delays = { 0f, 2.5f, 1.5f, 0.8f, 0.2f };
        await TimerAsync(delays[Mathf.Clamp(_autoPlaySpeed, 0, 4)]);
        _autoAdvance = true;
        if (_waitingClick) _waitingClick = false;
    }

    async Task TimerAsync(float seconds)
    {
        _lifetime.Token.ThrowIfCancellationRequested();
        await ToSignal(GetTree().CreateTimer(seconds, false), SceneTreeTimer.SignalName.Timeout);
        _lifetime.Token.ThrowIfCancellationRequested();
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
                _cgScale[layer] = 1f; _cgOffset[layer] = Vector2.Zero;
                int bgIdx = _bgImage?.GetIndex() + 1 ?? 0;
                _panel.MoveChild(cg, bgIdx + layer);
            }
            var tex = ResourceLoader.Load<Texture2D>("res://Resources/CG/" + cgName.Trim() + ".png");
            if (tex == null) { GD.PushWarning("[Ending] 找不到 CG：" + cgName); return; }
            _cgImages[layer].Texture = tex;
            _cgImages[layer].Visible = true;
            _cgScale[layer] = 1f; _cgOffset[layer] = Vector2.Zero;
            ApplyCGTransform(layer);
            _cgImages[layer].Modulate = new Color(1, 1, 1, 0);
            float t = 0f;
            while (t < duration)
            {
                t += (float)GetProcessDeltaTime();
                _cgImages[layer].Modulate = new Color(1, 1, 1, Mathf.Clamp(t / duration, 0, 1));
                await NextPlaybackFrameAsync();
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
                await NextPlaybackFrameAsync();
            }
            _cgImages[layer].Modulate = new Color(1, 1, 1, 0);
            _cgImages[layer].Texture = null;
            _cgScale[layer] = 1f; _cgOffset[layer] = Vector2.Zero;
            ApplyCGTransform(layer);
            _cgImages[layer].Visible = false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 立繪淡入/淡出
    // ─────────────────────────────────────────────────────────────
    // ── Spine 動態立繪（跟序章／客廳同一套做法）──
    [Export] public bool Spine18PlusOutfit = true;   // true＝R18 時劇本的 sleep 自動換成 sleep2（18+ 睡衣）
    static bool HasSpine(DialogueSystem.DialogueLine l)
        => !string.IsNullOrWhiteSpace(l.chOutfit) || !string.IsNullOrWhiteSpace(l.chSkin)
        || !string.IsNullOrWhiteSpace(l.chHair)   || !string.IsNullOrWhiteSpace(l.chSocks)
        || !string.IsNullOrWhiteSpace(l.chFace)   || !string.IsNullOrWhiteSpace(l.chAnim);

    async Task SpineApplyAsync(DialogueSystem.DialogueLine l, bool instant)
    {
        if (_spineChar == null || !_spineChar.EnsureLoaded()) return;
        string outfit = !string.IsNullOrWhiteSpace(l.chOutfit) ? l.chOutfit : l.chSkin;
        if (Spine18PlusOutfit && outfit != null && outfit.Trim() == "sleep" && (PlayerDataManager.Instance?.R18ContentEnabled ?? false)) outfit = "sleep2";
        _spineChar.Apply(outfit, l.chHair, l.chSocks, l.chFace, l.chAnim);

        if (_spineChar.Visible && _charBodyImage.Modulate.A > 0.01f) return;   // 已經在畫面上：只換外觀

        if (_charBodyImage.Texture != null || _charFaceImage.Texture != null)
        {
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
            await NextPlaybackFrameAsync();
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
                else
                {
                    bodyName = parts[0].Trim();
                    faceName = parts.Length > 1 ? parts[1].Trim() : null;
                }
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
            else if (bodyName != null)
                _charFaceImage.Modulate = new Color(1, 1, 1, 0);

            if (char2Name != null)
            {
                var tex = ResourceLoader.Load<Texture2D>("res://Resources/Character/" + char2Name + ".png");
                if (tex != null) { _char2Image.Texture = tex; _char2Image.Visible = true; hasChar2 = true; }
            }

            if (hasBody && (_charBodyImage.Modulate.A < 0.9f || forceFade))
            {
                float startA = _charBodyImage.Modulate.A;
                float t = 0f;
                while (t < duration)
                {
                    t += (float)GetProcessDeltaTime();
                    float a = Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0, 1));
                    _charBodyImage.Modulate = new Color(1, 1, 1, a);
                    if (hasFace) _charFaceImage.Modulate = new Color(1, 1, 1, a);
                    await NextPlaybackFrameAsync();
                }
                _charBodyImage.Modulate = Colors.White;
                if (hasFace) _charFaceImage.Modulate = Colors.White;
            }
            else if (hasBody) { _charBodyImage.Modulate = Colors.White; if (hasFace) _charFaceImage.Modulate = Colors.White; }

            if (!hasBody && hasFace && forceFade)
            {
                float startA = _charFaceImage.Modulate.A;
                float t = 0f;
                while (t < duration)
                {
                    t += (float)GetProcessDeltaTime();
                    _charFaceImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0, 1)));
                    await NextPlaybackFrameAsync();
                }
                _charFaceImage.Modulate = Colors.White;
            }
            else if (!hasBody && hasFace) _charFaceImage.Modulate = Colors.White;

            if (hasChar2 && forceFade)
            {
                float startA = _char2Image.Modulate.A;
                float t = 0f;
                while (t < duration)
                {
                    t += (float)GetProcessDeltaTime();
                    _char2Image.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0, 1)));
                    await NextPlaybackFrameAsync();
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
                _charBodyImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(bodyA,  0, p));
                _charFaceImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(faceA,  0, p));
                _char2Image.Modulate    = new Color(1, 1, 1, Mathf.Lerp(c2A,    0, p));
                await NextPlaybackFrameAsync();
            }
            _charBodyImage.Modulate = new Color(1, 1, 1, 0); _charBodyImage.Texture = null;
            _charFaceImage.Modulate = new Color(1, 1, 1, 0); _charFaceImage.Texture = null;
            _char2Image.Modulate    = new Color(1, 1, 1, 0); _char2Image.Texture    = null;
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
            if (_charBodyImage.Texture is Texture2D bt && bt.ResourcePath.GetFile().GetBaseName() == n) hideBody = true;
            if (_charFaceImage.Texture is Texture2D ft && ft.ResourcePath.GetFile().GetBaseName() == n) hideFace = true;
            if (_char2Image.Texture    is Texture2D c2t && c2t.ResourcePath.GetFile().GetBaseName() == n) hideChar2 = true;
        }
        if (!hideBody && !hideFace && !hideChar2) return;

        float bodyA = _charBodyImage.Modulate.A, faceA = _charFaceImage.Modulate.A, c2A = _char2Image.Modulate.A;
        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            float p = Mathf.Clamp(t / duration, 0, 1);
            if (hideBody)  _charBodyImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(bodyA, 0, p));
            if (hideFace)  _charFaceImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(faceA, 0, p));
            if (hideChar2) _char2Image.Modulate    = new Color(1, 1, 1, Mathf.Lerp(c2A,   0, p));
            await NextPlaybackFrameAsync();
        }
        if (hideBody)  { _charBodyImage.Modulate = new Color(1,1,1,0); _charBodyImage.Texture = null; }
        if (hideFace)  { _charFaceImage.Modulate = new Color(1,1,1,0); _charFaceImage.Texture = null; }
        if (hideChar2) { _char2Image.Modulate    = new Color(1,1,1,0); _char2Image.Texture    = null; }
        if (_charBodyImage.Texture == null && _charFaceImage.Texture == null)
        { _charBodyOffset = Vector2.Zero; _charBodySize = new Vector2(650, 1338); ApplyCharBodyTransform(); }
        if (_char2Image.Texture == null)
        { _char2Offset = Vector2.Zero; _char2Size = new Vector2(650, 1338); ApplyChar2Transform(); }
    }

    async Task HandleHideCHAsync(string hideCH)
    {
        string val = (hideCH ?? "").Trim();
        if (val == "" || val == "0" || val == "1")
            await FadeCharAsync("", false);
        else
            await FadeCharSelectiveAsync(val);
    }

    async Task FadeChar2ShowAsync(string name)
    {
        float duration = 0.5f;
        bool forceFade = false;
        string imgName = name.Trim();
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
            float startA = _char2Image.Modulate.A;
            float t = 0f;
            while (t < duration)
            {
                t += (float)GetProcessDeltaTime();
                _char2Image.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0, 1)));
                await NextPlaybackFrameAsync();
            }
        }
        _char2Image.Modulate = Colors.White;
    }

    async Task FadeChar2Async(bool fadeIn)
    {
        float duration = 0.5f;
        float startA = _char2Image.Modulate.A;
        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            _char2Image.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 0f, Mathf.Clamp(t / duration, 0, 1)));
            await NextPlaybackFrameAsync();
        }
        _char2Image.Modulate = new Color(1, 1, 1, 0); _char2Image.Texture = null;
        _char2Offset = Vector2.Zero; _char2Size = new Vector2(650, 1338); ApplyChar2Transform();
    }

    // ─────────────────────────────────────────────────────────────
    // 背景處理
    // ─────────────────────────────────────────────────────────────
    protected virtual string ResolveBackgroundName(string name) => name;
    protected virtual string ForegroundForBackground(string name) => null;
    TextureRect _backgroundForeground;

    async Task HandleBGAsync(string bgName)
    {
        string name = bgName.Trim(); bool crossfade = false;
        if (name.Contains(","))
        {
            var parts = name.Split(',');
            name = parts[0].Trim();
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
            var tex = ResourceHelper.LoadTexAny("Backgrounds/" + ResolveBackgroundName(name));
            if (tex != null)
            {
                if (crossfade) await CrossfadeBGAsync(tex);
                else { _bgImage.Texture = tex; _bgImage.Modulate = Colors.White; }
                _bgImage.Visible = true;
            }
        }
        string foreground = ForegroundForBackground(name);
        if (_backgroundForeground != null)
        {
            _backgroundForeground.Texture = string.IsNullOrEmpty(foreground)
                ? null : ResourceHelper.LoadTexAny("Backgrounds/" + foreground);
            _backgroundForeground.Visible = _backgroundForeground.Texture != null;
        }
    }

    async Task CrossfadeBGAsync(Texture2D newTex, float duration = 0.6f)
    {
        var overlay = new TextureRect();
        overlay.Texture    = newTex;
        overlay.StretchMode = TextureRect.StretchModeEnum.Scale;
        overlay.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        overlay.Modulate    = new Color(1, 1, 1, 0);
        _panel.AddChild(overlay);
        _panel.MoveChild(overlay, _bgImage.GetIndex() + 1);
        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            overlay.Modulate = new Color(1, 1, 1, Mathf.Clamp(t / duration, 0, 1));
            await NextPlaybackFrameAsync();
        }
        _bgImage.Texture = newTex; _bgImage.Modulate = Colors.White;
        overlay.QueueFree();
    }

    async Task FadeBGColorAsync(Color targetColor, float duration = 0.6f)
    {
        Color startColor = _bgImage.Modulate;
        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            _bgImage.Modulate = startColor.Lerp(targetColor, Mathf.Clamp(t / duration, 0, 1));
            await NextPlaybackFrameAsync();
        }
        _bgImage.Texture = null; _bgImage.Modulate = targetColor;
    }

    // ─────────────────────────────────────────────────────────────
    // 音訊
    // ─────────────────────────────────────────────────────────────
    async Task HandleAudioAsync(DialogueSystem.DialogueLine line)
    {
        if (!string.IsNullOrEmpty(line.bgm))
        {
            if (line.bgm.Trim().ToLower() == "stop")
                _ = FadeOutAudioAsync(_bgmPlayer, 0.8f);
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
                var parts = line.se.Split(',');
                seName = parts[0].Trim();
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
    { await TimerAsync(delay); _sePlayer.Stream = stream; _sePlayer.Play(); }

    async Task FadeOutAudioAsync(AudioStreamPlayer player, float duration)
    {
        float startVol = Mathf.DbToLinear(player.VolumeDb);
        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            player.VolumeDb = Mathf.LinearToDb(Mathf.Lerp(startVol, 0f, t / duration));
            await NextPlaybackFrameAsync();
        }
        player.Stop(); player.VolumeDb = Mathf.LinearToDb(startVol);
    }

    async Task RestoreBGMAfterMEAsync(float targetVol)
    {
        while (_mePlayer.Playing) await NextPlaybackFrameAsync();
        float t = 0f;
        while (t < 0.5f)
        {
            t += (float)GetProcessDeltaTime();
            _bgmPlayer.VolumeDb = Mathf.LinearToDb(Mathf.Lerp(0.2f, targetVol, t / 0.5f));
            await NextPlaybackFrameAsync();
        }
        _bgmPlayer.VolumeDb = Mathf.LinearToDb(targetVol);
    }

    // ─────────────────────────────────────────────────────────────
    // 立繪 / CG 位移動畫
    // ─────────────────────────────────────────────────────────────
    async Task AnimateCharTransformAsync(string csRaw, string chX, string chY)
    {
        var chSP = DialogueSystem.ParseValueWithDuration(csRaw);
        var chXP = DialogueSystem.ParseValueWithDuration(chX);
        var chYP = DialogueSystem.ParseValueWithDuration(chY);
        Vector2 targetSize = !string.IsNullOrEmpty(csRaw) && chSP.value > 0f
            ? new Vector2(650f * chSP.value, 1338f * chSP.value) : _charBodySize;
        bool hasX = !string.IsNullOrEmpty(chX); bool hasY = !string.IsNullOrEmpty(chY);
        Vector2 targetPos = new(hasX ? chXP.value : _charBodyOffset.X, hasY ? chYP.value : _charBodyOffset.Y);
        float dur = 0.4f;
        if (!string.IsNullOrEmpty(csRaw)) dur = Mathf.Max(dur, chSP.durationSec);
        if (hasX) dur = Mathf.Max(dur, chXP.durationSec);
        if (hasY) dur = Mathf.Max(dur, chYP.durationSec);
        if (targetSize == _charBodySize && targetPos == _charBodyOffset) return;
        Vector2 startSize = _charBodySize, startPos = _charBodyOffset;
        float t = 0f;
        while (t < dur)
        {
            t += (float)GetProcessDeltaTime();
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t / dur, 0, 1));
            _charBodySize = startSize.Lerp(targetSize, p); _charBodyOffset = startPos.Lerp(targetPos, p);
            ApplyCharBodyTransform();
            await NextPlaybackFrameAsync();
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
        bool hasX2 = !string.IsNullOrEmpty(chX2); bool hasY2 = !string.IsNullOrEmpty(chY2);
        Vector2 tgtPos = new(hasX2 ? c2XP.value : _char2Offset.X, hasY2 ? c2YP.value : _char2Offset.Y);
        float dur = 0.4f;
        if (!string.IsNullOrEmpty(csRaw2)) dur = Mathf.Max(dur, c2SP.durationSec);
        if (hasX2) dur = Mathf.Max(dur, c2XP.durationSec);
        if (hasY2) dur = Mathf.Max(dur, c2YP.durationSec);
        if (tgtSize == _char2Size && tgtPos == _char2Offset) return;
        Vector2 sSize = _char2Size, sPos = _char2Offset;
        float t = 0f;
        while (t < dur)
        {
            t += (float)GetProcessDeltaTime();
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t / dur, 0, 1));
            _char2Size = sSize.Lerp(tgtSize, p); _char2Offset = sPos.Lerp(tgtPos, p);
            ApplyChar2Transform();
            await NextPlaybackFrameAsync();
        }
        _char2Size = tgtSize; _char2Offset = tgtPos; ApplyChar2Transform();
    }

    async Task AnimateCGTransformAsync(DialogueSystem.DialogueLine line)
    {
        for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
        {
            if (_cgImages[li] == null) continue;
            var cg  = DialogueSystem.GetCGLayer(line, li);
            var cgSP = DialogueSystem.ParseValueWithDuration(cg.scale);
            var cgXP = DialogueSystem.ParseValueWithDuration(cg.x);
            var cgYP = DialogueSystem.ParseValueWithDuration(cg.y);
            float tgtScale = !string.IsNullOrEmpty(cg.scale) && cgSP.value > 0f ? cgSP.value : _cgScale[li];
            bool hasCgX = !string.IsNullOrEmpty(cg.x); bool hasCgY = !string.IsNullOrEmpty(cg.y);
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
            float startScale = _cgScale[li]; Vector2 startOff = _cgOffset[li];
            float t = 0f;
            while (t < dur)
            {
                t += (float)GetProcessDeltaTime();
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t / dur, 0, 1));
                _cgScale[li]  = Mathf.Lerp(startScale, tgtScale, p);
                _cgOffset[li] = startOff.Lerp(new Vector2(tgtOX, tgtOY), p);
                ApplyCGTransform(li);
                await NextPlaybackFrameAsync();
            }
            _cgScale[li] = tgtScale; _cgOffset[li] = new Vector2(tgtOX, tgtOY);
            ApplyCGTransform(li);
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
            bool hasCgX = !string.IsNullOrEmpty(cg.x); bool hasCgY = !string.IsNullOrEmpty(cg.y);
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
        bool hasX = !string.IsNullOrEmpty(line.chX); bool hasY = !string.IsNullOrEmpty(line.chY);
        if (hasX || hasY)
        {
            float posX = hasX ? DialogueSystem.ParseValueWithDuration(line.chX).value : _charBodyOffset.X;
            float posY = hasY ? DialogueSystem.ParseValueWithDuration(line.chY).value : _charBodyOffset.Y;
            _charBodyOffset = new Vector2(posX, posY);
        }
        ApplyCharBodyTransform();
    }

    // ─────────────────────────────────────────────────────────────
    // Transform 應用（Unity→Godot 座標轉換）
    // ─────────────────────────────────────────────────────────────
    void ApplyCharBodyTransform()
    {
        float hw = _charBodySize.X / 2f, h = _charBodySize.Y;
        float ox = _charBodyOffset.X,    oy = _charBodyOffset.Y;
        _charBodyImage.OffsetLeft  = -hw + ox; _charBodyImage.OffsetRight  =  hw + ox;
        _charBodyImage.OffsetTop   = -h  - oy; _charBodyImage.OffsetBottom = -oy;
        _charFaceImage.OffsetLeft  = -hw + ox; _charFaceImage.OffsetRight  =  hw + ox;
        _charFaceImage.OffsetTop   = -h  - oy; _charFaceImage.OffsetBottom = -oy;
    }

    void ApplyChar2Transform()
    {
        float hw = _char2Size.X / 2f, h = _char2Size.Y;
        float ox = _char2Offset.X,    oy = _char2Offset.Y;
        _char2Image.OffsetLeft  = -hw + ox; _char2Image.OffsetRight  =  hw + ox;
        _char2Image.OffsetTop   = -h  - oy; _char2Image.OffsetBottom = -oy;
    }

    void ApplyCGTransform(int layer)
    {
        _cgImages[layer].Scale    = new Vector2(_cgScale[layer], _cgScale[layer]);
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
        if (foVal != null)
        {
            var parsed = DialogueSystem.ParseFadeoutValue(foVal);
            foFrames = parsed.framesStr; foPriority = parsed.priority;
        }
        return (foVal, foColor, foPriority, foFrames);
    }

    (string val, bool priority) ParseHideCH(string raw)
    {
        string val = (raw ?? "").Trim(); bool priority = false;
        if (!string.IsNullOrEmpty(val) && val.Contains(","))
        {
            var parts = val.Split(',');
            val = parts[0].Trim();
            priority = parts.Length > 1 && parts[1].Trim() == "1";
        }
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
        if (_nameBarGO   != null) _nameBarGO.Visible   = v;
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
    protected virtual void BuildUI()
    {
        var panel = new Control();
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.AddChild(panel);
        _panel = panel;

        // 背景
        var bg = new TextureRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.StretchMode = TextureRect.StretchModeEnum.Scale;
        bg.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        bg.Modulate    = new Color(0.08f, 0.04f, 0.12f);
        panel.AddChild(bg);
        _bgImage = bg;

        // 前景獨立於背景貼圖，跟隨背景位置／縮放並位於角色下方。
        _backgroundForeground = new TextureRect
        {
            Name = "BackgroundForeground",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        bg.AddChild(_backgroundForeground);
        _backgroundForeground.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // 立繪
        var portraitLayer = new Control { Name = "PortraitFloatRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(portraitLayer);
        portraitLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        void SetupChar(out TextureRect tr, string goName)
        {
            tr = new TextureRect();
            tr.Name = goName;
            tr.MouseFilter = Control.MouseFilterEnum.Ignore;
            tr.StretchMode = TextureRect.StretchModeEnum.Scale;
            tr.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
            tr.AnchorLeft  = 0.5f; tr.AnchorRight  = 0.5f;
            tr.AnchorTop   = 1.0f; tr.AnchorBottom = 1.0f;
            tr.Modulate    = new Color(1, 1, 1, 0);
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

        // 對話框
        var box = new Panel();
        box.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        box.OffsetTop = -boxH; box.OffsetBottom = 0;
        var boxStyle = new StyleBoxFlat();
        boxStyle.BgColor = new Color(0, 0, 0, 0.88f);
        box.AddThemeStyleboxOverride("panel", boxStyle);
        panel.AddChild(box);
        _dialogueBox = box;

        var contentLabel = new Label();
        contentLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        contentLabel.OffsetLeft = 60; contentLabel.OffsetRight  = -60;
        contentLabel.OffsetTop  = 20; contentLabel.OffsetBottom = -20;
        contentLabel.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        contentLabel.AddThemeFontSizeOverride("font_size", 28);
        contentLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.96f, 1f));
        box.AddChild(contentLabel);
        _contentText = contentLabel;

        var hint = new Label();
        hint.Text = "▼ 點擊繼續";
        hint.AddThemeFontSizeOverride("font_size", 15);
        hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.4f));
        hint.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        hint.OffsetLeft = -180; hint.OffsetBottom = -10;
        box.AddChild(hint);

        // 名字框
        var nameBar = new Panel();
        nameBar.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        nameBar.OffsetLeft   = 50f;  nameBar.OffsetRight   = 350f;
        nameBar.OffsetBottom = -(boxH + 4f); nameBar.OffsetTop = -(boxH + 52f);
        _nameBarStyle = new StyleBoxFlat();
        _nameBarStyle.BgColor = new Color(0.65f, 0.12f, 0.25f, 0.95f);
        _nameBarStyle.CornerRadiusTopLeft = _nameBarStyle.CornerRadiusTopRight = 4;
        _nameBarStyle.CornerRadiusBottomLeft = _nameBarStyle.CornerRadiusBottomRight = 4;
        nameBar.AddThemeStyleboxOverride("panel", _nameBarStyle);
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

        // 控制按鈕列
        var btnBar = new HBoxContainer();
        btnBar.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        btnBar.OffsetRight  = -20f; btnBar.OffsetBottom = -(boxH + 6f);
        btnBar.OffsetTop    = -(boxH + 46f);
        panel.AddChild(btnBar);
        _btnBarGO = btnBar;
        // 初始隱藏：避免開場淡入時先出現空白對話框，等第一句台詞才顯示
        btnBar.Visible = false;
        if (_dialogueBox != null) _dialogueBox.Visible = false;

        var btnFF   = MkCtrlBtn(btnBar, "快速播放", 140f);
        var btnAuto = MkCtrlBtn(btnBar, "自動播放", 140f);
        var btnSkip = MkCtrlBtn(btnBar, "跳　過",   110f);
        MkCtrlBtn(btnBar, "💾 保存", 110f);
        MkCtrlBtn(btnBar, "📂 讀檔", 110f);

        _fastFwdBtnTxt  = btnFF.GetNode<Label>("T");
        _autoPlayBtnTxt = btnAuto.GetNode<Label>("T");
        var skipTxt     = btnSkip.GetNode<Label>("T");

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
        btnSkip.Pressed += () => {
            _skipAll = !_skipAll;
            skipTxt.AddThemeColorOverride("font_color",
                _skipAll ? new Color(1f, 0.45f, 0.45f) : new Color(1, 1, 1, 0.55f));
        };

        // 螢幕遮罩
        var fade = new ColorRect();
        fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        fade.Color       = new Color(0, 0, 0, 0);
        fade.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(fade);
        _fadeOverlay = fade;
    }

    Button MkCtrlBtn(Control parent, string label, float w)
    {
        var btn = new Button();
        btn.Name = "Btn_" + label;
        btn.CustomMinimumSize = new Vector2(w, 36f);
        var sb = new StyleBoxFlat(); sb.BgColor = new Color(0, 0, 0, 0.45f);
        btn.AddThemeStyleboxOverride("normal", sb);
        var sbH = new StyleBoxFlat(); sbH.BgColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        btn.AddThemeStyleboxOverride("hover",   sbH);
        btn.AddThemeStyleboxOverride("pressed", sbH);

        var lbl = new Label();
        lbl.Name = "T"; lbl.Text = label;
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
