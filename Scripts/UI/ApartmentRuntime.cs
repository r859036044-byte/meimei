using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Apartment 場景的主控腳本 — 自動播放 Day2_Start 對話
/// Godot 4 C# port of Unity ApartmentRuntime.cs
/// </summary>
public partial class ApartmentRuntime : Node
{
    NovelReader _reader;
    void EnsureReader()
    {
        _reader ??= NovelReader.Install(this, _dialogueBox, _contentLabel, _btnBarGO,
            () => _isTyping, () => _waitingClick, () => _lockClick,
            () => _skipTyping = true, () => _waitingClick = false, null);
        _reader.SetSpeakerBadge(_nameBarGO, _speakerLabel);
    }

    void BeginReadingLine(string text)
    {
        EnsureReader();
        _reader.BeginLine(_speakerLabel?.Text, text);
    }

    // 快進時動畫（立繪移動、淡入淡出）同步加速
    float AnimDt() => (float)GetProcessDeltaTime() * ((_reader?.FastNow ?? false) ? NovelReader.SkipSpeedMult : 1f);

    // ── 初次進入判斷 ─────────────────────────────────────────────
    static readonly HashSet<string> _playedDialogues = new HashSet<string>();
    static bool HasDialoguePlayed(string id) => _playedDialogues.Contains(id);
    static void MarkDialoguePlayed(string id) => _playedDialogues.Add(id);
    /// <summary>開新遊戲時清掉「已播過」紀錄（NewGameReset 呼叫）</summary>
    public static void ResetForNewGame() => _playedDialogues.Clear();

    // ── UI 節點 ──────────────────────────────────────────────────
    Label           _speakerLabel;
    RichTextLabel   _contentLabel;
    Control         _panel;
    TextureRect     _charBodyImage;
    TextureRect     _charFaceImage;
    TextureRect     _char2Image;
    Panel           _nameBarPanel;
    TextureRect     _bgImage;
    ColorRect       _fadeRect;
    bool            _screenBlack;
    Color           _screenFadeColor = Colors.Black;
    Control         _dialogueBox;
    Control         _nameBarGO;
    Control         _btnBarGO;
    CanvasLayer     _canvas;

    // Character transform state (tracked in Unity-coord space: Y-up)
    Vector2 _charBodyPosOffset  = Vector2.Zero;
    Vector2 _charBodySize       = new Vector2(650f, 1338f);
    Vector2 _char2PosOffset     = Vector2.Zero;
    Vector2 _char2Size          = new Vector2(650f, 1338f);

    // CG transform state per layer
    float[] _cgScale  = new float[DialogueSystem.CG_LAYER_COUNT];
    Vector2[] _cgOffset = new Vector2[DialogueSystem.CG_LAYER_COUNT];

    // ── 音訊 ─────────────────────────────────────────────────────
    AudioStreamPlayer _bgmPlayer;
    AudioStreamPlayer _sePlayer;
    AudioStreamPlayer _mePlayer;

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
    Label _fastFwdBtnLabel;
    Label _autoPlayBtnLabel;
    bool _hoveringSaveLoad;

    // ── CG 圖層 ──────────────────────────────────────────────────
    TextureRect[] _cgImages = new TextureRect[DialogueSystem.CG_LAYER_COUNT];

    public override void _Ready()
    {
        for (int i = 0; i < DialogueSystem.CG_LAYER_COUNT; i++)
        {
            _cgScale[i] = 1f;
            _cgOffset[i] = Vector2.Zero;
        }

        if (!HasDialoguePlayed("Day2_Start"))
        {
            _bgmPlayer = new AudioStreamPlayer(); AddChild(_bgmPlayer);
            _sePlayer  = new AudioStreamPlayer(); AddChild(_sePlayer);
            _mePlayer  = new AudioStreamPlayer(); AddChild(_mePlayer);
            SceneBgm.Install(this, SceneBgm.Morning, _bgmPlayer);

            // Apply volume from SettingsManager (stub — replace when ported)
            // var sm = SettingsManager.Instance;
            // if (sm != null) { _bgmPlayer.VolumeDb = Mathf.LinearToDb(sm.MusicVolume); ... }

            TaskLogger.Run(PlayDialogueAsync(), "Apartment.PlayDialogueAsync");
        }
    }

    public override void _Process(double delta)
    {
        // NovelReader 統一處理點擊、自動及快進。
    }

    public override void _Input(InputEvent @event)
    {
        // NovelReader 會排除工具列與歷史視窗的輸入。
    }

    // ── 主對話流程 ───────────────────────────────────────────────
    async Task PlayDialogueAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        // Day2_Start.json 已不存在（內容已併入 Day2/Day2_Start2.json，由 LivingRoom 先播放）。
        // 以前找不到檔案就直接 return，畫面會卡在空白的 Apartment 場景、後面流程全部不會跑。
        // 現在找不到或解析不到台詞時，直接走「對話結束後」的流程。
        string path = "res://Resources/Data/Dialogues/Day2/Day2_Start.json";
        DialogueSystem.DialogueData data = null;
        if (FileAccess.FileExists(path))
        {
            try { data = DialogueSystem.ParseDialogueData(DialogueSystem.PreprocessJson(FileAccess.GetFileAsString(path))); }
            catch (System.Exception e) { GD.PrintErr($"[Apartment] Day2_Start.json 解析失敗：{e.Message}"); }
        }

        BuildUI();
        if (data == null || data.Lines == null || data.Lines.Count == 0)
        {
            GD.Print("[Apartment] 沒有 Day2_Start 台詞，直接進入後續流程");
            EnsureReader();
            await FinishDay2StartAsync();
            return;
        }

        _panel.Visible = true;
        // 劇情播放期間（含立繪移動、淡入淡出）保留工具列與選單，可快進／返回主畫面
        EnsureReader();
        _reader.KeepChrome = true;

        if (_fadeRect != null)
        {
            _fadeRect.Color = Colors.Black;
            _panel.MoveChild(_fadeRect, _panel.GetChildCount() - 1);
        }

