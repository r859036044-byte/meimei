using Godot;

/// <summary>
/// TrainingRoomRuntime — 肩帶（衣服/左帶、衣服/右帶）。
/// 肩帶是剛體：只會「平移」和「旋轉」，不縮放、不彎、不受「上半部往下拉壓縮」影響（才會留在手臂那邊看得到）。
/// 動作順序：上半部旋轉進度（旋轉角 / 最大角度）先讓肩帶往外往下平移，超過 StrapRotStart 之後才開始繞「自己最底端」旋轉。
/// 旋轉本身要左右移動超過 StrapRotTriggerPx 才會觸發（見 Cloth.cs 的旋轉拖曳）。
/// 所有參數是 Export，Play 模式下可即時調。
/// </summary>
public partial class TrainingRoomRuntime
{
    [Export] public float StrapOutPx          = 24f;    // 平移：往外（左帶往左、右帶往右）的距離（畫布 px）
    [Export] public float StrapDownPx         = 25f;     // 平移：往下的距離（畫布 px）
    [Export] public float StrapRotStart       = 0.25f;  // 動作順序（進度 0~1）：0~這個值＝只橫向平移；到這個值才開始旋轉
    [Export] public float StrapBelowShrinkK   = 0.1f;    // 橘線以下的衣服往中間靠的幅度：每往下拉 1px，左半往右／右半往左 k px（上面外擴是 StrapTopSpreadK）
    [Export] public float StrapMark2OffsetPx   = -56f;  // 橘線微調（畫布 px；負＝往上、正＝往下）
    [Export] public float StrapCreaseStepPx   = 14f;    // 往下拉每多幾 px 就多出一條折疊褶皺（第一條在青線以下一開始拉就出現）
    [Export] public bool  StrapMark2AtChestDot = true;  // 橘線放在胸部綠點的底部（關掉＝青線再往下 StrapShrinkMarkPx）
    [Export] public float StrapShrinkMarkPx   = 5f;     // 第二條標註線（橘）：青線再往下幾 px，衣服開始內縮的位置
    [Export] public bool  StrapShowDownMark   = false;   // 標註線（青色）：肩帶向下走完 StrapDownPx 的位置
    [Export] public float StrapTopSpreadK     = 0.35f;   // 超過這條線繼續往下拉：每往下 1px，左半往左／右半往右 k px（衣服上半部與肩帶一起）
    [Export] public bool  StrapBowProtect     = false;   // 蝴蝶結（衣服左右上角）不被變形，只整塊跟著位移
    [Export] public float StrapBowHoldPx      = 100f;   // 不變形的半徑（畫布 px）
    [Export] public float StrapBowFadePx      = 60f;    // 往外漸變回一般變形的寬度（畫布 px）
    [Export] public float StrapFanDownPx      = 20f;    // 花灑褶皺起點再往下多少 px
    [Export] public float StrapFanInsetPx     = 15f;    // 花灑褶皺起點離衣服左右最邊緣往內縮多少 px
    [Export] public bool  StrapCornerBind     = true;   // 衣服最左上角／最右上角綁在肩帶底部
    [Export] public float StrapCornerUpPx     = 1.5f;   // 綁定點：肩帶底部往上幾 px
    [Export] public float StrapCornerR         = 240f;   // 角落綁定的外半徑：超過就不受肩帶帶動（畫布 px）
    [Export] public float StrapCornerHoldPx   = 100f;   // 角落綁定的內半徑：這個範圍內（含衣服左上角／右上角、最上面 3px）完全跟著肩帶底部走
    [Export] public float StrapDownEnd        = 0.80f;  // 進度到這個值＝肩帶向下走完（StrapDownPx）；之後繼續往下拖，衣服就往下（不用拉到底）
    float StrapUnlockDeg => StrapDownEnd * ClothTopRotMax;   // 衣服可以往下拉的解鎖角度
    [Export] public float StrapDownStart      = 0.60f;  // 進度超過這個值才開始往下平移（旋轉完才往下）
    [Export] public float StrapRotTriggerPx   = 5f;     // 拖曳要左右移動超過幾 px 才會觸發旋轉
    [Export] public bool  StrapAutoAnchor     = true;   // 開機時從肩帶圖的不透明範圍自動取「最底端中心」當錨點
    [Export] public Vector2 StrapAnchorLeftPx  = new Vector2(855.5f, 925f);   // 左帶旋轉錨點（畫布 px）
    [Export] public Vector2 StrapAnchorRightPx = new Vector2(1074f, 919f);    // 右帶旋轉錨點（畫布 px）

