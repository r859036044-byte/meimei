using Godot;

/// <summary>
/// TrainingRoomRuntime — 被碰時的嘴巴：
///   被摸／被操作時（跟眉毛同一個「正在操作」判定，見 Brow.cs）：
///     雌小鬼值 &lt; MouthBratSmileAt  → 抖動嘴；不舒服時（跟眉毛的不舒服同一個程度 0~1）嘴巴慢慢左右向內縮
///     雌小鬼值 ≥ MouthBratSmileAt  → 笑臉張嘴1（跟預設 ZZZ 同一張）
///   停手後（BrowLingerSec 之後）回到目前表情本來指定的嘴巴。
/// 只動「沒有事件專屬嘴型」的表情（ZZZ 與 害怕／有點驚訝／忍耐／害羞／開心／舒服／傲嬌）；
/// 插入、做愛、高潮這些表情有自己指定的嘴巴，不會被蓋掉。舌頭／舔進行中也不動。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("被碰時的嘴巴")]
    [Export] public bool  MouthReactOnOperate = true;
    [Export] public float MouthBratSmileAt    = 60f;    // 雌小鬼值到多少以上，被碰時是笑臉張嘴1（否則抖動嘴）
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float MouthMinScaleX = 0.20f;  // 最不舒服時，抖動嘴左右縮到原寬的幾成（0.2＝只剩 20%，像小嘴，位置不動）
    [Export] public float MouthSqueezeSec     = 1.5f;   // 左右內縮的平滑時間（越大越慢）
    [Export] public Vector2 MouthCenterTex    = new Vector2(965.5f, 753.5f);   // 抖動嘴中心（貼圖 2000x3200 像素）：縮放繞著它

    [ExportGroup("快高潮時的嘴巴")]
    [Export(PropertyHint.Range, "0,100,1")] public float MouthNearStart  = 40f;   // 被摸時高潮值到多少開始：小嘴巴慢慢放大
    [Export(PropertyHint.Range, "0,100,1")] public float MouthTongueAt   = 80f;   // 高潮值到多少換成小嘴吐舌
    [Export(PropertyHint.Range, "1,3,0.05")] public float MouthNearMaxScale = 1.6f;   // 小嘴巴放到最大的倍率（在 MouthTongueAt 時）
    [Export] public float MouthNearSec       = 1.0f;   // 放大跟著高潮值走的平滑時間（越大越慢）
    [Export] public float MouthClimaxShakeSec = 1.5f;  // 高潮瞬間：先抖動嘴幾秒，之後換小嘴吐舌
    float _mouthClimaxShakeT, _mouthNearTarget = 1f;

    /// <summary>「快高潮」程度 0~100：穴高潮值，和現在正在被碰的部位（胸／乳頭／嘴／頭）自己的舒服度，取最大。
    /// 單摸胸、乳頭、摸頭不會增加穴高潮值，所以也要看各部位的舒服度，不然永遠不會進小嘴巴／小嘴吐舌。</summary>
    float MouthNearLevel()
    {
        float v = ClimaxLevel;
        var f = _reactFlags;
        if (f.breast) v = Mathf.Max(v, PartArousal["胸"]);
        if (f.nipple) v = Mathf.Max(v, PartArousal["乳頭"]);
        if (f.mouth)  v = Mathf.Max(v, PartArousal["嘴"]);
        if (f.head)   v = Mathf.Max(v, PartArousal["頭"]);
        return v;
    }

    static readonly string[] _mouthFreeExprs =
        { "", "ZZZ", "害怕", "有點驚訝", "忍耐", "害羞", "開心", "舒服", "傲嬌" };

    [Export] public float MouthBigScale = 2.0f;   // 被插入／被射精時嘴巴放大的倍率（2.0＝變大 100%）
    [Export] public float MouthBigSec   = 0.25f;  // 放大／縮回的平滑時間（秒）

    // 各嘴巴圖的中心（貼圖 2000x3200 像素，從各圖不透明範圍量出來）：放大繞著各自的中心
    static readonly System.Collections.Generic.Dictionary<string, Vector2> _mouthCenters = new()
    {
        ["小嘴"] = new Vector2(964f, 753.5f), ["抖動嘴"] = new Vector2(965.5f, 753.5f), ["小嘴巴"] = new Vector2(964f, 757f),
        ["表情/嘴巴"] = new Vector2(961f, 754.5f), ["小嘴吐舌"] = new Vector2(965.5f, 761f), ["張嘴1"] = new Vector2(964f, 752f),
    };
    float _mouthBigK = 1f;

    /// <summary>被插入（雞雞插入中）或被射精時，嘴巴放大 MouthBigScale 倍。每幀由 UpdateMouthOperate 呼叫。</summary>
    void UpdateMouthBig(float dt)
    {
        bool big = BratInserted || _insertEjacActive || _ejacRunning;
        float tgt = big ? MouthBigScale : _mouthNearTarget;
        float sec = big ? MouthBigSec : MouthNearSec;
        if (Mathf.IsEqualApprox(_mouthBigK, tgt) && !big) { if (_mouthBigK != 1f) { _mouthBigK = 1f; ApplyMouthBig(1f); } return; }
        _mouthBigK = Mathf.Lerp(_mouthBigK, tgt, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, sec)));
        ApplyMouthBig(_mouthBigK);
    }

    void ApplyMouthBig(float k)
    {
        foreach (var kv in _mouthCenters)
        {
            if (!Layers.TryGetValue(kv.Key, out var node) || node is not Control c) continue;
            c.PivotOffset = kv.Value * c.Size / new Vector2(2000f, 3200f);
            c.Scale = new Vector2(k, k);
        }
    }

    [Export] public float MouthSmileSec = 2.5f;   // 出現愛心（愛心氣泡、台詞加愛心）時，嘴巴換成「笑」維持幾秒
    float _mouthSmileT;

    /// <summary>出現愛心的時候呼叫：嘴巴換成「笑」（去掉虎牙的版本）MouthSmileSec 秒。</summary>
    public void TriggerMouthSmile() { _mouthSmileT = MouthSmileSec; _mouthSmileHold = true; }
    bool _mouthSmileHold;   // 笑了之後一直維持，直到有其他情況（不舒服／痛／換表情／被舌頭等佔用）
    [Export(PropertyHint.Range, "0,1,0.05")] public float MouthPatFastAt = 0.6f;   // 摸頭速度（0~1）到這個以上算摸太快：不笑，變抖動嘴

    string _mouthShown;      // 目前由這支程式顯示的嘴巴（null＝沒接管）
    float  _mouthScaleX = 1f;

    void UpdateMouthOperate(float dt)
    {
        UpdateMouthOperateCore(dt);
        UpdateMouthBig(dt);   // 被插入／被射精：嘴巴放大 100%（放在最後，才不會被上面的還原縮放蓋掉）
    }

    void UpdateMouthOperateCore(float dt)
    {
        _mouthSmileT -= dt;
        _mouthNearTarget = 1f;
        if (!MouthReactOnOperate || FaceController == null || !FaceController.IsReady) return;

        string expr = FaceController.CurrentExpression ?? "";
        if (_mouthClimaxShakeT > 0f)
        {   // 高潮：先抖動嘴 → 時間到換回高潮表情指定的小嘴吐舌
            _mouthClimaxShakeT -= dt;
            if (_mouthShown != "抖動嘴") { ShowOnlyMouth("抖動嘴"); _mouthScaleX = 1f; }
            ApplyMouthScaleX("抖動嘴", 1f);
            if (_mouthClimaxShakeT <= 0f) ReleaseMouth(expr);
            return;
        }
        bool free = System.Array.IndexOf(_mouthFreeExprs, expr) >= 0;
        bool busy = _tongueOn || _lickOn || _jijiPhase != 0;   // 舌頭／舔／雞雞出現：嘴巴交給原本的流程
        bool operating = _browLingerT > 0f;                      // 跟眉毛同一個「正在操作」判定

        if (_mouthSmileHold && (!free || busy || _browUncomfy && _browLingerT > 0f || CurrentGirlTalkState() == GirlTalkState.Pain)) _mouthSmileHold = false;   // 其他情況：不舒服／痛／換表情／嘴被佔用 → 不再維持笑
        bool patFast = _patOn && HeadPatSpeedLvMax > 0f && _patSpeedLv / HeadPatSpeedLvMax >= MouthPatFastAt;   // 摸頭摸太快 → 抖動嘴
        bool smiling = (_mouthSmileT > 0f || _mouthSmileHold) && !patFast;   // 笑一次就一直維持（摸頭時也是笑）；摸太快才變抖動嘴
        if (!free || busy || (!operating && !smiling && !(_mouthSmileHold && patFast)))
        {
            if (_mouthShown != null) ReleaseMouth(expr);
            return;
        }

        // 快高潮：被摸時高潮值越高，小嘴巴慢慢放大；到 MouthTongueAt 換成小嘴吐舌
        string nearWant = null;
        float nearLv = MouthNearLevel();
        if (operating && !BratInserted && nearLv >= MouthNearStart)
        {
            float tN = Mathf.Clamp((nearLv - MouthNearStart) / Mathf.Max(1f, MouthTongueAt - MouthNearStart), 0f, 1f);
            _mouthNearTarget = Mathf.Lerp(1f, MouthNearMaxScale, tN);
            nearWant = nearLv >= MouthTongueAt ? "小嘴吐舌" : "小嘴巴";
        }
        string want = nearWant ?? (smiling ? "笑" : (!patFast && BratValue >= MouthBratSmileAt ? "張嘴1" : "抖動嘴"));
        if (_mouthShown != want) ShowOnlyMouth(want);

        // 抖動嘴：不舒服才慢慢左右向內縮（舒服程度的平滑已在 Brow.cs 做好）
        float uncomfy = (want == "抖動嘴" && _browUncomfy) ? Mathf.Clamp(_browNeg, 0f, 1f) : 0f;
        float tgt = Mathf.Lerp(1f, MouthMinScaleX, uncomfy);
        _mouthScaleX = Mathf.Lerp(_mouthScaleX, tgt, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, MouthSqueezeSec)));
        ApplyMouthScaleX(want, _mouthScaleX);
    }

    void ShowOnlyMouth(string name)
    {
        foreach (var n in _mouthLayerNames)
            if (Layers.TryGetValue(n, out var node) && node != null) { node.Visible = n == name; if (n != name) ResetMouthScale(node); }
        _mouthShown = name;
    }

    /// <summary>停手：還原成目前表情本來指定的嘴巴（沒有指定就用預設小嘴）。</summary>
    void ReleaseMouth(string expr)
    {
        string orig = FaceController.GetExpressionMouth(expr) ?? "小嘴";
        if (Layers.TryGetValue("抖動嘴", out var shake) && shake != null) ResetMouthScale(shake);
        ShowOnlyMouth(orig);
        _mouthShown = null;
        _mouthScaleX = 1f;
    }

    void ApplyMouthScaleX(string layerName, float sx)
    {
        if (layerName != "抖動嘴" || !Layers.TryGetValue(layerName, out var node) || node is not Control c) return;
        c.PivotOffset = MouthCenterTex * c.Size / new Vector2(2000f, 3200f);
        c.Scale = new Vector2(sx, 1f);
    }

    static void ResetMouthScale(Node node) { if (node is Control c) c.Scale = Vector2.One; }
}
