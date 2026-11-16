using Godot;

/// <summary>
/// 全年齡：莉奈主動說話滿 AAMaxTalks 次 → 她累了，提示後自動離開房間（走 EndToday，不扣心情）。
/// 沒滿就自己按「離開房間」→ 心情大幅下降（做得越少扣越多），並顯示謎語人式提示。R18 不套用。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("對話次數上限（全年齡）")]
    [Export] public int   AAMaxTalks = 8;                 // 莉奈主動說話幾次後強制結束
    [Export] public float AAEarlyLeaveMoodDown = 40f;     // 一句都沒互動就離開：心情扣多少（做得越多扣越少：× (1 − 次數/上限)）
    [Export] public float AATalkEndNoticeSec = 3.5f;      // 提示停留秒數
    [Export] public string AATalkEndNotice = "莉奈好像累了…今天就到這裡吧。";
    [Export] public string AAEarlyLeaveNotice = "（現實裡，我和莉奈的關係，或許不會因此好轉……）";

    int  _aaTalkCount;
    bool _aaAutoLeaving;

    /// <summary>莉奈每主動說一句呼叫（BratProcess）。</summary>
    void AATalkCounted() { if (!BratR18) _aaTalkCount++; }

    /// <summary>每次一輪對話結算完呼叫（BratResolve）：滿了就收工。</summary>
    void AACheckTalkLimit()
    {
        if (BratR18 || _aaAutoLeaving || _endingDay || _aaTalkCount < AAMaxTalks) return;
        _aaAutoLeaving = true;
        TaskLogger.Run(AAAutoLeaveAsync(), "TrainingRoom.AAAutoLeave");
    }

    async System.Threading.Tasks.Task AAAutoLeaveAsync()
    {
        AAShowNotice(AATalkEndNotice, AATalkEndNoticeSec);
        await ToSignal(GetTree().CreateTimer(AATalkEndNoticeSec), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this)) return;
        BratSave();
        EndToday();
        if (!_endingDay) _aaAutoLeaving = false;   // 被擋下（例如還沒擦乾淨）：之後可再試
    }

    /// <summary>玩家自己按離開／結束今晚。</summary>
    void LeaveByPlayer()
    {
        if (_endingDay || _aaAutoLeaving || _timingChallengeActive || _tutActive) return;
        if (!BratR18 && BratEnabled && _aaTalkCount > 0 && _aaTalkCount < AAMaxTalks && TissueAllowLeave())
        {
            float loss = AAEarlyLeaveMoodDown * (1f - (float)_aaTalkCount / AAMaxTalks);
            Mood = Mathf.Max(1f, Mood - loss);   // 不會因為離開直接觸發心情 0 結局
            BratSave();
            GD.Print($"[Brat] 主動離開（只互動 {_aaTalkCount}/{AAMaxTalks} 次）→ 心情 -{loss:F0}");
            AAShowNotice(AAEarlyLeaveNotice, 2.5f);
            _aaAutoLeaving = true;
            TaskLogger.Run(AALeaveDelayed(2.5f), "TrainingRoom.AALeaveDelayed");
            return;
        }
        EndToday();
    }

    async System.Threading.Tasks.Task AALeaveDelayed(float sec)
    {
        await ToSignal(GetTree().CreateTimer(sec), SceneTreeTimer.SignalName.Timeout);
        if (!IsInstanceValid(this)) return;
        EndToday();
        if (!_endingDay) _aaAutoLeaving = false;
    }

    /// <summary>用男主的對話框說這句提示（跟平常的男主台詞同一個氣泡）。</summary>
    void AAShowNotice(string text, float sec)
    {
        _dlgLockEnd = 0;   // 不被別的台詞的對話鎖擋住
        ShowBubble(ref _bubblePlayer, ref _bubblePlayerLbl, text, true);
        if (_bubblePlayer != null && IsInstanceValid(_bubblePlayer)) _bubblePlayer.SetMeta("hide_t", sec);
    }
}
