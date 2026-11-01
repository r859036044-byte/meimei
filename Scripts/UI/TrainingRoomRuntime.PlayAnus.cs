using Godot;

/// <summary>
/// 肛門濕潤度（和小穴濕度分開）：
///  • 舌頭舔肛門（選 👅 後點肛門）會增加肛門濕潤度。
///  • 肛門抽插要「足夠濕潤」（≥ AnusWetNeed）才算舒服：不夠濕時穴高潮值速度打折（AnusDryMult），舒服時尻部舒服度上升。
///  • 抽插肛門時濕潤度會下降；但精液超過 AnusSemenNoDropPct（30）時抽插不會掉濕潤。
///  • 肛門舒服的時候，小穴濕度上升變快（AnusComfortWetMult 倍）。
/// 數值全部在 Inspector「肛門濕潤」群組，Play 中可即時調。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("肛門濕潤")]
    [Export(PropertyHint.Range, "0,100,1")] public float AnusWetNeed = 50f;            // 要多濕才算「足夠濕潤」＝肛門舒服
    [Export(PropertyHint.Range, "0,20,0.1")] public float AnusWetGainLick = 6f;        // 舔肛門每秒增加（隨舌頭速度加成）
    [Export(PropertyHint.Range, "0,10,0.1")] public float AnusWetDrainThrust = 2.5f;   // 抽插肛門每秒下降（精液夠多時不降）
    [Export(PropertyHint.Range, "0,100,1")] public float AnusSemenNoDropPct = 30f;     // 精液超過這個值，抽插不掉濕潤
    [Export(PropertyHint.Range, "0,5,0.05")] public float AnusWetDecay = 0.3f;         // 什麼都沒做時每秒下降
    [Export(PropertyHint.Range, "0,1,0.05")] public float AnusDryMult = 0.1f;          // 完全不濕時肛門抽插的穴高潮值速度倍率
    [Export(PropertyHint.Range, "1,5,0.1")] public float AnusComfortWetMult = 2f;      // 肛門舒服時小穴濕度上升速度倍率
    [Export(PropertyHint.Range, "0,20,0.1")] public float AnusComfortRate = 4f;        // 肛門舒服時每秒增加的尻部舒服度

    float _anusWet; bool _prevLickA;
    public float AnusWetness => _anusWet;
    bool _anusComfort;   // 目前肛門是否舒服（足夠濕潤＋抽插中）

    float AnusWetFactor() => Mathf.Lerp(AnusDryMult, 1f, Mathf.Clamp(_anusWet / Mathf.Max(1f, AnusWetNeed), 0f, 1f));

    /// <summary>每幀（Play.cs）呼叫：更新肛門濕潤度與肛門舒服狀態。</summary>
    void StepAnusWet(float dt, bool thrust, float sTongue)
    {
        if (_lickOn && _lickTarget == "A" && (!_mLegOpen || _jijiPhase != 0)) StopLick();
        bool lickA = _lickOn && _lickTarget == "A";
        if (lickA && !_prevLickA) SayMaleEv(MaleAnusLickLines);
        _prevLickA = lickA;
        float d = 0f;
        if (lickA) d += AnusWetGainLick * (1f + sTongue);
        bool anusThrust = thrust && _jijiAnus;
        if (anusThrust)
        {
            if (SemenStock <= AnusSemenNoDropPct) d -= AnusWetDrainThrust;   // 精液量超過 30：不掉濕潤
        }
        else if (!lickA) d -= AnusWetDecay;
        _anusWet = Mathf.Clamp(_anusWet + d * dt, 0f, 100f);
        if (lickA) ComfortButt = Mathf.Min(100f, ComfortButt + TongueComfortRate * 0.5f * dt);

        _anusComfort = anusThrust && _anusWet >= AnusWetNeed;
        if (_anusComfort) ComfortButt = Mathf.Min(100f, ComfortButt + AnusComfortRate * dt);
    }
}
