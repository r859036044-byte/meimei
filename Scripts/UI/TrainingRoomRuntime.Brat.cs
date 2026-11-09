using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 雌小鬼互動小遊戲（全年齡版）：莉奈會不定時說話，玩家用「捏臉」回應。
///  • 說雌小鬼的話 → 玩家捏她：雌小鬼值下降（捏得越用力/越久降越多），她會扭臉；不理她：雌小鬼值上升。
///  • 說正常的話 → 玩家捏她：心情下降（雌小鬼值也小降一點）；不捏：心情小升。
///  • 心情 ≤ 0 → 隔天結局「莉奈搬走」（BratEnd = 1）；雌小鬼值 ≥ 100 → 隔天結局「莉奈讓男主當奴隸」（BratEnd = 2）。
///  • R18 版（插入時）：她說雌小鬼話時，氣泡下面會出現選項「教訓她」「不理她」。
///      教訓＝雞雞快速頂到底，她求饒、雌小鬼值大降，但心情也扣，扣多少看她舒不舒服（濕度＋穴高潮值）。
///      不理＝雌小鬼值上升。也可以摸頭安撫或捏臉安全過關；做的時候摸頭也會慢慢回心情。
///  • 數值存在 PlayerDataManager 的對話變數（Brat／Mood／BratEnd），會跟著存檔。
/// 所有數值與台詞都在 Inspector「雌小鬼互動」群組（台詞目前是占位字，請自行改）。
/// Godot 搜尋關鍵字：BratEnabled／BratOnPinchRelease／PendingBratEnding
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("雌小鬼互動")]
    [Export] public bool BratEnabled = true;
    [Export(PropertyHint.Range, "0,100,1")] public float BratInitial = 30f;           // 第一次進來的雌小鬼值
    public float BratValue = 30f;                                                      // 目前雌小鬼值（0~100）
    [Export] public Vector2 BratLineInterval = new Vector2(9f, 15f);                   // 隔多久說一句（秒，隨機）
    [Export] public float BratWindowSec = 5f;                                          // 說完話後多久內捏才算回應
    [Export(PropertyHint.Range, "0,1,0.01")] public float BratChanceBase = 0.3f;       // 雌小鬼值 0 時說雌小鬼話的機率
    [Export(PropertyHint.Range, "0,1,0.01")] public float BratChanceAt100 = 0.8f;      // 雌小鬼值 100 時說雌小鬼話的機率
    [Export] public float BratPinchDown = 14f;          // 雌小鬼話 + 捏：雌小鬼值下降（× 捏的幅度 0~1）
    [Export] public float BratIgnoredUp = 6f;           // 雌小鬼話沒人理：雌小鬼值上升
    [Export] public float NormalPinchMoodDown = 12f;    // 正常話 + 捏：心情下降（× (0.4 + 0.6×幅度)）
    [Export] public float NormalPinchBratDown = 4f;     // 正常話 + 捏：雌小鬼值下降（× 幅度）
    [Export] public float NormalIgnoredMoodUp = 3f;     // 正常話沒被捏：心情上升
    [Export] public float LessonBratDown = 35f;          // R18「教訓」：雌小鬼值下降
    [Export] public float LessonMoodBase = 18f;          // R18「教訓」：心情扣的基準（舒服度高會少扣：× (1.6 − 1.2×舒服度)）
    [Export] public float LessonDuration = 2.5f;         // 「教訓」快速頂到底持續秒數
    [Export] public float HeadSootheNeedSec = 1.2f;      // 雌小鬼話期間摸頭多久算安撫成功
    [Export] public float HeadSootheBratDown = 5f;       // 安撫成功：雌小鬼值下降
    [Export] public float HeadSootheMoodUp = 4f;         // 安撫成功：心情上升
    [Export] public float HeadSootheMoodPerSec = 1.5f;   // R18 做的時候摸頭：每秒心情上升
    [Export] public float BratTwistImpulse = 130f;       // 扭臉：被捏放手時臉往反方向甩的力道
    [Export] public string[] BratBegLines = { "對、對不起～！", "我錯了啦～！", "慢、慢一點…求你了…！" };   // 被教訓時求饒（占位）
    [Export] public string[] BratSootheLines = { "……嗯。", "哼…這次就放過你。", "…呼。" };                       // 被摸頭安撫（占位）
    // 莉奈定時說話的台詞（雌小鬼話／正常話）依狀態分成四個群組，見 TrainingRoomRuntime.GirlTalk.cs
    [Export] public string[] BratPinchReplyLines = { "痛痛痛～！", "嗚…", "放手啦～！" };                     // 被捏時的反應（占位）
    [Export] public string[] BratPinchNormalReplyLines = { "…你幹嘛？", "好痛…", "……" };                    // 正常話被捏時的反應（占位）

    const string BratVarBrat = "Brat", BratVarMood = "Mood", BratVarEnd = "BratEnd", BratVarInit = "BratInit";

    bool  _bratLoaded;
    float _bratTimer = -1f, _bratWindow;
    int   _bratLineType;                 // 0 沒有、1 雌小鬼話、2 正常話
    float _lessonT, _headSootheT;
    Label _bratLabel; ProgressBar _bratBar;

    // ── 外部可呼叫：隔天要不要觸發結局（0 沒有、1 搬走、2 奴隸）──
    public static int PendingBratEnding() => PlayerDataManager.Instance?.GetDialogueVar(BratVarEnd) ?? 0;

    static void BratSetVar(string name, int v)
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm == null) return;
        pdm.AddDialogueVar(name, v - pdm.GetDialogueVar(name));
    }

    void BratLoad()
    {
        _bratLoaded = true;
        var pdm = PlayerDataManager.Instance;
        if (pdm == null) { BratValue = BratInitial; return; }
        if (pdm.GetDialogueVar(BratVarInit) == 0)
        {
            BratSetVar(BratVarInit, 1);
            BratSetVar(BratVarBrat, Mathf.RoundToInt(BratInitial));
            BratSetVar(BratVarMood, Mathf.RoundToInt(Mood));
        }
        BratValue = pdm.GetDialogueVar(BratVarBrat);
        Mood = pdm.GetDialogueVar(BratVarMood);
    }

    void BratSave()
    {
        BratValue = Mathf.Clamp(BratValue, 0f, 100f);
        Mood = Mathf.Clamp(Mood, 0f, 100f);
        BratSetVar(BratVarBrat, Mathf.RoundToInt(BratValue));
        BratSetVar(BratVarMood, Mathf.RoundToInt(Mood));
        var pdm = PlayerDataManager.Instance;
        if (pdm == null || pdm.GetDialogueVar(BratVarEnd) != 0) return;
        if (Mood <= 0f)           { BratSetVar(BratVarEnd, 1); GD.Print("[Brat] 心情歸 0 → 隔天結局：莉奈搬走"); }
        else if (BratValue >= 100f) { BratSetVar(BratVarEnd, 2); GD.Print("[Brat] 雌小鬼值 100 → 隔天結局：莉奈讓男主當奴隸"); }
    }

    /// <summary>莉奈說話（台詞可用「|」分段、「=>」男主回應，舒服加愛心、不舒服加「不要…」，見 GirlTalk.cs）。回傳這句有沒有帶男主回應。</summary>
    bool BratSay(string[] lines, bool allowReply = true)
    {
        lines = DemoMode.Trim(lines);   // 試玩版：每區只留 2 句
        if (lines == null || lines.Length == 0) return false;
        string t = Pick(lines);
        return !string.IsNullOrEmpty(t) && GirlSayRich(t, allowReply);
    }

    bool BratR18 => PlayerDataManager.Instance?.R18ContentEnabled == true;
    bool BratInserted => _jijiPhase == 3 && _jijiStage >= 1;

    /// <summary>每幀（Pinch.cs 的 PinchProcess 開頭呼叫）。</summary>
    void BratProcess(float dt)
    {
        if (_bratBar != null) { _bratBar.Value = BratValue; if (_bratLabel != null) _bratLabel.Text = $"{Mathf.RoundToInt(BratValue)}"; }
        if (!BratEnabled) { BratHideChoices(); return; }
        if (!_bratLoaded) BratLoad();
        bool r18 = BratR18;

        // R18 做的時候摸頭：慢慢回心情
        if (r18 && BratInserted && _reactFlags.head) { Mood += HeadSootheMoodPerSec * dt; if (Mood > 100f) Mood = 100f; _bratDirty = true; }
        if (_bratDirty && _bratLineType == 0) { _bratDirty = false; BratSave(); }

        // 「教訓」：雞雞快速頂到底
        if (_lessonT > 0f)
        {
            _lessonT -= dt;
            if (BratInserted) { _s2Paused = false; _s2SpeedLv = S2SpeedLvMax; }
            if (_lessonT <= 0f) _s2SpeedLv = Mathf.Min(_s2SpeedLv, S2SpeedLvMax * 0.5f);
        }

        // 不定時說話
        if (_bratLineType == 0)
        {
            BratHideChoices();
            if (_bratTimer < 0f) _bratTimer = (float)GD.RandRange(BratLineInterval.X, BratLineInterval.Y);
            _bratTimer -= dt / TalkTouchedMul();   // 有觸碰時說話間隔拉長（沒觸碰＝正常）
            if (_bratTimer <= 0f && _lessonT <= 0f)
            {
                float p = Mathf.Lerp(BratChanceBase, BratChanceAt100, Mathf.Clamp(BratValue / 100f, 0f, 1f));
                _bratLineType = GD.Randf() < p ? 1 : 2;
                bool hasChoices = r18 && BratInserted;   // 插入中（在做的時候）：莉奈每說一句，男主都有選項（名稱每次不同）
                // 依狀態（什麼都沒做／衣服脫了／觸摸中／插入中…）挑台詞（GirlTalk.cs）；台詞自己帶「=>男主回應」的話，這裡就會播
                bool painNoChoice = !hasChoices && CurrentGirlTalkState() == GirlTalkState.Pain;   // 痛：男主沒有選擇，就不會自己答應「我慢一點」
                bool lineHasReply = BratSay(GirlTalkLines(_bratLineType == 1), allowReply: !hasChoices && !painNoChoice);
                if (!hasChoices && !lineHasReply && !painNoChoice) GirlTalkMaleReply(_bratLineType == 1);   // 台詞沒帶回應、也沒有選項：用區域的男主回應（痛／要高潮，GirlTalk.cs）
                _bratWindow = BratWindowSec; _headSootheT = 0f;
                if (_bubbleGirl != null && IsInstanceValid(_bubbleGirl)) _bubbleGirl.SetMeta("hide_t", BratWindowSec);   // 氣泡留到回應時間結束
                _bratTimer = -1f;
                if (hasChoices && _lastGirlLineSoundOnly) BratResolve();   // 只是「嗯…」「啊哈…」這種聲音：不用男主選擇，也不算被無視
                else if (hasChoices && !GirlTalkShowStateChoices()) { if (_bratLineType == 1) BratShowChoices(); else BratShowChoicesNormal(); }   // 痛／莉奈要高潮：專屬選項；雌小鬼話：教訓／不理；正常話：溫柔／繼續
            }
        }
        else
        {
            _bratWindow -= dt;
            // 摸頭安撫（只有雌小鬼話才算「過關」）
            if (_reactFlags.head) _headSootheT += dt; else _headSootheT = Mathf.Max(0f, _headSootheT - dt);
            if (_bratLineType == 1 && _headSootheT >= HeadSootheNeedSec)
            {
                BratValue -= HeadSootheBratDown; Mood += HeadSootheMoodUp;
                BratSay(BratSootheLines); BratResolve();
            }
            else if (_bratWindow <= 0f)
            {   // 沒人理她
                if (_bratLineType == 1) BratValue += BratIgnoredUp;
                else { Mood += NormalIgnoredMoodUp; BratValue -= 1f; }
                BratResolve();
            }
            else BratLayoutChoices();
        }
    }

    bool _bratDirty;

    void BratResolve()
    {
        _bratLineType = 0; _bratWindow = 0f; _bratTimer = -1f; _headSootheT = 0f;
        BratHideChoices();
        BratSave();
    }

    // ── R18 選項：做在男主的對話框裡（PlayerChoice.cs）──
    void BratShowChoices()
    {
        if (_playerChoiceActive) return;
        string a = PickName(ChoiceLessonNames, "教訓她"), b = PickName(ChoiceIgnoreNames, "不理她");   // 名稱每次不同
        ShowPlayerChoices(BratChoicePrompt, new[] { a, b },
            new Action[] { () => GirlChoose(a, BratChooseLesson), () => GirlChoose(b, BratChooseIgnore) }, BratWindowSec);
    }

    /// <summary>做的時候莉奈說的是正常話：選項是「溫柔一點（放慢）」或「繼續」。</summary>
    void BratShowChoicesNormal()
    {
        if (_playerChoiceActive) return;
        string a = PickName(ChoiceSoftNames, "溫柔一點"), b = PickName(ChoiceCarryOnNames, "繼續");
        ShowPlayerChoices(BratChoicePrompt, new[] { a, b },
            new Action[] { () => GirlChoose(a, ApplySlowDown), () => GirlChoose(b, ApplyIgnoreNormal) }, BratWindowSec);
    }

    static Button BratMakeBtn(string text, System.Action onPress)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(84f, 30f) };
        var sb = new StyleBoxFlat { BgColor = new Color(1f, 0.97f, 0.99f, 0.92f), BorderColor = new Color(0.82f, 0.38f, 0.58f) };
        sb.SetBorderWidthAll(2); sb.SetCornerRadiusAll(12);
        var sbH = (StyleBoxFlat)sb.Duplicate(); sbH.BgColor = new Color(1f, 0.85f, 0.92f, 0.96f);
        b.AddThemeStyleboxOverride("normal", sb); b.AddThemeStyleboxOverride("hover", sbH); b.AddThemeStyleboxOverride("pressed", sbH);
        b.AddThemeColorOverride("font_color", new Color(0.22f, 0.10f, 0.18f));
        b.AddThemeColorOverride("font_hover_color", new Color(0.22f, 0.10f, 0.18f));
        b.Pressed += onPress;
        return b;
    }

    void BratLayoutChoices() { }   // 選項在男主氣泡裡，位置由 LayoutBubbles 排，不用另外排

    void BratHideChoices() => HidePlayerChoices();

    void BratChooseLesson()
    {
        if (_bratLineType != 1) return;
        ApplyLesson();
    }

    /// <summary>「教訓」的效果（不檢查莉奈剛剛說的是不是雌小鬼話，給各種選項共用）。</summary>
    void ApplyLesson()
    {
        float comfort = Mathf.Clamp((Wetness / 100f) * 0.5f + (ClimaxLevel / 100f) * 0.5f, 0f, 1f);
        float moodLoss = Mathf.Max(5f, LessonMoodBase * (1.6f - 1.2f * comfort));
        BratValue -= LessonBratDown;
        Mood -= moodLoss;
        _lessonT = LessonDuration;
        BratSay(BratBegLines);
        GD.Print($"[Brat] 教訓：舒服度 {comfort:F2} → 心情 -{moodLoss:F1}、雌小鬼值 -{LessonBratDown:F0}");
        BratResolve();
    }

    void BratChooseIgnore()
    {
        if (_bratLineType != 1) return;
        BratValue += BratIgnoredUp;
        BratResolve();
    }

    /// <summary>捏臉放手時由 Pinch.cs 呼叫。strength：這次捏的幅度 0~1。</summary>
    void BratOnPinchRelease(float strength)
    {
        if (!BratEnabled || strength < 0.05f) return;
        strength = Mathf.Clamp(strength, 0f, 1f);
        // 扭臉：往捏的反方向轉開
        _pinchActive = true; _pinchVel += new Vector2(-_pinchSide * BratTwistImpulse * strength, 0f);
        if (_bratLineType == 1)
        {
            BratValue -= BratPinchDown * strength;
            _inPinchBratReply = true;   // 這句的語音要用被捏（雌小鬼）專屬音檔，見 Voice.cs PinchBratVoiceMap
            BratSay(BratPinchReplyLines);
            _inPinchBratReply = false;
        }
        else if (_bratLineType == 2)
        {
            Mood -= NormalPinchMoodDown * (0.4f + 0.6f * strength);
            BratValue -= NormalPinchBratDown * strength;
            _inPinchNormalReply = true;   // 這句的語音判斷要知道「現在是被捏的正常反應」（第三句「……」不發聲，見 Voice.cs）
            BratSay(BratPinchNormalReplyLines);
            _inPinchNormalReply = false;
        }
        else
        {
            Mood -= 1.5f * strength;   // 沒在說話時被亂捏：心情小降
        }
        BratResolve();
    }
}
