using Godot;

/// <summary>
/// Ending14 場景：浴室裡的沉淪（高潮線 浴室邀請高潮）
/// 對應圖鑒 E14，對話路線節點 N085。
/// </summary>
public partial class Ending14Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/N085.json";

    protected override string PlayedKey => "E14_N085";

    // 對應圖鑒 E14（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 14;
}
