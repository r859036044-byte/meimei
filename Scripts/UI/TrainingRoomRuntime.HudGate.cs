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
    [Export(PropertyHint.Range, "0.3,1,0.01")] public float JijiGaugeHeightFrac = 0.93f;   // 雞雞造型按鈕高度（相對原本直條；0.93＝比原本 0.62 放大 50%；只在建立 UI 時套用）
    [Export] public bool  UseJijiGauge     = true;    // 用程式畫的雞雞造型按鈕（取代 🍆 按鈕與獨立精力條）；關掉＝回到 🍆 按鈕＋精力條

    Button  _btnJijiNode;
    Control _staminaPanel, _vertBarsPanel;
    JijiGauge _jijiGauge;   // 程式畫的雞雞造型按鈕（JijiGauge.cs），建立在 BuildUI.cs
    float   _hudGateA = 0f;

    /// <summary>保險：不是 M 腿、也不在 M 腿切換中，「m腿」那一組圖層（腿底、小穴_M、腿1_M…）一律藏起來。
    /// 腿底是一大塊平塗的粉色底，如果因為任何原因在一般腿時被打開，就會變成小穴下面一塊粉膚色。</summary>
    void EnforceMLegLayersHidden()
    {
        // 腿底跟 M 腿「完全同步」：M 腿開著（不在切換中）就一定顯示，M 腿關掉就一定不顯示
        if (!_mLegTransitioning && Layers.TryGetValue("m腿/腿底", out var floorBase) && floorBase != null && floorBase.Visible != _mLegOpen)
        {
            floorBase.Visible = _mLegOpen;
            if (_mLegOpen) floorBase.Modulate = new Color(1f, 1f, 1f, 1f);
            GD.Print($"[MLegGuard] 腿底跟 M 腿同步 → {(_mLegOpen ? "顯示" : "隱藏")}（jijiPhase={_jijiPhase} holeLeftOpen={_holeLeftOpen} bTouch={_bTouchRunning}）");
        }
        if (_mLegOpen || _mLegTransitioning) return;
        // 洞／開1／開上／插入／肚子：M 腿專用的疊層（名字不是 m腿 開頭，上面的迴圈抓不到），腿放下時一律藏起來
        foreach (var n in new[] { "洞", "開1", "開上", "插入", "肚子" })
            if (Layers.TryGetValue(n, out var ov) && ov != null && ov.Visible)
            {
                ov.Visible = false; ov.Scale = Vector2.One;
                GD.Print($"[MLegGuard] 非 M 腿卻開著 {n} → 已藏起來（jijiPhase={_jijiPhase} holeLeftOpen={_holeLeftOpen} bTouch={_bTouchRunning}）");
            }
        foreach (var kv in Layers)
            if (kv.Key.StartsWith("m腿") && kv.Value != null && kv.Value.Visible)
            {
                kv.Value.Visible = false;
                // 找出是誰把它打開的：把這行（輸出面板）貼給我
                GD.Print($"[MLegGuard] 非 M 腿卻開著 {kv.Key} → 已藏起來（jijiPhase={_jijiPhase} holeLeftOpen={_holeLeftOpen} bTouch={_bTouchRunning} mLegOpen={_mLegOpen}）");
            }
    }

    void UpdateHudGate(float dt)
    {
        EnforceMLegLayersHidden();
        if (AllAgesLocked) HideAllAgesBottomUi();
        UpdateBreakChoices();   // 破功結束就收掉「慢下來／射進去」選項（BreakChoice.cs）
        if (_vertBarsPanel != null && IsInstanceValid(_vertBarsPanel)) _vertBarsPanel.Visible = !HideErectBar;

        bool show = (!GateJijiHud || _erectOn || _jijiPhase != 0) && !AllAgesLocked;   // 全年齡：左下角的雞雞按鈕、精力條、忍耐、射精都不出現
        float step = dt / Mathf.Max(0.01f, HudGateFadeSec);
        _hudGateA = Mathf.MoveToward(_hudGateA, show ? 1f : 0f, step);
        var col = new Color(1f, 1f, 1f, _hudGateA);
        bool vis = _hudGateA > 0.01f;

        // 舊的 🍆 按鈕和獨立精力條：被雞雞造型按鈕取代，一律不顯示（只隱藏，節點還在）
        if (_btnJijiNode != null && IsInstanceValid(_btnJijiNode)) _btnJijiNode.Visible = !UseJijiGauge && vis;
        if (_staminaPanel != null && IsInstanceValid(_staminaPanel)) _staminaPanel.Visible = !UseJijiGauge && vis;
        if (!UseJijiGauge) { if (_btnJijiNode != null && IsInstanceValid(_btnJijiNode)) _btnJijiNode.Modulate = col; if (_staminaPanel != null && IsInstanceValid(_staminaPanel)) _staminaPanel.Modulate = col; }

        // 忍耐按鈕（在雞雞按鈕旁邊）：跟雞雞按鈕一起，硬了才出現
        if (_endureBtn != null && IsInstanceValid(_endureBtn)) _endureBtn.Visible = vis;
        if (_ejacBtn != null && IsInstanceValid(_ejacBtn)) _ejacBtn.Visible = vis;

        // 雞雞造型按鈕：硬了才出現；叫出雞雞後裡面顯示綠色精力條
        if (_jijiGauge != null && IsInstanceValid(_jijiGauge))
        {
            _jijiGauge.Visible = UseJijiGauge && vis;
            _jijiGauge.Modulate = col;
            _jijiGauge.ShowGauge = _jijiPhase != 0;
            _jijiGauge.Value = Mathf.Clamp(Stamina / 100f, 0f, 1f);   // 溫度計滿格＝精力 100（破功線）
            _jijiGauge.Full = _breaking || Stamina >= 100f;           // 滿了：放大並震動，這段時間可以按忍耐回去
            _jijiGauge.Urgency = BreakProgress;                       // 破功倒數越接近射，放大和震動越強
        }
    }

    // 全年齡：左下角那排工具按鈕（左手／右手／雞雞／舌頭／衣服模式／滑鼠）全部不出現（右上「?」和離開房間不動）
    static readonly string[] _allAgesHiddenUi = { "BtnLeft", "BtnRight", "Btn2", "BtnTongue", "BtnDrag", "BtnMouse", "JijiGauge", "StaminaPanel", "VertBars", "BtnEndure", "BtnEjac" };
    void HideAllAgesBottomUi()
    {
        foreach (var n in _allAgesHiddenUi)
        {
            var c = UiRoot?.GetNodeOrNull<Control>(n);
            if (c != null && c.Visible) c.Visible = false;
        }
    }
}
