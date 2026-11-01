using Godot;

/// <summary>
/// Ending9 場景：Day4(true end)
/// </summary>
public partial class Ending9Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/Day4(true end).json";

    protected override string PlayedKey => "Day4_true_end";

    // 對應圖鑒 E9（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 9;
}
