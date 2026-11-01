using Godot;

/// <summary>
/// Ending13 場景：沉淪的課後輔導（插入線 傍晚插入）
/// 對應圖鑒 E13，對話路線節點 N076。
/// </summary>
public partial class Ending13Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/N076.json";

    protected override string PlayedKey => "E13_N076";

    // 對應圖鑒 E13（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 13;
}
