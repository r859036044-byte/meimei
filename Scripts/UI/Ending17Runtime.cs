using Godot;

/// <summary>
/// Ending17 場景：被榨取的雜魚玩具（插入高潮線 傍晚分歧B）
/// 對應圖鑒 E17，對話路線節點 N089。
/// </summary>
public partial class Ending17Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/N089.json";

    protected override string PlayedKey => "E17_N089";

    // 對應圖鑒 E17（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 17;
}
