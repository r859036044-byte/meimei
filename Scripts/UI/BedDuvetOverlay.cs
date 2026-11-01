using Godot;
using System;

/// <summary>
/// 床上被子疊層（兩種模式，各自一個節點）：
///  ‧ 蓋著模式（KickedMode=false）：角色的子節點，插在「前髮」前面（身體／衣服上面、前髮下面）。
///    上緣可以被抓下來：上緣 GrabFrac（預設 20%）被壓縮成折疊，而且 20%~InfluenceFrac（預設 50%）
///    的區域也會跟著被帶動，只是離上緣越遠影響越小（smoothstep 衰減）。
///    皺褶用 noise 做成不規則。
///    因為它在角色底下，會被「人物夜晚濾鏡」「人物光影濾鏡」一起染暗；CancelCharFilter 會在 shader 裡把這兩層抵銷掉，
///    讓被子跟床／背景一樣不受人物濾鏡影響。
///  ‧ 踢掉模式（KickedMode=true）：放在 _canvas 裡「背景／床」之後、「角色」之前（＝床上、所有人物下面）。
///    用新的踢掉被子圖；可以被滑鼠拖動；只顯示「床範圍外擴」以內的部分，裁切邊沿著床邊圓潤地忽大忽小，
///    被子和裁切邊相交的尖角會被磨圓，裁切邊補一圈黑色邊框線。
///    裁切用「距離場」做：床的透明圖、被子圖各算一次有號距離（貼圖 px），之後全部在 shader 裡即時調。
/// 兩個節點都每幀用「背景的全域變換 → 轉成相對於自己父節點的變換」對齊，不受角色縮放影響。
/// </summary>
public partial class BedDuvetOverlay : TextureRect
{
    public Control Target;
    public Func<int> StateSource;
    public Texture2D Covered;
    public Texture2D Kicked;

    /// <summary>true＝這個節點專門顯示「踢掉的被子」。</summary>
    public bool KickedMode;
    // ── 蓋著模式參數（由 TrainingRoomRuntime.UpdateDuvet() 每幀寫入，Inspector 可即時調）──
    public float OffsetY;          // 蓋著狀態時整體上下位移（背景本地 px，負＝往上）
    public float Bleach, Bright, TintAmt, TintU0 = 0f, TintU1 = 1f;
    public Color Tint = new Color(1f, 0.8f, 0.88f);
    public Color BlueCol = new Color(0.35f, 0.42f, 0.75f);   // 左側深藍色（色彩增值）
    public float BlueAmt, BlueAmtR, BlueU0 = 0f, BlueU1 = 0.5f, BlueV0 = 0.5f;
    public Color LtCol = new Color(0.45f, 0.55f, 0.95f); public float LtAmt = 0.3f, LtRange = 0.3f, KickedTopK = 0.4f;   // 左邊＋上面再疊一層偏藍的色彩增值
    bool _ltScanned; Vector4 _ltBox = new Vector4(0f, 0f, 1f, 1f);
    public float LtBotAmt = 0.2f, LtTopRight = 0.3f, LtTopK = 0.5f;
    public Color DgCol = new Color(0.49f, 0.50f, 0.83f); public float DgAmt = 0.5f, DgPos = 0.7f, DgSoft = 0.04f, DgAngleDeg = 10f, DgShiftPx = 50f;   // 左上角斜切變暗
    public Color BlueColBot = new Color(0.12f, 0.16f, 0.40f), KickedBlueColBot = new Color(0.14f, 0.18f, 0.42f);
    public float FoldTarget;       // 折疊進度 0~1（1＝抓滿 GrabFrac）
    public bool  FoldSnap;         // true＝直接跟手；false＝平滑回彈
    public float FoldShadow = 0.35f, FoldWaves = 3f;
    public float FoldIrregular = 0.6f;   // 皺褶／折疊邊緣的不規則程度 0~1（0＝跟以前一樣規則）
    public float Fold { get; private set; }
    public float FoldLineWidth = 0.45f;                     // 皺褶線條粗細
    public Color FoldLineColor = new Color(0.18f, 0.13f, 0.40f);   // 皺褶線條顏色（深藍紫）
    public float BreathPx, BreathWide; public Vector4 BreathC = new Vector4(0.5f, 0.5f, 0.25f, 0.2f);   // 呼吸（由 TrainingRoomRuntime 每幀寫入）
    public float FadeInTime = 0.12f, FadeOutTime = 0.35f;    // 切換（蓋著⇄踢掉）時：快速淡入、較慢淡出（秒）
    float _fade;                   // 目前淡入淡出進度 0~1
    bool _fadeInit;
    // 皺褶線條的拉扯紀錄：每次新的拉扯（BeginPull）各一組（x＝被拉的點 U、y＝拉的程度、z＝隨機種子），
    // 最多保留 4 組；最新那組的程度跟著目前的折疊進度走，舊的停在被取代時的程度（線條留著，不被新的蓋掉）
    readonly Vector4[] _pulls = new Vector4[4];
    int _pullCount;
    static readonly string[] PlNames = { "pl0", "pl1", "pl2", "pl3" };

    /// <summary>開始一次新的拉扯（在被抓的 U 位置）：新增一組皺褶線條，舊的保留。</summary>
    public void BeginPull(float u)
    {
        if (_pullCount == _pulls.Length)
        {
            for (int i = 1; i < _pulls.Length; i++) _pulls[i - 1] = _pulls[i];
            _pullCount--;
        }
        _pulls[_pullCount++] = new Vector4(u, Fold, GD.Randf() * 40f + 1f, 0f);
    }
    public void ClearPulls() { _pullCount = 0; for (int i = 0; i < _pulls.Length; i++) _pulls[i] = Vector4.Zero; }

    public float PullX = 0.5f;     // 被抓住的點（U，0~1）
    public float PullW = 0.22f;    // 往左右影響的寬度（U）
    public float PullLean = 0.5f;  // 左右往被拉的點傾斜的程度
    float _pullCur = 0.5f;

    // 抵銷「人物夜晚濾鏡＋人物光影濾鏡」（被子是角色的子節點，會被一起染暗）
    public bool CancelCharFilter;
    public ShaderMaterial CharShadeMat;           // 人物光影群組的材質（讀它的參數來算出一模一樣的乘色）
    public Color NightMul = Colors.White;         // 人物夜晚濾鏡的乘色（_charRoot.Modulate）

