using Godot;

/// <summary>
/// 試玩版總開關。
///
/// 開啟時：
///   1. 主選單：不播 LOGO／OP 開場動畫，直接停在標題畫面（等同 SkipIntroOnce）；
///      不預載（不讀調教室貼圖、不載入整個標題圖資料夾），標題圖不漸入。
///   2. 按「開始遊戲」：不跳「要不要看序章」確認，也不播序章劇情，直接進 TrainingRoom。
///   3. 使用 DL 版圖片（DlMode.Enabled）：雞雞／開／插入／洞／M 腿小穴 載入 12px 馬賽克版，原圖保留。
///   4. 雞雞要先脫掉內褲才叫得出來；沒脫的話男主會說「先把內褲脫掉吧，不然插不進去」。
///   5. TrainingRoom 衣服限制：
///        可以：調整衣服下面（衣擺往上拉、中間區復位）、把內褲往下拉脫掉（含床上的內褲穿回去）。
///        不行：肩帶／衣服上半部調整、內褲「拉開」（一般狀態的拉開與 M 腿撥開）。
///
/// 開關方式（任選一種）：
///   - 改下面的 DefaultEnabled（true／false）。
///   - 匯出時在 Export Preset → Features → Custom 加上自訂 feature tag「demo」，
///     不用改程式碼就能出一份試玩版（DefaultEnabled 維持 false 即可）。
/// </summary>
public static class DemoMode
{
    /// <summary>試玩版預設開關：true＝開啟。</summary>
    public const bool DefaultEnabled = true;

    static bool? _cached;

    public static bool Enabled => _cached ??= DefaultEnabled || OS.HasFeature("demo");
}
