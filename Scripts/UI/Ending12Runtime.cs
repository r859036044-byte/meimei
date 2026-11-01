using Godot;

/// <summary>
/// Ending12 場景：粗暴的代價（插入線 傍晚沒插）
/// 對應圖鑒 E12，對話路線節點 N077。
/// </summary>
public partial class Ending12Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/N077.json";

    protected override string PlayedKey => "E12_N077";

    // 對應圖鑒 E12（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 12;
}
