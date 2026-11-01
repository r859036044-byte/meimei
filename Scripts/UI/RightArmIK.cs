using Godot;

/// <summary>
/// 右手版 ArmIK（Unity RightArmIK 的 Godot 移植版）
/// 繼承 ArmIK 的全部邏輯，預設啟用 MirrorX（X 軸翻轉），
/// 讓右手臂的拖動/旋轉/吸附行為與左手完全對稱。
/// </summary>
public partial class RightArmIK : ArmIK
{
    // Unity 版的 MirrorX = true 只寫在 Reset()（元件剛掛上時的預設值），
    // 場景檔 fileID 903285455 實際序列化的是 MirrorX: 0。原本在 _Ready 強制設 true
    // 會蓋掉 BuildRightArmIK() 給的 false，讓右手拖動旋轉全部反向，已移除。
}
