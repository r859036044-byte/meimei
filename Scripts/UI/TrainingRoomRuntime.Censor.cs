using Godot;

/// <summary>
/// TrainingRoomRuntime — 遮擋（F1 面板開關）。
///   • 方塊覆蓋：雞雞、小穴、左右胸各蓋一塊黑色方塊
///   • 馬賽克：雞雞、小穴各一塊區域做 8px 馬賽克
/// 兩種只能開一種。
///
/// 跟著走：UpdateCensor() 在 _Process 最後（雞雞位置、抽插、晃動、鏡頭都算完之後）才呼叫，
///   量的是這一幀雞雞／小穴實際畫出來的位置，不會慢一幀或蓋歪。
/// 雞雞的範圍 = 雞雞 + 精液 兩層的聯集，再往外多留 CensorJijiPadPx。
/// 馬賽克的格子是「以遮擋框左上角為原點」切的，所以格子會跟著雞雞一起移動，不會在原地閃爍。
/// 遮擋層放在場景最上面（角色、精液、液體都在它下面），UI 不受影響。
/// </summary>
public partial class TrainingRoomRuntime
{
    public enum CensorMode { None, Box, Mosaic }
    public CensorMode Censor { get; private set; } = CensorMode.None;

    [Export] public float CensorMosaicPx  = 6.5f;    // 馬賽克格子大小（螢幕 px）：越小越不明顯（原本 8）
    [Export(PropertyHint.Range, "0.1,1,0.05")] public float CensorPussyCenterX = 0.5f;   // 小穴只蓋「中間」：寬度比例
    [Export(PropertyHint.Range, "0.1,1,0.05")] public float CensorPussyCenterY = 0.5f;   // 小穴只蓋「中間」：高度比例
    [Export] public float CensorClipMarginPx = 2f;   // 雞雞馬賽克只蓋到「裁切線」往上多這麼多 px，再往上（被裁掉的地方）不打（螢幕 px）
    [Export] public float CensorPadPx     = 6f;    // 小穴／胸 遮擋範圍往外多留（螢幕 px）
    [Export] public float CensorJijiPadPx = 10f;   // 雞雞遮擋範圍往外多留（螢幕 px）

    ColorRect _censorPussy, _censorJiji, _censorBreastL, _censorBreastR;
    static Shader _mosaicShader;
    ShaderMaterial _mosaicMatPussy, _mosaicMatJiji;

    // 各圖層實際畫出內容的範圍（2000x3200 畫布像素，PIL 量 alpha bbox）
    static readonly Rect2 _rcMLegPussy = new Rect2(860f, 1321f, 238f, 258f);
    static readonly Rect2 _rcPussy     = new Rect2(766f, 1384f, 421f, 227f);
    static readonly Rect2 _rcInsert    = new Rect2(856f, 1166f, 239f, 266f);
    static readonly Rect2 _rcOpen1     = new Rect2(808f, 1324f, 354f, 275f);
    static readonly Rect2 _rcJiji      = new Rect2(850f, 1571f, 269f, 559f);
    static readonly Rect2 _rcSemen     = new Rect2(889f, 1579f, 197f, 465f);
    static readonly Rect2 _rcBreast1   = new Rect2(806f,  865f, 356f, 272f);   // 胸1
    static readonly Rect2 _rcBreast2   = new Rect2(772f,  857f, 208f, 262f);   // 胸2

    public void SetCensor(CensorMode mode)
    {
        Censor = mode;
        EnsureCensorNodes();
        bool mosaic = mode == CensorMode.Mosaic;
        _censorPussy.Material = mosaic ? _mosaicMatPussy : null;
        _censorJiji.Material  = mosaic ? _mosaicMatJiji  : null;
        foreach (var r in new[] { _censorPussy, _censorJiji, _censorBreastL, _censorBreastR })
        {
            r.Color = Colors.Black;
            r.Visible = false;
        }
        UpdateCensor();
    }

