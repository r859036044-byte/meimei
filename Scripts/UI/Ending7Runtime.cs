using Godot;

/// <summary>
/// Ending7 場景：Day4(special end1)
/// </summary>
public partial class Ending7Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/Day4(special end1).json";

    protected override string PlayedKey => "Day4_special_end1";

    // 對應圖鑒 E7（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 7;
}
