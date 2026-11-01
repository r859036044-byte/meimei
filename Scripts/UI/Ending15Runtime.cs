using Godot;

/// <summary>
/// Ending15 場景：笨拙的代價（高潮線 浴室邀請沒高潮）
/// 對應圖鑒 E15，對話路線節點 N084。
/// </summary>
public partial class Ending15Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/N084.json";

    protected override string PlayedKey => "E15_N084";

    // 對應圖鑒 E15（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 15;
}