    void EnsureCensorNodes()
    {
        _mosaicShader ??= new Shader
        {
            Code = @"shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_nearest;
uniform float block_px = 8.0;
uniform sampler2D belly_tex;                 // 肚子貼圖：肚子畫到的地方不打馬賽克
uniform vec2 belly_ix = vec2(1.0, 0.0);      // 螢幕 px → 肚子圖層本地 px 的反矩陣
uniform vec2 belly_iy = vec2(0.0, 1.0);
uniform vec2 belly_io = vec2(0.0);
uniform vec2 belly_size = vec2(2000.0, 3200.0);
uniform vec2 vp_size = vec2(1920.0, 1080.0);
uniform float belly_on = 0.0;
void fragment() {
    // 遮擋框左上角在螢幕 UV 的位置（框是軸對齊的，用導數換算框的螢幕大小）
    vec2 size_uv = vec2(dFdx(SCREEN_UV.x) / dFdx(UV.x), dFdy(SCREEN_UV.y) / dFdy(UV.y));
    vec2 origin = SCREEN_UV - UV * size_uv;
    vec2 cell = SCREEN_PIXEL_SIZE * block_px;
    // 以框的左上角為原點切格子 → 格子跟著框（雞雞）一起移動
    vec2 uv = origin + (floor((SCREEN_UV - origin) / cell + 1e-4) + 0.5) * cell;
    uv = clamp(uv, origin, origin + size_uv);
    vec3 mos = texture(screen_tex, uv).rgb;
    if (belly_on > 0.5) {
        vec2 sp = SCREEN_UV * vp_size;
        vec2 tuv = (belly_ix * sp.x + belly_iy * sp.y + belly_io) / belly_size;
        if (tuv.x >= 0.0 && tuv.x <= 1.0 && tuv.y >= 0.0 && tuv.y <= 1.0 && texture(belly_tex, tuv).a > 0.3)
            mos = texture(screen_tex, SCREEN_UV).rgb;   // 肚子：原樣顯示
    }
    COLOR = vec4(mos, 1.0);
}"
        };
        _mosaicMatPussy ??= new ShaderMaterial { Shader = _mosaicShader };
        _mosaicMatJiji  ??= new ShaderMaterial { Shader = _mosaicShader };
        _mosaicMatPussy.SetShaderParameter("block_px", CensorMosaicPx);
        _mosaicMatJiji.SetShaderParameter("block_px", CensorMosaicPx);

        ColorRect Make(string name)
        {
            var r = new ColorRect { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.Black, Visible = false };
            _canvas.AddChild(r);
            return r;
        }
        if (_censorPussy   == null || !IsInstanceValid(_censorPussy))   _censorPussy   = Make("CensorPussy");
        if (_censorJiji    == null || !IsInstanceValid(_censorJiji))    _censorJiji    = Make("CensorJiji");
        if (_censorBreastL == null || !IsInstanceValid(_censorBreastL)) _censorBreastL = Make("CensorBreast1");
        if (_censorBreastR == null || !IsInstanceValid(_censorBreastR)) _censorBreastR = Make("CensorBreast2");
    }

    /// <summary>把圖層本地的內容範圍換算成場景（_canvas）座標的外框（含旋轉/縮放/位移）。</summary>
    Rect2? LayerRect(string layer, Rect2 local)
    {
        if (!Layers.TryGetValue(layer, out var c) || c == null || !c.IsVisibleInTree()) return null;
        if (c.Modulate.A * c.SelfModulate.A < 0.02f) return null;   // 完全透明（還沒淡入）就不用蓋
        // 貼圖大小跟 TextureRect 大小不同時換算（目前都是 2000x3200，等比 1）
        if (c is TextureRect tr && tr.Texture != null && tr.Texture.GetWidth() > 0)
        {
            float sx = c.Size.X / tr.Texture.GetWidth(), sy = c.Size.Y / tr.Texture.GetHeight();
            local = new Rect2(local.Position * new Vector2(sx, sy), local.Size * new Vector2(sx, sy));
        }
        var xf = c.GetGlobalTransform();
        Vector2 a = xf * local.Position, b = xf * new Vector2(local.End.X, local.Position.Y);
        Vector2 d = xf * local.End,      e = xf * new Vector2(local.Position.X, local.End.Y);
        var r = new Rect2(a, Vector2.Zero);
        r = r.Expand(b); r = r.Expand(d); r = r.Expand(e);
        return r;
    }

    /// <summary>肚子圖層畫到的地方不打馬賽克：把肚子的貼圖和「螢幕→圖層本地」反矩陣丟給馬賽克 shader。</summary>
    void UpdateBellyMask(ShaderMaterial m)
    {
        if (m == null) return;
        bool on = Layers.TryGetValue("肚子", out var b) && b is TextureRect tr && tr.Texture != null && tr.IsVisibleInTree() && tr.Modulate.A > 0.02f;
        m.SetShaderParameter("belly_on", on ? 1f : 0f);
        if (!on) return;
        var t = (Layers["肚子"] as TextureRect);
        var inv = t.GetGlobalTransformWithCanvas().AffineInverse();
        m.SetShaderParameter("belly_tex", t.Texture);
        m.SetShaderParameter("belly_ix", inv.X);
        m.SetShaderParameter("belly_iy", inv.Y);
        m.SetShaderParameter("belly_io", inv.Origin);
        m.SetShaderParameter("belly_size", t.Size);
        m.SetShaderParameter("vp_size", GetViewport().GetVisibleRect().Size);
    }