    [Export] public float StrapHitLenPx       = 95f;    // 抓取判定：肩帶長度（畫布 px）
    [Export] public float StrapHitRadiusPx    = 40f;    // 抓取判定：離肩帶中線多近算點到
    [Export] public float StrapDragPx         = 45f;    // 滑鼠往外拖多少 px（畫布）＝肩帶轉滿；數字越小越靈敏

    float _strapY0, _strapSt0;   // 肩帶拉到底後「繼續往下拖＝衣服往下」用
    bool _rotTriggered; float _rotTrigX;   // Cloth.cs 的旋轉拖曳用
    ShaderMaterial _strap2Mat, _strap2MatR;   // _strap2Mat＝左帶、_strap2MatR＝右帶
    bool _strapAnchorDone;

    static readonly string[] _strap2Copy =
    {
        "top_cx", "show_guides",
        "lift_top_v", "lift_bottom_v", "lift_neck_v", "lift_navel_v", "lift_bottom_end_v",
        "chest1_c", "chest1_off", "chest1_s", "chest2_c", "chest2_off", "chest2_s", "chest_r", "chest_inner",
    };

    const string Strap2ShaderCode = @"
shader_type canvas_item;
uniform vec2 rot_ang = vec2(0.0);
uniform vec2 strap_t = vec2(0.0);
uniform vec4 strap_mv = vec4(24.0, 70.0, 0.4, 0.5);
uniform float top_cx = 0.488;
uniform float is_left = 0.0;
uniform float strap_de = 0.8;
uniform float strap_k = 0.5;
uniform vec2 strap_st = vec2(0.0);
uniform vec2 strap_sy = vec2(0.0);   // 肩帶跟著衣服往下的量（畫布 px，左/右，已乘過比例）   // 衣服上半部被往下拉的量（畫布 px，左/右）：肩帶跟著一起往下
uniform vec2 anchor_l = vec2(0.4278, 0.2891);
uniform vec2 anchor_r = vec2(0.5370, 0.2872);
uniform float show_guides = 0.0;
uniform float lift_top_v = 0.0;
uniform float lift_bottom_v = 0.0;
uniform float lift_neck_v = 0.12;
uniform float lift_navel_v = 0.36;
uniform float lift_bottom_end_v = 0.55;
uniform vec2 chest1_c = vec2(0.4885, 0.3119);
uniform vec2 chest1_off = vec2(0.0);
uniform float chest1_s = 1.0;
uniform vec2 chest2_c = vec2(0.4365, 0.3088);
uniform vec2 chest2_off = vec2(0.0);
uniform float chest2_s = 1.0;
uniform vec2 chest_r = vec2(260.0, 230.0);
uniform float chest_inner = 0.35;
varying vec4 vc;
void vertex() { vc = COLOR; }
void chest(inout vec2 uv, vec2 c, vec2 off, float s) {
    vec2 px = vec2(2000.0, 3200.0);
    vec2 d = (uv - c) * px / chest_r;
    float w = 1.0 - smoothstep(chest_inner, 1.0, length(d));
    uv -= w * off;
    uv = c + (uv - c) / (1.0 + w * (s - 1.0));
}
void fragment() {
    vec2 PXs = vec2(2000.0, 3200.0);
    float left_ = is_left;   // 每條肩帶各用自己的材質，只套自己這邊的動作（避免出現第二條殘影）
    float tt = mix(strap_t.y, strap_t.x, left_);
    float rs = clamp(strap_mv.z, 0.02, 0.9);            // 旋轉開始的進度
    float ds = clamp(strap_mv.w, rs + 0.02, 0.98);      // 往下開始的進度
    float mvx = smoothstep(0.0, rs, tt);                // ① 先橫向平移（左帶往左、右帶往右）
    float rtk = smoothstep(rs, ds, tt);                 // ② 橫向走完才旋轉（繞自己最底端）
    float mvy = smoothstep(ds, clamp(strap_de, ds + 0.02, 1.0), tt);                // ③ 旋轉完才往下
    vec2 T = vec2(mix(strap_mv.x, -strap_mv.x, left_) * mvx + (1.0 - 2.0 * left_) * mix(strap_st.y, strap_st.x, left_) * strap_k, strap_mv.y * mvy + mix(strap_sy.y, strap_sy.x, left_)) / PXs;   // 肩帶只在「向下 StrapDownPx」之後停住，不再跟著衣服往下
    vec2 pv = mix(anchor_r, anchor_l, left_);
    vec2 P = UV - T;
    float ang_ = (rot_ang.y * (1.0 - left_) - rot_ang.x * left_) * rtk;
    vec2 d_ = (P - pv) * PXs;
    float cs_ = cos(ang_), sn_ = sin(ang_);
    vec2 d2_ = vec2(d_.x * cs_ + d_.y * sn_, -d_.x * sn_ + d_.y * cs_);
    P = pv + d2_ / PXs;
    vec2 uv = P;
    uv.y += lift_top_v * smoothstep(lift_neck_v, lift_navel_v, P.y) + lift_bottom_v * smoothstep(lift_navel_v, lift_bottom_end_v, P.y);
    chest(uv, chest1_c, chest1_off, chest1_s);
    chest(uv, chest2_c, chest2_off, chest2_s);
    vec4 col = texture(TEXTURE, uv) * vc;
    if (show_guides > 0.5) {
        // 臨時標註：白點＝肩帶旋轉錨點（原位）；洋紅圈＝錨點目前位置（平移之後）
        float mA = 0.0; vec3 gc = vec3(0.0);
        for (int k = 0; k < 2; k++) {
            vec2 a0 = k == 0 ? anchor_l : anchor_r;
            float lf = k == 0 ? 1.0 : 0.0;
            float t1 = k == 0 ? strap_t.x : strap_t.y;
            float rs1 = clamp(strap_mv.z, 0.02, 0.9);
            float ds1 = clamp(strap_mv.w, rs1 + 0.02, 0.98);
            vec2 Tt = vec2(mix(strap_mv.x, -strap_mv.x, lf) * smoothstep(0.0, rs1, t1), strap_mv.y * smoothstep(ds1, clamp(strap_de, ds1 + 0.02, 1.0), t1)) / PXs;
            float m0 = 1.0 - smoothstep(6.0, 8.0, length((UV - a0) * PXs));
            float rr = length((UV - (a0 + Tt)) * PXs);
            float m1 = (1.0 - smoothstep(12.0, 14.0, rr)) * smoothstep(7.0, 9.0, rr);
            if (m0 > mA) { mA = m0; gc = vec3(1.0); }
            if (m1 > mA) { mA = m1; gc = vec3(1.0, 0.0, 1.0); }
        }
        col.rgb = mix(col.rgb, gc, mA);
        col.a = max(col.a, mA);
    }
    COLOR = col;
}
";

