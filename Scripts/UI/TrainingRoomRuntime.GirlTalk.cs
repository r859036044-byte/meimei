using Godot;

/// <summary>
/// TrainingRoomRuntime — 莉奈自己開口說話（Brat.cs 的定時說話）依「目前狀態」挑不同的台詞：
///   Idle      什麼都沒做（衣服沒怎麼被動）→ 用 Brat.cs 原本的 BratLines／BratNormalLines
///   Undressed 衣服脫了（衣擺拉起、內褲脫掉等，ExposureScore ≥ GirlTalkUndressAt），但什麼都沒做
///   Touching  觸摸中（胸、乳頭、嘴、摸頭、摸 B、抽插…任何一個互動進行中）
///   Inserted  插入中（雞雞已經插進去）
///   Pain         痛／不舒服（插入或碰小穴時，速度不對或太乾）
///   NearClimax   莉奈要高潮了（穴高潮值 ≥ GirlTalkNearClimaxAt，而且有在被碰或插入）
///   MaleNearEjac 男主要射了（插入中，男主精力 ≥ GirlTalkMaleNearEjacAt 或破功中）
/// 「痛」和「莉奈要高潮了」是莉奈先說、過 GirlTalkReplyDelay 秒男主再在他的對話框回應（MaleReply* 台詞）。
/// 優先順序：莉奈要高潮 &gt; 男主要射 &gt; 痛 &gt; 插入中 &gt; 觸摸中 &gt; 衣服脫了 &gt; 什麼都沒做。
/// 每個狀態各有「雌小鬼話」和「正常話」兩組（雌小鬼話 = 之後可以「教訓／不理」的那種，見 Brat.cs）。
/// 台詞都是占位，自行在 Inspector 的「莉奈台詞 1～7」七個群組各自增減。
/// （被碰時另外有「點擊反應」台詞 Play.cs ReactNow，是碰到的當下才說，跟這裡的定時說話是兩套。）
/// </summary>
public partial class TrainingRoomRuntime
{
    public enum GirlTalkState { Idle, Undressed, Touching, Inserted, Pain, NearClimax, MaleNearEjac }

    [ExportGroup("莉奈台詞 0：狀態判定")]
    [Export] public float GirlTalkUndressAt = 1.0f;   // 「看到多少」（ExposureScore，0~約 4）到多少以上算「衣服脫了」
    [Export(PropertyHint.Range, "0,100,1")] public float GirlTalkNearClimaxAt = 90f;   // 穴高潮值到多少以上算「莉奈要高潮了」
    [Export(PropertyHint.Range, "0,120,1")] public float GirlTalkMaleNearEjacAt = 90f; // 男主精力到多少以上算「男主要射了」（也包含破功中）
    [Export(PropertyHint.Range, "0.2,6,0.1")] public float GirlTalkReplyDelay = 1.4f;   // 莉奈說完之後，男主多久才回應（秒；「痛」和「莉奈要高潮了」才有回應）

    // 每個狀態各自一個群組（Inspector 裡分開折疊）；每組有「雌小鬼話」（可以教訓／不理）和「正常話」兩種
    [ExportGroup("莉奈台詞 1：什麼都沒做")]
    [Export] public string[] BratLines =
    {
        "哼～雜魚❤～|～弱雞❤～=>又開始了嗎…你才是嘴上不饒人的那個吧。", "這種雜魚也敢命令我嗎？=>我哪有命令你，只是問問你要不要而已。", "笨蛋笨蛋～", "才不聽你的呢～", "大叔好沒用～",
        "喂～我都等好久了耶～", "你是不是在發呆啊～？", "真是的，一點用都沒有～", "要我誇你嗎？才不要～",
        "大叔的臉好呆喔～", "哼，別以為我會乖乖聽話～",
    };                                                                                        // 雌小鬼的話（占位）
    [Export] public string[] BratNormalLines =
    {
        "今天辛苦了。", "謝謝你。", "嗯，我知道了。", "要不要一起吃點東西？",
        "今天天氣不錯呢。", "你今天有好好吃飯嗎？", "……我有點睏了。", "那個，明天要做什麼呢？",
        "有你在，我比較安心。", "要不要早點休息？",
    };                                                                                        // 正常的話（占位）

