using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Bathroom 場景的主控腳本（Godot 4 port）
/// 自動播放 Day3_shower 對話，然後進入選項循環
/// </summary>
public partial class BathroomRuntime : Node
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
    static void MarkDialoguePlayed(string id) => _playedDialogues.Add(id);
    /// <summary>開新遊戲時清掉「已播過」紀錄（NewGameReset 呼叫）</summary>
    public static void ResetForNewGame() => _playedDialogues.Clear();

    // ── UI 節點 ───────────────────────────────────────────────────
    Label _speakerText;
    Label _contentText;
    Control _panel;
    TextureRect _charBodyImage;
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

    // ── 選項系統 ─────────────────────────────────────────────────
    Control _choicePanel;
    int _choiceResult = -1;
    int _completedCount = 0;
    bool _hairDone;
    bool _chestDone;
    bool _holeDone;

    // ── 音樂 ─────────────────────────────────────────────────────
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

    // ── 角色位置狀態（Unity 座標空間）────────────────────────────
    Vector2 _charBodySize   = new(650f, 1338f);
    Vector2 _charBodyOffset = Vector2.Zero;
    Vector2 _char2Size      = new(650f, 1338f);
    Vector2 _char2Offset    = Vector2.Zero;

    // ── CG 圖層 ──────────────────────────────────────────────────
    float[]      _cgScale  = new float[DialogueSystem.CG_LAYER_COUNT];
    Vector2[]    _cgOffset = new Vector2[DialogueSystem.CG_LAYER_COUNT];
    TextureRect[] _cgImages = new TextureRect[DialogueSystem.CG_LAYER_COUNT];

    // ── CanvasLayer ──────────────────────────────────────────────
    CanvasLayer _canvas;

    // ────────────────────────────────────────────────────────────
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
        SceneBgm.Install(this, SceneBgm.Gentle, _bgmPlayer);

        for (int i = 0; i < DialogueSystem.CG_LAYER_COUNT; i++)
            _cgScale[i] = 1f;

        TaskLogger.Run(StartAsync(), "Bathroom.StartAsync");
    }

    async Task StartAsync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!HasDialoguePlayed("Day3_shower"))
            await PlayDialogueAsync();
    }

    // ── _Process: 處理點擊推進 ───────────────────────────────────
    public override void _Process(double delta)
    {
        // NovelReader 統一處理點擊、自動及快進。
    }
    bool _wasMousePressed;

    // ─────────────────────────────────────────────────────────────
    // 主對話流程
    // ─────────────────────────────────────────────────────────────
    async Task PlayDialogueAsync()
    {
        string json = FileAccess.GetFileAsString("res://Resources/Data/Dialogues/Day3/Shower/Day3_shower.json");
        if (string.IsNullOrEmpty(json)) { GD.PrintErr("[Bathroom] 找不到 Day3_shower.json"); return; }

        var data = DialogueSystem.ParseDialogueData(DialogueSystem.PreprocessJson(json));
        if (data == null || data.Lines == null || data.Lines.Count == 0) return;

        BuildUI();
        _panel.Visible = true;

        foreach (var line in data.Lines)
        {
            // ── BGM / SE / ME ────────────────────────────────────
            await HandleAudioAsync(line);

            // ── fadeout 解析 ─────────────────────────────────────
            var (foVal, foColor, foPriority, foFrames) = ParseFadeout(line);

            // ── hideCH 解析 ──────────────────────────────────────
            var (hideCHVal, hideCHPriority) = ParseHideCH(line.hideCH);
            bool hideCHHandled = false;

            if (!foPriority && hideCHPriority && !string.IsNullOrEmpty(hideCHVal))
            {
                await HandleHideCHAsync(hideCHVal);
                hideCHHandled = true;
            }

            // ── 背景切換 ─────────────────────────────────────────
            if (!string.IsNullOrEmpty(line.bgName))
                await HandleBGAsync(line.bgName);

            // wait
            if (line.wait > 0f)
                await TimerAsync(line.wait / 60f);

            // ── hideCG 優先 ──────────────────────────────────────
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!foPriority && cg.hasHide && DialogueSystem.IsHideCGPriority(cg.hide))
                    await FadeCGAsync(li, "", false);
            }
            // ── showCG ───────────────────────────────────────────
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!foPriority && cg.hasShow)
                    await FadeCGAsync(li, cg.show, true);
            }
            // ── CG 位移/縮放動畫 ─────────────────────────────────
            if (!foPriority) await AnimateCGTransformAsync(line);

            // ── 立繪位移/縮放動畫 ────────────────────────────────
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

            // ── hideCH + showCH 同行 → 先淡出再淡入 ─────────────
            if (!foPriority && !hideCHHandled && line.hideCH != null && !string.IsNullOrEmpty(line.showCH))
            {
                await HandleHideCHAsync(hideCHVal);
                hideCHHandled = true;
            }
            // ── 帶圖名的 chScale（在 showCH 前套用正確尺寸）───────
            if (!foPriority && chScaleDeferred)
            {
                var chSP = DialogueSystem.ParseValueWithDuration(csRaw);
                if (chSP.value > 0f)
                {
                    _charBodySize = new Vector2(650f * chSP.value, 1338f * chSP.value);
                    ApplyCharBodyTransform();
                }
            }
            // ── char2 位移/縮放 ──────────────────────────────────
            if (!foPriority) await AnimateChar2TransformAsync(line.chScale2, line.chX2, line.chY2);

            // ── hideCH2 + showCH2 同行 → 先淡出 ─────────────────
            if (!foPriority && line.hideCH2 != null && !string.IsNullOrEmpty(line.showCH2))
                await FadeChar2Async(false);

            // ── showCH / showCH2（可並行）────────────────────────
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

            // ── fadein ──────────────────────────────────────────
            if (!string.IsNullOrEmpty(line.fadein))
            {
                float.TryParse(line.fadein.Trim(), out float fiFrames);
                float fiDur = fiFrames > 0f ? fiFrames / 60f : 0.5f;
                await ScreenFadeAsync(false, fiDur, _screenFadeColor);
                _screenBlack = false;
                if (Mathf.IsEqualApprox(line.autoNext, 1f)) continue;
            }

            // ── hideCG 非優先 ────────────────────────────────────
            for (int li = 0; li < DialogueSystem.CG_LAYER_COUNT; li++)
            {
                var cg = DialogueSystem.GetCGLayer(line, li);
                if (!foPriority && cg.hasHide && !DialogueSystem.IsHideCGPriority(cg.hide))
                    await FadeCGAsync(li, "", false);
            }

            // ── 說話者 / 名字框 ──────────────────────────────────
            string spk = (line.speaker ?? "").Trim();
            if (_speakerText != null) _speakerText.Text = spk;
            // nameBar 可見性與樣式由 NovelReader.ApplySpeakerStyle 統一控制，這裡不碰

            // ── 黑屏中處理 ───────────────────────────────────────
            if (_screenBlack)
            {
                if (!string.IsNullOrEmpty(line.text))
                {
                    MoveToFront(_dialogueBox);
                    _dialogueBox.Visible = true;
                    if (_reader != null && !string.IsNullOrEmpty(spk)) MoveToFront(_nameBarGO);
                    _btnBarGO.Visible = true;
                    await TypeLineAsync(line.text);
                    await WaitClickAsync(line.autoNext);
                    SetDialogueUIVisible(false);
                    MoveToFront(_fadeOverlay);
                }
                else
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                if (!hideCHHandled && line.hideCH != null)
                    await HandleHideCHAsync(hideCHVal);
                continue;
            }

            // ── autoNext == 1 ────────────────────────────────────
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

            // ── fadeout ──────────────────────────────────────────
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

        // ── 主對話結束，進入選項階段 ─────────────────────────────
        MarkDialoguePlayed("Day3_shower");
        SetDialogueUIVisible(false);
        GD.Print("[Bathroom] Day3_shower 播放完畢，進入選項階段");

        BuildChoiceUI();

        while (_completedCount < 3)
        {
            // 重置選項顏色
            foreach (Node child in _choicePanel.GetChildren())
            {
                if (child is Button btn)
                {
                    var sb = new StyleBoxFlat();
                    sb.BgColor = new Color(0f, 0f, 0f, 0.9f);
                    btn.AddThemeStyleboxOverride("normal", sb);
                }
            }

            _choicePanel.Visible = true;
            _reader?.Stop(); // 選擇點需要玩家手動選擇。
        _choiceResult = -1;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            while (_choiceResult < 0)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _choicePanel.Visible = false;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            string eventName = "";
            bool isFirstTime = false;
            switch (_choiceResult)
            {
                case 0:
                    isFirstTime = !_hairDone;
                    eventName = isFirstTime ? "Day3_shower_hair1" : "Day3_shower_hair2";
                    if (isFirstTime) _hairDone = true;
                    break;
                case 1:
                    isFirstTime = !_chestDone;
                    eventName = isFirstTime ? "Day3_shower_chest1" : "Day3_shower_chest2";
                    if (isFirstTime) _chestDone = true;
                    break;
                case 2:
                    isFirstTime = !_holeDone;
                    eventName = isFirstTime ? "Day3_shower_hole1" : "Day3_shower_hole2";
                    if (isFirstTime) _holeDone = true;
                    break;
            }

            await PlayEventDialogueAsync(eventName);
            if (isFirstTime) _completedCount++;
            GD.Print("[Bathroom] 判定進度：" + _completedCount + "/3");
        }

        GD.Print("[Bathroom] 判定已滿，播放 Day3_showerLast");
        await PlayEventDialogueAsync("Day3_showerLast");

        await ScreenFadeAsync(true, 1f, Colors.Black);
        GD.Print("[Bathroom] Day3_showerLast 播放完畢，前往 TrainingRoom 場景");
        if (PlayerDataManager.Instance?.R18ContentEnabled == true)
        {
            TrainingRoomRuntime.PendingDay3First = true;
            await SceneLoader.GoToAsync(this, "res://Scenes/TrainingRoom.tscn");
        }
        else
        {
            GD.Print("[Bathroom] 全年齡模式：略過 TrainingRoom，顯示蓋被子選擇");
            var pdm = PlayerDataManager.Instance;
            if (pdm != null) { pdm.ResetTodayTracking(); pdm.CurrentNight = 2; }
            await AllAgesChoiceHelper.ShowBlanketChoiceAsync(this);
            LivingRoomRuntime.PendingDay3 = true;
            GetTree().ChangeSceneToFile("res://Scenes/LivingRoom.tscn");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 事件對話（簡化版，只處理 bgm/se/me、bgName、showCH、fadein/out、autoNext）
    // ─────────────────────────────────────────────────────────────
    async Task PlayEventDialogueAsync(string jsonName)
    {
        string json = FileAccess.GetFileAsString(
            "res://Resources/Data/Dialogues/Day3/Shower/" + jsonName + ".json");
        if (string.IsNullOrEmpty(json)) { GD.PrintErr("[Bathroom] 找不到 " + jsonName + ".json"); return; }

        var data = DialogueSystem.ParseDialogueData(DialogueSystem.PreprocessJson(json));
        if (data == null || data.Lines == null || data.Lines.Count == 0) return;

        _panel.Visible = true;

        foreach (var line in data.Lines)
        {
            // ── BGM / SE / ME ─────────────────────────────────
            await HandleAudioAsync(line);

            // ── fadeout 解析 ──────────────────────────────────
            string _foVal = null; string _foFrames = "0"; Color _foColor = Colors.Black;
            if (!string.IsNullOrEmpty(line.fadeoutBlack))
                { _foVal = line.fadeoutBlack; _foColor = Colors.Black; }
            else if (!string.IsNullOrEmpty(line.fadeoutWhite))
                { _foVal = line.fadeoutWhite; _foColor = Colors.White; }
            if (_foVal != null && _foVal.Contains(","))
            {
                var foParts = _foVal.Split(',');
                _foFrames = foParts[0].Trim();
            }

            // ── hideCH 解析 ──────────────────────────────────
            var (hideCHVal, _) = ParseHideCH(line.hideCH);
            bool hideCHHandled = false;

            // ── bgName ────────────────────────────────────────
            if (!string.IsNullOrEmpty(line.bgName))
            {
                string raw = line.bgName.Trim(); bool bgFade = false;
                if (raw.Contains(","))
                {
                    var p = raw.Split(',');
                    bgFade = p[^1].Trim().ToLower() == "fade";
                    raw = p[0].Trim();
                }
                var tex = ResourceHelper.LoadTexAny("Backgrounds/" + raw);
                if (tex != null && _bgImage != null)
                {
                    _bgImage.Texture = tex;
                    _bgImage.Visible = true;
                    if (bgFade)
                    {
                        _bgImage.Modulate = new Color(1, 1, 1, 0);
                        float t2 = 0f;
                        while (t2 < 0.5f)
                        {
                            t2 += (float)GetProcessDeltaTime();
                            _bgImage.Modulate = new Color(1, 1, 1, Mathf.Clamp(t2 / 0.5f, 0, 1));
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        }
                        _bgImage.Modulate = Colors.White;
                    }
                }
            }

            // ── showCH / showCH2 ─────────────────────────────
            bool hasCH  = !string.IsNullOrEmpty(line.showCH);
            bool hasCH2 = !string.IsNullOrEmpty(line.showCH2);
            if (hasCH && hasCH2)
                await Task.WhenAll(FadeCharAsync(line.showCH, true), FadeChar2ShowAsync(line.showCH2));
            else if (hasCH)
                await FadeCharAsync(line.showCH, true);
            else if (hasCH2)
                await FadeChar2ShowAsync(line.showCH2);

            // ── fadein ────────────────────────────────────────
            if (!string.IsNullOrEmpty(line.fadein))
            {
                float.TryParse(line.fadein.Trim(), out float fiFrames);
                float fiDur = fiFrames > 0f ? fiFrames / 60f : 0.5f;
                await ScreenFadeAsync(false, fiDur, _screenFadeColor);
                _screenBlack = false;
                if (Mathf.IsEqualApprox(line.autoNext, 1f)) continue;
            }

            // ── 說話者 / 名字框 ───────────────────────────────
            string spk = (line.speaker ?? "").Trim();
            if (_speakerText != null) _speakerText.Text = spk;
            if (_nameBarGO != null) _nameBarGO.Visible = !string.IsNullOrEmpty(spk);
            ApplyNameBarColor(spk);

            // ── 黑屏中處理 ────────────────────────────────────
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
                else
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                if (!hideCHHandled && line.hideCH != null)
                    await HandleHideCHAsync(hideCHVal);
                continue;
            }

            // ── autoNext == 1 ─────────────────────────────────
            if (Mathf.IsEqualApprox(line.autoNext, 1f))
            {
                SetDialogueUIVisible(false);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            else
            {
                SetDialogueUIVisible(true);
                if (_nameBarGO != null && string.IsNullOrEmpty(spk)) _nameBarGO.Visible = false;
                await TypeLineAsync(line.text);
                await WaitClickAsync(line.autoNext);
            }

            // ── fadeout ───────────────────────────────────────
            if (_foVal != null)
            {
                SetDialogueUIVisible(false);
                float foDur = float.TryParse(_foFrames, out float ff) && ff > 0f ? ff / 60f : 0.5f;
                _screenFadeColor = _foColor;
                await ScreenFadeAsync(true, foDur, _foColor);
                _screenBlack = true;
            }

            if (!hideCHHandled && line.hideCH != null && string.IsNullOrEmpty(line.showCH))
                await HandleHideCHAsync(hideCHVal);
        }

        SetDialogueUIVisible(false);
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
        float startA = toBlack ? 0f : 1f;
        float endA   = toBlack ? 1f : 0f;
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
                cg.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                cg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                cg.MouseFilter = Control.MouseFilterEnum.Ignore;
                _panel.AddChild(cg);
                _cgImages[layer] = cg;
                _cgScale[layer] = 1f; _cgOffset[layer] = Vector2.Zero;
                // 排在背景之後、立繪之前
                int bgIdx = _bgImage?.GetIndex() + 1 ?? 0;
                _panel.MoveChild(cg, bgIdx + layer);
            }

            var tex = ResourceLoader.Load<Texture2D>("res://Resources/CG/" + cgName.Trim() + ".png");
            if (tex == null) { GD.PushWarning("[Bathroom] 找不到 CG：" + cgName); return; }
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
            _cgImages[layer].Texture = null;
            _cgScale[layer] = 1f; _cgOffset[layer] = Vector2.Zero;
            ApplyCGTransform(layer);
            _cgImages[layer].Visible = false;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 立繪淡入/淡出
    // ─────────────────────────────────────────────────────────────
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
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                _char2Image.Modulate = Colors.White;
            }
            else if (hasChar2) _char2Image.Modulate = Colors.White;
        }
        else
        {
            float bodyA  = _charBodyImage.Modulate.A;
            float faceA  = _charFaceImage.Modulate.A;
            float char2A = _char2Image.Modulate.A;
            float t = 0f;
            while (t < duration)
            {
                t += (float)GetProcessDeltaTime();
                float p = Mathf.Clamp(t / duration, 0, 1);
                _charBodyImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(bodyA,  0, p));
                _charFaceImage.Modulate = new Color(1, 1, 1, Mathf.Lerp(faceA,  0, p));
                _char2Image.Modulate    = new Color(1, 1, 1, Mathf.Lerp(char2A, 0, p));
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            _charBodyImage.Modulate = new Color(1, 1, 1, 0);
            _charFaceImage.Modulate = new Color(1, 1, 1, 0);
            _char2Image.Modulate    = new Color(1, 1, 1, 0);
            _charBodyImage.Texture = null; _charFaceImage.Texture = null; _char2Image.Texture = null;
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
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (hideBody)  { _charBodyImage.Modulate = new Color(1,1,1,0); _charBodyImage.Texture = null; }
        if (hideFace)  { _charFaceImage.Modulate = new Color(1,1,1,0); _charFaceImage.Texture = null; }
        if (hideChar2) { _char2Image.Modulate    = new Color(1,1,1,0); _char2Image.Texture    = null; }
        if (_charBodyImage.Texture == null && _charFaceImage.Texture == null)
        {
            _charBodyOffset = Vector2.Zero; _charBodySize = new Vector2(650, 1338); ApplyCharBodyTransform();
        }
        if (_char2Image.Texture == null)
        {
            _char2Offset = Vector2.Zero; _char2Size = new Vector2(650, 1338); ApplyChar2Transform();
        }
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
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _char2Image.Modulate = new Color(1, 1, 1, 0);
        _char2Image.Texture = null;
        _char2Offset = Vector2.Zero; _char2Size = new Vector2(650, 1338); ApplyChar2Transform();
    }

    // ─────────────────────────────────────────────────────────────
    // 背景交叉淡入 / 純色淡入
    // ─────────────────────────────────────────────────────────────
    async Task CrossfadeBGAsync(Texture2D newTex, float duration = 0.6f)
    {
        var overlay = new TextureRect();
        overlay.Texture = newTex;
        overlay.StretchMode = TextureRect.StretchModeEnum.Scale;
        overlay.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        overlay.Modulate = new Color(1, 1, 1, 0);
        _panel.AddChild(overlay);
        _panel.MoveChild(overlay, _bgImage.GetIndex() + 1);
        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            overlay.Modulate = new Color(1, 1, 1, Mathf.Clamp(t / duration, 0, 1));
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
            var tex = ResourceHelper.LoadTexAny("Backgrounds/" + name);
            if (tex != null && _bgImage != null)
            {
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
            if (line.bgm.Trim().ToLower() == "stop")
                _ = FadeOutAudioAsync(_bgmPlayer, 0.8f);
            else
            {
                var stream = ResourceLoader.Load<AudioStream>("res://Resources/Audio/bgm/" + line.bgm.Trim() + ".ogg");
                if (stream != null)
                {
                    _bgmPlayer.Stream = stream;
                    _bgmPlayer.Play();
                }
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
    {
        await TimerAsync(delay);
        _sePlayer.Stream = stream; _sePlayer.Play();
    }

    async Task FadeOutAudioAsync(AudioStreamPlayer player, float duration)
    {
        float startVol = Mathf.DbToLinear(player.VolumeDb);
        float t = 0f;
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
        while (_mePlayer.Playing)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
    // 計時器 / 等待點擊
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
        _autoAdvance = true;
        if (_waitingClick) _waitingClick = false;
    }

    // ─────────────────────────────────────────────────────────────
    // 角色/CG 位移動畫 helpers
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
            _charBodySize   = startSize.Lerp(targetSize, p);
            _charBodyOffset = startPos.Lerp(targetPos, p);
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
            _char2Size   = sSize.Lerp(tgtSize, p);
            _char2Offset = sPos.Lerp(tgtPos, p);
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
            float startScale = _cgScale[li];
            Vector2 startOff = _cgOffset[li];
            float t = 0f;
            while (t < dur)
            {
                t += (float)GetProcessDeltaTime();
                float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t / dur, 0, 1));
                _cgScale[li]  = Mathf.Lerp(startScale, tgtScale, p);
                _cgOffset[li] = startOff.Lerp(new Vector2(tgtOX, tgtOY), p);
                ApplyCGTransform(li);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
        // bottom-center anchor: AnchorLeft=0.5, AnchorRight=0.5, AnchorTop=1, AnchorBottom=1
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
        // _nameBarGO.Visible 由 NovelReader.BeginLine 統一控制，這裡不碰
        if (_btnBarGO    != null) _btnBarGO.Visible     = v;
    }

    void MoveToFront(Control node)
    {
        if (node?.GetParent() is Control parent)
            parent.MoveChild(node, parent.GetChildCount() - 1);
    }

    // ─────────────────────────────────────────────────────────────
    // 建立選項 UI
    // ─────────────────────────────────────────────────────────────
    void BuildChoiceUI()
    {
        var container = new VBoxContainer();
        container.SetAnchorsPreset(Control.LayoutPreset.Center);
        container.OffsetLeft = -400f; container.OffsetRight  =  400f;
        container.OffsetTop  = -270f; container.OffsetBottom =  270f;
        container.AddThemeConstantOverride("separation", 18);
        _canvas.AddChild(container);
        _choicePanel = container;

        string[] labels = { "洗頭髮", "洗海綿", "洗洞口" };
        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            var btn = new Button();
            btn.Text = labels[i];
            btn.CustomMinimumSize = new Vector2(800f, 90f);
            btn.AddThemeFontSizeOverride("font_size", 38);
            btn.AddThemeColorOverride("font_color", Colors.White);

            var sbNormal = new StyleBoxFlat();
            sbNormal.BgColor = new Color(0, 0, 0, 0.9f);
            sbNormal.CornerRadiusTopLeft = sbNormal.CornerRadiusTopRight = 4;
            sbNormal.CornerRadiusBottomLeft = sbNormal.CornerRadiusBottomRight = 4;
            var sbHover = new StyleBoxFlat();
            sbHover.BgColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);
            sbHover.CornerRadiusTopLeft = sbHover.CornerRadiusTopRight = 4;
            sbHover.CornerRadiusBottomLeft = sbHover.CornerRadiusBottomRight = 4;

            btn.AddThemeStyleboxOverride("normal",  sbNormal);
            btn.AddThemeStyleboxOverride("hover",   sbHover);
            btn.AddThemeStyleboxOverride("pressed", sbHover);
            btn.Pressed += () => { _choiceResult = idx; };
            container.AddChild(btn);
        }

        container.Visible = false;
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

        // 背景
        var bg = new TextureRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.StretchMode = TextureRect.StretchModeEnum.Scale;
        bg.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        bg.MouseFilter = Control.MouseFilterEnum.Ignore;
        bg.Modulate    = new Color(0.08f, 0.04f, 0.12f);
        panel.AddChild(bg);
        _bgImage = bg;

        // 立繪容器（全螢幕，底部對齊）
        var portraitLayer = new Control { Name = "PortraitFloatRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(portraitLayer);
        portraitLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        void SetupChar(out TextureRect tr, string goName)
        {
            tr = new TextureRect();
            tr.Name = goName;
            tr.MouseFilter  = Control.MouseFilterEnum.Ignore;
            tr.StretchMode  = TextureRect.StretchModeEnum.Scale;
            tr.ExpandMode   = TextureRect.ExpandModeEnum.IgnoreSize;
            tr.AnchorLeft   = 0.5f; tr.AnchorRight  = 0.5f;
            tr.AnchorTop    = 1.0f; tr.AnchorBottom = 1.0f;
            tr.Modulate     = new Color(1, 1, 1, 0);
            portraitLayer.AddChild(tr);
        }
        SetupChar(out _charBodyImage, "CharBody");
        SetupChar(out _charFaceImage, "CharFace");
        SetupChar(out _char2Image,    "Char2");
        ApplyCharBodyTransform(); ApplyChar2Transform();

        float boxH = 260f;

        // 對話框底板
        var box = new Panel();
        box.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        box.OffsetTop = -boxH; box.OffsetBottom = 0;
        var boxStyle = new StyleBoxFlat();
        boxStyle.BgColor = new Color(0, 0, 0, 0.88f);
        box.AddThemeStyleboxOverride("panel", boxStyle);
        panel.AddChild(box);
        _dialogueBox = box;

        // 內文
        var contentLabel = new Label();
        contentLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        contentLabel.OffsetLeft = 60; contentLabel.OffsetRight = -60;
        contentLabel.OffsetTop  = 20; contentLabel.OffsetBottom = -20;
        contentLabel.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        contentLabel.AddThemeFontSizeOverride("font_size", 28);
        contentLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.96f, 1f));
        box.AddChild(contentLabel);
        _contentText = contentLabel;

        // 點擊提示
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
        nameBar.OffsetLeft  = 50f; nameBar.OffsetRight  = 350f;
        nameBar.OffsetBottom = -(boxH + 4f); nameBar.OffsetTop = -(boxH + 52f);
        _nameBarStyle = new StyleBoxFlat();
        _nameBarStyle.BgColor = new Color(0f, 0f, 0f, 0f); // 透明初始，防閃爍
        _nameBarStyle.CornerRadiusTopLeft = _nameBarStyle.CornerRadiusTopRight = 4;
        _nameBarStyle.CornerRadiusBottomLeft = _nameBarStyle.CornerRadiusBottomRight = 4;
        nameBar.AddThemeStyleboxOverride("panel", _nameBarStyle);
        nameBar.Modulate = new Color(1f, 1f, 1f, 0f); // 完全透明，等 NovelReader 接管後才顯示
        panel.AddChild(nameBar);
        _nameBarGO = nameBar;
        _nameBarPanel = nameBar;

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

        // 螢幕遮罩
        var fade = new ColorRect();
        fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        fade.Color         = new Color(0, 0, 0, 0);
        fade.MouseFilter   = Control.MouseFilterEnum.Ignore;
        panel.AddChild(fade);
        _fadeOverlay = fade;
    }

    Button MkCtrlBtn(Control parent, string label, float w)
    {
        var btn = new Button();
        btn.Name = "Btn_" + label;
        btn.CustomMinimumSize = new Vector2(w, 36f);
        var sb = new StyleBoxFlat();
        sb.BgColor = new Color(0, 0, 0, 0.45f);
        btn.AddThemeStyleboxOverride("normal", sb);
        var sbH = new StyleBoxFlat();
        sbH.BgColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        btn.AddThemeStyleboxOverride("hover", sbH);
        btn.AddThemeStyleboxOverride("pressed", sbH);

        var lbl = new Label();
        lbl.Name = "T";
        lbl.Text = label;
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
