using Godot;

/// <summary>
/// 在調教室裡切換全年齡／R18：先問「確定？」，確定後把今天的進度還原成進房間時的樣子，換版本後重新載入調教室（等於重新開始這一天）。
/// 進房間時的狀態（劇情變數、第幾晚、待播旗標）在 ConsumePendingFlags 開頭記下來（SnapshotDayStart）。
/// </summary>
public partial class TrainingRoomRuntime
{
    public static TrainingRoomRuntime Current { get; private set; }

    System.Collections.Generic.Dictionary<string, int> _dayVars;
    int _dayNight, _dayIdleNights;
    bool _dayD2, _dayD3, _dayN3, _dayMini;
    ConfirmationDialog _ageDlg;

    void SnapshotDayStart()
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm == null) return;
        _dayVars = pdm.GetAllDialogueVars();
        _dayNight = pdm.CurrentNight; _dayIdleNights = pdm.IdleNightCount;
        _dayD2 = PendingDay2First; _dayD3 = PendingDay3First; _dayN3 = PendingNight3; _dayMini = PendingMiniGameOnly;
    }

    /// <summary>設定視窗裡切換全年齡／R18 時呼叫。toR18＝要切成 R18。</summary>
    public void RequestAgeSwitch(bool toR18)
    {
        if (_endingDay) return;
        _ageDlg?.QueueFree();
        _ageDlg = new ConfirmationDialog
        {
            Title = "切換版本",
            DialogText = (toR18 ? "要切換成 R18 版嗎？" : "要切換成全年齡版嗎？") + "\n切換會重新開始這一天（今天在房間裡的進度不會保留）。\n確定要切換嗎？",
            OkButtonText = "確定切換", CancelButtonText = "取消",
        };
        _ageDlg.Confirmed += () => RestartDayWithAge(toR18);
        AddChild(_ageDlg);
        _ageDlg.PopupCentered();
    }

    void RestartDayWithAge(bool toR18)
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm == null || _dayVars == null) return;
        if (_tutActive) TutorialEnd();
        pdm.SetAllDialogueVars(_dayVars);
        pdm.CurrentNight = _dayNight; pdm.IdleNightCount = _dayIdleNights;
        pdm.ResetTodayTracking();
        PendingDay2First = _dayD2; PendingDay3First = _dayD3; PendingNight3 = _dayN3; PendingMiniGameOnly = _dayMini;
        pdm.SetR18(toR18);
        SettingsManager.Instance?.Save();
        GD.Print($"[AgeSwitch] 切換成{(toR18 ? "R18" : "全年齡")}，重新開始這一天");
        GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
    }
}