    /// <summary>取肩帶圖不透明範圍的「最底端中心」（畫布 px）；失敗時回傳 fallback。</summary>
    Vector2 StrapBottomAnchor(string layer, Vector2 fallback)
    {
        if (!Layers.TryGetValue(layer, out var c) || c is not TextureRect tr || tr.Texture == null) return fallback;
        var img = tr.Texture.GetImage();
        if (img == null) return fallback;
        if (img.IsCompressed()) img.Decompress();
        img.Convert(Image.Format.Rgba8);
        int w = img.GetWidth(), h = img.GetHeight();
        var d = img.GetData();
        int minX = w, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            int row = y * w * 4;
            for (int x = 0; x < w; x++)
            {
                if (d[row + x * 4 + 3] > 25)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        }
        if (maxX < 0) return fallback;
        // 圖在畫布上的大小可能不是貼圖像素（Scale 拉伸），換成畫布 px
        float sx = c.Size.X / Mathf.Max(1, w), sy = c.Size.Y / Mathf.Max(1, h);
        return new Vector2((minX + maxX + 1) * 0.5f * sx, (maxY + 1) * sy);
    }

    [Export] public float StrapFanBandPx      = 40f;      // 褶皺只影響衣服上緣往下這麼多 px（畫布，5~8）
    [Export] public float StrapLineFollow     = 1.0f;    // 黃線／橘線跟著肩帶往下的比例（1＝肩帶下移多少線就下移多少）
    [Export] public Vector4 StrapFanParams    = new Vector4(18f, 46f, 70f, 3.2f);   // 褶皺：兩條線角度(度)、長度(px)、線寬(px)

