using Godot;

public partial class Ending6Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/Day3(good end - sofa).json";
    protected override string PlayedKey => "Day3_GoodEnd_Sofa";

    // 對應圖鑒 E10（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 10;

    protected override string ResolveBackgroundName(string name)
        => name == "bg_LivingRoom" ? "bg_LivingRoomSofa_day_room" : name;

    protected override string ForegroundForBackground(string name)
        => name == "bg_LivingRoom" ? "bg_LivingRoomSofa_day_sofa" : null;
}
