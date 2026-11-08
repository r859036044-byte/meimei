using Godot;

/// <summary>
/// TrainingRoomRuntime — 滑鼠滾輪縮放鏡頭（方便仔細觀看）。
///
/// 操作：
///   • 滾輪往上＝放大、往下＝縮小，以「滑鼠所在位置」為中心縮放（最小 1 倍＝原畫面，最大 CamZoomMax 倍）
///   • 放大後按住「滑鼠中鍵」拖曳＝平移畫面
///   • 按 Home 鍵＝一鍵回到原本 1 倍
///
/// 做法：整個場景（背景、角色、熱點、特效）都在 _canvas（CanvasLayer）底下，直接改 _canvas 的
/// Transform 就能整體縮放；HUD／右側選單／底部按鈕列／對話框／淡入淡出這些 UI 在 Build 完之後
/// 搬到另一個 CanvasLayer（_uiCanvas），不會跟著縮放。
/// 滑鼠座標跟場景座標之間要換算時用 ScreenToWorld()（縮放後滑鼠座標 ≠ 場景內座標）。
/// </summary>
public partial class TrainingRoomRuntime
{
    [Export] public float CamZoomMax       = 3f;
    [Export] public float CamDefaultOffsetY = 40f;   // 預設鏡頭視角從置中位置再往上移的像素（越大越看不到下方）
    [Export] public float CamBottomMargin   = 15f;   // 底部保留不可移動的像素
    [Export] public float CamZoomStep   = 1.15f;   // 滾一格放大/縮小的倍數
    [Export] public float CamZoomSmooth = 14f;     // 縮放平滑速度（越大越快跟上）

    CanvasLayer _uiCanvas;
    float   _camZoom = 1.1f, _camZoomTarget = 1.1f;
    Vector2 _camOffset = Vector2.Zero;
    Vector2 _camAnchorScreen, _camAnchorWorld;
    bool    _camPanning;
    Vector2 _camPanLast;