    [ExportGroup("莉奈台詞 2：衣服脫了（但什麼都沒做）")]
    [Export] public string[] BratLinesUndress =
    {
        "哼～看什麼看啦，大叔～", "衣服都亂掉了～變態～", "這麼想看喔？笨蛋～", "眼睛都直了喔～好噁心～",
        "再看我就收費囉～", "才、才不是故意讓你看的～", "大叔果然是變態～", "哼，只准看，不准亂來～",
        "臉都紅了耶～純情大叔～", "你的視線好煩喔～",
    };
    [Export] public string[] BratNormalLinesUndress =
    {
        "……衣服，有點涼呢。", "那個…可以幫我整理一下嗎？", "你、你在看哪裡啦…", "有點不好意思…",
        "可以…把被子拉上來嗎？", "不要一直盯著看啦……", "感覺有點冷。", "我、我自己來整理就好…",
        "……不要看啦。", "身體好像有點熱熱的。",
    };

    [ExportGroup("莉奈台詞 3：觸摸中")]
    [Export] public string[] BratLinesTouch =
    {
        "只有這種程度嗎～？", "手好笨拙喔～弱雞～", "才不會舒服呢～", "大叔的手好癢～別碰啦～",
        "這樣就想讓我認輸？太天真了～", "呵呵，一點感覺都沒有喔～", "再用力一點啊～弱雞～",
        "手在抖喔～好沒用～", "哼，才沒有在忍耐呢～", "你就只會這樣嗎～？",
    };
    [Export] public string[] BratNormalLinesTouch =
    {
        "嗯…有點癢。", "……輕一點。", "那裡…有點奇怪。", "嗯…手好溫暖。", "等、等一下…",
        "……嗯。", "不要突然這樣啦…", "有點…不好意思。", "慢一點的話…可以。", "呼…有點喘。",
    };

    [ExportGroup("莉奈台詞 4：插入中")]
    [Export] public string[] BratLinesInsert =
    {
        "哼嗯…這種程度…才不算什麼～", "不、不要得意忘形喔～！", "嗯…才沒有很舒服啦～", "大叔…別、別太用力～",
        "嘴上說不要…身體才沒有誠實呢～", "哼…你、你別笑啊～", "才、才不會輸給你～", "這、這點小事…！",
        "再…再快一點也沒關係喔～…才怪～", "喂…不許看我的臉啦～",
    };
    [Export] public string[] BratNormalLinesInsert =
    {
        "嗯…慢一點…", "……呼…", "啊…有點…奇怪的感覺…", "不要…停下來…", "嗯…抱緊我…",
        "……哥哥…", "呼…呼…有點喘不過氣。", "嗯…可以…再靠近一點。", "……好熱。", "唔…不要太急…",
    };

    [ExportGroup("莉奈台詞 5：痛／不舒服")]
    [Export] public string[] BratLinesPain =
    {
        "痛、痛啦～你不會輕一點嗎～", "笨蛋雜魚～弄得人家好痛～=>好好好，我慢一點，別生氣。", "哼…這樣一點都不舒服～", "速度不對啦～大叔真的很笨～",
        "好痛…你是故意的吧～", "嗚…別、別那麼用力啦～", "這種技術也敢上場喔～？", "痛痛痛…我要生氣囉～",
    };
    [Export] public string[] BratNormalLinesPain =
    {
        "唔…有點痛…", "等、等一下…啊哈……|太快了…慢一點=>抱歉，我放慢一點。", "……慢一點，拜託。", "嗚…好痛…", "那裡…不舒服…",
        "不要…這樣會痛…", "請、請輕一點…", "……我有點受不了。",
    };
    // 莉奈說完之後，男主回應（在男主的對話框）。分「回雌小鬼話」和「回正常話」
    [Export] public string[] MaleReplyBratPain =
        { "好好好，我慢一點。", "抱歉抱歉，是不是太快了？", "別逞強啊，痛就說。", "那我換個節奏。" };
    [Export] public string[] MaleReplyNormalPain =
        { "對不起，我會輕一點。", "還痛嗎？我先停一下。", "別忍著，告訴我哪裡不舒服。", "我放慢一點，好嗎？" };