    /// <summary>肩帶往下平移的量（兩邊平均，畫布 px）→ 黃線、橘線一起往下。</summary>
    float StrapLineFollowPx()
    {
        float rmax = Mathf.Max(1f, ClothTopRotMax);
        float rs = Mathf.Clamp(StrapRotStart, 0.02f, 0.9f), ds = Mathf.Clamp(StrapDownStart, rs + 0.02f, 0.98f);
        float sum = 0f;
        for (int sd = 0; sd < 2; sd++) sum += Ss(ds, Mathf.Clamp(StrapDownEnd, ds + 0.02f, 1f), Mathf.Clamp(_rotA[sd] / rmax, 0f, 1f));
        return StrapDownPx * StrapLineFollow * sum * 0.5f;
    }

    static float Ss(float a, float b, float x) { float t = Mathf.Clamp((x - a) / Mathf.Max(1e-6f, b - a), 0f, 1f); return t * t * (3f - 2f * t); }

    /// <summary>滑鼠（圖層局部 px）是否點在目前位置的肩帶上（已被拉開的肩帶也要能抓回來）。回傳 0=左、1=右、-1=沒點到。</summary>
    int StrapHitSide(Vector2 lpt)
    {
        float rmax = Mathf.Max(1f, ClothTopRotMax);
        for (int sd = 0; sd < 2; sd++)
        {
            if (_rotT[sd] < 0.5f) continue;   // 還在原位：交給原本的旋轉區抓取
            float tt = Mathf.Clamp(_rotA[sd] / rmax, 0f, 1f);
            float rs = Mathf.Clamp(StrapRotStart, 0.02f, 0.9f), ds = Mathf.Clamp(StrapDownStart, rs + 0.02f, 0.98f);
            float mvx = Ss(0f, rs, tt), rtk = Ss(rs, ds, tt), mvy = Ss(ds, Mathf.Clamp(StrapDownEnd, ds + 0.02f, 1f), tt);
            float sgn = sd == 0 ? -1f : 1f;
            Vector2 T = new Vector2(sgn * StrapOutPx * mvx + sgn * StrapSpreadPx(sd) * StrapTopSpreadK, StrapDownPx * mvy + StrapFollowPx(sd));
            Vector2 pv = sd == 0 ? StrapAnchorLeftPx : StrapAnchorRightPx;
            float ang = (sd == 0 ? -1f : 1f) * Mathf.DegToRad(_rotA[sd]) * rtk;   // 與 shader 的 ang_ 同號
            float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            for (int k = 0; k <= 6; k++)
            {
                Vector2 q = new Vector2(0f, -StrapHitLenPx * k / 6f);
                Vector2 o = T + pv + new Vector2(cs * q.X - sn * q.Y, sn * q.X + cs * q.Y);
                if (o.DistanceTo(lpt) < StrapHitRadiusPx) return sd;
            }
        }
        return -1;
    }

