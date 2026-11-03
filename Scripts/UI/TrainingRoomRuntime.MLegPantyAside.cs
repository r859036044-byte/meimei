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
    [Export] public float MPantyAsideMaxPx   = 130f;   // 最多撥開多寬（畫布 px；開口寬度）
    [Export] public float MPantyAsideCenterX = 979f;   // 小穴中間（畫布 px，跟 小穴_M 內容中心一致）
    [Export] public float MPantyAsideGrabHalfW = 90f;  // 按下的判定：小穴中間左右各多寬（畫布 px）
    [Export] public Vector2 MPantyAsideGrabV = new Vector2(1400f, 1580f);   // 按下的判定：上下範圍（畫布 px）
    [Export] public Vector2 MPantyAsideBandTop = new Vector2(1380f, 1440f); // 開口上端：從 0 漸變到全開的範圍（畫布 px）
    [Export] public Vector2 MPantyAsideBandBot = new Vector2(1560f, 1650f); // 開口下端：從全開漸變到 0 的範圍（畫布 px）
    [Export] public float MPantyAsideFollow  = 14f;    // 跟手速度
    [Export(PropertyHint.Range, "0,1,0.01")] public float MPantyAsideFoldK = 0.8f;   // 褶皺（陰影／亮面）濃度
    [Export] public float MPantyAsideFoldLenPx = 110f;  // 褶皺從開口右緣往右延伸多遠（畫布 px）
    [Export] public float MPantyAsideFoldFreq  = 0.16f; // 褶皺間距（越大越密）
    [Export] public float MPantyAsidePanRightEdge = 1250f;   // 內褲右緣（畫布 px），布料被推擠後在這裡之前壓縮

    [Export(PropertyHint.Range, "0,1,0.05")] public float MPantyAsideInsertFrac = 0.6f;   // 內褲撥開到最大的幾成以上才能插入（0.6＝撥開 60%）
    /// <summary>M 腿內褲還穿著、而且沒撥開到足夠的寬度：不能插入。</summary>
    bool MPantyBlocksInsert =>
        MPantyAsideEnabled && _mLegOpen && !_pantyGone && MPantyLayer is TextureRect { Visible: true }
        && _mpA < MPantyAsideMaxPx * MPantyAsideInsertFrac;

    bool _mpDrag, _mpMoved;
    float _mpT, _mpA, _mpStartT, _mpStartLX;
    Vector2 _mpDownScreen;
    ShaderMaterial _mpMat;

    const string MPantyAsideShader = @"
