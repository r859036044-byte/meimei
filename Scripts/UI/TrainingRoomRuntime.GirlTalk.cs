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

    // 每個狀態各自一個群組（Inspector 裡分開折疊）；每組有「雌小鬼話」（可以教訓／不理）和「正常話」兩種
    [ExportGroup("莉奈台詞 1：什麼都沒做")]
    [Export] public string[] BratLines =
    {
        "哼～弱雞～", "這種程度也敢叫我做事？", "笨蛋笨蛋～", "才不聽你的呢～", "大叔好沒用～",
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
        "痛、痛啦～你不會輕一點嗎～", "笨蛋～弄得人家好痛～", "哼…這樣一點都不舒服～", "速度不對啦～大叔真的很笨～",
        "好痛…你是故意的吧～", "嗚…別、別那麼用力啦～", "這種技術也敢上場喔～？", "痛痛痛…我要生氣囉～",
    };
    [Export] public string[] BratNormalLinesPain =
    {
        "唔…有點痛…", "等、等一下…太快了…", "……慢一點，拜託。", "嗚…好痛…", "那裡…不舒服…",
        "不要…這樣會痛…", "請、請輕一點…", "……我有點受不了。",
    };

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
        return lines != null && lines.Length > 0 ? lines : (brat ? BratLines : BratNormalLines);
    }
}
