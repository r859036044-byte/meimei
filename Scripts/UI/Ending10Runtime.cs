using Godot;

/// <summary>
/// Ending10 場景：斷線的風箏（Nothing線 插入後）
/// 對應圖鑒 E1，對話路線節點 N024。
/// </summary>
public partial class Ending10Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/N024.json";

    protected override string PlayedKey => "E1_N024";

    // 對應圖鑒 E1（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 1;
}