    [ExportGroup("莉奈台詞 6：莉奈要高潮了")]
    [Export] public string[] BratLinesNearClimax =
    {
        "才、才沒有要到呢～…哈啊…", "不、不可以～…再這樣的話…", "大叔…你、你給我停下來～…", "哼…才不會輸給你…嗯啊…",
        "要、要壞掉了啦～…笨蛋…", "不行…那裡…不要再弄了～", "我、我才沒有…啊…", "別、別看我的臉啦～…",
    };
    [Export] public string[] BratNormalLinesNearClimax =
    {
        "啊…不行…要、要到了…", "嗯…哥哥…快、快要…", "等、等一下…有什麼要來了…", "呼…呼…不要停…", "啊…我、我快不行了…",
        "嗯嗯…腦袋…一片空白…", "哥哥…抱緊我…", "啊…好奇怪…快要…",
    };
    // 莉奈說完之後，男主回應（在男主的對話框）。分「回雌小鬼話」和「回正常話」
    [Export] public string[] MaleReplyBratNearClimax =
        { "嘴硬什麼，都這樣了。", "是是是，你才沒有。", "想到就到吧，別忍。", "我看你還能撐多久。" };
    [Export] public string[] MaleReplyNormalNearClimax =
        { "我在這裡，別怕。", "沒事的，放鬆就好。", "嗯，我抱著你。", "不用忍，交給我。" };

    [ExportGroup("莉奈台詞 7：男主要射了")]
    [Export] public string[] BratLinesMaleNearEjac =
    {
        "喂…你是不是快要不行了～？", "哼～這麼快就想投降了嗎～弱雞～", "大叔的表情好奇怪喔～要射了嗎～？", "笨蛋…別在裡面…才怪～",
        "撐不住了吧～我都看出來了～", "忍、忍一下啦～真是的～",
    };
    [Export] public string[] BratNormalLinesMaleNearEjac =
    {
        "哥哥…你是不是快要…？", "嗯…感覺到了…你在發抖…", "沒關係…想射的話…可以喔。", "……我、我會接住的。", "呼…要、要來了嗎…？",
        "哥哥…不用忍耐也沒關係…", "嗯…我也…一起…", "……好熱。",
    };

    /// <summary>現在是哪個狀態。</summary>
    GirlTalkState CurrentGirlTalkState()
    {
        var f = _reactFlags;
        bool touching = f.breast || f.nipple || f.mouth || f.head || f.pussy || f.thrust;
        // 優先順序（由高到低）：莉奈要高潮 > 男主要射 > 痛 > 插入中 > 觸摸中 > 衣服脫了 > 什麼都沒做
        if (ClimaxLevel >= GirlTalkNearClimaxAt && (touching || BratInserted)) return GirlTalkState.NearClimax;
        if (BratInserted && (Stamina >= GirlTalkMaleNearEjacAt || _breaking)) return GirlTalkState.MaleNearEjac;
        if ((BratInserted || f.pussy) && ArousalRateNow < 0f) return GirlTalkState.Pain;   // 速度不對／太乾（跟眉毛的「不舒服」同一個判定）
        if (BratInserted) return GirlTalkState.Inserted;
        if (touching) return GirlTalkState.Touching;
        if (ExposureScore() >= GirlTalkUndressAt) return GirlTalkState.Undressed;
        return GirlTalkState.Idle;
    }

    [ExportGroup("莉奈台詞 8：男主選項與回應")]
    // 插入中，莉奈說「痛」或「要高潮了」時，男主在自己的對話框選擇怎麼回。選了之後男主會說出按鈕上那句話。
    [Export] public string ChoicePromptPain       = "她喊痛了…";
    [Export] public string ChoicePainPunish       = "小鬼，這就是你的懲罰";     // 效果＝教訓（雌小鬼值↓、心情↓、雞雞快速頂到底；莉奈求饒）
    [Export] public string ChoicePainSlow         = "慢下來吧";                 // 效果＝放慢抽插、心情↑
    [Export] public string ChoicePromptNearClimax = "她快要不行了…";
    [Export] public string ChoiceClimaxKeep       = "別停，就這樣";             // 效果＝維持速度、莉奈回應
    [Export] public string ChoiceClimaxSlow       = "慢下來吧";                 // 效果＝放慢抽插、心情↑
    [Export(PropertyHint.Range, "0,20,0.5")] public float SlowDownMoodUp = 3f;        // 選「慢下來」：心情上升
    [Export(PropertyHint.Range, "0,1,0.05")] public float SlowDownSpeedFrac = 0.3f;   // 選「慢下來」：抽插速度最多降到最高速的幾成
    [Export] public string[] GirlSlowReplyLines = { "……嗯，謝謝。", "呼…好多了。", "……算你還有點體貼。", "嗯…就這樣…慢慢來。" };   // 莉奈對「慢下來」的回應
    [Export] public string[] GirlKeepReplyLines = { "啊…不、不要停…♡", "嗯…就、就這樣…", "哈啊…♡", "……笨蛋…別停下來…" };       // 莉奈對「別停」的回應

