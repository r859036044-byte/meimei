using Godot;

/// <summary>
/// Ending4 場景：Day2_Start(bad end)
/// </summary>
public partial class Ending4Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/Day2_Start(bad end).json";

    protected override string PlayedKey => "Day2_Start_bad_end";

    // 對應圖鑒 E4（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 4;
    protected override int AAEndingNumber => 7;   // 全年齡結局7「斷線的風箏」
}
