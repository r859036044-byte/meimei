using Godot;

/// <summary>
/// TrainingRoomRuntime — 左下角男主 HUD 的顯示條件（只是「顯示／不顯示」，節點與數值邏輯都還在，沒有刪）：
///   • 黃色「勃起度」直條（VertBars）：一律不顯示（HideErectBar）。勃起度照常計算，只是不畫出來。
///   • 🍆 按鈕、綠色「精力」直條：平常不顯示，男主說硬了（勃起，_erectOn）之後才淡入出現；
///     雞雞已經出現（_jijiPhase != 0）時也保持顯示，避免用到一半突然不見。軟掉（雞雞收起來）後再淡出。
///   • 綠色精力條縮短：高度＝按鈕條的 StaminaBarHeightFrac 倍（見 BuildUI.cs BuildBottomButtons）。
/// 全部可在 Inspector「男主 HUD 顯示」群組調整。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("男主 HUD 顯示")]
    [Export] public bool  HideErectBar     = true;    // 黃色勃起條：不顯示
    [Export] public bool  GateJijiHud      = true;    // 🍆 按鈕與精力條：要男主說硬了才顯示（關掉＝一直顯示）
    [Export(PropertyHint.Range, "0.2,1,0.05")] public float StaminaBarHeightFrac = 0.5f;   // 綠色精力條高度（相對按鈕條；只在建立 UI 時套用）
    [Export] public float HudGateFadeSec   = 0.3f;    // 淡入／淡出秒數

    Button  _btnJijiNode;
    Control _staminaPanel, _vertBarsPanel;
    float   _hudGateA = 0f;

    void UpdateHudGate(float dt)
    {
        if (_vertBarsPanel != null && IsInstanceValid(_vertBarsPanel)) _vertBarsPanel.Visible = !HideErectBar;

        bool show = !GateJijiHud || _erectOn || _jijiPhase != 0;
        float step = dt / Mathf.Max(0.01f, HudGateFadeSec);
        _hudGateA = Mathf.MoveToward(_hudGateA, show ? 1f : 0f, step);
        var col = new Color(1f, 1f, 1f, _hudGateA);
        bool vis = _hudGateA > 0.01f;

        if (_btnJijiNode != null && IsInstanceValid(_btnJijiNode)) { _btnJijiNode.Visible = vis; _btnJijiNode.Modulate = col; }
        if (_staminaPanel != null && IsInstanceValid(_staminaPanel)) { _staminaPanel.Visible = vis; _staminaPanel.Modulate = col; }
    }
}
