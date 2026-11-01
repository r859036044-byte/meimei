using Godot;

/// <summary>
/// Ending5 場景：錯失的信號（摸兩天，客廳浴後選擇放棄）
/// 莉奈收起期待，曖昧就此熄滅。
/// </summary>
public partial class Ending5Runtime : EndingBaseRuntime
{
    protected override string DialoguePath
        => "res://Resources/Data/Dialogues/Endings/Day3(normal end - missed signal).json";

    protected override string PlayedKey => "Day3_NormalEnd_MissedSignal";

    // 對應圖鑒 E5（見 GalleryScreen.EndingSceneFile 對照表）
    protected override int GalleryEndingNumber => 5;
}
