using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// WhiteFlashRunner（Unity → Godot 移植版）
/// 轉場後在新場景播放 PrologueLast 對話。
/// 本節點應在播放時加入場景樹根節點（相當於 DontDestroyOnLoad）。
/// </summary>
public partial class WhiteFlashRunner : Node
{
    NovelReader _reader;
    // 建立面板時先套用新版介面，不等待第一句文字。
    void EnsureReader()
    {
        _reader ??= NovelReader.Install(this, _dialogueBox, _contentLabel, _btnBarRoot,
            () => _isTyping, () => _waitingClick, () => _lockClick,
            () => _skipTyping = true, () => _waitingClick = false, null);
        _reader.SetSpeakerBadge(_nameBarRoot, _speakerLabel);
    }

    void BeginReadingLine(string text)
    {
        EnsureReader();
        _reader.BeginLine(_speakerLabel?.Text, text);
    }

    // ── 私有 UI 元件 ───────────────────────────────────────────────
    Label         _speakerLabel;
    RichTextLabel _contentLabel;
    Control       _panel;
    TextureRect   _charBodyRect;
    TextureRect   _charFaceRect;
    Panel         _nameBarPanel;
    AudioStreamPlayer _bgmPlayer;
    AudioStreamPlayer _sePlayer;
    AudioStreamPlayer _mePlayer;
    ColorRect     _fadeRect;
    CanvasLayer   _canvas;
    Control       _dialogueBox;
    Control       _nameBarRoot;
    Control       _btnBarRoot;
    bool          _screenBlack;
    Color         _screenFadeColor = Colors.Black;
    bool          _waitingClick;
    bool          _lockClick;
    bool          _isTyping;
    bool          _skipTyping;
    bool          _fastForward;
    bool          _mouseHoldActive;
    float         _mouseHoldTime;
    int           _autoPlaySpeed;
    bool          _autoAdvance;

    // ── 公開 API ──────────────────────────────────────────────────

    /// <summary>開始播放（外部呼叫，相當於 Unity 的 StartFlash()）</summary>
    public void StartFlash()
    {
        // 加入場景根節點（相當於 DontDestroyOnLoad）
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, this);

        _bgmPlayer          = new AudioStreamPlayer(); AddChild(_bgmPlayer);
        _sePlayer           = new AudioStreamPlayer(); AddChild(_sePlayer);
        _mePlayer           = new AudioStreamPlayer(); AddChild(_mePlayer);
        _bgmPlayer.Autoplay = false;
        _sePlayer.Autoplay  = false;
        _mePlayer.Autoplay  = false;

