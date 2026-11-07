using Godot;
using System;

/// <summary>
/// TrainingRoomRuntime — 精力滿了（100，破功倒數開始）時，男主說「不行了，要射了」，
/// 並在男主的對話框出現兩個選項（選項放在男主氣泡裡，見 PlayerChoice.cs）：
///   慢下來　降低抽插速度、把破功倒數退回一點（一定成功，但只是爭取時間）。
///   射進去　直接射精。
/// 兩個選項都可以搭配「忍耐」按鈕：
///   • 選項出現期間隨時可以按忍耐（成功＝倒數歸零、精力退回去，選項收掉；失敗＝選項還在，可以再試）。
///   • 選「慢下來」之後，再按忍耐才能完全退回去。
///   • 選「射進去」之後，射精過程中按忍耐可以少射（原本的機制）。
/// 破功倒數結束（沒選也沒忍）會自動射精，選項跟著收掉。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("破功選項")]
    [Export] public string ChoiceBreakSlow = "慢下來";
    [Export] public string ChoiceBreakCum  = "射進去";
    [Export(PropertyHint.Range, "0,5,0.1")] public float BreakSlowRollbackSec = 1.5f;   // 選「慢下來」：破功倒數退回幾秒
    [Export(PropertyHint.Range, "0,1,0.05")] public float BreakSlowSpeedFrac  = 0.3f;   // 選「慢下來」：抽插速度最多降到最高速的幾成

    [ExportGroup("忍耐失敗（漏出）")]
    [Export(PropertyHint.Range, "0,1,0.05")] public float EndureTooFastSpeed = 0.7f;   // 破功中按忍耐時，抽插速度（0~1）到這個以上就一定忍不住
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float EndureLeakMin = 0.3f;     // 忍不住時只射出一部分（漏出）：最少佔平常的幾成
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float EndureLeakMax = 0.5f;     // 最多佔平常的幾成（每次在最少～最多之間隨機）；剩下的留在庫存
    [Export] public string[] EndureLeakLines = { "糟糕…還是忍不住！", "糟、糟糕…忍不住了…！", "啊…糟了，漏出來了…！" };

    bool _breakChoiceShown;

    /// <summary>忍耐失敗（還在抽插）：男主說「糟糕，還是忍不住」，這次只射出 EndureLeakMin~Max 的量，剩下的留著。</summary>
    void LeakEjaculate()
    {
        float frac = (float)GD.RandRange(Mathf.Min(EndureLeakMin, EndureLeakMax), Mathf.Max(EndureLeakMin, EndureLeakMax));
        GD.Print($"[Male] 忍耐失敗（速度 {_maleThrustSpeed:F2}）→ 漏出 {frac:P0}");
        MaleEjaculate(frac, EndureLeakLines);
    }

    /// <summary>破功開始時呼叫：男主說不行了、要射了，同時顯示兩個選項。</summary>
    void ShowBreakChoices()
    {
        _breakChoiceShown = true;
        ShowPlayerChoices(Pick(MaleBreakFeelLines), new[] { ChoiceBreakSlow, ChoiceBreakCum },
            new Action[] { OnBreakChooseSlow, OnBreakChooseCum }, BreakWindowSec);
    }

    /// <summary>每幀：破功結束（忍住了、射了）就把選項收掉。</summary>
    void UpdateBreakChoices()
    {
        if (_breakChoiceShown && !_breaking) { _breakChoiceShown = false; HidePlayerChoices(); }
    }

    /// <summary>收掉破功選項，但氣泡留著（接下來馬上要換成男主說的話）。</summary>
    void CloseBreakChoicesKeepBubble()
    {
        if (!_breakChoiceShown) return;
        _breakChoiceShown = false;
        HidePlayerChoices(false);
    }

    void OnBreakChooseSlow()
    {
        if (!_breaking) return;
        CloseBreakChoicesKeepBubble();
        _breakT = Mathf.Max(0f, _breakT - BreakSlowRollbackSec);
        if (_jijiPhase == 3 && _jijiStage >= 1) _s2SpeedLv = Mathf.Min(_s2SpeedLv, S2SpeedLvMax * BreakSlowSpeedFrac);
        SayMale(new[] { ChoiceBreakSlow });
        GD.Print($"[Male] 選「慢下來」→ 破功倒數退回 {BreakSlowRollbackSec:F1} 秒、抽插降速（還能再按忍耐）");
    }

    void OnBreakChooseCum()
    {
        if (!_breaking) return;
        CloseBreakChoicesKeepBubble();
        SayMale(new[] { ChoiceBreakCum });
        MaleEjaculate();   // 射精過程中還能按忍耐少射（OnEndurePressed 的射精中分支）
        GD.Print("[Male] 選「射進去」→ 直接射精");
    }

    /// <summary>忍耐的台詞：破功選項還在時，成功＝收掉選項換成這句；失敗＝選項留著，只更新氣泡上的字。</summary>
    void BreakChoiceSay(string[] lines, bool success)
    {
        if (!_breakChoiceShown) { SayMale(lines); return; }
        if (success) { CloseBreakChoicesKeepBubble(); SayMale(lines); }
        else if (_bubblePlayerLbl != null && IsInstanceValid(_bubblePlayerLbl) && lines != null && lines.Length > 0)
        {
            _bubblePlayerLbl.Text = Pick(lines);
            _bubblePlayer?.ResetSize();
        }
    }
}