        foreach (var line in data.Lines)
        {
            // ── 音樂播放 ─────────────────────────────────────────
            if (!string.IsNullOrEmpty(line.bgm))
            {
                string bgmName = line.bgm.Trim();
                if (bgmName.ToLower() == "stop")
                    _ = FadeOutAudioAsync(_bgmPlayer, 0.8f);
                else
                {
                    var stream = ResourceLoader.Load<AudioStream>("res://Resources/Audio/bgm/" + bgmName + ".ogg");
                    if (stream != null) { _bgmPlayer.Stream = stream; _bgmPlayer.Play(); }
                }
            }
            if (!string.IsNullOrEmpty(line.se))
            {
                string seName = line.se;
                float seDelay = 0f;
                if (line.se.Contains(","))
                {
                    var parts = line.se.Split(',');
                    seName = parts[0].Trim();
                    float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float frames);
                    seDelay = frames / 60f;
                }
                var seStream = ResourceLoader.Load<AudioStream>("res://Resources/Audio/se/" + seName.Trim() + ".ogg");
                if (seStream != null)
                {
                    if (seDelay > 0f) _ = PlaySEDelayedAsync(seStream, seDelay);
                    else { _sePlayer.Stream = seStream; _sePlayer.Play(); }
                }
            }
            if (!string.IsNullOrEmpty(line.me))
            {
                var meStream = ResourceLoader.Load<AudioStream>("res://Resources/Audio/me/" + line.me.Trim() + ".ogg");
                if (meStream != null)
                {
                    float bgmVol = Mathf.DbToLinear(_bgmPlayer.VolumeDb);
                    _bgmPlayer.VolumeDb = Mathf.LinearToDb(0.2f);
                    _mePlayer.Stream = meStream; _mePlayer.Play();
                    _ = RestoreBGMAfterMEAsync(bgmVol);
                }
            }

            // ── fadeout 資訊 ─────────────────────────────────────
            string _foVal = null; Color _foColor = Colors.Black; bool _foPriority = false;
            var (foValCheck, foColorCheck) = DialogueSystem.GetFadeoutInfo(line);
            if (foValCheck != null) { _foVal = foValCheck; _foColor = foColorCheck; }
            string _foFrames = "";
            if (_foVal != null)
            {
                var (_foF, _foP) = DialogueSystem.ParseFadeoutValue(_foVal);
                _foFrames   = _foF;
                _foPriority = _foP;
            }

            // ── hideCH 優先解析 ───────────────────────────────────
            bool _hideCHPriority = false;
            bool _hideCHHandled  = false;
            string _hideCHVal    = (line.hideCH ?? "").Trim();
            if (!string.IsNullOrEmpty(_hideCHVal) && _hideCHVal.Contains(","))
            {
                var hcParts = _hideCHVal.Split(',');
                _hideCHVal = hcParts[0].Trim();
                _hideCHPriority = hcParts.Length > 1 && hcParts[1].Trim() == "1";
            }

            if (!_foPriority && _hideCHPriority && !string.IsNullOrEmpty(_hideCHVal))
            {
                await HandleHideCHAsync(_hideCHVal);
                _hideCHHandled = true;
            }

            // ── 背景切換 ─────────────────────────────────────────
            if (!string.IsNullOrEmpty(line.bgName))
            {
                string bgName = line.bgName.Trim();
                bool crossfade = false;
                if (bgName.Contains(","))
                {
                    var parts = bgName.Split(',');
                    bgName    = parts[0].Trim();
                    crossfade = parts.Length > 1 && parts[1].Trim().ToLower() == "fade";
                }
                if (bgName.ToLower() == "black")
                {
                    if (_bgImage != null)
                    {
                        if (crossfade) await FadeBGColorAsync(Colors.Black);
                        else { _bgImage.Texture = null; _bgImage.Modulate = Colors.Black; }
                    }
                }
                else if (bgName.ToLower() == "white")
                {
                    if (_bgImage != null)
                    {
                        if (crossfade) await FadeBGColorAsync(Colors.White);
                        else { _bgImage.Texture = null; _bgImage.Modulate = Colors.White; }
                    }
                }
                else
                {
                    var tex = ResourceHelper.LoadTexAny("Backgrounds/" + bgName);
                    if (tex != null && _bgImage != null)
                    {
                        if (crossfade) await CrossfadeBGAsync(tex);
                        else { _bgImage.Texture = tex; _bgImage.Modulate = Colors.White; }
                    }
                }
            }

            // 等待
            if (line.wait > 0f)
                await ToSignal(GetTree().CreateTimer(line.wait / 60f, false), SceneTreeTimer.SignalName.Timeout);

