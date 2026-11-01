using Godot;

/// <summary>
/// Ending8 場景：Day4(special end2)
/// </summary>
public partial class Ending8Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/Day4(special end2).json";

    protected override string PlayedKey => "Day4_special_end2";

    // 對應圖鑒 E8（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 8;
}