        _ = Run();
    }

    // ── 主流程 ────────────────────────────────────────────────────

    async Task Run()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var jsonAsset = ResourceLoader.Load<FileAccess>(
            "res://Resources/Data/Dialogues/Prologue/PrologueLast.json");
        if (jsonAsset == null)
        {
            GD.PrintErr("[Dialogue] 找不到 PrologueLast.json");
            QueueFree(); return;
        }

        // 簡易 JSON 解析（依賴 DialogueSystem 靜態方法移植版）
        var text = FileAccess.GetFileAsString("res://Resources/Data/Dialogues/Prologue/PrologueLast.json");
        if (string.IsNullOrEmpty(text)) { QueueFree(); return; }
        var data = DialogueSystem.ParseDialogueData(DialogueSystem.PreprocessJson(text));
        if (data == null || data.Lines == null || data.Lines.Count == 0) { QueueFree(); return; }

        BuildUI();
        EnsureReader();
        _panel.Visible = true;

        bool isR18 = PlayerDataManager.Instance?.R18ContentEnabled ?? false;

        int activeChoice = 0; // 這個播放器沒有選擇框 UI，遇到 choice 一律走第一個選項

        for (int _i = 0; _i < data.Lines.Count; _i++)
        {
            var line = data.Lines[_i];
            if (line == null) continue;

            // ── 分歧跳轉：全年齡模式跳過 r18 段落，R18 模式跳過 allAges 段落（支援巢狀）──
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

            if (line.choice != null && line.choice.Length > 0)
            {
                GD.PushWarning("[WhiteFlash] 此播放器不支援選擇框，自動選第一個選項");
                activeChoice = 0;
                continue;
            }
            if (line.optionStart >= 0)
            {
                if (line.optionStart != activeChoice)
                    _i = DialogueSystem.FindOptionEnd(data.Lines, _i);
                continue;
            }
            if (line.optionEnd) continue;


            // ── BGM / SE / ME ───────────────────────────────────────────
            if (!string.IsNullOrEmpty(line.bgm))
            {
                if (line.bgm.Trim().ToLower() == "stop")
                    _ = FadeOutAudio(_bgmPlayer, 0.8f);
                else
                {
                    var clip = ResourceLoader.Load<AudioStream>("res://Resources/Audio/bgm/" + line.bgm.Trim() + ".ogg");
                    if (clip != null) { _bgmPlayer.Stream = clip; _bgmPlayer.Play(); }
                }
            }
            if (!string.IsNullOrEmpty(line.se))
            {
                string seName  = line.se;
                float  seDelay = 0f;
                if (line.se.Contains(","))
                {
                    var parts = line.se.Split(',');
                    seName = parts[0].Trim();
                    if (float.TryParse(parts[1].Trim(), out float frames))
                        seDelay = frames / 60f;
                }
                var seClip = ResourceLoader.Load<AudioStream>("res://Resources/Audio/se/" + seName.Trim() + ".ogg");
                if (seClip != null)
                {
                    if (seDelay > 0f) _ = PlaySEDelayed(seClip, seDelay);
                    else { _sePlayer.Stream = seClip; _sePlayer.Play(); }
                }
            }
            if (!string.IsNullOrEmpty(line.me))
            {
                var meClip = ResourceLoader.Load<AudioStream>("res://Resources/Audio/me/" + line.me.Trim() + ".ogg");
                if (meClip != null)
                {
                    float bgmVol  = _bgmPlayer.VolumeDb;
                    _bgmPlayer.VolumeDb = Mathf.LinearToDb(0.2f);
                    _mePlayer.Stream    = meClip; _mePlayer.Play();
                    _ = RestoreBGMAfterME(bgmVol);
                }
            }

            // ── Fadeout 預解析 ─────────────────────────────────────────
            string foVal = null; Color foColor = Colors.Black; bool foPriority = false;
            if (!string.IsNullOrEmpty(line.fadeoutWhite))
                { foVal = line.fadeoutWhite; foColor = Colors.White; }
            else if (!string.IsNullOrEmpty(line.fadeoutBlack))
                { foVal = line.fadeoutBlack; foColor = Colors.Black; }
            else if (!string.IsNullOrEmpty(line.fadeout))
                { foVal = line.fadeout;      foColor = Colors.Black; }
            string foFrames = "";
            if (foVal != null)
            {
                var parsed = DialogueSystem.ParseFadeoutValue(foVal);
                foFrames   = parsed.framesStr;
                foPriority = parsed.priority;
            }

            // 等待
            if (line.wait > 0f)
                await ToSignal(GetTree().CreateTimer(line.wait / 60f, false), SceneTreeTimer.SignalName.Timeout);

            // 立繪位置/大小平滑（fadeout 優先旗標時延後）
            if (!foPriority)
                await AnimateCharacterTransform(line);

            // 立繪淡入（fadein 前）
            if (!foPriority && !string.IsNullOrEmpty(line.showCH))
                await FadeChar(line.showCH, true);

            // fadein
            if (!string.IsNullOrEmpty(line.fadein))
            {
                float.TryParse(line.fadein.Trim(), out float fFrames);
                float dur = fFrames > 0f ? fFrames / 60f : 0.5f;
                await ScreenFade(false, dur, _screenFadeColor);
                _screenBlack = false;
                if (Mathf.IsEqualApprox(line.autoNext, 1f)) continue;
                SetDialogueUIVisible(true);
            }

            // 說話者名稱
            string spk = (line.speaker ?? "").Trim();
            if (_speakerLabel != null) _speakerLabel.Text = spk;
            if (_nameBarRoot  != null) _nameBarRoot.Visible = !string.IsNullOrEmpty(spk);
            if (_nameBarPanel != null && !string.IsNullOrEmpty(spk))
            {
                if (spk.Contains("莉奈") || spk.Contains("Linai"))
                    _nameBarPanel.SelfModulate = new Color(0.85f, 0.65f, 0.75f, 0.95f);
                else if (spk.Contains("男主") || spk.Contains("主角"))
                    _nameBarPanel.SelfModulate = new Color(0.55f, 0.70f, 0.90f, 0.95f);
                else
                    _nameBarPanel.SelfModulate = new Color(0.65f, 0.12f, 0.25f, 0.95f);
            }

            // 黑屏中處理
            if (_screenBlack)
            {
                bool hasNarration = !string.IsNullOrEmpty(line.text) && string.IsNullOrEmpty(spk);
                if (hasNarration)
                {
                    _dialogueBox.Visible    = true;
                    _nameBarRoot.Visible    = false;
                    _btnBarRoot.Visible     = true;
                    await TypeLine(line.text);
                    await WaitForAdvance(line.autoNext);
                    SetDialogueUIVisible(false);
                    _fadeRect?.MoveToFront();
                }
                if (line.hideCH != null) await FadeChar("", false);
                continue;
            }

            if (Mathf.IsEqualApprox(line.autoNext, 1f))
            {
                SetDialogueUIVisible(false);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            else
            {
                SetDialogueUIVisible(true);
                await TypeLine(line.text);
                await WaitForAdvance(line.autoNext);
            }

            // fadeout
            if (foVal != null)
            {
                SetDialogueUIVisible(false);
                float.TryParse(foFrames, out float foF);
                float dur = foF > 0f ? foF / 60f : 0.5f;
                _screenFadeColor = foColor;
                await ScreenFade(true, dur, foColor);
                _screenBlack = true;

                if (foPriority)
                {
                    await AnimateCharacterTransformImmediate(line);
                    if (!string.IsNullOrEmpty(line.showCH)) await FadeChar(line.showCH, true);
                }
                if (line.hideCH != null) await FadeChar("", false);
                continue;
            }

            if (line.hideCH != null) await FadeChar("", false);
        }

        _canvas?.QueueFree();
        QueueFree();
    }

    // ── 輸入 ──────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        // NovelReader 統一處理點擊、自動及快進。
    }

    // ── 輔助協程（async Task）────────────────────────────────────

    async Task WaitForAdvance(float autoNext)
    {
        if (autoNext > 1f)
        {
            _waitingClick = true; _lockClick = true; _autoAdvance = false;
            _ = AutoNextTimer(autoNext / 60f);
            // 實際等待用 spinwait
            while (_waitingClick) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _lockClick = false;
        }
        else if (autoNext <= 0f)
        {
            _waitingClick = true; _autoAdvance = false;
            if (_autoPlaySpeed > 0) _ = AutoAdvanceTimer();
            while (_waitingClick) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    async Task TypeLine(string text)
    {
        BeginReadingLine(text);
        if (_contentLabel == null) return;
        _contentLabel.Text = "";
        _isTyping = true; _skipTyping = false;
        foreach (char c in (text ?? ""))
        {
            while (_reader.Paused) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_skipTyping) { _contentLabel.Text = text; break; }
            _contentLabel.Text += c;
            await ToSignal(GetTree().CreateTimer(_fastForward ? 0.005f : 0.03f / NovelReader.AutoTypeMul, false), SceneTreeTimer.SignalName.Timeout);
        }
        _isTyping = false;
    }

    async Task ScreenFade(bool toBlack, float duration, Color color)
    {
        if (_fadeRect == null) return;
        _fadeRect.MoveToFront();
        float startA = toBlack ? 0f : 1f;
        float endA   = toBlack ? 1f : 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += (float)GetProcessDeltaTime();
            float a = Mathf.Lerp(startA, endA, Mathf.Clamp(elapsed / duration, 0f, 1f));
            _fadeRect.Color = new Color(color.R, color.G, color.B, a);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _fadeRect.Color = new Color(color.R, color.G, color.B, endA);
    }

    async Task FadeChar(string name, bool fadeIn)
    {
        const float duration = 0.5f;
        if (fadeIn)
        {
            string bodyName = null, faceName = null;
            if (name.Contains(","))
            {
                var parts = name.Split(',');
                bodyName = parts[0].Trim();
                faceName = parts.Length > 1 ? parts[1].Trim() : null;
            }
            else if (name.Contains("Linai") || name.Contains("linai")) bodyName = name.Trim();
            else faceName = name.Trim();

            bool hasBody = false, hasFace = false;
            if (bodyName != null)
            {
                var spr = ResourceLoader.Load<Texture2D>("res://Resources/Character/" + bodyName + ".png");
                if (spr != null) { _charBodyRect.Texture = spr; _charBodyRect.Visible = true; hasBody = true; }
            }
            if (faceName != null)
            {
                var spr = ResourceLoader.Load<Texture2D>("res://Resources/Character/" + faceName + ".png");
                if (spr != null) { _charFaceRect.Texture = spr; _charFaceRect.Visible = true; hasFace = true; }
            }
            if (hasBody && _charBodyRect.Modulate.A < 0.9f)
            {
                float t = 0f;
                while (t < duration)
                {
                    t += (float)GetProcessDeltaTime();
                    float a = Mathf.Clamp(t / duration, 0f, 1f);
                    _charBodyRect.Modulate = new Color(1f, 1f, 1f, a);
                    if (hasFace) _charFaceRect.Modulate = new Color(1f, 1f, 1f, a);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
            _charBodyRect.Modulate = Colors.White;
            if (hasFace) _charFaceRect.Modulate = Colors.White;
        }
        else
        {
            float t = 0f;
            while (t < duration)
            {
                t += (float)GetProcessDeltaTime();
                float a = 1f - Mathf.Clamp(t / duration, 0f, 1f);
                _charBodyRect.Modulate = new Color(1f, 1f, 1f, a);
                _charFaceRect.Modulate = new Color(1f, 1f, 1f, a);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            _charBodyRect.Modulate = new Color(1f, 1f, 1f, 0f);
            _charFaceRect.Modulate = new Color(1f, 1f, 1f, 0f);
            _charBodyRect.Texture  = null; _charFaceRect.Texture = null;
            _charBodyRect.Size     = new Vector2(650f, 1338f);
            _charFaceRect.Size     = new Vector2(650f, 1338f);
            _charBodyRect.Position = Vector2.Zero;
            _charFaceRect.Position = Vector2.Zero;
        }
    }

    async Task AnimateCharacterTransform(DialogueSystem.DialogueLine line)
    {
        const float charDur = 0.4f;
        if (_charBodyRect == null) return;
        var chSP = DialogueSystem.ParseValueWithDuration(line.chScale);
        bool hasX   = !string.IsNullOrEmpty(line.chX);
        bool hasY   = !string.IsNullOrEmpty(line.chY);
        var chXP    = hasX ? DialogueSystem.ParseValueWithDuration(line.chX) : default;
        var chYP    = hasY ? DialogueSystem.ParseValueWithDuration(line.chY) : default;
        Vector2 targetSize = !string.IsNullOrEmpty(line.chScale) && chSP.value > 0f
            ? new Vector2(650f * chSP.value, 1338f * chSP.value)
            : _charBodyRect.Size;
        float posX   = hasX ? chXP.value : _charBodyRect.Position.X;
        float posY   = hasY ? chYP.value : _charBodyRect.Position.Y;  // Godot Y 下正
        Vector2 targetPos = new Vector2(posX, posY);
        bool needMove = targetSize != _charBodyRect.Size || targetPos != _charBodyRect.Position;
        if (!needMove) return;
        Vector2 startSize = _charBodyRect.Size;
        Vector2 startPos  = _charBodyRect.Position;
        float t = 0f;
        while (t < charDur)
        {
            t += (float)GetProcessDeltaTime();
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(t / charDur, 0f, 1f));
            var s   = startSize.Lerp(targetSize, p);
            var pos = startPos.Lerp(targetPos, p);
            _charBodyRect.Size = s; _charFaceRect.Size = s;
            _charBodyRect.Position = pos; _charFaceRect.Position = pos;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _charBodyRect.Size = targetSize; _charFaceRect.Size = targetSize;
        _charBodyRect.Position = targetPos; _charFaceRect.Position = targetPos;
    }

    async Task AnimateCharacterTransformImmediate(DialogueSystem.DialogueLine line)
    {
        if (_charBodyRect == null) return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var chSP = DialogueSystem.ParseValueWithDuration(line.chScale);
        if (!string.IsNullOrEmpty(line.chScale) && chSP.value > 0f)
        {
            var s = new Vector2(650f * chSP.value, 1338f * chSP.value);
            _charBodyRect.Size = s; _charFaceRect.Size = s;
        }
        if (!string.IsNullOrEmpty(line.chX) || !string.IsNullOrEmpty(line.chY))
        {
            var cxP = DialogueSystem.ParseValueWithDuration(line.chX);
            var cyP = DialogueSystem.ParseValueWithDuration(line.chY);
            float px = !string.IsNullOrEmpty(line.chX) ? cxP.value : _charBodyRect.Position.X;
            float py = !string.IsNullOrEmpty(line.chY) ? cyP.value : _charBodyRect.Position.Y;
            var pos = new Vector2(px, py);
            _charBodyRect.Position = pos; _charFaceRect.Position = pos;
        }
    }

    async Task PlaySEDelayed(AudioStream clip, float delay)
    {
        await ToSignal(GetTree().CreateTimer(delay, false), SceneTreeTimer.SignalName.Timeout);
        _sePlayer.Stream = clip; _sePlayer.Play();
    }

    async Task FadeOutAudio(AudioStreamPlayer player, float duration)
    {
        float startVol = player.VolumeDb;
        float t = 0f;
        while (t < duration)
        {
            t += (float)GetProcessDeltaTime();
            player.VolumeDb = Mathf.Lerp(startVol, -80f, t / duration);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        player.Stop(); player.VolumeDb = startVol;
    }

    async Task RestoreBGMAfterME(float targetVol)
    {
        while (_mePlayer.Playing)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        float t = 0f;
        while (t < 0.5f)
        {
            t += (float)GetProcessDeltaTime();
            _bgmPlayer.VolumeDb = Mathf.Lerp(Mathf.LinearToDb(0.2f), targetVol, t / 0.5f);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _bgmPlayer.VolumeDb = targetVol;
    }

    async Task AutoNextTimer(float seconds)
    {
        if (_reader != null && await _reader.WaitScriptDelay(seconds))
            _waitingClick = false;
    }

    async Task AutoAdvanceTimer()
    {
        float[] delays = { 0f, 2.5f, 1.5f, 0.8f, 0.2f };
        float d = delays[Mathf.Clamp(_autoPlaySpeed, 0, 4)];
        await ToSignal(GetTree().CreateTimer(d, false), SceneTreeTimer.SignalName.Timeout);
        _autoAdvance = true;
        if (_waitingClick) _waitingClick = false;
    }

    void SetDialogueUIVisible(bool visible)
    {
        if (_dialogueBox != null) _dialogueBox.Visible = visible;
        if (_nameBarRoot  != null) _nameBarRoot.Visible  = visible;
        if (_btnBarRoot   != null) _btnBarRoot.Visible   = visible;
    }

    // ── 建立對話 UI（程式碼建立，仿 Unity 版結構）─────────────────

    void BuildUI()
    {
        _canvas             = new CanvasLayer();
        _canvas.Layer       = 100;
        GetTree().Root.AddChild(_canvas);

        _panel              = new Control();
        _panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.AddChild(_panel);

        // 立繪容器
        var charRoot        = new Control { Name = "CharRoot" };
        charRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _panel.AddChild(charRoot);

        // 身體圖層
        _charBodyRect            = new TextureRect();
        _charBodyRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _charBodyRect.Size        = new Vector2(650f, 1338f);
        _charBodyRect.Position    = new Vector2(635f, -258f);  // 螢幕下方中央對齊（1920×1080）
        _charBodyRect.SelfModulate = new Color(1f, 1f, 1f, 0f);
        charRoot.AddChild(_charBodyRect);

        // 表情圖層
        _charFaceRect            = new TextureRect();
        _charFaceRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _charFaceRect.Size        = new Vector2(650f, 1338f);
        _charFaceRect.Position    = new Vector2(635f, -258f);
        _charFaceRect.SelfModulate = new Color(1f, 1f, 1f, 0f);
        charRoot.AddChild(_charFaceRect);

        const float boxHeight = 260f;

        // 對話框底板
        _dialogueBox = new Panel();
        var dbStyle  = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.88f) };
        ((Panel)_dialogueBox).AddThemeStyleboxOverride("panel", dbStyle);
        _dialogueBox.SetAnchorAndOffset(Side.Left,   0f, 0f);
        _dialogueBox.SetAnchorAndOffset(Side.Right,  1f, 0f);
        _dialogueBox.SetAnchorAndOffset(Side.Bottom, 1f, 0f);
        _dialogueBox.SetAnchorAndOffset(Side.Top,    1f, -boxHeight);
        _panel.AddChild(_dialogueBox);

        // 名字框
        _nameBarRoot = new Control();
        _nameBarRoot.Size     = new Vector2(300f, 48f);
        _nameBarRoot.Position = new Vector2(50f, 1080f - boxHeight - 52f);
        _panel.AddChild(_nameBarRoot);
        _nameBarPanel = new Panel();
        _nameBarPanel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _nameBarRoot.AddChild(_nameBarPanel);

        _speakerLabel = new Label();
        _speakerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _speakerLabel.VerticalAlignment   = VerticalAlignment.Center;
        _speakerLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _nameBarRoot.AddChild(_speakerLabel);

        // 內文
        _contentLabel                    = new RichTextLabel();
        _contentLabel.BbcodeEnabled      = false;
        _contentLabel.SetAnchorAndOffset(Side.Left,   0f, 60f);
        _contentLabel.SetAnchorAndOffset(Side.Right,  1f, -60f);
        _contentLabel.SetAnchorAndOffset(Side.Top,    0f, 20f);
        _contentLabel.SetAnchorAndOffset(Side.Bottom, 1f, -20f);
        _dialogueBox.AddChild(_contentLabel);

        // 控制按鈕列
        _btnBarRoot          = new Control();
        _btnBarRoot.Size     = new Vector2(460f, 32f);
        _btnBarRoot.Position = new Vector2(1920f - 480f, 1080f - boxHeight - 38f);
        _panel.AddChild(_btnBarRoot);

        MkBtn(_btnBarRoot, "快速播放", new Vector2(0f, 0f), () =>
        {
            _fastForward = !_fastForward;
        });
        MkBtn(_btnBarRoot, "自動播放", new Vector2(120f, 0f), () =>
        {
            _autoPlaySpeed = (_autoPlaySpeed + 1) % 5;
        });

        // 遮罩層（最上面）
        _fadeRect            = new ColorRect();
        _fadeRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _fadeRect.Color      = new Color(0f, 0f, 0f, 0f);
        _fadeRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        _panel.AddChild(_fadeRect);
    }

    Button MkBtn(Control parent, string label, Vector2 pos, System.Action onClick)
    {
        var btn            = new Button();
        btn.Text           = label;
        btn.Size           = new Vector2(110f, 28f);
        btn.Position       = pos;
        btn.Pressed        += onClick;
        parent.AddChild(btn);
        return btn;
    }
}
