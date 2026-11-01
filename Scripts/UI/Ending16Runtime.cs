using Godot;

/// <summary>
/// Ending16 場景：笨拙的做愛（高潮線 浴室邀請高潮後）
/// 對應圖鑒 E16，對話路線節點 N086。
/// </summary>
public partial class Ending16Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/N086.json";

    protected override string PlayedKey => "E16_N086";

    // 對應圖鑒 E16（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 16;
}
