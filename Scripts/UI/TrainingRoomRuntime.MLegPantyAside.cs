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
    [Export] public bool MPantyAsideDebugLog = true;   // 按下左鍵時在 Output 印出能不能撥、不能的原因
    [Export] public float MPantyAsideMaxPx   = 180f;   // 內褲下半段最多被推到右邊多遠（畫布 px）
    [Export] public float MPantyAsideGrabHalfW = 150f; // 按下的判定：小穴中間左右各多寬（畫布 px）
    [Export] public float MPantyAsideCenterX = 979f;   // 小穴中間（畫布 px，跟 小穴_M 內容中心一致）
    [Export] public Vector2 MPantyAsideGrabV = new Vector2(1440f, 1580f);   // 按下的判定：內褲 Y 中間的範圍（畫布 px；內褲高度 1326~1684，中間約 1505）
    [Export] public Vector2 MPantyAsideShearV = new Vector2(1420f, 1520f);  // 上轉折帶：上面不動，過了這裡開始被推到全量（畫布 px）
    [Export] public Vector2 MPantyAsideFixV   = new Vector2(1590f, 1670f);  // 下轉折帶：被推的量慢慢回到 0，最底下那一截固定不動（畫布 px）
    [Export] public bool MPantyJijiClip = true;   // 內褲被雞雞裁切（雞雞蓋過內褲的地方，內褲被裁掉）
    [Export] public float MPantyAsideFollow  = 14f;    // 跟手速度
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantyAsideFoldK = 0.8f;   // 褶皺（陰影／亮面）濃度
    [Export] public float MPantyAsideFoldFreq  = 0.11f; // 褶皺間距（越大越密）
    [Export(PropertyHint.Range, "0,1,0.05")] public float MPantyAsideInsertFrac = 0.6f;   // 內褲撥開到最大的幾成以上才能插入（0.6＝撥開 60%）
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
                if (!_mpMoved && _mpT < 0.5f) OnHotspotClicked(4);   // 沒拖就放手：當成點小穴（跟一般內褲一樣）
                return true;
            }
        }
        else if (e is InputEventMouseMotion mm && _mpDrag)
        {
            if ((mm.Position - _mpDownScreen).Length() >= PantyAsideTrigPx) _mpMoved = true;
            var lp = panty.GetGlobalTransform().AffineInverse() * ScreenToWorld(mm.Position);
            float px = lp.X / Mathf.Max(1f, panty.Size.X) * 2000f;
            _mpT = Mathf.Clamp(_mpStartT + (px - _mpStartLX), 0f, MPantyAsideMaxPx);
            return true;
        }
        return false;
    }

    /// <summary>每幀（Cloth.cs UpdateClothBump）：平滑跟隨、把參數推進 shader；不能撥時復原。</summary>
    void UpdateMLegPantyAside(float dt)
    {
        if (MPantyLayer is not TextureRect panty) return;
        bool can = MPantyAsideEnabled && _mLegOpen && !_pantyGone && _jijiPhase < 3;
        if (!can && !_mpDrag) _mpT = 0f;
        if (!can) _mpDrag = false;
        _mpA += (_mpT - _mpA) * Mathf.Clamp(MPantyAsideFollow * dt, 0f, 1f);
        if (Mathf.Abs(_mpT - _mpA) < 0.05f) _mpA = _mpT;

        // 用 Cloth.cs 的 M 腿內褲 shader（_mPantyMat，同時負責被腿輪廓裁切），不另外掛材質，避免互相覆蓋
        if (_mPantyMat == null) _mPantyMat = new ShaderMaterial { Shader = new Shader { Code = MPantyShaderCode } };
        if (panty.Material != _mPantyMat) panty.Material = _mPantyMat;
        _mPantyMat.SetShaderParameter("aside", _mpA);
        _mPantyMat.SetShaderParameter("shear_v", MPantyAsideShearV);
        _mPantyMat.SetShaderParameter("fix_v", MPantyAsideFixV);
        UpdateMPantyJijiClip(panty);
        _mPantyMat.SetShaderParameter("fold_k", MPantyAsideFoldK);
        _mPantyMat.SetShaderParameter("fold_freq", MPantyAsideFoldFreq);
    }

    // ── 雞雞裁切內褲：把「雞雞／精液」離屏渲染成遮罩，內褲 shader 用它把被雞雞蓋到的地方裁掉 ──
    static readonly string[] MPantyJijiClipLayers = { "雞雞", "精液" };
    SubViewport _mpJjVP; Node2D _mpJjRoot;
    readonly System.Collections.Generic.Dictionary<string, TextureRect> _mpJjDup = new();

    void UpdateMPantyJijiClip(TextureRect panty)
    {
        bool on = MPantyJijiClip && _mLegOpen && !_pantyGone
                  && Layers.TryGetValue("雞雞", out var jj) && jj != null && jj.IsVisibleInTree() && jj.Modulate.A > 0.02f;
        _mPantyMat.SetShaderParameter("jj_on", on ? 1f : 0f);
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
    }
}
