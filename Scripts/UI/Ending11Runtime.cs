using Godot;

/// <summary>
/// Ending11 場景：淪為妹妹的專屬玩具（B線 插入高潮）
/// 對應圖鑒 E11，對話路線節點 N056。
/// </summary>
public partial class Ending11Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/N056.json";

    protected override string PlayedKey => "E11_N056";

    // 對應圖鑒 E11（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 11;
}
