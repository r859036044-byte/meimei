using Godot;

/// <summary>
/// IdleStateController（Unity 版移植）
/// 管理角色的普通待機狀態（待機1、2、3）。
/// 待機1：啟用眼睛追蹤（EyeFollowController）。
/// 待機2/3：預留，可日後擴充。
/// </summary>
public partial class IdleStateController : Node
{
    [Export] public IdleStateType DefaultState = IdleStateType.Idle1;
    [Export] public NodePath EyeFollowPath;

    public IdleStateType CurrentState => _current;

    private IdleStateType       _current   = IdleStateType.None;
    private EyeFollowController _eyeFollow;

    public override void _Ready()
    {
        if (_eyeFollow == null && EyeFollowPath != null && !EyeFollowPath.IsEmpty)
            _eyeFollow = GetNodeOrNull<EyeFollowController>(EyeFollowPath);
        SetState(DefaultState);
    }

    /// <summary>供程式建構時直接指定 EyeFollowController（節點是純程式建立，NodePath 無法解析，
    /// 需在 AddChild/_Ready 之前呼叫此方法直接注入）。</summary>
    public void ConfigureEyeFollow(EyeFollowController eyeFollow)
    {
        _eyeFollow = eyeFollow;
        if (IsInsideTree())
            ApplyState();
    }

    public void SetState(IdleStateType state)
    {
        _current = state;
        ApplyState();
    }

    public void SetState(int index) => SetState((IdleStateType)index);

    private void ApplyState()
    {
        if (_eyeFollow == null) return;

        bool active = _current == IdleStateType.Idle1;
        if (!active)
        {
            // 對應 Unity 版 EyeFollowController.enabled = false 觸發的 OnDisable() 歸位行為，
            // Godot 的 SetProcess(false) 沒有這個副作用，必須手動呼叫，避免眼睛卡在偏移位置
            _eyeFollow.ResetToCenter();
        }
        _eyeFollow.SetProcess(active);
    }
}

public enum IdleStateType
{
    None  = 0,
    Idle1 = 1,
    Idle2 = 2,
    Idle3 = 3,
}