    [Export] public float StrapDownFollowK    = 0.7f;    // 肩帶跟著衣服往下的比例（0.4＝衣服每往下 1px，肩帶往下 0.4px；越小越慢、幅度越小）
    [Export] public float StrapOverMaxPx      = 300f;     // 拉到下拉上限後還能再「過拉」多少 px（中間出現往下移動的折疊褶皺）
    float[] _overT = new float[2], _overA = new float[2], _extraT = new float[2], _extraA = new float[2];
    float _mark2V = -1f;
    [Export] public float StrapNoShrinkBelowPx = 20f;   // 第三條線：橘線再往下幾 px，這條以下衣服不再內縮
    [Export] public float StrapMinBelowLine3Px = 30f;   // 肩帶最低可以到第三條線再往下幾 px
    float StrapClampRatio(int sd)   // 肩帶往下被洋紅線限制住之後，左右外擴也跟著停（避免肩帶往旁邊跑走）
    {
        float raw = (_stA[sd] + _overA[sd] + _extraA[sd]) * 3200f * StrapDownFollowK;
        return raw > 0.001f ? Mathf.Clamp(StrapFollowPx(sd) / raw, 0f, 1f) : 1f;
    }
    [Export] public float StrapOverCompK = 1.5f;   // 超過洋紅線之後，肩帶往中間補償的比例（左邊往右、右邊往左；0.5＝每超過 1px 往中間 0.5px）
    float StrapSpreadPx(int sd)   // 傳給肩帶 shader 的「外擴量」（px）：肩帶走到最低位置就整個凍結（外擴和補償都不再變），滑鼠繼續往下也不會再往內跑
    {
        float fol = StrapFollowPx(sd);
        float k = Mathf.Max(0.01f, StrapDownFollowK);
        float spread = Mathf.Min(_stA[sd] * 3200f, fol / k);   // 肩帶被擋住後，外擴量也停在當下
        float comp = 0f;
        if (_mark2V >= 0f)
        {
            float ay = (sd == 0 ? StrapAnchorLeftPx : StrapAnchorRightPx).Y;
            float follow0 = _mark2V * 3200f + StrapNoShrinkBelowPx - ay - StrapDownPx;   // 肩帶剛好走到洋紅線的位置
            float below = Mathf.Clamp(fol - follow0, 0f, Mathf.Max(0f, StrapMinBelowLine3Px));   // 洋紅線以下走了多少（到最低處就封頂）
            comp = below * StrapOverCompK / Mathf.Max(0.01f, StrapTopSpreadK);
        }
        return spread - comp;
    }
    float StrapMaxFollowPx(int sd)   // 肩帶最低能往下多少 px（洋紅線下面 StrapMinBelowLine3Px）；還沒有標註線時＝不限
    {
        if (_mark2V < 0f) return float.MaxValue;
        float ay = (sd == 0 ? StrapAnchorLeftPx : StrapAnchorRightPx).Y;
        return Mathf.Max(0f, _mark2V * 3200f + StrapNoShrinkBelowPx + StrapMinBelowLine3Px - ay - StrapDownPx);
    }
    float StrapDressMaxV(int sd)   // 衣服往下拉的總量上限（V）：肩帶到底了，衣服就跟著停
    {
        float m = StrapMaxFollowPx(sd);
        return m >= 1e8f ? float.MaxValue : m / (3200f * Mathf.Max(0.01f, StrapDownFollowK));
    }
    float StrapFollowPx(int sd)
    {
        float raw = (_stA[sd] + _overA[sd] + _extraA[sd]) * 3200f * StrapDownFollowK;
        if (_mark2V >= 0f)
        {
            float ay = (sd == 0 ? StrapAnchorLeftPx : StrapAnchorRightPx).Y;
            float mx = Mathf.Max(0f, _mark2V * 3200f + StrapNoShrinkBelowPx + StrapMinBelowLine3Px - ay - StrapDownPx);
            raw = Mathf.Min(raw, mx);
        }
        return raw;
    }
    [Export] public bool StrapDebugLog = true;   // 輸出視窗印出左右兩邊的旋轉／下拉數值（除錯用，不需要時關掉）
    float _dbgKey;
    [Export] public float StrapExtraDownPx    = 115f;     // 衣服原本能往下拉的最低位置，再多往下這麼多 px
    Vector2 _topExtU = Vector2.Zero;   // 衣服上緣左右兩端（U），開機掃描一次
    Vector2[] _fanCorner = { new Vector2(-1f, -1f), new Vector2(-1f, -1f) };   // 衣服上緣左端／右端的角落（畫布 px），開機掃描一次

