using Godot;

/// <summary>
/// 全年齡摸頭規則：
///  • 一天（一次進調教室）摸頭累計不超過 PatOverSec：摸頭會漲心情、莉奈說友好台詞（每句再加心情）。
///  • 超過 PatOverSec：摸頭反而扣心情、雌小鬼值上升，只剩雌小鬼台詞，不再有友好台詞。
///  • 連續摸太久：先皺眉頭（PatFrownSec），再嚴厲說「雜魚別摸了」（PatAnnoyedSec）；
///    男主接著說「莉奈好像不耐煩了？」、「還是別一直摸了」，這兩句對應的台詞會多加心情。
/// 所有秒數／數值都在 Inspector「摸頭不耐煩（全年齡）」群組。R18 不套用。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("摸頭不耐煩（全年齡）")]
    [Export] public float PatOverSec = 30f;                   // 一天摸頭累計超過這個秒數：反而扣心情
    [Export] public float PatOverMoodDownPerSec = 1.0f;       // 超過後：每秒扣的心情
    [Export] public float PatOverBratUpPerSec = 0.6f;         // 超過後：每秒加的雌小鬼值（頂多到 99）
    [Export] public float PatFrownSec = 8f;                   // 連續摸頭多久，莉奈開始皺眉
    [Export] public float PatAnnoyedSec = 14f;                // 連續摸頭多久，莉奈說「雜魚別摸了」
    [Export] public float PatFriendlyLineMoodUp = 3f;         // 莉奈每說一句友好台詞：心情加多少
    [Export] public float PatStopLineMoodUp = 6f;             // 男主說「還是別一直摸了」：心情加多少（比一般台詞多）
    [Export] public string[] PatGirlLinesFriendly = { "嗯…被摸頭的感覺…還不錯。", "謝謝你…哥哥。", "呼…好舒服，這樣就好…", "你的手…好溫暖呢♡", "偶爾這樣…也不錯啦…" };
    [Export] public string[] PatAnnoyedGirlLines = { "雜魚別摸了！", "夠了啦…雜魚別摸了！" };
    [Export] public string PatPlayerAnnoyedLine1 = "莉奈好像不耐煩了？";
    [Export] public string PatPlayerAnnoyedLine2 = "還是別一直摸了。";

    float _patDayT;               // 今天摸頭累計秒數
    float _patContT, _patGapT;    // 連續摸頭秒數／停手秒數
    bool  _patAnnoyed;            // 莉奈正在皺眉（眉毛固定成皺眉）
    float _patAnnoySeqT = -1f;    // 「別摸了」之後男主兩句台詞的計時（-1＝沒在播）
    int   _patAnnoySeqStep;

    /// <summary>全年齡且今天摸頭已超過上限：摸頭不再加心情。</summary>
    bool PatOverLimit => !BratR18 && _patDayT > PatOverSec;

    void PatAnnoyProcess(float dt)
    {
        if (BratR18) { _patAnnoyed = false; return; }

        if (_patOn)
        {
            _patDayT += dt; _patContT += dt; _patGapT = 0f;
            if (_patDayT > PatOverSec)
            {   // 摸太久：心情反而下降、雌小鬼值上升
                Mood = Mathf.Max(1f, Mood - PatOverMoodDownPerSec * dt);
                BratValue = Mathf.Min(99f, BratValue + PatOverBratUpPerSec * dt);
                _bratDirty = true;
            }
            if (_patContT >= PatFrownSec) _patAnnoyed = true;
            if (_patContT >= PatAnnoyedSec && _patAnnoySeqT < 0f)
            {
                string line = Pick(PatAnnoyedGirlLines);
                ShowBubble(ref _bubbleGirl, ref _bubbleGirlLbl, line, false);
                PlayTrVoice("talk_brat/brat_zako_2.wav");
                _patAnnoySeqT = 0f; _patAnnoySeqStep = 0;
                _patContT = PatFrownSec;   // 繼續摸的話，過一陣子會再嫌一次
            }
        }
        else
        {
            _patGapT += dt;
            if (_patGapT > 1.5f) { _patContT = 0f; _patAnnoyed = false; }
        }

        if (_patAnnoySeqT >= 0f)
        {
            _patAnnoySeqT += dt;
            if (_patAnnoySeqStep == 0 && _patAnnoySeqT >= 1.6f)
            {
                ShowBubble(ref _bubblePlayer, ref _bubblePlayerLbl, PatPlayerAnnoyedLine1, true);
                _patAnnoySeqStep = 1;
            }
            else if (_patAnnoySeqStep == 1 && _patAnnoySeqT >= 3.6f)
            {
                ShowBubble(ref _bubblePlayer, ref _bubblePlayerLbl, PatPlayerAnnoyedLine2, true);
                Mood = Mathf.Min(100f, Mood + PatStopLineMoodUp);   // 男主體貼的這句：心情多加一些
                _bratDirty = true;
                _patAnnoySeqStep = 2; _patAnnoySeqT = -1f;
            }
        }
    }

    /// <summary>摸頭台詞池：全年齡沒超過上限→友好台詞；超過上限→只剩雌小鬼台詞。（R18 照舊）</summary>
    string[] PatGirlPoolNonSex()
        => (!BratR18 && !PatOverLimit && PatGirlLinesFriendly != null && PatGirlLinesFriendly.Length > 0) ? PatGirlLinesFriendly : PatGirlLines;
}