    /// <summary>預設鏡頭位移：放大後先置中，再把視角往上移 CamDefaultOffsetY（只露出上方、藏住下方出血不足處）。</summary>
    Vector2 CamDefaultOffset()
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        return new Vector2(0f, -vp.Y * (_camZoom - 1f) * 0.5f + CamDefaultOffsetY);
    }

    /// <summary>UI（不縮放）要掛的地方。</summary>
    CanvasLayer UiRoot => _uiCanvas ?? _canvas;

    static readonly string[] _uiPanelNames = { "TopHUD", "StatsPanel", "VertBars", "StaminaPanel", "JijiGauge", "BtnEndure","BtnEjac", "BtnMouse", "BtnDrag", "BtnLeft", "BtnRight", "Btn2", "BtnTongue", "LeavePanel", "SidePanel" };   // StaminaPanel：精力獨立直條（不跟鏡頭縮放、F2 可拖）

    /// <summary>Build() 完成後呼叫：建立 UI 專用 CanvasLayer，把 HUD／選單搬過去。</summary>
    void SetupCameraLayers()
    {
        _uiCanvas = new CanvasLayer { Name = "UICanvas", Layer = 11 };
        AddChild(_uiCanvas);
        foreach (var n in _uiPanelNames)
        {
            var node = _canvas.GetNodeOrNull(n);
            if (node == null) continue;
            node.Reparent(_uiCanvas, true);
        }
    }

    [Export] public float MLegCamZoomMul = 1.3f;   // 進入 M 腿時鏡頭再拉近的倍率（1.3＝再放大 30%；原本 1.48）
    [Export] public float MLegCamUpPx = 120f;   // 進入 M 腿時視角再往上移的螢幕 px（胸部在畫面中會偏下）
    [Export] public float MLegCamSmooth = 1f;   // M 腿拉近／縮回的縮放速度（越小越慢；滾輪縮放仍用 CamZoomSmooth）
    float _camSmoothOverride = -1f;
    float _mLegPrevZoomTarget = 1.1f;
    float _mLegSetZoom = -1f;   // 進入 M 腿時鏡頭自動設定的倍率（離開時若玩家沒再用滾輪調過才縮回原倍率）

    /// <summary>進入 M 腿：鏡頭以胸部為中心拉近 20%；離開 M 腿：縮回進入前的倍率。</summary>
    void MLegCameraFocus(bool opening)
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        _camSmoothOverride = MLegCamSmooth;
        if (opening)
        {
            _mLegPrevZoomTarget = _camZoomTarget;
            // 胸部中心（場景座標）：胸1／胸2 圖層外框中心的平均
            Vector2 sum = Vector2.Zero; int n = 0;
            foreach (var name in new[] { "胸1", "胸2" })
                if (Layers.TryGetValue(name, out var c) && c != null)
                { sum += ScreenToWorld(c.GetGlobalRect().GetCenter()); n++; }
            Vector2 chestWorld = n > 0 ? sum / n : ScreenToWorld(vp * 0.5f);
            // 已經拉近到 M 腿該有的倍率以上就不再多拉（原本是直接乘上去，拉近時抬腳會變得更近、還卡在上限）
            float mLegZoom = Mathf.Clamp(1.1f * MLegCamZoomMul, 1.1f, Mathf.Max(1.1f, CamZoomMax));
            if (_camZoomTarget < mLegZoom) _camZoomTarget = mLegZoom;
            _mLegSetZoom = _camZoomTarget;
            // X 不位移：以目前畫面中心的 X 當錨點；Y 才對準胸部（畫面中央）
            float centerWorldX = (vp.X * 0.5f - _camOffset.X) / _camZoom;
            _camAnchorWorld  = new Vector2(centerWorldX, chestWorld.Y);
            _camAnchorScreen = vp * 0.5f + new Vector2(0f, MLegCamUpPx);   // 視角往上＝胸部在畫面中往下
        }
        else
        {
            // 玩家在 M 腿中用滾輪調過倍率就尊重他的設定，不強制縮回
            if (Mathf.IsEqualApprox(_camZoomTarget, _mLegSetZoom)) _camZoomTarget = _mLegPrevZoomTarget;
            _mLegSetZoom = -1f;
            _camAnchorScreen = vp * 0.5f;
            _camAnchorWorld  = (vp * 0.5f - _camOffset) / _camZoom;   // 以目前畫面中心為中心縮回
        }
    }

    /// <summary>螢幕（視窗）座標 → 場景（_canvas 內）座標。</summary>
    public Vector2 ScreenToWorld(Vector2 screen) =>
        _canvas == null ? screen : _canvas.Transform.AffineInverse() * screen;

    bool IsPointerOverUi()
    {
        var hov = GetViewport().GuiGetHoveredControl();
        if (hov == null) return false;
        Node p = hov;
        while (p != null && p is not CanvasLayer) p = p.GetParent();
        return p != null && p != _canvas;   // 滑鼠在 UI 層 / DEV 面板上 → 不縮放
    }

    /// <summary>由 _Input 呼叫；有處理就回傳 true。</summary>
    bool HandleCameraInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            if (mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            {
                if (IsPointerOverUi()) return false;
                float f = mb.ButtonIndex == MouseButton.WheelUp ? CamZoomStep : 1f / CamZoomStep;
                float minZ = 1.1f;   // M 腿中滾輪也可以自由縮放
                _camZoomTarget = Mathf.Clamp(_camZoomTarget * f, minZ, Mathf.Max(minZ, CamZoomMax));
                _camSmoothOverride = -1f;
                _camAnchorScreen = mb.Position;
                _camAnchorWorld  = (mb.Position - _camOffset) / _camZoom;
                return true;
            }
            if (mb.ButtonIndex == MouseButton.Right && !mb.Pressed && _camPanning)
            {
                _camPanning = false;   // 右鍵拖動畫面放開（右鍵按下開始拖動在 Controls.cs HandleRightClick）
                return true;
            }
            if (mb.ButtonIndex == MouseButton.Middle)
            {
                _camPanning = mb.Pressed && _camZoomTarget > 1.101f;
                _camPanLast = mb.Position;
                return _camPanning;
            }
        }
        else if (e is InputEventMouseMotion mm && _camPanning)
        {
            _camOffset += mm.Position - _camPanLast;
            _camPanLast = mm.Position;
            // 平移後錨點要重算，不然下一幀縮放動畫會把畫面拉回去
            _camAnchorScreen = mm.Position;
            _camAnchorWorld  = (mm.Position - _camOffset) / _camZoom;
            return true;
        }
        else if (e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Home)
        {
            _camZoomTarget = 1.1f;
            _camOffset = CamDefaultOffset();   // Home 鍵重置也回到預設鏡頭位置
            return true;
        }
        return false;
    }

    /// <summary>每幀：平滑縮放＋限制平移範圍（畫面不會露出邊界外），套到 _canvas.Transform。</summary>
    void UpdateCamera(float dt)
    {
        if (_canvas == null) return;
        if (!Mathf.IsEqualApprox(_camZoom, _camZoomTarget))
        {
            _camZoom = Mathf.Lerp(_camZoom, _camZoomTarget, 1f - Mathf.Exp(-(_camSmoothOverride > 0f ? _camSmoothOverride : CamZoomSmooth) * dt));
            if (Mathf.Abs(_camZoom - _camZoomTarget) < 0.001f) _camZoom = _camZoomTarget;
            // 讓錨點（滑鼠當時指著的場景點）維持在滑鼠下
            _camOffset = _camAnchorScreen - _camAnchorWorld * _camZoom;
        }
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        // 背景實際範圍（場景座標）：BG 用中心錨點，Y 在 ApplyPointAnchor 內會取負，這裡要同步換算。
        // 鏡頭不能移到 BG 範圍外，否則會露出底下的空白／第二張背景。每幀重算，Inspector 即時調整有效。
        float bgL = vp.X * 0.5f + BackgroundOffset.X - BackgroundSize.X * 0.5f;
        float bgR = vp.X * 0.5f + BackgroundOffset.X + BackgroundSize.X * 0.5f;
        float bgT = vp.Y * 0.5f - BackgroundOffset.Y - BackgroundSize.Y * 0.5f;
        float bgB = vp.Y * 0.5f - BackgroundOffset.Y + BackgroundSize.Y * 0.5f;

        float loX = Mathf.Max(vp.X - vp.X * _camZoom, vp.X - bgR * _camZoom);
        float hiX = Mathf.Min(0f, -bgL * _camZoom);
        if (loX > hiX) hiX = loX;   // 背景放不下時以「不露出背景外」優先
        _camOffset.X = Mathf.Clamp(_camOffset.X, loX, hiX);

        float loY = Mathf.Max(vp.Y - vp.Y * _camZoom + CamBottomMargin, vp.Y - bgB * _camZoom);
        float hiY = Mathf.Min(0f, -bgT * _camZoom);
        if (loY > hiY) hiY = loY;
        _camOffset.Y = Mathf.Clamp(_camOffset.Y, loY, hiY);
        _canvas.Transform = new Transform2D(0f, new Vector2(_camZoom, _camZoom), 0f, _camOffset);
        RefreshHoverCursorForZoom();   // 綠點的半透明手游標跟著縮放變大（Interaction.cs）
    }
}
