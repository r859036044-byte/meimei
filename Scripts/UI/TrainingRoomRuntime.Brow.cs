using Godot;

/// <summary>
/// 眉毛：男主做任何操作（按住滑鼠、雞雞出現／插入中、舔、手摸 B）時，眉毛變成上下反轉（皺眉）；
/// 操作停下來一小段時間後回到初始。形態本體在 FaceExpressionController.SetBrowForm。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("眉毛")]
    [Export] public bool  BrowFrownOnOperate = true;   // 男主操作時眉毛上下反轉
    [Export] public float BrowFrownLingerSec = 0.8f;   // 操作停止後多久才恢復（避免單擊時一閃一閃）
    float _browOperateT;

    void UpdateBrowOperate(float dt)
    {
        if (FaceController == null || !FaceController.IsReady) return;

        bool operating = BrowFrownOnOperate && (
            Input.IsMouseButtonPressed(MouseButton.Left) ||
            Input.IsMouseButtonPressed(MouseButton.Right) ||
            Input.IsMouseButtonPressed(MouseButton.Middle) ||
            _jijiPhase != 0 || _lickOn || _bTouchRunning);

        _browOperateT = operating ? BrowFrownLingerSec : Mathf.Max(0f, _browOperateT - dt);
        FaceController.SetBrowForm(_browOperateT > 0f
            ? FaceExpressionController.BrowForm.Frown
            : FaceExpressionController.BrowForm.Normal);
    }
}