    readonly System.Collections.Generic.Dictionary<string, Rect2> _contentRects = new();
    /// <summary>圖層貼圖實際有內容的範圍（圖層本地座標，讀一次 alpha 後快取）。</summary>
    Rect2? LayerContentRect(string layer)
    {
        if (_contentRects.TryGetValue(layer, out var r0)) return r0.Size.X > 0f ? r0 : null;
        if (!Layers.TryGetValue(layer, out var c) || c is not TextureRect tr || tr.Texture == null) return null;
        var r = PinchScanBox(tr);   // Pinch.cs：4px 取樣的 alpha 外框
        _contentRects[layer] = r;
        return r.Size.X > 0f ? r : null;
    }

    static Rect2? Union(Rect2? a, Rect2? b) => a == null ? b : b == null ? a : a.Value.Merge(b.Value);

    /// <summary>每幀（_Process 最後）：遮擋框跟著實際位置走，並保持在場景最上層。</summary>
    void UpdateCensor()
    {
        if (Censor == CensorMode.None || _canvas == null) return;
        EnsureCensorNodes();
        _mosaicMatPussy.SetShaderParameter("block_px", CensorMosaicPx);   // Play 中可即時調
        _mosaicMatJiji.SetShaderParameter("block_px", CensorMosaicPx);
        UpdateBellyMask(_mosaicMatPussy);
        UpdateBellyMask(_mosaicMatJiji);
        // 遮擋框放在 _canvas（會跟鏡頭縮放），「螢幕 px」的留白要除以鏡頭倍率
        float z = Mathf.Max(_camZoom, 0.01f);

        // 只蓋兩個地方：小穴的「中間」＋ 開（開1）下面的「洞」整個
        var pc = _mLegOpen ? _rcMLegPussy : _rcPussy;
        Vector2 pcs = new Vector2(pc.Size.X * CensorPussyCenterX, pc.Size.Y * CensorPussyCenterY);
        var pcore = new Rect2(pc.GetCenter() - pcs * 0.5f, pcs);
        Rect2? pussy = LayerRect(_mLegOpen ? "m腿/小穴" : "腿/小穴", pcore);
        var holeR = LayerContentRect("洞");
        if (holeR.HasValue) pussy = Union(pussy, LayerRect("洞", holeR.Value));
        Place(_censorPussy, pussy, CensorPadPx / z);

        Rect2? jiji = null;
        if (_jijiPhase > 0)
        {
            Rect2 rj = _rcJiji, rsm = _rcSemen;
            if (JijiClipEnabled && _jijiCutLocalY > 0f)
            {   // 被裁切線裁掉（看不到）的部分不要打馬賽克：上緣放在裁切線往上 CensorClipMarginPx
                float top = _jijiCutLocalY - CensorClipMarginPx / CharScreenScale();
                if (top > rj.Position.Y) rj = new Rect2(rj.Position.X, top, rj.Size.X, Mathf.Max(1f, rj.End.Y - top));
                if (top > rsm.Position.Y) rsm = new Rect2(rsm.Position.X, top, rsm.Size.X, Mathf.Max(1f, rsm.End.Y - top));
            }
            jiji = LayerRect("雞雞", rj);
            jiji = Union(jiji, LayerRect("精液", rsm));
        }
        Place(_censorJiji, jiji, CensorJijiPadPx / z);

        bool box = Censor == CensorMode.Box;
        Place(_censorBreastL, box ? LayerRect("胸1", _rcBreast1) : null, CensorPadPx / z);
        Place(_censorBreastR, box ? LayerRect("胸2", _rcBreast2) : null, CensorPadPx / z);

        // 放到最上層：新產生的精液/液體也會被蓋到
        foreach (var r in new[] { _censorBreastL, _censorBreastR, _censorPussy, _censorJiji })
            _canvas.MoveChild(r, _canvas.GetChildCount() - 1);
    }

    static void Place(ColorRect r, Rect2? rect, float pad)
    {
        if (r == null) return;
        if (rect == null) { r.Visible = false; return; }
        var g = rect.Value.Grow(pad);
        r.Visible = true;
        r.Rotation = 0f;
        r.Position = g.Position;
        r.Size = g.Size;
    }
}