shader_type canvas_item;
uniform float aside = 0.0;        // 開口寬度（px）
uniform float cx = 979.0;         // 小穴中間（px）
uniform float right_edge = 1250.0;
uniform vec2 band_top = vec2(1380.0, 1440.0);
uniform vec2 band_bot = vec2(1560.0, 1650.0);
uniform float fold_k = 0.5;
uniform float fold_len = 80.0;
uniform float fold_freq = 0.16;
varying vec4 vc;
void vertex() { vc = COLOR; }
void fragment() {
    vec2 px = UV * vec2(2000.0, 3200.0);
    // 開口是橢圓形：上下端收尖（band_top.x 到 band_bot.y 之間，用開根號讓上下圓潤）
    float vc_ = 0.5 * (band_top.x + band_bot.y);
    float vh_ = 0.5 * (band_bot.y - band_top.x);
    float t_v = (px.y - vc_) / vh_;
    float vb = sqrt(clamp(1.0 - t_v * t_v, 0.0, 1.0));
    float a = aside * vb;
    float hl = cx - 0.3 * a;       // 開口左緣
    float hr = cx + 0.7 * a;       // 開口右緣
    float xs = px.x;
    float shade = 0.0;
    float edge = 0.0;
    float s_in = -1000.0;          // 開口內部的距離（正＝在開口裡，離邊緣多遠；負＝在布上）
    if (a > 0.5) {
        s_in = min(px.x - hl, hr - px.x);
        if (px.x >= hr) {
            // 被撥開的那一段布料往右擠：開口右邊的布料來源從 hl 開始（多出來的布被壓縮）
            xs = hl + (px.x - hr) * (right_edge - hl) / max(1.0, right_edge - hr);
            float d = px.x - hr;
            float env = exp(-d / max(1.0, fold_len * 0.5)) * smoothstep(0.5, 12.0, a);
            float f = sin(d * fold_freq * 6.2832 / 6.0 + px.y * 0.035 + sin(px.y * 0.05) * 1.3);
            shade = env * f;
            edge = exp(-d / 6.0) * smoothstep(0.5, 12.0, a);   // 開口右緣的布料堆疊暗線
        }
    }
    vec4 c = texture(TEXTURE, vec2(xs / 2000.0, UV.y));
    c.rgb *= 1.0 - fold_k * 0.28 * clamp(shade, 0.0, 1.0) - fold_k * 0.35 * edge;
    c.rgb += fold_k * 0.07 * clamp(-shade, 0.0, 1.0);
    if (s_in > 0.0) c.a = 0.0;   // 開口裡：看到底下的小穴
    // 布邊深色細線（跟內褲原本的線同色系）
    float line = (1.0 - smoothstep(0.6, 1.8, abs(s_in))) * step(0.5, a) * smoothstep(0.5, 6.0, a);
    c.rgb = mix(c.rgb, vec3(0.42, 0.18, 0.18), line * 0.85);
    c.a = max(c.a, line * 0.85);
    COLOR = c * vc;
}
";

    Control MPantyLayer => Layers.TryGetValue("m腿/內褲", out var l) ? l : null;

    bool MPantyAsideAvailable() =>
        MPantyAsideEnabled && _mLegOpen && !_pantyGone && _jijiPhase < 3
        && MPantyLayer is TextureRect { Texture: not null } tr && tr.IsVisibleInTree();

    /// <summary>輸入：按在小穴中間 → 往右拖撥開、往左拖放回。放手維持位置。</summary>
    bool MLegPantyAsideInput(InputEvent e)
    {
        if (!MPantyAsideAvailable() && !_mpDrag) return false;
        if (MPantyLayer is not TextureRect panty) return false;

        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed)
            {
                if (IsPointerOverUi() || _tongueTool || _handMode != HandMode.None) return false;
                Vector2 w = ScreenToWorld(mb.Position);
                if (PantyCoveredByDress(w)) return false;
                var lp = panty.GetGlobalTransform().AffineInverse() * w;
                float px = lp.X / Mathf.Max(1f, panty.Size.X) * 2000f;
                float py = lp.Y / Mathf.Max(1f, panty.Size.Y) * 3200f;
                // 開口目前的範圍也算可抓（已經撥開時，抓開口附近的布往左拖放回去）
                float reach = MPantyAsideGrabHalfW + Mathf.Max(0f, _mpA) * 1.2f;
                if (Mathf.Abs(px - MPantyAsideCenterX) > reach || py < MPantyAsideGrabV.X || py > MPantyAsideGrabV.Y) return false;
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

        if (_mpA <= 0.01f && _mpT <= 0.01f)
        {
            if (panty.Material == _mpMat && _mpMat != null) panty.Material = null;   // 沒撥開就不用 shader
            return;
        }
        if (_mpMat == null) _mpMat = new ShaderMaterial { Shader = new Shader { Code = MPantyAsideShader } };
        if (panty.Material != _mpMat) panty.Material = _mpMat;
        _mpMat.SetShaderParameter("aside", _mpA);
        _mpMat.SetShaderParameter("cx", MPantyAsideCenterX);
        _mpMat.SetShaderParameter("right_edge", MPantyAsidePanRightEdge);
        _mpMat.SetShaderParameter("band_top", MPantyAsideBandTop);
        _mpMat.SetShaderParameter("band_bot", MPantyAsideBandBot);
        _mpMat.SetShaderParameter("fold_k", MPantyAsideFoldK);
        _mpMat.SetShaderParameter("fold_len", MPantyAsideFoldLenPx);
        _mpMat.SetShaderParameter("fold_freq", MPantyAsideFoldFreq);
    }
}