    // ── 踢掉模式參數 ──
    public Texture2D BedMask;                       // 床的透明圖（alpha＝床的範圍），裁切用
    public float ClipPx = 5f;                       // 超出床多少「貼圖 px」以外裁掉（平均值）
    public float ClipIrregular = 0.8f;              // 裁切邊波動程度 0~1（0＝整齊貼著床邊）
    public float ClipWavePx = 12f;                  // 裁切距離的波動幅度（貼圖 px；±）
    public float ClipWaveLength = 90f;              // 波動的波長（貼圖 px，越大越圓緩）
    public float RoundPx = 14f;                     // 被子和裁切邊相交處的磨圓半徑（貼圖 px）
    public float OutlinePx = 4.5f;                  // 裁切邊黑框線的粗細（貼圖 px）
    public Color OutlineColor = new Color(0f, 0f, 0f, 1f);
    public Vector2 Drag;                            // 拖曳位移（UV；1＝整張背景寬／高）
    public bool DragSnap;                           // true＝直接跟手；false＝平滑回到 Drag
    public Vector2 DragCur { get; private set; }    // 目前實際顯示的位移
    public float KickedScale = 0.7f;                // 踢掉的被子縮放（1＝原圖大小）
    public Vector2 KickedPivot = new Vector2(0.33f, 1.0f);    // 縮放中心（UV）：y＝1 是被子底邊，縮放時底邊不動
    public Color KickedBlueCol = new Color(0.35f, 0.42f, 0.75f);   // 踢掉的被子左邊／下面的深藍色（色彩增值）
    public float KickedBlueCornerAmt = 0.5f, KickedBlueCornerRange = 0.5f;   // 左上角額外加強的強度／範圍
    public float KickedBlueAmt = 0.5f, KickedBlueRange = 0.45f;    // 最邊緣強度／往內漸層範圍（佔被子寬高）
    Vector4 _bedBox = new Vector4(0f, 0f, 1f, 1f);                // 床的範圍（UV）
    Vector4 _kbox = new Vector4(0f, 0f, 1f, 1f);                  // 被子內容在原圖上的範圍（UV）
    public Vector2 OffsetPx = new Vector2(100f, 50f);         // 踢掉的被子基本位置偏移（貼圖 px；右、下為正）

    /// <summary>OffsetPx 換算成 UV。</summary>
    public Vector2 BaseOffsetUV => Kicked != null
        ? OffsetPx / new Vector2(Math.Max(Kicked.GetWidth(), 1), Math.Max(Kicked.GetHeight(), 1))
        : Vector2.Zero;

    /// <summary>畫面上的 UV → 踢掉被子原圖上的 UV（扣掉位移、還原縮放），點擊判定用。</summary>
    public Vector2 SrcUV(Vector2 uv) =>
        (uv - DragCur - BaseOffsetUV - KickedPivot) / Mathf.Max(KickedScale, 0.01f) + KickedPivot;

    /// <summary>蓋著的被子「上緣內容」所在的 V 與內容高度（V）；由 Setup 從貼圖算出。</summary>
    public float TopV { get; private set; }
    public float BandV { get; private set; }
    ShaderMaterial _mat;

    // 人物光影群組 shader 的參數名稱（照抄它的值，才能算出完全一樣的乘色去抵銷）
    static readonly string[] CharShadeParamNames =
    {
        "char_rect", "top_enabled", "top_height", "top_softness", "top_tilt", "top_offset", "top_color",
        "right_enabled", "right_start", "right_softness", "right_color",
    };

    const string CoveredShader = @"
shader_type canvas_item;
uniform float fold_d = 0.0;      // 上緣被拉下的距離（V）
uniform float top_v = 0.0;       // 被子內容最上緣（V）
uniform float band_v = 0.2;      // 可抓取帶高度（V）
uniform float infl_v = 0.5;      // 影響範圍高度（V）：從上緣起算，越遠影響越小
uniform float fold_shadow = 0.35;
uniform float fold_waves = 3.0;
uniform float fold_irreg = 0.6;
uniform float fade = 1.0;        // 切換用的淡入淡出（0~1）
uniform float aspect = 1.87;     // 被子寬／高（算放射皺褶的角度用）
uniform vec3 crease_col = vec3(0.18, 0.13, 0.40);   // 皺褶線條顏色（深藍紫）
uniform float crease_w = 1.2;    // 皺褶線條粗細（約等於螢幕像素）
uniform vec4 pl0 = vec4(0.0);    // 每次拉扯的線條組：x＝被拉的點(U)、y＝拉的程度、z＝隨機種子
uniform vec4 pl1 = vec4(0.0);
uniform vec4 pl2 = vec4(0.0);
uniform vec4 pl3 = vec4(0.0);
uniform float pull_x = 0.5;      // 被抓住的點（U）
uniform float br_d = 0.0;        // 呼吸：胸口處被子往上鼓起的量（V，可正可負）
uniform float br_w = 0.0;        // 呼吸：鼓起時左右微微撐寬的比例
uniform vec4 br_c = vec4(0.5, 0.5, 0.25, 0.2);   // 呼吸中心 xy（UV）、半徑 zw（UV）
uniform float pull_w = 0.22;     // 往左右影響的寬度（U）
uniform float pull_lean = 0.5;   // 左右往被拉的點收攏、傾斜的程度
uniform float bleach = 0.0;
uniform float bright = 0.0;
uniform vec3 tint = vec3(1.0, 0.8, 0.88);
uniform float tint_amt = 0.0;
uniform float tint_u0 = 0.0;   // 粉紅濾鏡漸層：這個 U（貼圖左右位置）以左完全沒有
uniform float tint_u1 = 1.0;   // 這個 U 以右達到完整強度
uniform vec3 blue_col = vec3(0.35, 0.42, 0.75);   // 左側深藍色（色彩增值＝相乘）
uniform vec3 blue_col_bot = vec3(0.12, 0.16, 0.40);
uniform float blue_v0 = 0.5;
uniform vec3 lt_col = vec3(0.45, 0.55, 0.95);
uniform float lt_amt = 0.0;
uniform float lt_range = 0.3;
uniform vec3 dg_col = vec3(0.49, 0.50, 0.83);
uniform float dg_amt = 0.0;
uniform float dg_pos = 0.7;
uniform float dg_soft = 0.04;
uniform vec2 dg_n = vec2(1.0, 1.0);
uniform float dg_shift_px = 0.0;
uniform float lt_bot_amt = 0.0;
uniform float lt_top_r = 0.3;
uniform float lt_top_k = 0.5;
uniform vec4 lt_box = vec4(0.0, 0.0, 1.0, 1.0);   // 被子本體在貼圖裡的範圍（U/V，自動掃 alpha）
uniform float blue_amt = 0.0;    // 深藍增值強度（最左邊）
uniform float blue_amt_r = 0.0;  // 深藍增值強度（最右邊）：整張被子都有，左右之間漸層
uniform float blue_u0 = 0.0;     // 這個 U 以左＝左邊強度
uniform float blue_u1 = 0.5;     // 這個 U 以右＝右邊強度

// ── 抵銷人物濾鏡（參數照抄人物光影群組的 shader）──
uniform float cancel_on = 0.0;
uniform vec3 night_mul = vec3(1.0);
uniform vec2 s_org = vec2(0.0);
uniform vec2 s_bx = vec2(1.0, 0.0);
uniform vec2 s_by = vec2(0.0, 1.0);
uniform vec4 char_rect = vec4(0.0, 0.0, 1.0, 1.0);
uniform bool top_enabled = true;
uniform float top_height = 0.10;
uniform float top_softness = 0.01;
uniform float top_tilt = -0.12;
uniform float top_offset = 0.0;
uniform vec4 top_color : source_color = vec4(0.62, 0.56, 0.76, 1.0);
uniform bool right_enabled = true;
uniform float right_start = 0.5;
uniform float right_softness = 0.45;
uniform vec4 right_color : source_color = vec4(0.84, 0.82, 0.93, 1.0);

varying vec4 vc;
void vertex() { vc = COLOR; }

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}
float vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}
float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++) {
        v += a * vnoise(p);
        p = p * 2.03 + vec2(17.1, 9.2);
        a *= 0.5;
    }
    return v;
}
// 位移權重：上緣＝1，往下平滑衰減到影響範圍終點＝0
float wf(float t, float top, float L) {
    return 1.0 - smoothstep(top, top + L, t);
}

