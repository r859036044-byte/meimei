using Godot;

/// <summary>
/// TrainingRoomRuntime — M 腿內褲撥開：
/// M 腿模式下、內褲還穿著時，按住小穴中間往右拖，內褲（m腿/內褲，內褲_M.png）中間那一條被撥到小穴右邊，
/// 露出底下的小穴；被撥開的布料在右邊擠成一團，帶一點褶皺和陰影。
/// 放手後維持撥開的位置，可以再抓回來往左拖放回去；關掉 M 腿或脫掉內褲就復原。
/// 全部座標用畫布 px（貼圖 2000x3200）。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("M 腿內褲撥開")]
    [Export] public bool MPantyAsideEnabled = true;
    [Export] public bool MPantyAsideAnusHole = false;   // 在肛門位置把內褲挖洞（預設關：改成像小穴一樣把下半部壓縮靠到最右邊）
    [Export] public bool MPantyAsideDebugLog = true;   // 按下左鍵時在 Output 印出能不能撥、不能的原因
    [Export] public float MPantyAsideMaxPx   = 150f;   // 內褲左邊最多被推到右邊多遠（畫布 px；右邊固定，布被壓扁）
    [Export] public float MPantyAsideGrabHalfW = 150f; // 按下的判定：小穴中間左右各多寬（畫布 px）
    [Export] public float MPantyAsideCenterX = 979f;   // 小穴中間（畫布 px，跟 小穴_M 內容中心一致）
    [Export] public Vector2 MPantyAsideGrabV = new Vector2(1390f, 1500f);   // 按下的判定：可以拉伸的位置（畫布 px；往上移，內褲高度 1326~1684）
    [Export] public Vector2 MPantyAsideShearV = new Vector2(1370f, 1440f);  // 上轉折帶：上面不動，過了這裡被推到全量（畫布 px）
    [Export] public Vector2 MPantyAsideFixV   = new Vector2(1570f, 1680f);  // 下轉折帶：被推的量慢慢回到 0（畫布 px）。肛門那一帶（y≈1580~1630）也跟小穴一樣壓縮靠到最右邊，只擋住一點點；最下面左下角（>1650）固定不動，中間用弧度接起來
    [Export] public float MPantyJijiClipFromY = 1460f;   // 雞雞蓋在內褲上面只限這條線以下（畫布 px）；以上＝插進去的部分，內褲在雞雞上面
    [Export] public bool MPantyJijiClip = true;   // 內褲被雞雞裁切（雞雞蓋過內褲的地方，內褲被裁掉）
    [Export] public float MPantyAsideFollow  = 14f;    // 跟手速度
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantyAsideFoldK = 0f;   // 褶皺（陰影／亮面）濃度
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantyAsideRightFollow = 0.025f;  // 右邊跟著往右移多少（相對左邊被推的量；0＝右邊完全固定，只要一點點）
    [Export(PropertyHint.Range, "0,0.6,0.01")] public float MPantyAsideCreaseFrac = 0.22f;  // 右側黑色摺線的位置（從右緣往左，佔壓縮寬度的比例）
    [Export(PropertyHint.Range, "0,0.6,0.01")] public float MPantyAsideSag = 0.22f;   // 轉折處布往下垂（向下凹）的量（相對撥開距離）
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantyAsideLowFade = 0f;   // （預設關閉）下半部切斷到肛門上面的程度；你說下面的內部不該被切掉，所以改成 0，肛門改用「只在肛門位置挖洞」
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantyAsideSteepLine = 0.35f;     // 陡的（近垂直的）邊的黑線濃度倍率：直線的黑線少一點；斜的／平的邊一律全濃度
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantyThrustCut = 0.6f;          // 抽插時內褲往上的幅度減少多少（0.6＝減少 60%）
    [Export(PropertyHint.Range, "0,6,0.1")] public float MPantyThrustSpread = 2.5f;        // 抽插時，內褲每往上少移 1px 的推擠量，改成「左邊往右」多撐開幾 px
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantyThrustRightFollow = 0.3f;  // 抽插推到最大時，右邊也往右多跟著移（相對左邊被推的量）
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantyAsideLowStrength = 0.9f;   // 肛門那一帶的內褲位移強度倍率（1＝跟上面一樣；越小位移越少）
    [Export(PropertyHint.Range, "0,2,0.05")] public float MPantyLegRotFollow = 1f;          // 內褲和腳交接的地方跟著腳旋轉變形的程度（0＝不變形）
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantySeamFade = 1f;              // 內褲中間那條黑色縫線，被拉開時淡化的程度（0＝不淡化）
    [Export(PropertyHint.Range, "40,400,1")] public float MPantyAsideMaxReach = 140f;   // 撥開時內褲最多能離原本右緣往右多遠（畫布 px）；再遠的一律隱藏
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantyAsideOutline = 1f;   // 拉伸（壓縮）後邊緣變細，補一圈描邊的濃度（0＝不補）
    [Export(PropertyHint.Range, "0.5,2,0.01")] public float MPantyAsideLeftBoost = 1.25f;   // 左邊往右多縮的倍率（1＝原本；越大左邊越往右）
    [Export(PropertyHint.Range, "0,0.02,0.0005")] public float MPantyAsideLegRelax = 0f;   // 撥開時右上角被大腿切進內褲的部分往回讓多少（0.005≈10px）
    [Export(PropertyHint.Range, "0,0.02,0.0005")] public float MPantyAsideLegGrow = 0.004f; // 撥開時大腿輪廓再往左多裁一點（0.004≈8px）
    [Export] public Vector2 MPantySlashY  = new Vector2(1400f, 1470f);   // 右上歪進來的黑線：起訖高度（畫布 px）
    [Export] public Vector2 MPantySlashIn = new Vector2(4f, 50f);        // 黑線起點／終點離內褲右緣往內多遠（終點更多＝往左下歪進來）
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantySlashK = 0f;   // 自己畫的直斜線濃度（預設關掉；右邊那條彎曲的黑線是內褲原本的邊線，不是這個）
    [Export] public float MPantyAsideFoldFreq  = 0.10f; // 褶皺間距（越大越密）
    [Export(PropertyHint.Range, "0,1,0.05")] public float MPantyAsideInsertFrac = 0.65f;   // 內褲撥開到最大的幾成以上才能插入（0.65＝撥開 65%，剛好露出小穴中間）
    /// <summary>M 腿內褲還穿著、而且沒撥開到足夠的寬度：不能插入。</summary>
    bool MPantyBlocksInsert =>
        MPantyAsideEnabled && _mLegOpen && !_pantyGone && MPantyLayer is TextureRect { Visible: true }
        && _mpA < MPantyAsideMaxPx * MPantyAsideInsertFrac;

    bool _mpDrag, _mpMoved;
    float _mpT, _mpA, _mpStartT, _mpStartLX;
    Vector2 _mpDownScreen;

    Control MPantyLayer => Layers.TryGetValue("m腿/內褲", out var l) ? l : null;

    bool MPantyAsideAvailable() =>
        MPantyAsideEnabled && _mLegOpen && !_pantyGone && _jijiPhase < 3
        && MPantyLayer is TextureRect { Texture: not null } tr && tr.IsVisibleInTree();

    /// <summary>輸入：按在小穴中間 → 往右拖撥開、往左拖放回。放手維持位置。</summary>
    bool MLegPantyAsideInput(InputEvent e)
    {
        if (!MPantyAsideAvailable() && !_mpDrag)
        {
            if (MPantyAsideDebugLog && e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && _mLegOpen)
                GD.Print($"[MPantyDbg] 不能撥：內褲不可用（開啟={MPantyAsideEnabled} M腿={_mLegOpen} 已脫={_pantyGone} 雞雞階段={_jijiPhase} 圖層可見={MPantyLayer?.IsVisibleInTree()}）");
            return false;
        }
        if (MPantyLayer is not TextureRect panty) return false;

        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed)
            {
                if (IsPointerOverUi() || _tongueTool || _handMode != HandMode.None) { if (MPantyAsideDebugLog) GD.Print("[MPantyDbg] 不能撥：滑鼠在 UI 上／舌頭工具／手工具"); return false; }
                Vector2 w = ScreenToWorld(mb.Position);
                var lp = panty.GetGlobalTransform().AffineInverse() * w;
                float px = lp.X / Mathf.Max(1f, panty.Size.X) * 2000f;
                float py = lp.Y / Mathf.Max(1f, panty.Size.Y) * 3200f;
                // 開口目前的範圍也算可抓（已經撥開時，抓開口附近的布往左拖放回去）
                float reach = MPantyAsideGrabHalfW + Mathf.Max(0f, _mpA);
                if (Mathf.Abs(px - MPantyAsideCenterX) > reach || py < MPantyAsideGrabV.X || py > MPantyAsideGrabV.Y)
                { if (MPantyAsideDebugLog) GD.Print($"[MPantyDbg] 不能撥：按的位置不在小穴中間範圍（內褲圖座標 x={px:F0} y={py:F0}；要 x≈{MPantyAsideCenterX:F0}±{reach:F0}、y {MPantyAsideGrabV.X:F0}~{MPantyAsideGrabV.Y:F0}）"); return false; }
                if (MPantyAsideDebugLog) GD.Print($"[MPantyDbg] 抓住內褲（x={px:F0} y={py:F0}）");
                _mpDrag = true; _mpMoved = false;
                _mpDownScreen = mb.Position; _mpStartLX = px; _mpStartT = _mpT;
                return true;
            }
            if (_mpDrag)
            {
                _mpDrag = false;
                if (!_mpMoved) OnHotspotClicked(4);   // 沒拖就放手：當成點小穴（摸 B／雞雞靠近），不會動到已撥開的內褲
                return true;
            }
        }
        else if (e is InputEventMouseMotion mm && _mpDrag)
        {
            var lp = panty.GetGlobalTransform().AffineInverse() * ScreenToWorld(mm.Position);
            float px = lp.X / Mathf.Max(1f, panty.Size.X) * 2000f;
            if (!_mpMoved)
            {   // 死區：滑鼠移動超過 PantyAsideTrigPx 才算「拖」；之前的小抖動不會改變內褲
                if ((mm.Position - _mpDownScreen).Length() < PantyAsideTrigPx) return true;
                _mpMoved = true; _mpStartLX = px; _mpStartT = _mpT;   // 從開始拖的位置起算，不會跳一下
                return true;
            }
            _mpT = Mathf.Clamp(_mpStartT + (px - _mpStartLX), 0f, MPantyAsideMaxPx);
            return true;
        }
        return false;
    }

    /// <summary>每幀（Cloth.cs UpdateClothBump）：平滑跟隨、把參數推進 shader；不能撥時復原。</summary>
    void UpdateMLegPantyAside(float dt)
    {
        if (MPantyLayer is not TextureRect panty) return;
        // 插入中（雞雞階段 3）撥開要維持，不能馬上縮回去；只有關掉 M 腿、脫掉內褲、功能關閉才復原
        bool keep = MPantyAsideEnabled && _mLegOpen && !_pantyGone;
        if (!keep && !_mpDrag) _mpT = 0f;
        if (!keep || _jijiPhase >= 3) _mpDrag = false;
        _mpA += (_mpT - _mpA) * Mathf.Clamp(MPantyAsideFollow * dt, 0f, 1f);
        if (Mathf.Abs(_mpT - _mpA) < 0.05f) _mpA = _mpT;

        // 用 Cloth.cs 的 M 腿內褲 shader（_mPantyMat，同時負責被腿輪廓裁切），不另外掛材質，避免互相覆蓋
        if (_mPantyMat == null) _mPantyMat = new ShaderMaterial { Shader = new Shader { Code = MPantyShaderCode } };
        if (panty.Material != _mPantyMat) panty.Material = _mPantyMat;
        // 抽插時：內褲往上的幅度已經減少 MPantyThrustCut，改成往右撐開——左邊往右（aside 加大）、右邊往右（right_follow 加大）都擴大
        float thrustUp = MLegThrustOffset.TryGetValue("m腿/小穴", out var tOff) ? Mathf.Max(0f, -tOff.Y) : 0f;
        float thrust01 = Mathf.Clamp(thrustUp / 30f, 0f, 1f);
        float asideEff = _mpA > 0.5f ? Mathf.Min(_mpA + MPantyThrustSpread * thrustUp * MPantyThrustCut, MPantyAsideMaxPx * 1.5f) : _mpA;   // 少往上的 60% 幅度，換成多往右撐開的 px
        _mPantyMat.SetShaderParameter("aside", asideEff);
        _mPantyMat.SetShaderParameter("right_follow", MPantyAsideRightFollow + MPantyThrustRightFollow * thrust01 * (_mpA > 0.5f ? 1f : 0f));
        _mPantyMat.SetShaderParameter("line_steep", MPantyAsideSteepLine);
        _mPantyMat.SetShaderParameter("shear_v", MPantyAsideShearV);
        _mPantyMat.SetShaderParameter("fix_v", MPantyAsideFixV);   // edge_l／edge_r 用 shader 預設值（內褲_M 實測左右緣）
        // 內褲圖層的座標轉換（雞雞遮罩、衣服輪廓遮罩共用）
        {
            Transform2D Tp = panty.GetTransform();
            _mPantyMat.SetShaderParameter("jj_x", Tp.X);
            _mPantyMat.SetShaderParameter("jj_y", Tp.Y);
            _mPantyMat.SetShaderParameter("jj_o", Tp.Origin);
            _mPantyMat.SetShaderParameter("jj_size", panty.Size);
        }
        UpdateMPantyClothCut(panty);
        UpdateMPantyJijiClip(panty);
        _mPantyMat.SetShaderParameter("fold_k", MPantyAsideFoldK);
        _mPantyMat.SetShaderParameter("sag_k", MPantyAsideSag);
        _mPantyMat.SetShaderParameter("left_boost", MPantyAsideLeftBoost);
        _mPantyMat.SetShaderParameter("outline_k", MPantyAsideOutline);
        _mPantyMat.SetShaderParameter("max_reach", MPantyAsideMaxReach);
        _mPantyMat.SetShaderParameter("low_fade", MPantyAsideLowFade);
        _mPantyMat.SetShaderParameter("low_str", MPantyAsideLowStrength);
        _mPantyMat.SetShaderParameter("seam_fade", MPantySeamFade);
        {   // 腳的旋轉（右腿＝腿1、左腿＝腿2）：交接處的內褲跟著轉
            float rotR = Layers.TryGetValue("m腿/腿1", out var lg1) && lg1 != null ? Mathf.DegToRad(lg1.RotationDegrees) : 0f;
            float rotL = Layers.TryGetValue("m腿/腿2", out var lg2) && lg2 != null ? Mathf.DegToRad(lg2.RotationDegrees) : 0f;
            _mPantyMat.SetShaderParameter("leg_rot", new Vector2(rotR, rotL) * MPantyLegRotFollow);
        }
        // 肛門：只在肛門圖層的位置把內褲挖一個洞（肛門圖層在內褲下面，洞挖開就看得到）
        if (MPantyAsideAnusHole && Layers.TryGetValue("m腿/肛門", out var anL) && anL is TextureRect anT && anT.Texture != null && anT.Visible)
        {
            _mPantyMat.SetShaderParameter("an_tex", anT.Texture);
            _mPantyMat.SetShaderParameter("an_off", anT.Position - panty.Position);
            _mPantyMat.SetShaderParameter("an_on", 1f);
        }
        else _mPantyMat.SetShaderParameter("an_on", 0f);
        _mPantyMat.SetShaderParameter("leg_grow", MPantyAsideLegGrow);
        _mPantyMat.SetShaderParameter("leg_relax", MPantyAsideLegRelax);
        _mPantyMat.SetShaderParameter("slash_y", MPantySlashY);
        _mPantyMat.SetShaderParameter("slash_in", MPantySlashIn);
        _mPantyMat.SetShaderParameter("slash_k", MPantySlashK);
        _mPantyMat.SetShaderParameter("jj_from_y", MPantyJijiClipFromY);
        _mPantyMat.SetShaderParameter("crease_frac", MPantyAsideCreaseFrac);
        _mPantyMat.SetShaderParameter("fold_freq", MPantyAsideFoldFreq);
    }

    // ── 雞雞裁切內褲：把「雞雞／精液」離屏渲染成遮罩，內褲 shader 用它把被雞雞蓋到的地方裁掉 ──
    static readonly string[] MPantyJijiClipLayers = { "雞雞", "精液" };
    SubViewport _mpJjVP; Node2D _mpJjRoot;
    readonly System.Collections.Generic.Dictionary<string, TextureRect> _mpJjDup = new();

    void UpdateMPantyJijiClip(TextureRect panty)
    {
        // 雞雞（露在入口以下的部分）蓋在內褲「和衣服」上面：雞雞圖層本來排在衣服／內褲下面，
        // 所以內褲和衣服都要用雞雞的輪廓裁掉，雞雞才看得到。入口以上（插進去的部分）不裁，內褲／衣服照常蓋住。
        bool onJ = MPantyJijiClip && _mLegOpen
                   && Layers.TryGetValue("雞雞", out var jj) && jj != null && jj.IsVisibleInTree() && jj.Modulate.A > 0.02f;
        _mPantyMat.SetShaderParameter("jj_on", (onJ && !_pantyGone) ? 1f : 0f);
        if (_clothMat != null) _clothMat.SetShaderParameter("jc_on", (onJ && ClothOnTop) ? 1f : 0f);
        bool on = onJ;
        if (!on) { if (_mpJjVP != null) _mpJjVP.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled; return; }
        if (_mpJjVP == null)
        {
            _mpJjVP = new SubViewport { TransparentBg = true, Size = new Vector2I(1000, 1600), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            AddChild(_mpJjVP);
            _mpJjRoot = new Node2D { Scale = new Vector2(0.5f, 0.5f) };
            _mpJjVP.AddChild(_mpJjRoot);
        }
        _mpJjVP.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        foreach (var n in MPantyJijiClipLayers)
        {
            if (!Layers.TryGetValue(n, out var src) || src is not TextureRect st) continue;
            if (!_mpJjDup.TryGetValue(n, out var d) || !IsInstanceValid(d))
            {
                d = new TextureRect { MouseFilter = Control.MouseFilterEnum.Ignore };
                _mpJjRoot.AddChild(d); _mpJjDup[n] = d;
            }
            d.ExpandMode = st.ExpandMode; d.StretchMode = st.StretchMode;
            d.Texture = st.Texture; d.Size = st.Size; d.Position = st.Position;
            d.PivotOffset = st.PivotOffset; d.Scale = st.Scale; d.Rotation = st.Rotation;
            d.Material = st.Material; d.Modulate = st.Modulate; d.SelfModulate = st.SelfModulate;
            d.Visible = st.Visible && st.Texture != null;
        }
        Transform2D T = panty.GetTransform();
        _mPantyMat.SetShaderParameter("jj_tex", (Texture2D)_mpJjVP.GetTexture());
        _mPantyMat.SetShaderParameter("jj_x", T.X);
        _mPantyMat.SetShaderParameter("jj_y", T.Y);
        _mPantyMat.SetShaderParameter("jj_o", T.Origin);
        _mPantyMat.SetShaderParameter("jj_size", panty.Size);
        if (_clothMat != null && ClothOnTop && Layers.TryGetValue("衣服", out var clL) && clL is TextureRect cr)
        {
            Transform2D Tc = cr.GetTransform();
            _clothMat.SetShaderParameter("jc_tex", (Texture2D)_mpJjVP.GetTexture());
            _clothMat.SetShaderParameter("jc_x", Tc.X);
            _clothMat.SetShaderParameter("jc_y", Tc.Y);
            _clothMat.SetShaderParameter("jc_o", Tc.Origin);
            _clothMat.SetShaderParameter("jc_from_y", MPantyJijiClipFromY);
        }
    }

    // ── 內褲在衣服下面：衣服（含變形、被拉起的衣擺）離屏渲染成輪廓遮罩，內褲被衣服蓋住的地方裁掉 ──
    // 內褲圖層本身排在衣服上面（為了 M 腿／雞雞的順序不亂），所以不能直接把它放到衣服下面，
    // 改用「衣服變形後的實際輪廓」去裁內褲：效果等同內褲在衣服下面，衣服被拉開／腿裁切掉的地方才看得到內褲。
    [Export] public bool MPantyClothCut = true;
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantyClothCutK = 1f;   // 衣服蓋住內褲的程度（1＝完全蓋住）
    SubViewport _mpClVP; Node2D _mpClRoot; TextureRect _mpClDup;

    void UpdateMPantyClothCut(TextureRect panty)
    {
        bool on = MPantyClothCut && ClothOnTop && _mLegOpen && !_pantyGone
                  && Layers.TryGetValue("衣服", out var clL) && clL is TextureRect cr && cr.Texture != null && cr.IsVisibleInTree();
        _mPantyMat.SetShaderParameter("cc_on", on ? 1f : 0f);
        if (!on) { if (_mpClVP != null) _mpClVP.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled; return; }
        var cloth = (TextureRect)Layers["衣服"];
        if (_mpClVP == null)
        {
            _mpClVP = new SubViewport { TransparentBg = true, Size = new Vector2I(1000, 1600), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            AddChild(_mpClVP);
            _mpClRoot = new Node2D { Scale = new Vector2(0.5f, 0.5f) };
            _mpClVP.AddChild(_mpClRoot);
        }
        _mpClVP.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        if (_mpClDup == null || !IsInstanceValid(_mpClDup))
        {
            _mpClDup = new TextureRect { MouseFilter = Control.MouseFilterEnum.Ignore };
            _mpClRoot.AddChild(_mpClDup);
        }
        var d = _mpClDup;
        d.ExpandMode = cloth.ExpandMode; d.StretchMode = cloth.StretchMode;
        d.Texture = cloth.Texture; d.Size = cloth.Size; d.Position = cloth.Position;
        d.PivotOffset = cloth.PivotOffset; d.Scale = cloth.Scale; d.Rotation = cloth.Rotation;
        d.Material = cloth.Material;   // 共用衣服的 shader（變形、被腿／雞雞裁切後的實際輪廓）
        d.Modulate = new Color(1f, 1f, 1f, cloth.Modulate.A); d.SelfModulate = cloth.SelfModulate;
        d.Visible = cloth.Visible;
        _mPantyMat.SetShaderParameter("cc_tex", (Texture2D)_mpClVP.GetTexture());
        _mPantyMat.SetShaderParameter("cc_k", MPantyClothCutK);
    }
}
