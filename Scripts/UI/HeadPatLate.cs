using Godot;

/// <summary>摸頭用：排在所有節點之後處理（見 TrainingRoomRuntime.HeadPat.cs）。</summary>
public partial class HeadPatLate : Node
{
    public TrainingRoomRuntime Runtime;
    public override void _Process(double delta) => Runtime?.ApplyHeadPat((float)delta);
}
