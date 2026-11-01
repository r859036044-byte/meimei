using Godot;

/// <summary>
/// Ending1 場景：Day3(bad end)
/// </summary>
public partial class Ending1Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/Day3(bad end).json";

    protected override string PlayedKey => "Day3_bad_end";

    // 對應圖鑒 E2（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 2;
}