// 一組拉扯留下的放射皺褶線（固定在布料上：用貼圖座標，不跟著目前的變形跑）
float crease_set(vec4 pl, float x, float y) {
    float sd = pl.z;
    vec2 rel = vec2((x - pl.x) * aspect, y - (top_v + 0.01));
    float rl = length(rel);
    float ang = atan(rel.x, rel.y + 0.0001);
    float cr = vnoise(vec2(ang * fold_waves * 2.2 + sd, rl * 4.0 + sd * 0.37)) * 0.65
             + vnoise(vec2(ang * fold_waves * 4.7 + sd * 1.9, rl * 7.0 - sd)) * 0.35;
    float gd = abs(cr - 0.5);
    float aa = max(fwidth(cr), 0.0004);                      // 用螢幕微分讓線寬固定，不隨雜訊斜率忽粗忽細
    float ln = 1.0 - smoothstep(aa * crease_w * 0.5, aa * crease_w, gd);
    float soft = 1.0 - smoothstep(0.0, aa * crease_w * 6.0, gd);   // 線條兩側一圈很淡的漸層（一點點就好）
    float seg = smoothstep(0.30, 0.62, vnoise(vec2(ang * 3.0 + sd * 0.7, rl * 9.0)));   // 線段斷斷續續，長短不一
    float reach = pl.y * band_v * 2.2;                        // 拉得越多，線條延伸越遠
    float rmask = 1.0 - smoothstep(reach * 0.55, max(reach, 0.0001), rl);
    float wmask = 1.0 - smoothstep(0.0, max(pull_w, 0.01) * 1.3, abs(x - pl.x));
    return (ln + soft * 0.16) * seg * rmask * smoothstep(0.0, 0.5, wmask) * smoothstep(0.03, 0.25, pl.y);
}