            // ── hideCG 優先 ───────────────────────────────────────
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!_foPriority && cg.hasHide && DialogueSystem.IsHideCGPriority(cg.hide))
                    await FadeCGAsync(li, "", false);
            }

            // ── CG 淡入（台詞前）─────────────────────────────────
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!_foPriority && cg.hasShow)
                    await FadeCGAsync(li, cg.show, true);
            }

            // ── CG 位移/縮放 ──────────────────────────────────────
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                if (_cgImages[li] == null || _foPriority) continue;
                var cg = DialogueSystem.GetCGLayer(line, li);
                var cgSP = DialogueSystem.ParseValueWithDuration(cg.scale);
                var cgXP = DialogueSystem.ParseValueWithDuration(cg.x);
                var cgYP = DialogueSystem.ParseValueWithDuration(cg.y);

                float targetScale = !string.IsNullOrEmpty(cg.scale) && cgSP.value > 0f
                    ? cgSP.value : _cgScale[li];
                bool hasCgX = !string.IsNullOrEmpty(cg.x);
                bool hasCgY = !string.IsNullOrEmpty(cg.y);
                float curOffX = _cgOffset[li].X;
                float curOffY = _cgOffset[li].Y;
                float tgtOffX = hasCgX ? cgXP.value : curOffX;
                float tgtOffY = hasCgY ? cgYP.value : curOffY;
                float cgDur = 0.4f;
                if (!string.IsNullOrEmpty(cg.scale)) cgDur = Mathf.Max(cgDur, cgSP.durationSec);
                if (hasCgX) cgDur = Mathf.Max(cgDur, cgXP.durationSec);
                if (hasCgY) cgDur = Mathf.Max(cgDur, cgYP.durationSec);

                bool needMove = Mathf.Abs(targetScale - _cgScale[li]) > 0.001f
                             || Mathf.Abs(tgtOffX - curOffX) > 0.1f
                             || Mathf.Abs(tgtOffY - curOffY) > 0.1f;
                if (needMove)
                {
                    float startScale = _cgScale[li];
                    float startX = curOffX, startY = curOffY;
                    float t2 = 0f;
                    while (t2 < cgDur)
                    {
                        t2 += AnimDt();
                        float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t2 / cgDur, 0f, 1f));
                        float s = Mathf.Lerp(startScale, targetScale, p);
                        float ox = Mathf.Lerp(startX, tgtOffX, p);
                        float oy = Mathf.Lerp(startY, tgtOffY, p);
                        _cgScale[li] = s; _cgOffset[li] = new Vector2(ox, oy);
                        ApplyCGTransform(li);
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                    _cgScale[li] = targetScale; _cgOffset[li] = new Vector2(tgtOffX, tgtOffY);
                    ApplyCGTransform(li);
                }
            }

            // ── 立繪位移/縮放 ──────────────────────────────────────
            bool _chScaleDeferred = false;
            string _csRaw = line.chScale;
            if (!string.IsNullOrEmpty(_csRaw) && _csRaw.Contains(","))
            {
                if (!float.TryParse(_csRaw.Split(',')[0].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    _chScaleDeferred = true;
                    _csRaw = _csRaw.Split(',')[_csRaw.Split(',').Length - 1].Trim();
                }
            }

            if (!_foPriority && !_chScaleDeferred)
            {
                var chSP = DialogueSystem.ParseValueWithDuration(_csRaw);
                var chXP = DialogueSystem.ParseValueWithDuration(line.chX);
                var chYP = DialogueSystem.ParseValueWithDuration(line.chY);
                Vector2 targetSize = !string.IsNullOrEmpty(line.chScale) && chSP.value > 0f
                    ? new Vector2(650f * chSP.value, 1338f * chSP.value) : _charBodySize;
                bool hasX = !string.IsNullOrEmpty(line.chX);
                bool hasY = !string.IsNullOrEmpty(line.chY);
                float posX = hasX ? chXP.value : _charBodyPosOffset.X;
                float posY = hasY ? chYP.value : _charBodyPosOffset.Y;
                Vector2 targetPos = new Vector2(posX, posY);
                float chDur = 0.4f;
                if (!string.IsNullOrEmpty(line.chScale)) chDur = Mathf.Max(chDur, chSP.durationSec);
                if (hasX) chDur = Mathf.Max(chDur, chXP.durationSec);
                if (hasY) chDur = Mathf.Max(chDur, chYP.durationSec);

                bool needMove = targetSize != _charBodySize || targetPos != _charBodyPosOffset;
                if (needMove)
                {
                    Vector2 startSize = _charBodySize, startPos = _charBodyPosOffset;
                    float t2 = 0f;
                    while (t2 < chDur)
                    {
                        t2 += AnimDt();
                        float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t2 / chDur, 0f, 1f));
                        _charBodySize       = startSize.Lerp(targetSize, p);
                        _charBodyPosOffset  = startPos.Lerp(targetPos, p);
                        ApplyCharBodyTransform();
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                    _charBodySize = targetSize; _charBodyPosOffset = targetPos;
                    ApplyCharBodyTransform();
                }
            }

            // hideCH + showCH 同行 → 先淡出
            if (!_foPriority && !_hideCHHandled && line.hideCH != null && !string.IsNullOrEmpty(line.showCH))
            {
                await HandleHideCHAsync(_hideCHVal);
                _hideCHHandled = true;
            }

            // 帶圖名的 chScale → 立即設定大小
            if (!_foPriority && _chScaleDeferred)
            {
                var chSP = DialogueSystem.ParseValueWithDuration(_csRaw);
                if (chSP.value > 0f)
                {
                    _charBodySize = new Vector2(650f * chSP.value, 1338f * chSP.value);
                    ApplyCharBodyTransform();
                }
            }

            // ── 第二角色位移/縮放 ─────────────────────────────────
            if (!_foPriority && _char2Image != null &&
                (!string.IsNullOrEmpty(line.chScale2) || !string.IsNullOrEmpty(line.chX2) || !string.IsNullOrEmpty(line.chY2)))
            {
                var c2SP = DialogueSystem.ParseValueWithDuration(line.chScale2);
                var c2XP = DialogueSystem.ParseValueWithDuration(line.chX2);
                var c2YP = DialogueSystem.ParseValueWithDuration(line.chY2);
                Vector2 tgtSize = !string.IsNullOrEmpty(line.chScale2) && c2SP.value > 0f
                    ? new Vector2(650f * c2SP.value, 1338f * c2SP.value) : _char2Size;
                bool hasX2 = !string.IsNullOrEmpty(line.chX2);
                bool hasY2 = !string.IsNullOrEmpty(line.chY2);
                Vector2 tgtPos = new Vector2(hasX2 ? c2XP.value : _char2PosOffset.X,
                                             hasY2 ? c2YP.value : _char2PosOffset.Y);
                float c2Dur = 0.4f;
                if (!string.IsNullOrEmpty(line.chScale2)) c2Dur = Mathf.Max(c2Dur, c2SP.durationSec);
                if (hasX2) c2Dur = Mathf.Max(c2Dur, c2XP.durationSec);
                if (hasY2) c2Dur = Mathf.Max(c2Dur, c2YP.durationSec);
                if (tgtSize != _char2Size || tgtPos != _char2PosOffset)
                {
                    Vector2 sSize = _char2Size, sPos = _char2PosOffset;
                    float t2 = 0f;
                    while (t2 < c2Dur)
                    {
                        t2 += AnimDt();
                        float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t2 / c2Dur, 0f, 1f));
                        _char2Size = sSize.Lerp(tgtSize, p);
                        _char2PosOffset = sPos.Lerp(tgtPos, p);
                        ApplyChar2Transform();
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                    _char2Size = tgtSize; _char2PosOffset = tgtPos;
                    ApplyChar2Transform();
                }
            }

            // 第二角色：有 hideCH2 + showCH2 → 先淡出
            if (!_foPriority && line.hideCH2 != null && !string.IsNullOrEmpty(line.showCH2))
                await FadeChar2Async(false);

            // 立繪淡入
            if (!_foPriority)
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

            // 只隱藏第二角色
            if (!_foPriority && line.hideCH2 != null && string.IsNullOrEmpty(line.showCH2))
                await FadeChar2Async(false);

            // fadein
            if (!string.IsNullOrEmpty(line.fadein))
            {
                float.TryParse(line.fadein.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float frames);
                float dur = frames > 0f ? frames / 60f : 0.5f;
                await ScreenFadeAsync(false, dur, _screenFadeColor);
                _screenBlack = false;
                if (Mathf.IsEqualApprox(line.autoNext, 1f)) continue;
            }

            // hideCG 非優先
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!_foPriority && cg.hasHide && !DialogueSystem.IsHideCGPriority(cg.hide))
                    await FadeCGAsync(li, "", false);
            }

            // 說話者
            string spk = (line.speaker ?? "").Trim();
            if (_speakerLabel != null) _speakerLabel.Text = spk;
            // 若 _reader 尚未初始化（第一行），nameBar 保持隱藏直到 SetSpeakerBadge 接管
            if (_nameBarGO != null)
                _nameBarGO.Visible = _reader != null && !string.IsNullOrEmpty(spk);
            // 名字欄樣式由 NovelReader.ApplySpeakerStyle 統一控制，這裡不再重複覆蓋

            // 黑屏中處理
            if (_screenBlack)
            {
                bool hasText = !string.IsNullOrEmpty(line.text);
                if (hasText)
                {
                    _panel.MoveChild(_dialogueBox, _panel.GetChildCount() - 1);
                    _dialogueBox.Visible = true;
                    // nameBar Visible 由 NovelReader.BeginLine 控制；若 _reader 還沒初始化先隱藏
                    if (_nameBarGO != null)
                        _nameBarGO.Visible = _reader != null && !string.IsNullOrEmpty(spk);
                    if (!string.IsNullOrEmpty(spk))
                        _panel.MoveChild(_nameBarGO, _panel.GetChildCount() - 1);
                    _btnBarGO.Visible = true;
                    await TypeLineAsync(line.text);

                    if (line.autoNext > 1f)
                    {
                        _waitingClick = true; _lockClick = true; _autoAdvance = false;
                        _ = AutoNextTimerAsync(line.autoNext / 60f);
                        while (_waitingClick) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        _lockClick = false;
                    }
                    else if (!Mathf.IsEqualApprox(line.autoNext, 1f))
                    {
                        _waitingClick = true; _autoAdvance = false;
                        if (_autoPlaySpeed > 0) _ = AutoAdvanceTimerAsync();
                        while (_waitingClick) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    SetDialogueUIVisible(false);
                    if (_fadeRect != null) _panel.MoveChild(_fadeRect, _panel.GetChildCount() - 1);
                }
                else
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                if (!_hideCHHandled && line.hideCH != null)
                    await HandleHideCHAsync(_hideCHVal);
                continue;
            }

            // autoNext == 1 或台詞為空 → 不顯示對話框
            if (Mathf.IsEqualApprox(line.autoNext, 1f) || string.IsNullOrEmpty(line.text))
            {
                SetDialogueUIVisible(false);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            else
            {
                SetDialogueUIVisible(true);
                await TypeLineAsync(line.text);

                if (line.autoNext > 1f)
                {
                    _waitingClick = true; _lockClick = true; _autoAdvance = false;
                    _ = AutoNextTimerAsync(line.autoNext / 60f);
                    while (_waitingClick) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    _lockClick = false;
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                else
                {
                    _waitingClick = true; _autoAdvance = false;
                    if (_autoPlaySpeed > 0) _ = AutoAdvanceTimerAsync();
                    while (_waitingClick) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }

            // ── fadeout ──────────────────────────────────────────
            if (_foVal != null)
            {
                SetDialogueUIVisible(false);
                float.TryParse(_foFrames, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float foF);
                float dur = foF > 0f ? foF / 60f : 0.5f;
                _screenFadeColor = _foColor;
                await ScreenFadeAsync(true, dur, _foColor);
                _screenBlack = true;

                if (_foPriority)
                {
                    if (_hideCHPriority && !_hideCHHandled && !string.IsNullOrEmpty(_hideCHVal))
                    {
                        await HandleHideCHAsync(_hideCHVal);
                        _hideCHHandled = true;
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
                    // CG 位移/縮放 — 直接設定（螢幕已遮住）
                    for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
                    {
                        if (_cgImages[li] == null) continue;
                        var cg = DialogueSystem.GetCGLayer(line, li);
                        var cgSP = DialogueSystem.ParseValueWithDuration(cg.scale);
                        if (!string.IsNullOrEmpty(cg.scale) && cgSP.value > 0f)
                            _cgScale[li] = cgSP.value;
                        bool hasCgX = !string.IsNullOrEmpty(cg.x);
                        bool hasCgY = !string.IsNullOrEmpty(cg.y);
                        if (hasCgX || hasCgY)
                        {
                            var cxP = DialogueSystem.ParseValueWithDuration(cg.x);
                            var cyP = DialogueSystem.ParseValueWithDuration(cg.y);
                            _cgOffset[li] = new Vector2(
                                hasCgX ? cxP.value : _cgOffset[li].X,
                                hasCgY ? cyP.value : _cgOffset[li].Y);
                        }
                        ApplyCGTransform(li);
                    }
                    // 立繪位移/縮放 — 直接設定
                    {
                        string csRaw2 = line.chScale;
                        if (!string.IsNullOrEmpty(csRaw2) && csRaw2.Contains(","))
                        {
                            if (!float.TryParse(csRaw2.Split(',')[0].Trim(),
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out _))
                                csRaw2 = csRaw2.Split(',')[csRaw2.Split(',').Length - 1].Trim();
                        }
                        var chSP2 = DialogueSystem.ParseValueWithDuration(csRaw2);
                        if (!string.IsNullOrEmpty(line.chScale) && chSP2.value > 0f)
                            _charBodySize = new Vector2(650f * chSP2.value, 1338f * chSP2.value);
                        bool hasX = !string.IsNullOrEmpty(line.chX);
                        bool hasY = !string.IsNullOrEmpty(line.chY);
                        if (hasX || hasY)
                        {
                            var cxP = DialogueSystem.ParseValueWithDuration(line.chX);
                            var cyP = DialogueSystem.ParseValueWithDuration(line.chY);
                            _charBodyPosOffset = new Vector2(
                                hasX ? cxP.value : _charBodyPosOffset.X,
                                hasY ? cyP.value : _charBodyPosOffset.Y);
                        }
                        ApplyCharBodyTransform();
                    }
                    if (!string.IsNullOrEmpty(line.showCH))
                        await FadeCharAsync(line.showCH, true);
                    for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
                    {
                        var cg = DialogueSystem.GetCGLayer(line, li);
                        if (cg.hasHide && !DialogueSystem.IsHideCGPriority(cg.hide))
                            await FadeCGAsync(li, "", false);
                    }
                }

                if (!_hideCHHandled && line.hideCH != null)
                    await HandleHideCHAsync(_hideCHVal);
                continue;
            }

            // 立繪淡出（點擊後）
            if (!_hideCHHandled && line.hideCH != null && string.IsNullOrEmpty(line.showCH))
                await HandleHideCHAsync(_hideCHVal);
        }

        // 對話結束
        await FinishDay2StartAsync();
    }

    async Task FinishDay2StartAsync()
    {
        MarkDialoguePlayed("Day2_Start");
        if (_reader != null) _reader.KeepChrome = false;
        _panel.Visible = false;
        await ScreenFadeAsync(true, 1f, Colors.Black);
        GD.Print("[Apartment] Day2_Start 播放完畢，前往 TrainingRoom 場景");
        if (PlayerDataManager.Instance?.R18ContentEnabled == true)
        {
            TrainingRoomRuntime.PendingDay2First = true;
            await SceneLoader.GoToAsync(this, "res://Scenes/TrainingRoom.tscn");
        }
        else
        {
            GD.Print("[Apartment] 全年齡模式：略過 TrainingRoom，顯示蓋被子選擇");
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) { pdm.ResetTodayTracking(); pdm.CurrentNight = 1; }
            await AllAgesChoiceHelper.ShowBlanketChoiceAsync(this);
            LivingRoomRuntime.PendingDay3 = true;
            GetTree().ChangeSceneToFile("res://Scenes/LivingRoom.tscn");
        }
    }

    // ── 打字機 ───────────────────────────────────────────────────
    async Task TypeLineAsync(string text)
    {
        BeginReadingLine(text);
        if (_contentLabel == null) return;
        _contentLabel.Text = "";
        _isTyping = true; _skipTyping = false;
        foreach (char c in text)
        {
            while (_reader.Paused) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_skipTyping) { _contentLabel.Text = text; break; }
            _contentLabel.Text += c;
            await ToSignal(GetTree().CreateTimer(_fastForward ? 0.005f : 0.03f / NovelReader.AutoTypeMul, false),
                SceneTreeTimer.SignalName.Timeout);
        }
        _isTyping = false;
    }

    // ── 螢幕淡入/淡出 ────────────────────────────────────────────
    async Task ScreenFadeAsync(bool toBlack, float duration, Color color)
    {
        if (_fadeRect == null) return;
        _panel.MoveChild(_fadeRect, _panel.GetChildCount() - 1);
        float startA = toBlack ? 0f : 1f, endA = toBlack ? 1f : 0f;
        float t = 0f;
        while (t < duration)
        {
            t += AnimDt();
            float a = Mathf.Lerp(startA, endA, Mathf.Clamp(t / duration, 0f, 1f));
            _fadeRect.Color = new Color(color.R, color.G, color.B, a);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _fadeRect.Color = new Color(color.R, color.G, color.B, endA);
    }

    // ── CG 淡入/淡出 ─────────────────────────────────────────────
    async Task FadeCGAsync(int layer, string cgName, bool fadeIn, float duration = 0.6f)
    {
        if (fadeIn)
        {
            if (_cgImages[layer] == null)
            {
                var cg = new TextureRect();
                cg.Name = "CG_Layer_" + layer;
                cg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                cg.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                cg.StretchMode = TextureRect.StretchModeEnum.Scale;
                cg.MouseFilter = Control.MouseFilterEnum.Ignore;
                cg.Modulate = new Color(1, 1, 1, 0);
                _panel.AddChild(cg);
                int bgIdx = _bgImage != null ? _bgImage.GetIndex() + 1 : 0;
                _panel.MoveChild(cg, bgIdx + layer);
                _cgImages[layer] = cg;
                _cgScale[layer] = 1f;
                _cgOffset[layer] = Vector2.Zero;
            }
            var tex = ResourceLoader.Load<Texture2D>("res://Resources/CG/" + cgName.Trim() + ".png");
            if (tex == null) { GD.PushWarning($"[Apartment] 找不到 CG：{cgName}"); return; }
            _cgImages[layer].Texture = tex;
            _cgImages[layer].Visible = true;
            _cgScale[layer] = 1f; _cgOffset[layer] = Vector2.Zero;
            ApplyCGTransform(layer);

            float t = 0f;
            while (t < duration)
            {
                t += AnimDt();
                _cgImages[layer].Modulate = new Color(1, 1, 1, Mathf.Clamp(t / duration, 0f, 1f));
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
                t += AnimDt();
                _cgImages[layer].Modulate = new Color(1, 1, 1, 1f - Mathf.Clamp(t / duration, 0f, 1f));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            _cgImages[layer].Modulate = new Color(1, 1, 1, 0);
            _cgImages[layer].Texture = null;
            _cgScale[layer] = 1f; _cgOffset[layer] = Vector2.Zero;
            ApplyCGTransform(layer);
            _cgImages[layer].Visible = false;
        }
    }

    // ── 立繪淡入/淡出 ────────────────────────────────────────────
    async Task FadeCharAsync(string name, bool fadeIn)
    {
        const float duration = 0.5f;
        if (fadeIn)
        {
            string bodyName = null, faceName = null, char2Name = null;
            bool forceFade = false;

            if (name.Contains(","))
            {
                var parts = name.Split(',');
                string lastPart = parts[parts.Length - 1].Trim().ToLower();
                if (lastPart == "fade")
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
                float startA = _charBodyImage.Modulate.A, t = 0f;
                while (t < duration)
                {
                    t += AnimDt();
                    float a = Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0f, 1f));
                    _charBodyImage.Modulate = new Color(1, 1, 1, a);
                    if (hasFace) _charFaceImage.Modulate = new Color(1, 1, 1, a);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                _charBodyImage.Modulate = Colors.White;
                if (hasFace) _charFaceImage.Modulate = Colors.White;
            }
            else if (hasFace && forceFade)
            {
                float startA = _charFaceImage.Modulate.A, t = 0f;
                while (t < duration)
                {
                    t += AnimDt();
                    _charFaceImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0f, 1f)));
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                _charFaceImage.Modulate = Colors.White;
            }
            else if (hasFace) _charFaceImage.Modulate = Colors.White;

            if (hasChar2 && forceFade)
            {
                float startA = _char2Image.Modulate.A, t = 0f;
                while (t < duration)
                {
                    t += AnimDt();
                    _char2Image.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0f, 1f)));
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                _char2Image.Modulate = Colors.White;
            }
            else if (hasChar2) _char2Image.Modulate = Colors.White;
        }
        else
        {
            float bodyA = _charBodyImage.Modulate.A;
            float faceA = _charFaceImage.Modulate.A;
            float c2A   = _char2Image.Modulate.A;
            float t = 0f;
            while (t < duration)
            {
                t += AnimDt();
                float p = Mathf.Clamp(t / duration, 0f, 1f);
                _charBodyImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(bodyA, 0f, p));
                _charFaceImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(faceA, 0f, p));
                _char2Image.Modulate    = new Color(1, 1, 1, Mathf.Lerp(c2A,   0f, p));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            _charBodyImage.Texture = null; _charBodyImage.Modulate = new Color(1, 1, 1, 0);
            _charFaceImage.Texture = null; _charFaceImage.Modulate = new Color(1, 1, 1, 0);
            _char2Image.Texture    = null; _char2Image.Modulate    = new Color(1, 1, 1, 0);
            _charBodyPosOffset = Vector2.Zero; _charBodySize = new Vector2(650f, 1338f); ApplyCharBodyTransform();
            _char2PosOffset = Vector2.Zero; _char2Size = new Vector2(650f, 1338f); ApplyChar2Transform();
        }
    }

    async Task FadeCharSelectiveAsync(string names)
    {
        const float duration = 0.5f;
        var parts = names.Split(',');
        bool hideBody = false, hideFace = false, hideChar2 = false;
        foreach (var p in parts)
        {
            string n = p.Trim();
            if (_charBodyImage.Texture != null && System.IO.Path.GetFileNameWithoutExtension(_charBodyImage.Texture.ResourcePath) == n) hideBody = true;
            if (_charFaceImage.Texture != null && System.IO.Path.GetFileNameWithoutExtension(_charFaceImage.Texture.ResourcePath) == n) hideFace = true;
            if (_char2Image.Texture    != null && System.IO.Path.GetFileNameWithoutExtension(_char2Image.Texture.ResourcePath)    == n) hideChar2 = true;
        }
        if (!hideBody && !hideFace && !hideChar2) return;

        float bodyA = _charBodyImage.Modulate.A, faceA = _charFaceImage.Modulate.A, c2A = _char2Image.Modulate.A;
        float t = 0f;
        while (t < duration)
        {
            t += AnimDt();
            float p = Mathf.Clamp(t / duration, 0f, 1f);
            if (hideBody)  _charBodyImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(bodyA, 0f, p));
            if (hideFace)  _charFaceImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(faceA, 0f, p));
            if (hideChar2) _char2Image.Modulate    = new Color(1, 1, 1, Mathf.Lerp(c2A,   0f, p));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (hideBody)  { _charBodyImage.Texture = null; _charBodyImage.Modulate = new Color(1,1,1,0); }
        if (hideFace)  { _charFaceImage.Texture = null; _charFaceImage.Modulate = new Color(1,1,1,0); }
        if (hideChar2) { _char2Image.Texture    = null; _char2Image.Modulate    = new Color(1,1,1,0); }
        if (_charBodyImage.Texture == null && _charFaceImage.Texture == null)
        {
            _charBodyPosOffset = Vector2.Zero; _charBodySize = new Vector2(650f, 1338f); ApplyCharBodyTransform();
        }
        if (_char2Image.Texture == null)
        {
            _char2PosOffset = Vector2.Zero; _char2Size = new Vector2(650f, 1338f); ApplyChar2Transform();
        }
    }

    async Task HandleHideCHAsync(string hideCH)
    {
        string val = (hideCH ?? "").Trim();
        if (val == "" || val == "0" || val == "1") await FadeCharAsync("", false);
        else await FadeCharSelectiveAsync(val);
    }

    async Task FadeChar2ShowAsync(string name)
    {
        const float duration = 0.5f;
        bool forceFade = false;
        string imgName = name.Trim();
        if (imgName.Contains(","))
        {
            var parts = imgName.Split(',');
            if (parts[parts.Length - 1].Trim().ToLower() == "fade")
            {
                forceFade = true; imgName = parts[0].Trim();
            }
        }
        var tex = ResourceLoader.Load<Texture2D>("res://Resources/Character/" + imgName + ".png");
        if (tex == null) return;
        _char2Image.Texture = tex; _char2Image.Visible = true;
        if (forceFade)
        {
            float startA = _char2Image.Modulate.A, t = 0f;
            while (t < duration)
            {
                t += AnimDt();
                _char2Image.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 1f, Mathf.Clamp(t / duration, 0f, 1f)));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
        _char2Image.Modulate = Colors.White;
    }

    async Task FadeChar2Async(bool fadeIn)
    {
        const float duration = 0.5f;
        float startA = _char2Image.Modulate.A, t = 0f;
        while (t < duration)
        {
            t += AnimDt();
            _char2Image.Modulate = new Color(1, 1, 1, Mathf.Lerp(startA, 0f, Mathf.Clamp(t / duration, 0f, 1f)));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _char2Image.Texture = null; _char2Image.Modulate = new Color(1,1,1,0);
        _char2PosOffset = Vector2.Zero; _char2Size = new Vector2(650f, 1338f); ApplyChar2Transform();
    }

    // ── 背景交叉淡入/純色淡入 ────────────────────────────────────
    async Task CrossfadeBGAsync(Texture2D newTex, float duration = 0.6f)
    {
        var overlay = new TextureRect();
        overlay.Texture = newTex;
        overlay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        overlay.StretchMode = TextureRect.StretchModeEnum.Scale;
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        overlay.Modulate = new Color(1, 1, 1, 0);
        _panel.AddChild(overlay);
        _panel.MoveChild(overlay, _bgImage.GetIndex() + 1);

        float t = 0f;
        while (t < duration)
        {
            t += AnimDt();
            overlay.Modulate = new Color(1, 1, 1, Mathf.Clamp(t / duration, 0f, 1f));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
            t += AnimDt();
            _bgImage.Modulate = startColor.Lerp(targetColor, Mathf.Clamp(t / duration, 0f, 1f));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _bgImage.Texture = null; _bgImage.Modulate = targetColor;
    }

    // ── 音效 ─────────────────────────────────────────────────────
    async Task PlaySEDelayedAsync(AudioStream stream, float delay)
    {
        await ToSignal(GetTree().CreateTimer(delay, false), SceneTreeTimer.SignalName.Timeout);
        _sePlayer.Stream = stream; _sePlayer.Play();
    }

    async Task FadeOutAudioAsync(AudioStreamPlayer player, float duration)
    {
        float startVol = Mathf.DbToLinear(player.VolumeDb);
        float t = 0f;
        while (t < duration)
        {
            t += AnimDt();
            player.VolumeDb = Mathf.LinearToDb(Mathf.Lerp(startVol, 0f, t / duration));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        player.Stop(); player.VolumeDb = Mathf.LinearToDb(startVol);
    }

    async Task RestoreBGMAfterMEAsync(float targetVol)
    {
        while (_mePlayer.Playing)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        float t = 0f;
        while (t < 0.5f)
        {
            t += AnimDt();
            _bgmPlayer.VolumeDb = Mathf.LinearToDb(Mathf.Lerp(0.2f, targetVol, t / 0.5f));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _bgmPlayer.VolumeDb = Mathf.LinearToDb(targetVol);
    }

    async Task AutoNextTimerAsync(float seconds)
    {
        if (_reader != null && await _reader.WaitScriptDelay(seconds))
            _waitingClick = false;
    }

    async Task AutoAdvanceTimerAsync()
    {
        float[] delays = { 0f, 2.5f, 1.5f, 0.8f, 0.2f };
        float d = delays[Mathf.Clamp(_autoPlaySpeed, 0, 4)];
        if (d > 0f)
            await ToSignal(GetTree().CreateTimer(d, false), SceneTreeTimer.SignalName.Timeout);
        _autoAdvance = true;
        if (_waitingClick) _waitingClick = false;
    }

    // ── Transform helpers ────────────────────────────────────────
    // Character images are anchored bottom-center in a 1920×1080 parent.
    // posOffset: Unity space (Y up). size: pixels.
    void ApplyCharBodyTransform()
    {
        float hw = _charBodySize.X / 2f;
        float h  = _charBodySize.Y;
        float ox = _charBodyPosOffset.X;
        float oy = _charBodyPosOffset.Y; // Unity Y-up → Godot Y = -oy from bottom anchor
        _charBodyImage.OffsetLeft  = -hw + ox;
        _charBodyImage.OffsetRight =  hw + ox;
        _charBodyImage.OffsetTop   = -h - oy;  // Y-up: moving up = larger negative in Godot
        _charBodyImage.OffsetBottom = -oy;
        // Face image mirrors exactly
        _charFaceImage.OffsetLeft  = _charBodyImage.OffsetLeft;
        _charFaceImage.OffsetRight = _charBodyImage.OffsetRight;
        _charFaceImage.OffsetTop   = _charBodyImage.OffsetTop;
        _charFaceImage.OffsetBottom= _charBodyImage.OffsetBottom;
    }

    void ApplyChar2Transform()
    {
        float hw = _char2Size.X / 2f;
        float h  = _char2Size.Y;
        float ox = _char2PosOffset.X;
        float oy = _char2PosOffset.Y;
        _char2Image.OffsetLeft  = -hw + ox;
        _char2Image.OffsetRight =  hw + ox;
        _char2Image.OffsetTop   = -h - oy;
        _char2Image.OffsetBottom = -oy;
    }

    void ApplyCGTransform(int layer)
    {
        if (_cgImages[layer] == null) return;
        float s  = _cgScale[layer];
        float ox = _cgOffset[layer].X;
        float oy = _cgOffset[layer].Y; // Unity Y-up → Godot -Y
        _cgImages[layer].Scale    = new Vector2(s, s);
        _cgImages[layer].Position = new Vector2(ox, -oy); // shift from top-left of full-rect
    }

    void SetDialogueUIVisible(bool visible)
    {
        if (_dialogueBox != null) _dialogueBox.Visible = visible;
        // _nameBarGO.Visible 由 NovelReader.BeginLine 統一控制，這裡不碰
        if (_btnBarGO    != null) _btnBarGO.Visible     = visible;
    }

    // ── BuildUI ──────────────────────────────────────────────────
    void BuildUI()
    {
        _canvas = new CanvasLayer(); _canvas.Layer = 100;
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, _canvas);
        _canvas.CallDeferred(Node.MethodName.AddChild, BuildUIDeferred());
    }

    Control BuildUIDeferred()
    {
        var panel = new Control();
        panel.Name = "DialoguePanel";
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _panel = panel;

        // 背景
        var bg = new TextureRect();
        bg.Name = "BG";
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        bg.StretchMode = TextureRect.StretchModeEnum.Scale;
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        bg.Modulate = new Color(0.08f, 0.04f, 0.12f);
        panel.AddChild(bg);
        _bgImage = bg;

        // 立繪容器
        var charRoot = new Control();
        charRoot.Name = "CharRoot";
        charRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        charRoot.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(charRoot);

        // 身體圖層 (bottom-center anchor in 1920×1080 parent)
        _charBodyImage = new TextureRect();
        _charBodyImage.Name = "CharBody";
        _charBodyImage.AnchorLeft = 0.5f; _charBodyImage.AnchorRight = 0.5f;
        _charBodyImage.AnchorTop  = 1.0f; _charBodyImage.AnchorBottom = 1.0f;
        _charBodyImage.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _charBodyImage.StretchMode = TextureRect.StretchModeEnum.Scale;
        _charBodyImage.MouseFilter = Control.MouseFilterEnum.Ignore;
        _charBodyImage.Modulate = new Color(1, 1, 1, 0);
        charRoot.AddChild(_charBodyImage);

        _charFaceImage = new TextureRect();
        _charFaceImage.Name = "CharFace";
        _charFaceImage.AnchorLeft = 0.5f; _charFaceImage.AnchorRight = 0.5f;
        _charFaceImage.AnchorTop  = 1.0f; _charFaceImage.AnchorBottom = 1.0f;
        _charFaceImage.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _charFaceImage.StretchMode = TextureRect.StretchModeEnum.Scale;
        _charFaceImage.MouseFilter = Control.MouseFilterEnum.Ignore;
        _charFaceImage.Modulate = new Color(1, 1, 1, 0);
        charRoot.AddChild(_charFaceImage);

        _char2Image = new TextureRect();
        _char2Image.Name = "Char2";
        _char2Image.AnchorLeft = 0.5f; _char2Image.AnchorRight = 0.5f;
        _char2Image.AnchorTop  = 1.0f; _char2Image.AnchorBottom = 1.0f;
        _char2Image.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _char2Image.StretchMode = TextureRect.StretchModeEnum.Scale;
        _char2Image.MouseFilter = Control.MouseFilterEnum.Ignore;
        _char2Image.Modulate = new Color(1, 1, 1, 0);
        charRoot.AddChild(_char2Image);

        ApplyCharBodyTransform();
        ApplyChar2Transform();

        const float boxHeight = 260f;

        // 對話框
        var box = new Panel();
        box.Name = "Box";
        box.AnchorLeft = 0f; box.AnchorRight = 1f;
        box.AnchorTop  = 1f; box.AnchorBottom = 1f;
        box.OffsetTop = -boxHeight; box.OffsetBottom = 0f;
        var boxStyle = new StyleBoxFlat();
        boxStyle.BgColor = new Color(0f, 0f, 0f, 0.88f);
        box.AddThemeStyleboxOverride("panel", boxStyle);
        panel.AddChild(box);
        _dialogueBox = box;

        // 名字框
        var nameBar = new Panel();
        nameBar.Name = "NameBar";
        nameBar.AnchorLeft = 0f; nameBar.AnchorRight = 0f;
        nameBar.AnchorTop  = 1f; nameBar.AnchorBottom = 1f;
        nameBar.OffsetLeft = 50f; nameBar.OffsetRight = 350f;
        nameBar.OffsetTop = -(boxHeight + 4f + 48f); nameBar.OffsetBottom = -(boxHeight + 4f);
        var nbStyle = new StyleBoxFlat();
        nbStyle.BgColor = new Color(0f, 0f, 0f, 0f); // 透明初始，NovelReader 接管後再設樣式
        nameBar.AddThemeStyleboxOverride("panel", nbStyle);
        nameBar.Modulate = new Color(1f, 1f, 1f, 0f); // 完全透明，避免建構期閃出
        panel.AddChild(nameBar);
        _nameBarGO  = nameBar;
        _nameBarPanel = nameBar;

        _speakerLabel = new Label();
        _speakerLabel.Name = "SpeakerTxt";
        _speakerLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _speakerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _speakerLabel.VerticalAlignment = VerticalAlignment.Center;
        _speakerLabel.AddThemeFontSizeOverride("font_size", 26);
        _speakerLabel.AddThemeColorOverride("font_color", Colors.White);
        nameBar.AddChild(_speakerLabel);

        // 對話內文
        _contentLabel = new RichTextLabel();
        _contentLabel.Name = "ContentTxt";
        _contentLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _contentLabel.OffsetLeft = 60f; _contentLabel.OffsetRight = -60f;
        _contentLabel.OffsetTop = 20f;  _contentLabel.OffsetBottom = -20f;
        _contentLabel.BbcodeEnabled = false;
        _contentLabel.FitContent = false;
        _contentLabel.ScrollActive = false;
        _contentLabel.AddThemeFontSizeOverride("normal_font_size", 28);
        _contentLabel.AddThemeColorOverride("default_color", new Color(0.98f, 0.96f, 1f));
        box.AddChild(_contentLabel);

        // 點擊提示
        var hint = new Label();
        hint.Name = "HintTxt";
        hint.Text = "▼ 點擊繼續";
        hint.AnchorLeft = 1f; hint.AnchorRight = 1f;
        hint.AnchorTop  = 1f; hint.AnchorBottom = 1f;
        hint.OffsetLeft = -200f; hint.OffsetRight = -20f;
        hint.OffsetTop  = -34f;  hint.OffsetBottom = -10f;
        hint.HorizontalAlignment = HorizontalAlignment.Right;
        hint.AddThemeFontSizeOverride("font_size", 15);
        hint.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.4f));
        box.AddChild(hint);

        // 控制按鈕列
        var btnBar = new Control();
        btnBar.Name = "ControlBar";
        btnBar.AnchorLeft = 1f; btnBar.AnchorRight = 1f;
        btnBar.AnchorTop  = 1f; btnBar.AnchorBottom = 1f;
        btnBar.OffsetLeft = -800f; btnBar.OffsetRight = -20f;
        btnBar.OffsetTop  = -(boxHeight + 46f); btnBar.OffsetBottom = -(boxHeight + 6f);
        panel.AddChild(btnBar);
        _btnBarGO = btnBar;

        var btnFF   = MkCtrlBtn(btnBar, "快速播放", new Vector2(0f,    0f), 140f);
        var btnAuto = MkCtrlBtn(btnBar, "自動播放", new Vector2(-150f, 0f), 140f);
        var btnSkip = MkCtrlBtn(btnBar, "跳過",     new Vector2(-300f, 0f), 100f);
        var btnSave = MkCtrlBtn(btnBar, "💾 保存",  new Vector2(-410f, 0f), 140f);
        var btnLoad = MkCtrlBtn(btnBar, "📂 讀檔",  new Vector2(-560f, 0f), 140f);

        _fastFwdBtnLabel  = btnFF.GetNode<Label>("T");
        _autoPlayBtnLabel = btnAuto.GetNode<Label>("T");

        btnFF.Pressed += () =>
        {
            _fastForward = !_fastForward;
            _fastFwdBtnLabel.AddThemeColorOverride("font_color",
                _fastForward ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 1f, 1f, 0.55f));
        };
        btnAuto.Pressed += () =>
        {
            _autoPlaySpeed = (_autoPlaySpeed + 1) % 5;
            _autoPlayBtnLabel.Text = _autoPlaySpeed == 0 ? "自動播放" : "自動 X" + _autoPlaySpeed;
            _autoPlayBtnLabel.AddThemeColorOverride("font_color",
                _autoPlaySpeed == 0 ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 0.85f, 0.25f));
        };
        btnSkip.Pressed += () =>
        {
            // Stop all running tasks by marking complete
            MarkDialoguePlayed("Day2_Start");
            if (_panel != null) _panel.Visible = false;
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
                if (_cgImages[li] != null) _cgImages[li].Visible = false;
            if (PlayerDataManager.Instance?.R18ContentEnabled == true)
            {
                TrainingRoomRuntime.PendingDay2First = true;
                TaskLogger.Run(SkipFadeAndLoadAsync("TrainingRoom"), "Apartment.SkipFadeAndLoadAsync");
            }
            else
            {
                GD.Print("[Apartment] 全年齡模式（跳過）：略過 TrainingRoom，顯示蓋被子選擇");
                var pdmSkip = PlayerDataManager.Instance;
                if (pdmSkip != null) { pdmSkip.ResetTodayTracking(); pdmSkip.CurrentNight = 1; }
                LivingRoomRuntime.PendingDay3 = true;
                TaskLogger.Run(AllAgesChoiceHelper.ShowBlanketChoiceAndLoadAsync(this, "res://Scenes/LivingRoom.tscn"), "Apartment.AllAgesChoiceSkip");
            }
        };
        // Save/Load stubs — wire to SaveLoadController when ported
        btnSave.Pressed += () => GD.Print("[ApartmentRuntime] Save (TODO: wire SaveLoadController)");
        btnLoad.Pressed += () => GD.Print("[ApartmentRuntime] Load (TODO: wire SaveLoadController)");

        // 初始隱藏：避免開場淡入時先出現空白對話框，等第一句台詞才顯示
        box.Visible = false;
        btnBar.Visible = false;

        // 螢幕遮罩
        _fadeRect = new ColorRect();
        _fadeRect.Name = "FadeOverlay";
        _fadeRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _fadeRect.Color = new Color(0, 0, 0, 0);
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(_fadeRect);

        return panel;
    }

    async Task SkipFadeAndLoadAsync(string sceneName)
    {
        if (_fadeRect != null)
        {
            _panel.MoveChild(_fadeRect, _panel.GetChildCount() - 1);
            float t = 0f;
            while (t < 0.5f)
            {
                t += AnimDt();
                _fadeRect.Color = new Color(0, 0, 0, Mathf.Clamp(t / 0.5f, 0f, 1f));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
        if (sceneName == "TrainingRoom")
            await SceneLoader.GoToAsync(this, "res://Scenes/TrainingRoom.tscn");
        else
            GetTree().ChangeSceneToFile($"res://Scenes/{sceneName}.tscn");
    }

    Button MkCtrlBtn(Control parent, string label, Vector2 pos, float w)
    {
        var btn = new Button();
        btn.Name = "Btn";
        btn.AnchorLeft = 1f; btn.AnchorRight = 1f;
        btn.AnchorTop  = 0.5f; btn.AnchorBottom = 0.5f;
        btn.OffsetLeft = pos.X - w; btn.OffsetRight = pos.X;
        btn.OffsetTop  = -18f; btn.OffsetBottom = 18f;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0f, 0f, 0f, 0.45f);
        btn.AddThemeStyleboxOverride("normal", style);

        var lbl = new Label();
        lbl.Name = "T";
        lbl.Text = label;
        lbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        lbl.VerticalAlignment = VerticalAlignment.Center;
        lbl.AddThemeFontSizeOverride("font_size", 16);
        lbl.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.85f));
        btn.AddChild(lbl);

        parent.AddChild(btn);
        return btn;
    }
}
