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
        { "哼～弱雞～", "這種程度也敢叫我做事？", "笨蛋笨蛋～", "才不聽你的呢～", "大叔好沒用～" };            // 雌小鬼的話（占位）
    [Export] public string[] BratNormalLines =
        { "今天辛苦了。", "謝謝你。", "嗯，我知道了。", "要不要一起吃點東西？" };                                // 正常的話（占位）

    [ExportGroup("莉奈台詞 2：衣服脫了（但什麼都沒做）")]
    [Export] public string[] BratLinesUndress       = { "哼～看什麼看啦，大叔～", "衣服都亂掉了～變態～", "這麼想看喔？笨蛋～" };
    [Export] public string[] BratNormalLinesUndress = { "……衣服，有點涼呢。", "那個…可以幫我整理一下嗎？", "你、你在看哪裡啦…" };

    [ExportGroup("莉奈台詞 3：觸摸中")]
    [Export] public string[] BratLinesTouch         = { "只有這種程度嗎～？", "手好笨拙喔～弱雞～", "才不會舒服呢～" };
    [Export] public string[] BratNormalLinesTouch   = { "嗯…有點癢。", "……輕一點。", "那裡…有點奇怪。" };

    [ExportGroup("莉奈台詞 4：插入中")]
    [Export] public string[] BratLinesInsert        = { "哼嗯…這種程度…才不算什麼～", "不、不要得意忘形喔～！", "嗯…才沒有很舒服啦～" };
    [Export] public string[] BratNormalLinesInsert  = { "嗯…慢一點…", "……呼…", "啊…有點…奇怪的感覺…" };

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