    // ════════ 莉奈台詞的語法與加工 ════════════════════════════════════
    //   「|」  分段：一句台詞拆成幾段，依序顯示在莉奈的對話框（每段間隔 GirlSegmentGapSec 秒）。
    //   「=>」 男主回應：「=>」後面是男主在他的對話框回的話（最後一段出現後 GirlTalkReplyDelay 秒）。
    //   例："等、等一下…啊哈……|太快了…慢一點=>抱歉，我放慢一點。"
    //   沒有寫「=>」的台詞，維持原本的行為（例如「痛」「要高潮」區會用 MaleReply* 那組男主回應）。
    // 加工（說出口之前自動處理每一段）：
    //   • 莉奈舒服的時候：台詞後面加愛心（GirlHeart）。已經有 ♡／❤ 的不重複加。
    //   • 莉奈不舒服的時候（痛、速度不對、太乾）：台詞裡有「啊哈、嗯、唔…」這類擬聲詞，就在後面加上「不要…」之類的拒絕（GirlRefuseWords）。
    [ExportGroup("莉奈台詞 9：分段與加工")]
    [Export] public float GirlSegmentGapSec = 1.4f;   // 分段台詞：下一段幾秒後出現
    [Export] public string GirlHeart = "❤";            // 舒服時加在台詞後面的愛心
    [Export(PropertyHint.Range, "0,100,1")] public float GirlHeartClimaxAt = 35f;   // 穴高潮值到這個以上算「舒服」（也會看眉毛的舒服程度）
    [Export] public string[] GirlRefuseWords = { "不要…", "不、不要…", "不行…", "等、等一下…" };   // 不舒服時，擬聲詞後面加的話（隨機挑一個）
    [Export] public string[] GirlOnomatopoeia = { "啊哈", "哈啊", "嗯", "唔", "啊", "呼", "哈", "嗚" };   // 算擬聲詞的字

    /// <summary>莉奈現在不舒服：痛，或眉毛判定不舒服（速度不對／太乾）。</summary>
    bool GirlFeelsBad() => CurrentGirlTalkState() == GirlTalkState.Pain || (_browUncomfy && _browLingerT > 0f);

    /// <summary>莉奈現在舒服：穴高潮值夠高，或眉毛判定很舒服（而且不是不舒服）。</summary>
    bool GirlFeelsGood() => !GirlFeelsBad() && (ClimaxLevel >= GirlHeartClimaxAt || (_browLingerT > 0f && _browPos >= 0.5f));

    string DressGirlSegment(string seg, bool good, bool bad)
    {
        if (string.IsNullOrEmpty(seg)) return seg;
        if (good)
        {
            if (seg.Contains("♡") || seg.Contains("❤") || seg.Contains("♥")) return seg;
            return seg + GirlHeart;
        }
        if (bad && GirlOnomatopoeia != null && GirlRefuseWords != null && GirlRefuseWords.Length > 0)
        {
            foreach (var w in new[] { "不要", "不行", "等一下", "等、等一下", "停" }) if (seg.Contains(w)) return seg;   // 已經在拒絕了
            foreach (var o in GirlOnomatopoeia) if (!string.IsNullOrEmpty(o) && seg.Contains(o)) return seg + Pick(GirlRefuseWords);
        }
        return seg;
    }