    /// <summary>掃描衣服本體圖：上緣（每欄第一個不透明列）離最高點 40px 以內的範圍，左端＝最左上角、右端＝最右上角。</summary>
    void ScanDressCorners()
    {
        _fanCorner[0] = StrapAnchorLeftPx; _fanCorner[1] = StrapAnchorRightPx;
        if (!Layers.TryGetValue("衣服", out var c) || c is not TextureRect tr || tr.Texture == null) return;
        var img = tr.Texture.GetImage();
        if (img == null) return;
        if (img.IsCompressed()) img.Decompress();
        img.Convert(Image.Format.Rgba8);
        int w = img.GetWidth(), h = img.GetHeight();
        var d = img.GetData();
        var top = new int[w];
        int ymin = h;
        for (int x = 0; x < w; x++)
        {
            top[x] = -1;
            for (int y = 0; y < h; y++) if (d[(y * w + x) * 4 + 3] > 40) { top[x] = y; if (y < ymin) ymin = y; break; }
        }
        int xl = -1, xr = -1;
        for (int x = 0; x < w; x++) if (top[x] >= 0 && top[x] <= ymin + 40) { if (xl < 0) xl = x; xr = x; }
        if (xl < 0) return;
        float sx = c.Size.X / Mathf.Max(1, w), sy = c.Size.Y / Mathf.Max(1, h);
        _topExtU = new Vector2(xl * sx / 2000f, xr * sx / 2000f);
        int il = Mathf.Clamp(xl + (int)(StrapFanInsetPx / sx), xl, xr), ir = Mathf.Clamp(xr - (int)(StrapFanInsetPx / sx), xl, xr);
        if (top[il] < 0) il = xl;
        if (top[ir] < 0) ir = xr;
        _fanCorner[0] = new Vector2(il * sx, top[il] * sy);
        _fanCorner[1] = new Vector2(ir * sx, top[ir] * sy);
        GD.Print($"[Strap] 衣服左上角={_fanCorner[0]} 右上角={_fanCorner[1]}");
    }

    float[] _fanEdgeY = { -1f, -1f };   // 衣服本體在左/右肩帶 x 位置的「上緣」y（畫布 px），開機掃描一次
    [Export] public float StrapFanStartUpPx   = 10f;     // 褶皺起點在衣服上緣往上多少 px（被衣服透明處自動遮掉，避免起點落在衣服外而看不到）

    /// <summary>掃描衣服本體圖：在指定 x（畫布 px）由上往下找第一個不透明的列。</summary>
    float DressEdgeY(float xCanvas, float fallback)
    {
        if (!Layers.TryGetValue("衣服", out var c) || c is not TextureRect tr || tr.Texture == null) return fallback;
        var img = tr.Texture.GetImage();
        if (img == null) return fallback;
        if (img.IsCompressed()) img.Decompress();
        img.Convert(Image.Format.Rgba8);
        int w = img.GetWidth(), h = img.GetHeight();
        var d = img.GetData();
        float sx = c.Size.X / Mathf.Max(1, w), sy = c.Size.Y / Mathf.Max(1, h);
        int x0 = Mathf.Clamp((int)(xCanvas / sx), 0, w - 1);
        for (int y = 0; y < h; y++)
            for (int x = Mathf.Max(0, x0 - 6); x <= Mathf.Min(w - 1, x0 + 6); x++)
                if (d[(y * w + x) * 4 + 3] > 40) return y * sy;
        return fallback;
    }