void fragment() {
    float br_g_ = exp(-pow((UV.x - br_c.x) / br_c.z, 2.0) - pow((UV.y - br_c.y) / br_c.w, 2.0));   // 胸口附近最強、往外自然衰減
    float ys = UV.y + br_d * br_g_;
    float y = ys;
    float x = UV.x - (UV.x - br_c.x) * br_w * br_g_;
    float shade = 1.0;
    float edge = 0.0;
    bool cut = false;
    float crease_k = 0.0;   // 皺褶線條的濃度
    if (fold_d > 0.0001) {
        float L = max(infl_v, 0.001);
        // 只有被抓住的點附近往下拉：左右用圓滑的鐘形衰減，上緣自然往被拉的點傾斜（不是整片往下平移）
        float wxp = 1.0 - smoothstep(0.0, max(pull_w, 0.01), abs(UV.x - pull_x));
        float k = clamp((fbm(vec2(UV.x * 3.0, 1.3)) - 0.44) * 2.0, -1.0, 1.0);
        float dx = max(fold_d * wxp * (1.0 + fold_irreg * 0.45 * k), 0.0);
        float prog = clamp(dx / band_v, 0.0, 1.0);   // 這一欄被拉的程度
        // 新上緣邊緣參差
        float rag = (vnoise(vec2(UV.x * 16.0, 7.7)) - 0.5) * fold_irreg * band_v * 0.35 * prog;
        float start = top_v + dx + rag;
        if (ys < start) {
            cut = true;
        } else if (ys < top_v + L) {
            // 整塊被子「上面被往下拉 dx、往下影響隨距離衰減」：螢幕 y = 貼圖 y + dx * 權重(貼圖 y)
            // 用二分法反推該取貼圖的哪一列
            float lo = top_v;
            float hi = ys;
            for (int i = 0; i < 10; i++) {
                float mid = 0.5 * (lo + hi);
                float f = mid + dx * wf(mid, top_v, L);
                if (f < ys) lo = mid; else hi = mid;
            }
            y = 0.5 * (lo + hi);
            float u = clamp((y - top_v) / L, 0.0, 1.0);
            float wloc = 1.0 - smoothstep(0.0, 1.0, u);
            float comp = clamp(dx * 6.0 * u * (1.0 - u) / L, 0.0, 1.0);   // 這一帶被壓縮的程度
            // 邊緣輕微左右扭動
            x += (fbm(vec2(y * 6.0, 3.3)) - 0.44) * 0.014 * fold_irreg * prog * wloc;
            // 左右的布料被往被拉的點收攏（取樣點往離開拉點的方向移）→ 兩側向拉點傾斜
            x -= (pull_x - UV.x) * pull_lean * 0.5 * prog * wloc;
            // 簡單的皺褶線條：每次拉扯各自一組（種子不同），線條固定在布料上，舊的會留著、不會被新的蓋掉；
            // 最多同時保留 4 組，重疊處取最大值（不會越疊越黑）
            float fade_y = 1.0 - smoothstep(0.0, 1.0, u);          // 越往下越淡
            float top_gap = smoothstep(band_v * 0.12, band_v * 0.45, ys - start);   // 最上面（新上緣附近）不畫線
            float cs = max(max(crease_set(pl0, x, y), crease_set(pl1, x, y)), max(crease_set(pl2, x, y), crease_set(pl3, x, y)));
            crease_k = cs * fade_y * top_gap * fold_shadow;
        }
        edge = (1.0 - smoothstep(0.0, band_v * 0.15, ys - start)) * prog;   // 新上緣的亮邊
    }
    vec4 c = texture(TEXTURE, vec2(x, y));
    vec3 rgb = c.rgb * shade + vec3(0.12) * edge;
    float lum = dot(rgb, vec3(0.299, 0.587, 0.114));
    rgb = mix(rgb, vec3(lum), bleach);          // 漂白：降低飽和（去紫）
    rgb = rgb + bright * (vec3(1.0) - rgb);     // 提亮
    float blue_k = mix(blue_amt, blue_amt_r, smoothstep(blue_u0, blue_u1, UV.x));   // 整張都有，左邊較濃、往右漸層變淡
    blue_k = max(blue_k, blue_amt * smoothstep(blue_v0, 1.0, UV.y));   // 下面也加強，右上較亮
    rgb = mix(rgb, rgb * mix(blue_col, blue_col_bot, smoothstep(0.0, 1.0, UV.y)), blue_k);
    vec2 lt_sz = max(lt_box.zw - lt_box.xy, vec2(0.001));
    vec2 lt_q = (UV - lt_box.xy) / lt_sz;
    float lt_l = 1.0 - smoothstep(0.0, max(lt_range, 0.01), lt_q.x);
    float lt_t = (1.0 - smoothstep(0.0, max(lt_range, 0.01), lt_q.y)) * lt_top_k * mix(1.0, lt_top_r, smoothstep(0.6, 1.0, lt_q.x));   // 上面往右越來越淡（右上角減弱）
    float lt_m = 1.0 - (1.0 - lt_l) * (1.0 - lt_t);
    float lt_b = smoothstep(1.0 - max(lt_range, 0.01), 1.0, lt_q.y);
    rgb = mix(rgb, rgb * lt_col, lt_bot_amt * lt_b);   // 底部再加一層藍
    rgb = mix(rgb, rgb * lt_col, lt_amt * lt_m);   // 左邊＋上面偏藍增值                    // 深藍色「色彩增值」（Multiply）
    float tint_g = smoothstep(tint_u0, tint_u1, UV.x);   // 右邊最濃、往左慢慢變淡
    // 粉紅濾鏡改用「覆蓋（Overlay）」混合：暗部相乘、亮部螢幕，比單純相乘更通透、不會變髒變暗
    vec3 ov = mix(2.0 * rgb * tint, vec3(1.0) - 2.0 * (vec3(1.0) - rgb) * (vec3(1.0) - tint), step(vec3(0.5), rgb));
    rgb = mix(rgb, ov, tint_amt * tint_g);               // 右邊最濃、往左漸層

    rgb = mix(rgb, crease_col, clamp(crease_k, 0.0, fold_shadow));   // 線條最濃不超過 fold_shadow（＝不透明度上限）   // 皺褶線條（在濾鏡之後上色，顏色才不會被漂白／提亮洗掉）

    float dg_m = 1.0 - smoothstep(dg_pos - dg_soft, dg_pos + dg_soft, dot(lt_q + vec2(dg_shift_px / (lt_sz.x * float(textureSize(TEXTURE, 0).x)), 0.0), dg_n));   // 左上角斜切：斜線左上＝變暗
    float dg_lum = dot(dg_col, vec3(0.299, 0.587, 0.114));
    rgb = mix(rgb, rgb * mix(vec3(dg_lum), dg_col, 0.25), dg_amt * dg_m);   // 單純變暗（只帶一點顏色），不是染色
    if (cancel_on > 0.5) {
        // 被子在人物群組裡，會被「夜晚乘色」和「光影群組 shader」乘上一層；先除回去，最後出來就是原色
        vec2 suv = s_org + s_bx * UV.x + s_by * UV.y;
        vec2 cu = (suv - char_rect.xy) / max(char_rect.zw - char_rect.xy, vec2(0.0001));
        vec3 gm = vec3(1.0);
        if (top_enabled) {
            float te = top_height + top_offset + top_tilt * (cu.x - 0.5);
            float tt = 1.0 - smoothstep(te - top_softness, te + top_softness, cu.y);
            gm *= mix(vec3(1.0), top_color.rgb, tt);
        }
        if (right_enabled) {
            float rt = 1.0 - smoothstep(right_start - max(right_softness, 0.0001), right_start, cu.x);
            gm *= mix(vec3(1.0), right_color.rgb, rt);
        }
        rgb = rgb / max(gm * night_mul, vec3(0.05));
    }
    COLOR = vec4(rgb, (cut ? 0.0 : c.a) * fade) * vc;   // Godot 的 fragment 不能用 return，改用旗標把被拉走的上緣變透明
}
";

    const string KickedShader = @"
