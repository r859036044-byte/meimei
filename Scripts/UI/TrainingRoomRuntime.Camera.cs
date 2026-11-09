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
    [Export(PropertyHint.Range, "1.1,3,0.01")] public float MLegCamMinZoom = 2.0f;   // 莉奈抬腳（M 腿）時，鏡頭「拉遠」的極限倍率：滾輪縮小（拉遠）不能小於這個，抬腳瞬間鏡頭也會先拉近到至少這個倍率（一般狀態下限是 1.1，最近是 CamZoomMax）。數字越大＝能拉遠的範圍越小；要微調就改這個；＝1.1 就等於沒有限制
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
            float mLegZoom = Mathf.Clamp(Mathf.Max(1.1f * MLegCamZoomMul, MLegCamMinZoom), 1.1f, Mathf.Max(1.1f, CamZoomMax));   // 抬腳拉近的倍率至少要到「拉遠極限」，不然一抬腳就在極限以外
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

    /// <summary>目前滾輪縮小鏡頭的下限：莉奈抬腳（M 腿開著或切換中）＝MLegCamMinZoom（可以縮小但有限制），否則 1.1。</summary>
    float MLegMinZoomNow() => (_mLegOpen || _mLegTransitioning) ? Mathf.Clamp(MLegCamMinZoom, 1.1f, Mathf.Max(1.1f, CamZoomMax)) : 1.1f;

    /// <summary>由 _Input 呼叫；有處理就回傳 true。</summary>
    bool HandleCameraInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb)
        {
            if (mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            {
                if (IsPointerOverUi()) return false;
                float f = mb.ButtonIndex == MouseButton.WheelUp ? CamZoomStep : 1f / CamZoomStep;
                float minZ = MLegMinZoomNow();   // 抬腳（M 腿）時縮小有下限（MLegCamMinZoom），其他時候 1.1
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
            _camZoomTarget = MLegMinZoomNow();   // 抬腳中回到「縮到最小」也要守下限
            _camOffset = CamDefaultOffset();   // Home 鍵重置也回到預設鏡頭位置
            return true;
        }
        return false;
    }

    // ── 男人身體下緣護欄（遊玩用）─────────────────────────────────────────────────
    // 男人肚子圖層的下緣是一條平邊，下面是空的；身體往上（插入、抽插）時這條邊會進到畫面，看起來就是一大塊空隙。
    // 所以鏡頭不能往下拉到讓這條邊進畫面；身體往小穴靠近時，限制跟著一起上移（鏡頭同步靠近）；
    // 鏡頭拉太遠、怎麼擺都會看到時，倍率會被拉近到剛好看不到。
    [Export] public bool  BellyGapGuard = true;       // 總開關
    [Export] public float BellyGapMarginPx = 6f;      // 安全距離：身體下緣離畫面底邊至少再多幾個螢幕 px 才算看不到（調大更保守）
    [Export(PropertyHint.Range, "0.02,0.5,0.01")] public float BellyGapAlpha = 0.1f;   // 判斷「不透明」的透明度門檻（貼圖邊緣有零星淡雜點，要略過）
    [Export] public bool  BellyDockFollow = true;     // 「掛在身體上」：鏡頭拉到最底時，會黏在男人身體上、跟著身體一起上下動（身歷其境）；關掉＝只是被限制頂住
    [Export(PropertyHint.Range, "0,30,0.5")] public float BellyFollowSmooth = 12f;   // 身體往下時鏡頭跟下去的順暢度（越大越貼、0＝完全同步硬貼）；身體往上時一律立刻跟上
    bool  _bellyDocked; float _bellyDockLast;
    float _bellyBottomTexRow = -1f;   // 貼圖上不透明部分下緣的那一列（像素）；-1＝還沒算、-2＝算不出來
    float _bellyMidXTex;

    /// <summary>男人身體下緣（不透明部分最下面那一列）在場景座標的 Y；身體沒顯示（或正淡出）時回傳 NaN。</summary>
    float BellyBottomWorldY()
    {
        if (_jijiPhase == 0 || !Layers.TryGetValue("男人肚子", out var bl) || bl is not TextureRect tr
            || !tr.Visible || tr.Texture == null || tr.Modulate.A < 0.3f) return float.NaN;
        if (_bellyBottomTexRow == -1f) ComputeBellyBottomRow(tr);
        if (_bellyBottomTexRow < 0f) return float.NaN;
        Vector2 texSize = new Vector2(tr.Texture.GetWidth(), tr.Texture.GetHeight());
        Vector2 local = new Vector2(_bellyMidXTex, _bellyBottomTexRow) * (tr.Size / texSize);
        return (tr.GetGlobalTransform() * local).Y;
    }

    void ComputeBellyBottomRow(TextureRect tr)
    {
        _bellyBottomTexRow = -2f;
        var img = tr.Texture.GetImage();
        if (img == null) return;
        if (img.IsCompressed()) img.Decompress();
        img.Convert(Image.Format.Rgba8);
        byte[] d = img.GetData();
        int w = img.GetWidth(), h = img.GetHeight();
        int th = Mathf.Clamp(Mathf.RoundToInt(BellyGapAlpha * 255f), 1, 255);
        for (int y = h - 1; y >= 0; y--)
        {   // 從最下面往上找第一列「有一小段連續不透明」的（零星雜點不算：一列至少 20 個不透明點）
            int cnt = 0; long sumX = 0;
            int row = y * w * 4;
            for (int x = 0; x < w; x++)
                if (d[row + x * 4 + 3] >= th) { cnt++; sumX += x; }
            if (cnt >= 20)
            {
                _bellyBottomTexRow = y + 1; _bellyMidXTex = sumX / (float)cnt;
                GD.Print($"[BellyGap] 男人肚子不透明下緣：貼圖第 {y + 1}／{h} 列（貼圖 {w}×{h}）");
                return;
            }
        }
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
        // 男人身體下緣護欄：男人身體（男人肚子圖層）出現時，鏡頭不能讓它下緣那條平邊進到畫面（不然會看到身體下面的大空隙）
        float gapB = BellyBottomWorldY();                        // 身體下緣的場景 Y（沒有身體／身體淡出中＝NaN）
        bool gapOn = BellyGapGuard && !float.IsNaN(gapB) && gapB > 1f;
        float gapNeedZoom = 0f;
        if (gapOn)
        {   // 拉遠限制：鏡頭太遠的話，不管怎麼擺位置畫面底都會超過身體下緣 → 倍率至少要到這個值
            float m0 = BellyGapMarginPx;
            float bgTop = vp.Y * 0.5f - BackgroundOffset.Y - BackgroundSize.Y * 0.5f;
            gapNeedZoom = (vp.Y + m0) / gapB;
            if (bgTop > 0f && gapB > bgTop) gapNeedZoom = Mathf.Max(gapNeedZoom, (vp.Y + m0) / (gapB - bgTop));
            if (_camZoom < gapNeedZoom)
            {
                _camZoom = gapNeedZoom;
                if (_camZoomTarget < gapNeedZoom) _camZoomTarget = gapNeedZoom;
                _camOffset = _camAnchorScreen - _camAnchorWorld * _camZoom;
            }
        }
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
        if (gapOn)
        {   // 往下拉的限制：身體下緣的畫面 Y（offset.Y + 下緣Y×倍率）不能高過畫面底邊；身體往小穴靠近（下緣上移）時，這個限制跟著上移，鏡頭就同步一起靠近
            float gapLo = Mathf.Min(vp.Y + BellyGapMarginPx - gapB * _camZoom, 0f);
            // 「掛在身體上」：玩家把鏡頭拉到最底（頂著這條限制）→ 鏡頭黏在身體上，身體上下動鏡頭就跟著動（第一人稱：我就是男人）
            if (_bellyDocked && _camOffset.Y > _bellyDockLast + 1.5f) _bellyDocked = false;      // 玩家自己把鏡頭往上移開了 → 解除
            if (!_bellyDocked && BellyDockFollow && _camOffset.Y <= gapLo + 2f) _bellyDocked = true;   // 拉到最底 → 掛上去
            if (_camOffset.Y < gapLo) _camOffset.Y = gapLo;   // 硬限制：任何時候都不會讓下緣進畫面
            if (_bellyDocked)
            {
                if (gapLo >= _camOffset.Y) _camOffset.Y = gapLo;   // 身體往上（靠近小穴）：鏡頭立刻同步跟上（不然會露出空隙）
                else if (BellyFollowSmooth <= 0f) _camOffset.Y = gapLo;
                else _camOffset.Y = Mathf.Lerp(_camOffset.Y, gapLo, 1f - Mathf.Exp(-BellyFollowSmooth * dt));   // 身體往下：鏡頭順順地跟下去
                _camOffset.Y = Mathf.Max(_camOffset.Y, loY);   // 還是不能超出背景範圍
                _bellyDockLast = _camOffset.Y;
            }
        }
        else _bellyDocked = false;
        _canvas.Transform = new Transform2D(0f, new Vector2(_camZoom, _camZoom), 0f, _camOffset);
        RefreshHoverCursorForZoom();   // 綠點的半透明手游標跟著縮放變大（Interaction.cs）
    }
}
