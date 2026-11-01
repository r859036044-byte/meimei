using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 「射後不理」async Task 的安全包裝。
///
/// 背景：C# 的 async Task 方法如果沒有被 await（也就是像 `_ = SomeAsync();` 這種寫法），
/// 內部拋出的例外不會顯示在任何地方——不會出現在「輸出」面板、也不會讓遊戲當掉，
/// 該 Task 只會在拋例外的那一行悄悄停止執行，後面的程式碼全部不會跑。
/// 對話流程（進場自動播放的那些 PlayDialogueAsync/StartAsync/FadeInAsync…）幾乎全部
/// 是這種寫法，所以只要中途某一行因為缺資源、空物件等原因丟例外，畫面就會卡死在
/// 當下那一刻（常見就是卡在黑屏，因為 fadeoutBlack 執行完、換場的下一步例外了）
/// ——而且完全不會留下任何錯誤訊息，非常難抓。
///
/// 用法：把 `_ = SomeAsync();` 改成 `TaskLogger.Run(SomeAsync(), "識別用的名稱");`
/// 一旦裡面真的丟例外，會在「輸出」面板印出完整的例外訊息和堆疊追蹤，附上你給的名稱，
/// 方便直接定位是哪一段流程、哪一行出的錯。
/// </summary>
public static class TaskLogger
{
    public static async void Run(Task task, string label)
    {
        try
        {
            await task;
        }
        catch (Exception e)
        {
            GD.PrintErr($"[TaskLogger] 「{label}」發生未處理的例外，流程已中斷：\n{e}");
        }
    }
}