    /// <summary>每幀（在 UpdateClothFollow 之後）把上半部旋轉角、身體上提、胸部位移同步給肩帶的剛體材質。</summary>
    void UpdateStrap()
    {
        if (_clothMat == null) return;
        if (_strap2Mat == null)
        {
            var sh = new Shader { Code = Strap2ShaderCode };
            _strap2Mat = new ShaderMaterial { Shader = sh };  _strap2Mat.SetShaderParameter("is_left", 1.0f);
            _strap2MatR = new ShaderMaterial { Shader = sh }; _strap2MatR.SetShaderParameter("is_left", 0.0f);
        }
        for (int pi = 0; pi < ClothPartLayers.Length; pi++)
        {
            var want = pi == 0 ? _strap2Mat : _strap2MatR;
            if (Layers.TryGetValue(ClothPartLayers[pi], out var part) && part != null && part.Material != want) part.Material = want;
        }

        if (!_strapAnchorDone)
        {
            _strapAnchorDone = true;
            if (StrapAutoAnchor)
            {
                StrapAnchorLeftPx  = StrapBottomAnchor("衣服/左帶", StrapAnchorLeftPx);
                StrapAnchorRightPx = StrapBottomAnchor("衣服/右帶", StrapAnchorRightPx);
                GD.Print($"[Strap] 錨點 左={StrapAnchorLeftPx} 右={StrapAnchorRightPx}");
            }
        }

        foreach (var n in _strap2Copy)
        {
            var v = _clothMat.GetShaderParameter(n);
            if (v.VariantType != Variant.Type.Nil) { _strap2Mat.SetShaderParameter(n, v); _strap2MatR.SetShaderParameter(n, v); }
        }
        var px = new Vector2(2000f, 3200f);
        var rotV = new Vector2(Mathf.DegToRad(_rotA[0]), Mathf.DegToRad(_rotA[1]));
        foreach (var m in new[] { _strap2Mat, _strap2MatR })
        {
            m.SetShaderParameter("rot_ang", rotV);
            m.SetShaderParameter("strap_de", StrapDownEnd);
            m.SetShaderParameter("strap_k", StrapTopSpreadK);
            m.SetShaderParameter("strap_sy", new Vector2(StrapFollowPx(0), StrapFollowPx(1)));
            m.SetShaderParameter("strap_st", new Vector2(StrapSpreadPx(0), StrapSpreadPx(1)));
            m.SetShaderParameter("anchor_l", StrapAnchorLeftPx / px);
            m.SetShaderParameter("anchor_r", StrapAnchorRightPx / px);
        float rmax = Mathf.Max(1f, ClothTopRotMax);
            m.SetShaderParameter("strap_t", new Vector2(Mathf.Clamp(_rotA[0] / rmax, 0f, 1f), Mathf.Clamp(_rotA[1] / rmax, 0f, 1f)));
            m.SetShaderParameter("strap_mv", new Vector4(StrapOutPx, StrapDownPx, StrapRotStart, StrapDownStart));
        }

        // 肩帶錨點目前位置（平移之後）→ 主衣服 shader 畫「花灑」褶皺（每條肩帶 2 條線）
        float rm2 = Mathf.Max(1f, ClothTopRotMax);
        float rs2 = Mathf.Clamp(StrapRotStart, 0.02f, 0.9f), ds2 = Mathf.Clamp(StrapDownStart, rs2 + 0.02f, 0.98f);
        var fa = new Vector4(0, 0, 0, 0); var ft = new Vector2();
        var cT = new Vector4(0, 0, 0, 0); var cP = new Vector4(0, 0, 0, 0);
        var bC = new Vector4(-10, -10, -10, -10); var bD = new Vector4(0, 0, 0, 0);
        for (int sd = 0; sd < 2; sd++)
        {
            float tt = Mathf.Clamp(_rotA[sd] / rm2, 0f, 1f);
            Vector2 T = new Vector2((sd == 0 ? -1f : 1f) * (StrapOutPx * Ss(0f, rs2, tt) + _stA[sd] * 3200f * StrapTopSpreadK), StrapDownPx * Ss(ds2, Mathf.Clamp(StrapDownEnd, ds2 + 0.02f, 1f), tt));
            // 褶皺起點：肩帶接在衣服上緣的位置（x 跟著肩帶、y＝衣服目前的上緣），不是肩帶最底端
            if (_fanCorner[0].X < 0f) ScanDressCorners();
            Vector2 an = new Vector2((_fanCorner[sd].X + (sd == 0 ? -1f : 1f) * _stA[sd] * 3200f * StrapTopSpreadK) / px.X, (_fanCorner[sd].Y - StrapFanStartUpPx) / px.Y + _stA[sd] + StrapFanDownPx / px.Y);   // 花灑褶皺只跟衣服（上緣往下、外擴），不再跟肩帶
            // 衣服角落綁肩帶底部（往上 StrapCornerUpPx）：位移＝肩帶的向外＋向下（拉伸／外擴衣服本身已經會處理）
            Vector2 anc = sd == 0 ? StrapAnchorLeftPx : StrapAnchorRightPx;
            Vector2 tc = new Vector2((sd == 0 ? -1f : 1f) * StrapOutPx * Ss(0f, rs2, tt), StrapDownPx * Ss(ds2, Mathf.Clamp(StrapDownEnd, ds2 + 0.02f, 1f), tt)) / px;
            Vector2 cp = new Vector2(anc.X, anc.Y - StrapCornerUpPx) / px;
            if (StrapBowProtect)
            {   // 蝴蝶結（左右上角）不變形：量出中心被整條變形鏈推到哪，範圍內整塊用同一個位移
                Vector2 uv0 = cp + tc + new Vector2(0f, _stA[sd] + _overA[sd]);
                Vector2 dd = ClothTopInv(uv0) - uv0 - tc;
                if (sd == 0) { bC.X = uv0.X; bC.Y = uv0.Y; bD.X = dd.X; bD.Y = dd.Y; } else { bC.Z = uv0.X; bC.W = uv0.Y; bD.Z = dd.X; bD.W = dd.Y; }
            }
            if (sd == 0) { cT.X = tc.X; cT.Y = tc.Y; cP.X = cp.X; cP.Y = cp.Y; } else { cT.Z = tc.X; cT.W = tc.Y; cP.Z = cp.X; cP.W = cp.Y; }
            if (sd == 0) { fa.X = an.X; fa.Y = an.Y; ft.X = Ss(rs2, 1f, tt); } else { fa.Z = an.X; fa.W = an.Y; ft.Y = Ss(rs2, 1f, tt); }
        }
        _clothMat.SetShaderParameter("bow_c", bC);
        _clothMat.SetShaderParameter("bow_d", bD);
        _clothMat.SetShaderParameter("bow_h", new Vector2(StrapBowHoldPx, StrapBowFadePx));
        _clothMat.SetShaderParameter("corner_t", StrapCornerBind ? cT : Vector4.Zero);
        _clothMat.SetShaderParameter("corner_p", cP);
        _clothMat.SetShaderParameter("corner_r", StrapCornerR);
        _clothMat.SetShaderParameter("corner_h", StrapCornerHoldPx);
        if (StrapDebugLog)
        {   // 除錯：左右兩邊的旋轉／下拉／過拉數值有變化才印一行（可貼給我看右邊為什麼沒有褶皺）
            float key = _rotA[0] + _rotA[1] * 3f + (_stA[0] + _overA[0]) * 3200f * 7f + (_stA[1] + _overA[1]) * 3200f * 11f;
            if (Mathf.Abs(key - _dbgKey) > 1.5f)
            {
                _dbgKey = key;
                GD.Print($"[StrapDbg] 旋轉 左={_rotA[0]:F0} 右={_rotA[1]:F0}  下拉px 左={_stA[0] * 3200f:F1} 右={_stA[1] * 3200f:F1}  過拉px 左={_overA[0] * 3200f:F1} 右={_overA[1] * 3200f:F1}");
            }
        }
        _clothMat.SetShaderParameter("fan_a", fa);
        _clothMat.SetShaderParameter("fan_t", ft);
        _clothMat.SetShaderParameter("fan_p", StrapFanParams);
        _clothMat.SetShaderParameter("mark_v", StrapShowDownMark ? ((StrapAnchorLeftPx.Y + StrapAnchorRightPx.Y) * 0.5f + StrapDownPx) / px.Y : -1f);
        {   // 第二條標註線（橘）：胸部綠點（兩顆）底部的位置；找不到綠點時退回「青線再往下 StrapShrinkMarkPx」
            float m2 = ((StrapAnchorLeftPx.Y + StrapAnchorRightPx.Y) * 0.5f + StrapDownPx + StrapShrinkMarkPx) / px.Y;
            if (StrapMark2AtChestDot && Layers.TryGetValue("衣服", out var clothM) && clothM != null && _hotspotImgs != null)
            {
                float sum = 0f; int cnt = 0;
                foreach (int hi in new[] { 2, 3 })
                {
                    if (hi >= _hotspotImgs.Length || _hotspotImgs[hi] == null) continue;
                    var hImg = _hotspotImgs[hi];   // 用兩邊的整體變換直接換算（不經過鏡頭縮放的反推），放大縮小鏡頭位置才不會跑
                    Vector2 bot = hImg.GetGlobalTransform() * new Vector2(hImg.Size.X * 0.5f, hImg.Size.Y);
                    Vector2 lp = clothM.GetGlobalTransform().AffineInverse() * bot;
                    sum += lp.Y / Mathf.Max(1f, clothM.Size.Y); cnt++;
                }
                if (cnt > 0) m2 = sum / cnt;
            }
            m2 += StrapMark2OffsetPx / px.Y;
            _mark2V = m2;
            _clothMat.SetShaderParameter("line3_v", m2 + StrapNoShrinkBelowPx / px.Y);
            _clothMat.SetShaderParameter("mark2_v", StrapShowDownMark ? m2 : -1f);
            _clothMat.SetShaderParameter("crease_s", StrapCreaseStepPx / 3200f);
            _clothMat.SetShaderParameter("shrink_v", m2);   // 橘線以下的衣服往中間靠（不論標註線有沒有顯示）
        }
        _clothMat.SetShaderParameter("fan_b", StrapFanBandPx + StrapFanStartUpPx);
    }
}
