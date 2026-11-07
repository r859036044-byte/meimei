using Godot;

/// <summary>
/// TrainingRoomRuntime — 莉奈自己開口說話（Brat.cs 的定時說話）依「目前狀態」挑不同的台詞：
///   Idle      什麼都沒做（衣服沒怎麼被動）→ 用 Brat.cs 原本的 BratLines／BratNormalLines
///   Undressed 衣服脫了（衣擺拉起、內褲脫掉等，ExposureScore ≥ GirlTalkUndressAt），但什麼都沒做
///   Touching  觸摸中（胸、乳頭、嘴、摸頭、摸 B、抽插…任何一個互動進行中）
///   Inserted  插入中（雞雞已經插進去）
/// 優先順序：插入中 &gt; 觸摸中 &gt; 衣服脫了 &gt; 什麼都沒做。
/// 每個狀態各有「雌小鬼話」和「正常話」兩組（雌小鬼話 = 之後可以「教訓／不理」的那種，見 Brat.cs）。
/// 台詞都是占位，自行在 Inspector 的「莉奈台詞 1～4」四個群組各自增減。
/// （被碰時另外有「點擊反應」台詞 Play.cs ReactNow，是碰到的當下才說，跟這裡的定時說話是兩套。）
/// </summary>
public partial class TrainingRoomRuntime
{
    public enum GirlTalkState { Idle, Undressed, Touching, Inserted }

    [ExportGroup("莉奈台詞 0：狀態判定")]
    [Export] public float GirlTalkUndressAt = 1.0f;   // 「看到多少」（ExposureScore，0~約 4）到多少以上算「衣服脫了」

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

    /// <summary>現在是哪個狀態。</summary>
    GirlTalkState CurrentGirlTalkState()
    {
        if (BratInserted) return GirlTalkState.Inserted;
        var f = _reactFlags;
        if (f.breast || f.nipple || f.mouth || f.head || f.pussy || f.thrust) return GirlTalkState.Touching;
        if (ExposureScore() >= GirlTalkUndressAt) return GirlTalkState.Undressed;
        return GirlTalkState.Idle;
    }

    /// <summary>依目前狀態回傳要說的那一組台詞。brat＝雌小鬼話（true）或正常話（false）。</summary>
    string[] GirlTalkLines(bool brat)
    {
        var lines = CurrentGirlTalkState() switch
        {
            GirlTalkState.Inserted  => brat ? BratLinesInsert  : BratNormalLinesInsert,
            GirlTalkState.Touching  => brat ? BratLinesTouch   : BratNormalLinesTouch,
            GirlTalkState.Undressed => brat ? BratLinesUndress : BratNormalLinesUndress,
            _                       => brat ? BratLines        : BratNormalLines,
        };
        // 某個狀態的台詞被清空時，退回「什麼都沒做」那組，不會突然不說話
        return lines != null && lines.Length > 0 ? lines : (brat ? BratLines : BratNormalLines);
    }
}
