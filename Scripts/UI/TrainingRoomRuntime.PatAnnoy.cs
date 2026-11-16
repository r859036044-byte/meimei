using Godot;

/// <summary>
/// 全年齡摸頭規則：
///  • 一天（一次進調教室）摸頭累計不超過 PatOverSec：摸頭會漲心情、莉奈說友好台詞（每句再加心情）。
///  • 超過 PatOverSec：摸頭反而扣心情、雌小鬼值上升，只剩雌小鬼台詞，不再有友好台詞。
///  • 連續摸頭累計「不耐煩條」（PatFillSec），滿了不耐煩 PatImpatientSec 秒，之後指數衰減；
///    男主接著說「莉奈好像不耐煩了？」、「還是別一直摸了」，這兩句對應的台詞會多加心情。
/// 所有秒數／數值都在 Inspector「摸頭不耐煩（全年齡）」群組。R18 不套用。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("摸頭不耐煩（全年齡）")]
    [Export] public float PatOverSec = 30f;                   // 一天摸頭累計超過這個秒數：反而扣心情
    [Export] public float PatOverMoodDownPerSec = 1.0f;       // 超過後：每秒扣的心情
    [Export] public float PatOverBratUpPerSec = 0.6f;         // 超過後：每秒加的雌小鬼值（頂多到 99）
    [ExportGroup("摸頭不耐煩條（全年齡）")]
    [Export] public float PatFillSec = 10f;                   // 連續摸頭大約多久條會滿（前慢後快）
    [Export] public float PatImpatientSec = 3f;               // 條滿了：莉奈不耐煩多久（這段時間摸頭沒有任何效果）
    [Export] public float PatDecayHalfSec = 6f;               // 條的下降：每隔這個秒數剩一半（指數衰減，剛氣完降得快、之後越降越慢）
    [Export] public float PatMeterRise0 = 0.5f;               // 上升曲線：條剛開始時的速度倍率
    [Export] public float PatMeterRise1 = 1.5f;               // 上升曲線：條越滿加速越多（速度＝(Rise0 + Rise1×條)）
    [ExportGroup("摸頭不耐煩（全年齡）")]
    [Export] public float PatFriendlyLineMoodUp = 3f;         // 莉奈每說一句友好台詞：心情加多少
    [Export] public float PatStopLineMoodUp = 6f;             // 男主說「還是別一直摸了」：心情加多少（比一般台詞多）
    [Export] public string[] PatGirlLinesFriendly = { "嗯…被摸頭的感覺…還不錯。", "謝謝你…哥哥。", "呼…好舒服，這樣就好…", "你的手…好溫暖呢♡", "偶爾這樣…也不錯啦…" };
    [Export] public string[] PatAnnoyedGirlLines = { "雜魚別摸了！", "夠了啦…雜魚別摸了！" };
    [Export] public string PatPlayerAnnoyedLine1 = "莉奈好像不耐煩了？";
    [Export] public string PatPlayerAnnoyedLine2 = "還是別一直摸了。";

    float _patDayT;               // 今天摸頭累計秒數
    float _patMeter;              // 不耐煩條 0~1
    float _patImpT;               // 不耐煩剩餘秒數（>0＝不耐煩中）
    bool  _patAnnoyed;            // 莉奈正在皺眉（眉毛固定成皺眉）
    float _patAnnoySeqT = -1f;    // 「別摸了」之後男主兩句台詞的計時（-1＝沒在播）
    int   _patAnnoySeqStep;

    /// <summary>全年齡且今天摸頭已超過上限：摸頭不再加心情。</summary>
    bool PatOverLimit => !BratR18 && (_patDayT > PatOverSec || _patImpT > 0f);

    void PatAnnoyProcess(float dt)
    {
        if (BratR18) { _patAnnoyed = false; return; }

        if (_patOn)
        {
            _patDayT += dt;
            if (_patDayT > PatOverSec)
            {   // 摸太久：心情反而下降、雌小鬼值上升
                Mood = Mathf.Max(1f, Mood - PatOverMoodDownPerSec * dt);
                BratValue = Mathf.Min(99f, BratValue + PatOverBratUpPerSec * dt);
                _bratDirty = true;
            }
        }

        if (_patImpT > 0f)
        {   // 不耐煩中：條維持滿，時間到才開始降
            _patImpT -= dt; _patAnnoyed = true;
            if (_patImpT <= 0f) { _patImpT = 0f; _patAnnoyed = false; }
        }
        else if (_patOn)
        {   // 上升：越接近滿越快（先慢後快）
            _patMeter += dt / Mathf.Max(0.1f, PatFillSec) * (PatMeterRise0 + PatMeterRise1 * _patMeter);
            if (_patMeter >= 1f)
            {
                _patMeter = 1f; _patImpT = PatImpatientSec; _patAnnoyed = true;
                string line = Pick(PatAnnoyedGirlLines);
                ShowBubble(ref _bubbleGirl, ref _bubbleGirlLbl, line, false);
                PlayTrVoice("talk_brat/brat_zako_2.wav");
                _patAnnoySeqT = 0f; _patAnnoySeqStep = 0;
            }
        }
        else if (_patMeter > 0f)
        {   // 下降：指數衰減（非直線）
            _patMeter *= Mathf.Pow(0.5f, dt / Mathf.Max(0.1f, PatDecayHalfSec));
            if (_patMeter < 0.03f) _patMeter = 0f;
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