shader_type canvas_item;
uniform sampler2D sdf_bed : filter_linear;      // 床的有號距離（R8：0.5＝邊界，往上＝床裡面）
uniform sampler2D sdf_duvet : filter_linear;    // 被子的有號距離
uniform float has_mask = 0.0;
uniform float fade = 1.0;                       // 切換用的淡入淡出（0~1）
uniform vec4 vbox = vec4(0.0, 0.0, 1.0, 1.0);  // 被子「畫面上看得到的範圍」（背景 UV：左上 xy、右下 zw；已扣掉床的裁切、被切出畫面的部分）
uniform vec3 kblue_col = vec3(0.35, 0.42, 0.75);   // 左邊／下面的深藍色（色彩增值）
uniform vec3 kblue_col_bot = vec3(0.14, 0.18, 0.42);
uniform vec3 lt_col = vec3(0.45, 0.55, 0.95);
uniform float lt_amt = 0.0;
uniform float lt_range = 0.3;
uniform vec3 dg_col = vec3(0.49, 0.50, 0.83);
uniform float dg_amt = 0.0;
uniform float dg_pos = 0.7;
uniform float dg_soft = 0.04;
uniform vec2 dg_n = vec2(1.0, 1.0);
uniform float dg_shift_px = 0.0;
uniform float kblue_top_k = 0.4;
uniform float kblue_amt = 0.0;                  // 強度（最邊緣）
uniform float kblue_range = 0.45;               // 從邊緣往內漸層的範圍（佔被子寬／高的比例）
uniform float kblue_corner_amt = 0.0;           // 左上角額外加強的強度
uniform float kblue_corner_range = 0.5;         // 左上角加強的範圍（佔被子寬／高的比例）
uniform float sdf_cap = 32.0;                  // 距離場編碼的範圍（±貼圖 px）
uniform vec2 drag_uv = vec2(0.0);               // 被子位移（UV）
uniform float kscale = 0.7;                     // 被子縮放
uniform vec2 kpivot = vec2(0.33, 1.0);          // 縮放中心（UV）
uniform float clip_px = 5.0;                    // 超出床幾 px 以外裁掉（平均）
uniform float clip_irreg = 0.8;                 // 裁切邊波動程度 0~1
uniform float clip_wave_px = 12.0;              // 波動幅度（±px）
uniform float clip_wave_len = 90.0;             // 波動波長（px）
uniform float round_px = 14.0;                  // 相交尖角磨圓半徑（px）
uniform float outline_px = 4.5;                 // 裁切邊黑框線粗細
uniform vec4 outline_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform vec2 tex_px = vec2(1716.0, 916.0);
varying vec4 vc;
void vertex() { vc = COLOR; }

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}
float vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void fragment() {
    vec2 uv = (UV - drag_uv - kpivot) / max(kscale, 0.01) + kpivot;
    vec2 cuv = clamp(uv, vec2(0.0), vec2(1.0));
    vec4 c = texture(TEXTURE, cuv);

    // 被子的有號距離（畫面 px）；超出原圖範圍的地方視為外面，並連續往外變負
    float d1 = (texture(sdf_duvet, cuv).r - 0.5) * 2.0 * sdf_cap * kscale;
    float ov = length((uv - cuv) * tex_px) * kscale;   // 超出原圖多遠（原圖範圍內＝0）
    if (ov > 0.0) d1 = min(d1, -ov);                   // 只有超出原圖範圍才套用；範圍內不能 min，否則整張被子的距離會被壓成 0＝半透明

    float d2 = 1000.0;
    if (has_mask > 0.5) {
        // 裁切距離沿著床邊圓緩地忽大忽小：兩層平滑 noise，波長 clip_wave_len
        float n = vnoise(UV * tex_px / max(clip_wave_len, 4.0)) * 0.7
                + vnoise(UV * tex_px / max(clip_wave_len * 0.4, 4.0) + 7.3) * 0.3;
        float r = clip_px + clip_irreg * clip_wave_px * (2.0 * n - 1.0);
        d2 = (texture(sdf_bed, UV).r - 0.5) * 2.0 * sdf_cap + r;   // 正＝在（床＋裁切距離）範圍內
    }

    // 圓角交集（smooth min）：被子外形和裁切邊相交的尖角會被磨圓
    float rr = max(round_px, 0.5);
    float h = max(rr - abs(d1 - d2), 0.0) / rr;
    float dd = min(d1, d2) - h * h * rr * 0.25;
    float a = smoothstep(-0.7, 0.7, dd);
    float ca = max(c.a, step(0.5, c.a));

    // 裁切邊內側一圈黑框線
    float line = a * (1.0 - smoothstep(outline_px - 0.8, outline_px + 0.8, d2));
    // 左邊和下面的深藍「色彩增值」漸層：以畫面上真正看得到的範圍（vbox）的左邊、下面為準，才看得到
    vec2 bsz = max(vbox.zw - vbox.xy, vec2(0.001));
    float gl = 1.0 - smoothstep(0.0, max(kblue_range, 0.01), (UV.x - vbox.x) / bsz.x);
    float gb = smoothstep(1.0 - max(kblue_range, 0.01), 1.0, (UV.y - vbox.y) / bsz.y);
    float gk = 1.0 - (1.0 - gl) * (1.0 - gb);
    // 左上角再額外加強：從畫面上看得到的範圍的左上角往外圓緩漸層
    vec2 qc = (UV - vbox.xy) / bsz;
    float gc = 1.0 - smoothstep(0.0, max(kblue_corner_range, 0.01), length(qc));
    float tf = mix(kblue_top_k, 1.0, smoothstep(0.0, 0.3, (UV.y - vbox.y) / bsz.y));   // 上面整段（含左上、左上角）的色彩增值比例
    float kk = clamp((kblue_amt * gk + kblue_corner_amt * gc) * tf, 0.0, 1.0);
    vec3 kcol = mix(kblue_col, kblue_col_bot, clamp((UV.y - vbox.y) / bsz.y, 0.0, 1.0));
    vec3 base = mix(c.rgb, c.rgb * kcol, kk);
    float ltl = (1.0 - smoothstep(0.0, max(lt_range, 0.01), (UV.x - vbox.x) / bsz.x)) * tf;
    float ltt = (1.0 - smoothstep(0.0, max(lt_range, 0.01), (UV.y - vbox.y) / bsz.y));
    base = mix(base, base * lt_col, lt_amt * (1.0 - (1.0 - ltl) * (1.0 - ltt)));   // 左邊＋上面偏藍增值
    float dgm = 1.0 - smoothstep(dg_pos - dg_soft, dg_pos + dg_soft, dot(qc + vec2(dg_shift_px / (bsz.x * tex_px.x), 0.0), dg_n));   // 左上角斜切：斜線左上＝變暗
    float dg_lum = dot(dg_col, vec3(0.299, 0.587, 0.114));
    base = mix(base, base * mix(vec3(dg_lum), dg_col, 0.25), dg_amt * dgm);   // 單純變暗（只帶一點顏色），不是染色
    vec3 rgb = mix(base, outline_color.rgb, line * outline_color.a);
    COLOR = vec4(rgb, a * ca * fade) * vc;
}
";

    public override void _Ready()
    {
        _mat = new ShaderMaterial { Shader = new Shader { Code = KickedMode ? KickedShader : CoveredShader } };
        Material = _mat;
    }

    public void Setup()
    {
        // 從蓋著貼圖算出被子「真正的」上下緣：只看夠多不透明像素的那幾列。
        // 不能用 GetUsedRect——貼圖上方有零星的極淡雜點，會把上緣誤判到很高的地方，
        // 結果「上緣 20%」落在一片空白裡，永遠抓不到。
        if (Covered == null) return;
        var img = Covered.GetImage();
        if (img == null) return;
        if (img.IsCompressed()) img.Decompress();
        img.Convert(Image.Format.Rgba8);
        int w = img.GetWidth(), h = img.GetHeight();
        var data = img.GetData();
        int minCount = Math.Max(8, w / 40);
        int top = -1, bottom = -1;
        for (int y = 0; y < h; y++)
        {
            int cnt = 0;
            int row = y * w * 4;
            for (int x = 0; x < w; x++) if (data[row + x * 4 + 3] > 128) cnt++;
            if (cnt >= minCount) { if (top < 0) top = y; bottom = y; }
        }
        if (top < 0) { top = 0; bottom = h - 1; }
        TopV = top / (float)h;
        BandV = (bottom - top + 1) / (float)h;
    }

    public void SetGrabFraction(float frac) => _bandFrac = Mathf.Clamp(frac, 0.02f, 1f);
    /// <summary>影響範圍（佔被子內容高度）：抓取帶以外、一直到這裡，影響隨距離遞減到 0。</summary>
    public void SetInfluenceFraction(float frac) => _inflFrac = Mathf.Clamp(frac, 0.05f, 1f);
    float _bandFrac = 0.2f;
    float _inflFrac = 0.5f;
    /// <summary>抓取帶高度（V）＝內容高度 × GrabFrac。</summary>
    public float GrabBandV => BandV * _bandFrac;
    /// <summary>影響範圍高度（V）。至少是抓取帶的 2.5 倍，避免拉太多時被子摺到自己身上（折回去）。</summary>
    public float InfluenceV => BandV * Mathf.Max(_inflFrac, _bandFrac * 2.5f);

    // ── 距離場（踢掉模式）──────────────────────────────────────────
    const float SdfCap = 32f;          // 編碼範圍 ±32 貼圖 px（R8：0.5＝邊界）
    bool _sdfTried;
    ImageTexture _sdfBed, _sdfDuvet;

    void EnsureSdfs()
    {
        if (_sdfTried || BedMask == null || Kicked == null) return;
        _sdfTried = true;
        try
        {
            _sdfBed = BuildSdf(BedMask);
            _sdfDuvet = BuildSdf(Kicked);
            _kbox = AlphaBounds(Kicked);
            _bedBox = AlphaBounds(BedMask);
        }
        catch (Exception ex) { GD.PushWarning("[Duvet] 距離場建立失敗：" + ex.Message); _sdfBed = null; _sdfDuvet = null; }
    }

    /// <summary>貼圖不透明內容的外框（UV：x0,y0,x1,y1）。</summary>
    static Vector4 AlphaBounds(Texture2D tex)
    {
        var img = tex.GetImage();
        if (img == null) return new Vector4(0f, 0f, 1f, 1f);
        if (img.IsCompressed()) img.Decompress();
        img.Convert(Image.Format.Rgba8);
        int w = img.GetWidth(), h = img.GetHeight();
        var data = img.GetData();
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (data[(y * w + x) * 4 + 3] > 128) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
        if (x1 < 0) return new Vector4(0f, 0f, 1f, 1f);
        return new Vector4(x0 / (float)w, y0 / (float)h, (x1 + 1) / (float)w, (y1 + 1) / (float)h);
    }

    static ImageTexture BuildSdf(Texture2D tex)
    {
        var img = tex.GetImage();
        if (img == null) return null;
        if (img.IsCompressed()) img.Decompress();
        img.Convert(Image.Format.Rgba8);
        int w = img.GetWidth(), h = img.GetHeight();
        var data = img.GetData();
        var inside = new bool[w * h];
        for (int i = 0; i < inside.Length; i++) inside[i] = data[i * 4 + 3] > 128;
        float[] dOut = Edt(inside, w, h, true);    // 外面的像素：到最近「裡面」像素的距離
        float[] dIn = Edt(inside, w, h, false);    // 裡面的像素：到最近「外面」像素的距離
        var bytes = new byte[w * h];
        for (int i = 0; i < bytes.Length; i++)
        {
            float s = inside[i] ? dIn[i] - 0.5f : -(dOut[i] - 0.5f);
            s = Mathf.Clamp(s, -SdfCap, SdfCap);
            bytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt((s / SdfCap * 0.5f + 0.5f) * 255f), 0, 255);
        }
        var si = Image.CreateFromData(w, h, false, Image.Format.R8, bytes);
        return ImageTexture.CreateFromImage(si);
    }

    /// <summary>歐幾里得距離轉換（Felzenszwalb）。featureIsInside＝true 時，特徵點是「裡面」的像素。</summary>
    static float[] Edt(bool[] inside, int w, int h, bool featureIsInside)
    {
        const float INF = 1e12f;
        var g = new float[w * h];
        for (int i = 0; i < g.Length; i++) g[i] = inside[i] == featureIsInside ? 0f : INF;
        int m = Math.Max(w, h);
        var f = new float[m]; var d = new float[m]; var v = new int[m]; var z = new float[m + 1];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++) f[y] = g[y * w + x];
            Edt1D(f, h, d, v, z);
            for (int y = 0; y < h; y++) g[y * w + x] = d[y];
        }
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++) f[x] = g[y * w + x];
            Edt1D(f, w, d, v, z);
            for (int x = 0; x < w; x++) g[y * w + x] = d[x];
        }
        for (int i = 0; i < g.Length; i++) g[i] = Mathf.Sqrt(g[i]);
        return g;
    }

    static void Edt1D(float[] f, int n, float[] d, int[] v, float[] z)
    {
        int k = 0;
        v[0] = 0; z[0] = float.NegativeInfinity; z[1] = float.PositiveInfinity;
        for (int q = 1; q < n; q++)
        {
            float s = ((f[q] + (float)q * q) - (f[v[k]] + (float)v[k] * v[k])) / (2f * q - 2f * v[k]);
            while (k > 0 && s <= z[k])
            {
                k--;
                s = ((f[q] + (float)q * q) - (f[v[k]] + (float)v[k] * v[k])) / (2f * q - 2f * v[k]);
            }
            k++;
            v[k] = q; z[k] = s; z[k + 1] = float.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < n; q++)
        {
            while (z[k + 1] < q) k++;
            float dq = q - v[k];
            d[q] = dq * dq + f[v[k]];
        }
    }

    public override void _Process(double delta) => Sync();

    public void Sync()
    {
        if (!GodotObject.IsInstanceValid(Target)) return;
        int state = StateSource?.Invoke() ?? 0;
        float dt = (float)GetProcessDeltaTime();

        // 切換淡入淡出：要顯示的快速淡入、不顯示的較慢淡出；第一次不淡入（進場景直接到位）
        bool want = KickedMode ? state == 1 : state == 0;
        float fadeTarget = want ? 1f : 0f;
        if (!_fadeInit) { _fade = fadeTarget; _fadeInit = true; }
        else if (_fade < fadeTarget) _fade = Mathf.Min(fadeTarget, _fade + dt / Mathf.Max(FadeInTime, 0.001f));
        else if (_fade > fadeTarget) _fade = Mathf.Max(fadeTarget, _fade - dt / Mathf.Max(FadeOutTime, 0.001f));
        bool hidden = _fade <= 0.001f;

        if (KickedMode)
        {
            EnsureSdfs();
            // 踢掉被子的拖曳位移：拖曳中直接跟手；放開後平滑回到目標；
            // 不是踢掉狀態時，等淡出完才歸零（淡出期間保持在被放開的位置，才不會跳回去）
            if (state != 1) { if (hidden) { Drag = Vector2.Zero; DragCur = Vector2.Zero; } }
            else if (DragSnap) DragCur = Drag;
            else DragCur = DragCur.Lerp(Drag, 1f - Mathf.Exp(-14f * dt));
        }
        else
        {
            // 折疊進度：拖曳中直接跟手；放開後平滑回彈到目標（放手只回彈一部分，其餘維持）；
            // 不是蓋著狀態時，等淡出完才歸零（淡出期間保留被拉下的樣子）
            if (state == 0)
            {
                if (FoldSnap) Fold = FoldTarget;
                else Fold = Mathf.MoveToward(Fold, FoldTarget, dt * 5f);
            }
            else if (hidden) { Fold = 0f; ClearPulls(); }   // 淡出完（被踢開／隱藏）→ 清掉皺褶紀錄，下次蓋回去是乾淨的
            // 最新那組線條的程度跟著目前折疊進度
            if (_pullCount > 0 && state == 0) { var pv = _pulls[_pullCount - 1]; pv.Y = Fold; _pulls[_pullCount - 1] = pv; }
        }

        // 對齊背景：全域變換 → 相對父節點
        var parent = GetParent() as CanvasItem;
        Transform2D P = parent != null ? parent.GetGlobalTransform() : Transform2D.Identity;
        Transform2D L = P.AffineInverse() * Target.GetGlobalTransform();
        if (!KickedMode && state == 0 && Mathf.Abs(OffsetY) > 0.001f) L = L.TranslatedLocal(new Vector2(0f, OffsetY));
        PivotOffset = Vector2.Zero;
        Position = L.Origin;
        Rotation = L.Rotation;
        Scale = L.Scale;
        Size = Target.Size;

        Texture = KickedMode ? Kicked : Covered;
        Visible = !hidden && Target.IsVisibleInTree();

        if (_mat == null) return;
        _mat.SetShaderParameter("fade", _fade * _fade * (3f - 2f * _fade));   // 緩入緩出
        if (KickedMode)
        {
            _mat.SetShaderParameter("sdf_bed", _sdfBed);
            _mat.SetShaderParameter("sdf_duvet", _sdfDuvet);
            _mat.SetShaderParameter("has_mask", (_sdfBed != null && _sdfDuvet != null) ? 1f : 0f);
            _mat.SetShaderParameter("sdf_cap", SdfCap);
            {
                // 畫面上看得到的範圍＝（被子外框，套用縮放／位移）∩（床外框外擴裁切距離）∩ 畫面
                Vector2 kp = KickedPivot, off = DragCur + BaseOffsetUV;
                float ks = KickedScale;
                float tw = Kicked != null ? Math.Max(Kicked.GetWidth(), 1) : 1716f, th = Kicked != null ? Math.Max(Kicked.GetHeight(), 1) : 916f;
                float kx0 = (_kbox.X - kp.X) * ks + kp.X + off.X, kx1 = (_kbox.Z - kp.X) * ks + kp.X + off.X;
                float ky0 = (_kbox.Y - kp.Y) * ks + kp.Y + off.Y, ky1 = (_kbox.W - kp.Y) * ks + kp.Y + off.Y;
                float cu = ClipPx / tw, cv = ClipPx / th;
                float vx0 = Mathf.Max(kx0, _bedBox.X - cu), vx1 = Mathf.Min(kx1, _bedBox.Z + cu);
                float vy0 = Mathf.Max(ky0, _bedBox.Y - cv), vy1 = Mathf.Min(Mathf.Min(ky1, _bedBox.W + cv), 1f);
                _mat.SetShaderParameter("vbox", new Vector4(vx0, vy0, Mathf.Max(vx1, vx0 + 0.001f), Mathf.Max(vy1, vy0 + 0.001f)));
            }
            _mat.SetShaderParameter("kblue_col", new Vector3(KickedBlueCol.R, KickedBlueCol.G, KickedBlueCol.B));
            _mat.SetShaderParameter("kblue_amt", KickedBlueAmt);
            _mat.SetShaderParameter("dg_col", new Vector3(DgCol.R, DgCol.G, DgCol.B));
            _mat.SetShaderParameter("dg_amt", DgAmt);
            _mat.SetShaderParameter("dg_pos", DgPos);
            _mat.SetShaderParameter("dg_soft", DgSoft);
            _mat.SetShaderParameter("dg_n", new Vector2(Mathf.Cos(Mathf.DegToRad(DgAngleDeg)) + Mathf.Sin(Mathf.DegToRad(DgAngleDeg)), Mathf.Cos(Mathf.DegToRad(DgAngleDeg)) - Mathf.Sin(Mathf.DegToRad(DgAngleDeg))));
            _mat.SetShaderParameter("dg_shift_px", DgShiftPx);
            _mat.SetShaderParameter("kblue_top_k", KickedTopK);
            _mat.SetShaderParameter("lt_col", new Vector3(LtCol.R, LtCol.G, LtCol.B));
            _mat.SetShaderParameter("lt_amt", LtAmt);
            _mat.SetShaderParameter("lt_range", LtRange);
            _mat.SetShaderParameter("kblue_col_bot", new Vector3(KickedBlueColBot.R, KickedBlueColBot.G, KickedBlueColBot.B));
            _mat.SetShaderParameter("kblue_range", KickedBlueRange);
            _mat.SetShaderParameter("kblue_corner_amt", KickedBlueCornerAmt);
            _mat.SetShaderParameter("kblue_corner_range", KickedBlueCornerRange);
            _mat.SetShaderParameter("drag_uv", DragCur + BaseOffsetUV);
            _mat.SetShaderParameter("kscale", KickedScale);
            _mat.SetShaderParameter("kpivot", KickedPivot);
            _mat.SetShaderParameter("clip_px", ClipPx);
            _mat.SetShaderParameter("clip_irreg", ClipIrregular);
            _mat.SetShaderParameter("clip_wave_px", ClipWavePx);
            _mat.SetShaderParameter("clip_wave_len", ClipWaveLength);
            _mat.SetShaderParameter("round_px", RoundPx);
            _mat.SetShaderParameter("outline_px", OutlinePx);
            _mat.SetShaderParameter("outline_color", OutlineColor);
            var tex = Kicked ?? Texture;
            if (tex != null) _mat.SetShaderParameter("tex_px", new Vector2(tex.GetWidth(), tex.GetHeight()));
        }
        else
        {
            _mat.SetShaderParameter("br_d", BreathPx / Mathf.Max(Size.Y, 1f));
            _mat.SetShaderParameter("br_w", BreathWide);
            _mat.SetShaderParameter("br_c", BreathC);
            _mat.SetShaderParameter("fold_d", Fold * GrabBandV);
            _mat.SetShaderParameter("top_v", TopV);
            _mat.SetShaderParameter("band_v", GrabBandV);
            _mat.SetShaderParameter("infl_v", InfluenceV);
            _mat.SetShaderParameter("fold_shadow", FoldShadow);
            _mat.SetShaderParameter("fold_waves", FoldWaves);
            _mat.SetShaderParameter("fold_irreg", FoldIrregular);
            _mat.SetShaderParameter("crease_w", FoldLineWidth);
            for (int i = 0; i < PlNames.Length; i++)
                _mat.SetShaderParameter(PlNames[i], i < _pullCount ? _pulls[i] : Vector4.Zero);
            _mat.SetShaderParameter("crease_col", new Vector3(FoldLineColor.R, FoldLineColor.G, FoldLineColor.B));
            _mat.SetShaderParameter("aspect", Size.X / Mathf.Max(Size.Y, 1f));
            // 被抓的點：還沒被拉（Fold≈0）時直接到位；已經有殘留的拉痕時再抓別的點，就平滑移過去
            _pullCur = Fold < 0.002f ? PullX : Mathf.Lerp(_pullCur, PullX, 1f - Mathf.Exp(-16f * dt));
            _mat.SetShaderParameter("pull_x", _pullCur);
            _mat.SetShaderParameter("pull_w", PullW);
            _mat.SetShaderParameter("pull_lean", PullLean);
            _mat.SetShaderParameter("bleach", Bleach);
            _mat.SetShaderParameter("bright", Bright);
            _mat.SetShaderParameter("tint", new Vector3(Tint.R, Tint.G, Tint.B));
            _mat.SetShaderParameter("tint_amt", TintAmt);
            _mat.SetShaderParameter("blue_col", new Vector3(BlueCol.R, BlueCol.G, BlueCol.B));
            _mat.SetShaderParameter("blue_amt", BlueAmt);
            _mat.SetShaderParameter("blue_amt_r", BlueAmtR);
            _mat.SetShaderParameter("blue_u0", BlueU0);
            _mat.SetShaderParameter("blue_u1", BlueU1);
            _mat.SetShaderParameter("dg_col", new Vector3(DgCol.R, DgCol.G, DgCol.B));
            _mat.SetShaderParameter("dg_amt", DgAmt);
            _mat.SetShaderParameter("dg_pos", DgPos);
            _mat.SetShaderParameter("dg_soft", DgSoft);
            _mat.SetShaderParameter("dg_n", new Vector2(Mathf.Cos(Mathf.DegToRad(DgAngleDeg)) + Mathf.Sin(Mathf.DegToRad(DgAngleDeg)), Mathf.Cos(Mathf.DegToRad(DgAngleDeg)) - Mathf.Sin(Mathf.DegToRad(DgAngleDeg))));
            _mat.SetShaderParameter("dg_shift_px", DgShiftPx);
            _mat.SetShaderParameter("lt_col", new Vector3(LtCol.R, LtCol.G, LtCol.B));
            _mat.SetShaderParameter("lt_amt", LtAmt);
            _mat.SetShaderParameter("lt_range", LtRange);
            if (!_ltScanned && Covered != null)
            {
                _ltScanned = true;
                try
                {
                    var im = Covered.GetImage();
                    if (im.IsCompressed()) im.Decompress();
                    int w = im.GetWidth(), h = im.GetHeight(), x0 = w, y0 = h, x1 = 0, y1 = 0;
                    for (int yy = 0; yy < h; yy += 8) for (int xx = 0; xx < w; xx += 8)
                        if (im.GetPixel(xx, yy).A > 0.3f) { if (xx < x0) x0 = xx; if (xx > x1) x1 = xx; if (yy < y0) y0 = yy; if (yy > y1) y1 = yy; }
                    if (x1 > x0 && y1 > y0) _ltBox = new Vector4(x0 / (float)w, y0 / (float)h, x1 / (float)w, y1 / (float)h);
                }
                catch (Exception) { }
            }
            _mat.SetShaderParameter("lt_box", _ltBox);
            _mat.SetShaderParameter("lt_bot_amt", LtBotAmt);
            _mat.SetShaderParameter("lt_top_r", LtTopRight);
            _mat.SetShaderParameter("lt_top_k", LtTopK);
            _mat.SetShaderParameter("blue_v0", BlueV0);
            _mat.SetShaderParameter("blue_col_bot", new Vector3(BlueColBot.R, BlueColBot.G, BlueColBot.B));
            _mat.SetShaderParameter("tint_u0", TintU0);
            _mat.SetShaderParameter("tint_u1", TintU1);

            if (CancelCharFilter)
            {
                // 算出被子每個像素在螢幕上的位置（跟人物光影群組用同一套算法：畫布變換 × 全域變換 ÷ 視窗大小）
                Vector2 vp = GetViewport().GetVisibleRect().Size;
                Transform2D xf = GetCanvasTransform() * GetGlobalTransform();
                _mat.SetShaderParameter("cancel_on", 1f);
                _mat.SetShaderParameter("night_mul", new Vector3(NightMul.R, NightMul.G, NightMul.B));
                _mat.SetShaderParameter("s_org", new Vector2(xf.Origin.X / vp.X, xf.Origin.Y / vp.Y));
                _mat.SetShaderParameter("s_bx", new Vector2(xf.X.X * Size.X / vp.X, xf.X.Y * Size.X / vp.Y));
                _mat.SetShaderParameter("s_by", new Vector2(xf.Y.X * Size.Y / vp.X, xf.Y.Y * Size.Y / vp.Y));
                if (CharShadeMat != null)
                {
                    foreach (var n in CharShadeParamNames)
                    {
                        Variant v = CharShadeMat.GetShaderParameter(n);
                        if (v.VariantType != Variant.Type.Nil) _mat.SetShaderParameter(n, v);
                    }
                }
                else
                {
                    // 沒有人物光影群組：只剩夜晚乘色要抵銷
                    _mat.SetShaderParameter("top_enabled", false);
                    _mat.SetShaderParameter("right_enabled", false);
                }
            }
            else _mat.SetShaderParameter("cancel_on", 0f);
        }
    }
}