    static void ParseGirlLine(string raw, out string[] segs, out string reply)
    {
        reply = null; string main = raw ?? "";
        int i = main.IndexOf("=>", System.StringComparison.Ordinal);
        if (i >= 0) { reply = main.Substring(i + 2).Trim(); main = main.Substring(0, i); }
        segs = main.Split('|', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
        if (segs.Length == 0) segs = new[] { main.Trim() };
        if (string.IsNullOrEmpty(reply)) reply = null;
    }

    /// <summary>莉奈說一句台詞：支援「|」分段、「=>」男主回應，並依舒服／不舒服加工。allowReply＝false 時不播男主回應（例如這時男主對話框已經是選項）。回傳這句有沒有帶男主回應。</summary>
    bool GirlSayRich(string raw, bool allowReply = true)
    {
        ParseGirlLine(raw, out var segs, out var reply);
        bool good = GirlFeelsGood(), bad = !good && GirlFeelsBad();
        for (int k = 0; k < segs.Length; k++) segs[k] = DressGirlSegment(segs[k], good, bad);
        ShowBubble(ref _bubbleGirl, ref _bubbleGirlLbl, segs[0], false);
        float t = 0f;
        for (int k = 1; k < segs.Length; k++)
        {
            t += GirlSegmentGapSec;
            string seg = segs[k];
            GetTree().CreateTimer(t).Timeout += () =>
            {
                if (!IsInstanceValid(this)) return;
                ShowBubble(ref _bubbleGirl, ref _bubbleGirlLbl, seg, false);
                // 回應時間還沒結束時，氣泡留到結束（跟 BratProcess 一致）
                if (_bubbleGirl != null && IsInstanceValid(_bubbleGirl))
                    _bubbleGirl.SetMeta("hide_t", Mathf.Max(PatTalkShowTime, _bratLineType != 0 ? Mathf.Max(_bratWindow, 0.5f) : 0f));
            };
        }
        if (reply != null && allowReply)
        {
            string r = reply;
            GetTree().CreateTimer(t + GirlTalkReplyDelay).Timeout += () =>
            {
                if (IsInstanceValid(this) && !_playerChoiceActive) SayMale(new[] { r });   // 選項還在時不蓋掉
            };
        }
        return reply != null;
    }

    // ════════ 男主選項的名稱：每次出現隨機挑一個，名字不要每次都一樣 ═════════
    [ExportGroup("男主選項名稱（每次隨機挑一個）")]
    [Export] public string[] ChoiceLessonNames  = { "教訓她", "好好教訓這個雌小鬼", "讓她知道厲害", "懲罰這個雌小鬼" };      // 雌小鬼話：教訓
    [Export] public string[] ChoiceIgnoreNames  = { "不理她", "裝作沒聽到", "隨她去說", "繼續做自己的" };                    // 雌小鬼話：不理
    [Export] public string[] ChoiceSoftNames    = { "溫柔一點", "慢慢來", "聽她的，放慢一點" };                              // 正常話：放慢、溫柔
    [Export] public string[] ChoiceCarryOnNames = { "繼續", "不用管，繼續", "就這樣" };                                      // 正常話：繼續
    [Export] public string[] ChoicePainPunishNames = { "小鬼，這就是你的懲罰", "叫你亂說話，這是懲罰", "再嘴硬就別怪我了" };    // 痛：懲罰
    [Export] public string[] ChoiceSlowNames    = { "慢下來吧", "好吧，放慢一點", "別急，慢慢來" };                          // 痛／要高潮：放慢
    [Export] public string[] ChoiceKeepNames    = { "別停，就這樣", "繼續，不要停", "就這樣繼續" };                          // 要高潮：繼續

    string PickName(string[] pool, string fallback)
    {
        var s = Pick(pool);
        return string.IsNullOrEmpty(s) ? fallback : s;
    }

    /// <summary>正常話時選「繼續」：照原本「沒人理她」的算法。</summary>
    void ApplyIgnoreNormal()
    {
        Mood += NormalIgnoredMoodUp; BratValue -= 1f;
        BratResolve();
    }

    /// <summary>目前狀態是不是「痛」或「莉奈要高潮了」（這兩種插入中會給男主專屬選項）。</summary>
    bool GirlTalkStateHasChoices()
    {
        var st = CurrentGirlTalkState();
        return st == GirlTalkState.Pain || st == GirlTalkState.NearClimax;
    }

    /// <summary>在男主對話框顯示「痛」或「莉奈要高潮了」的專屬選項。不是這兩種狀態就回傳 false（由呼叫端改用一般選項）。</summary>
    bool GirlTalkShowStateChoices()
    {
        var st = CurrentGirlTalkState();
        if (st == GirlTalkState.Pain)
        {
            string pa = PickName(ChoicePainPunishNames, ChoicePainPunish), pb = PickName(ChoiceSlowNames, ChoicePainSlow);   // 名稱每次不同
            ShowPlayerChoices(ChoicePromptPain, new[] { pa, pb },
                new System.Action[] { () => GirlChoose(pa, ApplyLesson), () => GirlChoose(pb, ApplySlowDown) }, BratWindowSec);
            return true;
        }
        if (st == GirlTalkState.NearClimax)
        {
            string ca = PickName(ChoiceKeepNames, ChoiceClimaxKeep), cb = PickName(ChoiceSlowNames, ChoiceClimaxSlow);   // 名稱每次不同
            ShowPlayerChoices(ChoicePromptNearClimax, new[] { ca, cb },
                new System.Action[] { () => GirlChoose(ca, ApplyKeepGoing), () => GirlChoose(cb, ApplySlowDown) }, BratWindowSec);
            return true;
        }
        return false;
    }

    /// <summary>選了某個選項：男主說出那句話 → 套用效果（莉奈回應）。</summary>
    void GirlChoose(string said, System.Action effect)
    {
        HidePlayerChoices(false);                       // 選項收掉，但氣泡留著，直接換成男主說的那句
        SayMale(new[] { said });
        effect?.Invoke();
    }

    void ApplySlowDown()
    {
        Mood += SlowDownMoodUp;
        if (BratInserted) _s2SpeedLv = Mathf.Min(_s2SpeedLv, S2SpeedLvMax * SlowDownSpeedFrac);
        BratSay(GirlSlowReplyLines);
        GD.Print($"[GirlTalk] 選「慢下來」→ 心情 +{SlowDownMoodUp:F0}、抽插速度降到 {SlowDownSpeedFrac:P0} 以下");
        BratResolve();
    }

    void ApplyKeepGoing()
    {
        BratSay(GirlKeepReplyLines);
        GD.Print("[GirlTalk] 選「別停」→ 維持速度");
        BratResolve();
    }

    /// <summary>莉奈說完定時台詞後，男主在自己的對話框回應。只有「痛」和「莉奈要高潮了」兩種狀態才回。
    /// brat＝剛剛莉奈說的是雌小鬼話（true）或正常話（false）。</summary>
    void GirlTalkMaleReply(bool brat)
    {
        var lines = CurrentGirlTalkState() switch
        {
            GirlTalkState.Pain       => brat ? MaleReplyBratPain       : MaleReplyNormalPain,
            GirlTalkState.NearClimax => brat ? MaleReplyBratNearClimax : MaleReplyNormalNearClimax,
            _ => null,
        };
        lines = DemoMode.Trim(lines);   // 試玩版：每區只留 2 句
        if (lines == null || lines.Length == 0) return;
        GetTree().CreateTimer(GirlTalkReplyDelay).Timeout += () =>
        {
            if (IsInstanceValid(this) && !_playerChoiceActive) SayMale(lines);   // 選項還在時不蓋掉
        };
    }

    /// <summary>依目前狀態回傳要說的那一組台詞。brat＝雌小鬼話（true）或正常話（false）。</summary>
    string[] GirlTalkLines(bool brat)
    {
        var lines = CurrentGirlTalkState() switch
        {
            GirlTalkState.NearClimax   => brat ? BratLinesNearClimax   : BratNormalLinesNearClimax,
            GirlTalkState.MaleNearEjac => brat ? BratLinesMaleNearEjac : BratNormalLinesMaleNearEjac,
            GirlTalkState.Pain         => brat ? BratLinesPain         : BratNormalLinesPain,
            GirlTalkState.Inserted  => brat ? BratLinesInsert  : BratNormalLinesInsert,
            GirlTalkState.Touching  => brat ? BratLinesTouch   : BratNormalLinesTouch,
            GirlTalkState.Undressed => brat ? BratLinesUndress : BratNormalLinesUndress,
            _                       => brat ? BratLines        : BratNormalLines,
        };
        // 某個狀態的台詞被清空時，退回「什麼都沒做」那組，不會突然不說話
        return DemoMode.Trim(lines != null && lines.Length > 0 ? lines : (brat ? BratLines : BratNormalLines));   // 試玩版：每區只留 2 句
    }
}
