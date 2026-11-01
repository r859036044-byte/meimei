/// <summary>
/// 開新遊戲時把所有遊玩進度歸零（主選單「觀看劇情」「直接開始」都會呼叫）。
///
/// 背景：很多進度是存在 static 變數或常駐節點裡，切換場景不會清掉。
/// 通關一次再開新遊戲時，「已播過 PrologueLast」「第幾晚」「摸過胸」「劇情變數」等
/// 都還留著上一輪的值，劇情就會被跳過或走錯分歧。
/// 以後新增會跨場景保存的進度，也要加到這裡。
/// （不動的：設定、全年齡/R18 開關、圖鑒解鎖紀錄、自動播放倍速）
/// </summary>
public static class NewGameReset
{
    public static void ResetAll()
    {
        PlayerDataManager.Instance?.ResetForNewGame();

        LivingRoomRuntime.ResetForNewGame();
        ApartmentRuntime.ResetForNewGame();
        BathroomRuntime.ResetForNewGame();
        EndingBaseRuntime.ResetForNewGame();
        EndingBaseRuntime.FromGallery = false;
        Ending2Runtime.UseNothingRoute = false;

        TrainingRoomRuntime.PendingDay2First    = false;
        TrainingRoomRuntime.PendingDay3First    = false;
        TrainingRoomRuntime.PendingNight3       = false;
        TrainingRoomRuntime.PendingMiniGameOnly = false;
        TrainingRoomRuntime.HadBreastTouch      = false;
        TrainingRoomRuntime.BreastComfortRoute  = true;

        // 對話歷史清空、跳過／自動模式回到手動（避免上一輪的「跳過」狀態帶進新遊戲）
        NovelReader.ClearHistory();

        Godot.GD.Print("[NewGame] 所有遊玩進度已重置");
    }
}
