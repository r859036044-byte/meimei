using Godot;

/// <summary>
/// Ending18 場景：沉迷於主從遊戲的兄妹（插入高潮線 傍晚分歧A）
/// 對應圖鑒 E18，對話路線節點 N088。
/// </summary>
public partial class Ending18Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/N088.json";

    protected override string PlayedKey => "E18_N088";

    // 對應圖鑒 E18（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 18;
}
