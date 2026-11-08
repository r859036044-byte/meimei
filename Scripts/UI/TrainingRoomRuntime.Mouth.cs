using Godot;

/// <summary>
/// TrainingRoomRuntime — 嘴巴外觀與說話動作控制系統：
/// 支援：
///   1. 常態基礎嘴型：以可愛的「小嘴巴」為基底（ZZZ 睡覺表情時為「張嘴1」）。
///   2. 發聲說話開合嘴型（Lip-sync）：當有語音播放或發出聲音時，嘴巴瞬間放大縮小，
///      並配合「倒過來的笑」（Y 軸反轉翻轉拱形弧度）與「小嘴巴」交替開合，呈現自然生動的說話反應。
///   3. 舒服與笑臉：觸發愛心或心情良好時呈現「笑」。
///   4. 快感與快高潮反應：隨高潮度提升小嘴巴漸進放大，滿了轉為「小嘴吐舌」。
///   5. 極度不舒服時微縮抖動。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("被碰時的嘴巴")]
    [Export] public bool  MouthReactOnOperate = true;
    [Export] public float MouthBratSmileAt    = 60f;    // 雌小鬼值到多少以上，被碰時偏向笑臉
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float MouthMinScaleX = 0.20f;  // 極度不舒服時，抖動嘴左右縮到原寬比例
    [Export] public float MouthSqueezeSec     = 1.5f;   // 左右內縮的平滑時間
    [Export] public Vector2 MouthCenterTex    = new Vector2(965.5f, 753.5f);   // 嘴巴中心

    [ExportGroup("快高潮時的嘴巴")]
    [Export(PropertyHint.Range, "0,100,1")] public float MouthNearStart  = 30f;   // 被摸時高潮值到多少開始：小嘴巴慢慢放大
    [Export(PropertyHint.Range, "0,100,1")] public float MouthTongueAt   = 70f;   // 高潮值到多少換成小嘴吐舌
    [Export(PropertyHint.Range, "1,3,0.05")] public float MouthNearMaxScale = 1.6f;   // 小嘴巴放到最大的倍率
    [Export] public float MouthNearSec       = 1.0f;   // 放大跟著高潮值走的平滑時間
    [Export] public float MouthClimaxShakeSec = 1.5f;  // 高潮瞬間：先抖動嘴幾秒，之後換小嘴吐舌
    float _mouthClimaxShakeT, _mouthNearTarget = 1f;

    [ExportGroup("說話嘴巴開合動畫")]
    [Export] public bool  MouthTalkAnimEnabled = true;
    [Export] public float MouthTalkCycleSec    = 0.15f; // 說話開合一個週期的時間（秒）
    float _voiceMouthTimer = 0f;
    float _voiceMouthT = 0f;

    /// <summary>通知嘴巴開始說話開合動畫</summary>
    public void NotifyVoiceSpeak(float duration = 0.65f)
    {
        _voiceMouthT = Mathf.Max(_voiceMouthT, duration);
    }

    /// <summary>「快高潮」程度 0~100</summary>
    float MouthNearLevel()
    {
        float v = ClimaxLevel;
        var f = _reactFlags;
        if (f.breast) v = Mathf.Max(v, PartArousal["胸"]);
        if (f.nipple) v = Mathf.Max(v, PartArousal["乳頭"]);
        if (f.mouth)  v = Mathf.Max(v, PartArousal["嘴"]);
        if (f.head)   v = Mathf.Max(v, PartArousal["頭"]);
        if (f.pussy)  v = Mathf.Max(v, Wetness);
        return v;
    }

    static readonly string[] _mouthFreeExprs =
        { "", "ZZZ", "害怕", "有點驚訝", "忍耐", "害羞", "開心", "舒服", "傲嬌" };

    [Export] public float MouthBigScale = 2.0f;   // 被插入／被射精時嘴巴放大的倍率
    [Export] public float MouthBigSec   = 0.25f;  // 放大／縮回的平滑時間（秒）

    // 各嘴巴圖的中心點（貼圖 2000x3200 像素）
    static readonly System.Collections.Generic.Dictionary<string, Vector2> _mouthCenters = new()
    {
        ["小嘴"] = new Vector2(964f, 753.5f),
        ["抖動嘴"] = new Vector2(965.5f, 753.5f),
        ["小嘴巴"] = new Vector2(964f, 757f),
        ["表情/嘴巴"] = new Vector2(961f, 754.5f),
        ["小嘴吐舌"] = new Vector2(965.5f, 761f),
        ["張嘴1"] = new Vector2(964f, 752f),
        ["笑"] = new Vector2(964f, 754f),
        ["笑2"] = new Vector2(964f, 754f),
    };
    float _mouthBigK = 1f;

    void UpdateMouthBig(float dt)
    {
        bool big = BratInserted || _insertEjacActive || _ejacRunning;
        float tgt = big ? MouthBigScale : _mouthNearTarget;
        float sec = big ? MouthBigSec : MouthNearSec;
        if (Mathf.IsEqualApprox(_mouthBigK, tgt) && !big)
        {
            if (_mouthBigK != 1f) { _mouthBigK = 1f; ApplyMouthBig(1f); }
            return;
        }
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

    [Export] public float MouthSmileSec = 2.5f;
    float _mouthSmileT;
    bool  _mouthSmileHold;
    [Export(PropertyHint.Range, "0,1,0.05")] public float MouthPatFastAt = 0.6f;

    public void TriggerMouthSmile() { _mouthSmileT = MouthSmileSec; _mouthSmileHold = true; }

    string _mouthShown;
    float  _mouthScaleX = 1f;

    void UpdateMouthOperate(float dt)
    {
        UpdateMouthOperateCore(dt);
        UpdateMouthBig(dt);
    }

    void UpdateMouthOperateCore(float dt)
    {
        _mouthSmileT -= dt;
        if (_voiceMouthT > 0f) _voiceMouthT -= dt;
        _mouthNearTarget = 1f;
        if (!MouthReactOnOperate || FaceController == null || !FaceController.IsReady) return;

        string expr = FaceController.CurrentExpression ?? "";
        if (_mouthClimaxShakeT > 0f)
        {
            _mouthClimaxShakeT -= dt;
            if (_mouthShown != "抖動嘴") { ShowOnlyMouth("抖動嘴"); _mouthScaleX = 1f; }
            ApplyMouthScale("抖動嘴", new Vector2(1f, 1f));
            if (_mouthClimaxShakeT <= 0f) ReleaseMouth(expr);
            return;
        }

        bool free = System.Array.IndexOf(_mouthFreeExprs, expr) >= 0;
        bool busy = _tongueOn || _lickOn || _jijiPhase != 0;
        bool operating = _browLingerT > 0f;
        bool isVocalizing = IsVoicePlaying;

        if (_mouthSmileHold && (!free || busy || (_browUncomfy && _browLingerT > 0f) || CurrentGirlTalkState() == GirlTalkState.Pain))
        {
            _mouthSmileHold = false;
        }

        bool patFast = _patOn && HeadPatSpeedLvMax > 0f && _patSpeedLv / HeadPatSpeedLvMax >= MouthPatFastAt;
        bool smiling = (_mouthSmileT > 0f || _mouthSmileHold) && !patFast;

        // 若被佔用或不在自由表情列表，交還表情系統
        if (!free || busy)
        {
            if (_mouthShown != null) ReleaseMouth(expr);
            _voiceMouthTimer = 0f;
            return;
        }

        // ════════════════════════════════════════════════════════════
        //  1. 發聲說話開合嘴型（瞬間放大縮小 ＋ 倒過來的笑）
        // ════════════════════════════════════════════════════════════
        if (MouthTalkAnimEnabled && isVocalizing)
        {
            _voiceMouthTimer += dt;
            float cycle = Mathf.Max(0.08f, MouthTalkCycleSec);
            float phase = (_voiceMouthTimer % cycle) / cycle;
            int cycleIdx = (int)(_voiceMouthTimer / cycle);

            if (phase < 0.60f)
            {
                // 張嘴說話階段：配合「倒過來的笑」（上下翻轉）或瞬間放大
                if (cycleIdx % 2 == 0)
                {
                    // 倒過來的笑：Y 軸 -1.25f 形成開口拱形，水平微寬 1.2f
                    ShowOnlyMouth("笑");
                    ApplyMouthScale("笑", new Vector2(1.20f, -1.25f));
                }
                else
                {
                    // 小嘴巴瞬間放大：1.35x
                    ShowOnlyMouth("小嘴巴");
                    ApplyMouthScale("小嘴巴", new Vector2(1.35f, 1.30f));
                }
            }
            else
            {
                // 微閉／回彈階段：小嘴巴微縮
                ShowOnlyMouth("小嘴巴");
                ApplyMouthScale("小嘴巴", new Vector2(1.0f, 0.95f));
            }
            return;
        }
        else
        {
            _voiceMouthTimer = 0f;
        }

        // ════════════════════════════════════════════════════════════
        //  2. 快高潮時的嘴巴（隨快感逐漸放大）
        // ════════════════════════════════════════════════════════════
        string nearWant = null;
        float nearLv = MouthNearLevel();
        if (operating && !BratInserted && nearLv >= MouthNearStart)
        {
            float tN = Mathf.Clamp((nearLv - MouthNearStart) / Mathf.Max(1f, MouthTongueAt - MouthNearStart), 0f, 1f);
            _mouthNearTarget = Mathf.Lerp(1f, MouthNearMaxScale, tN);
            nearWant = nearLv >= MouthTongueAt ? "小嘴吐舌" : "小嘴巴";
        }

        // ════════════════════════════════════════════════════════════
        //  3. 常態基礎嘴型（基本上應該是小嘴巴）
        // ════════════════════════════════════════════════════════════
        if (!operating && !smiling)
        {
            if (_mouthShown != null) ReleaseMouth(expr);
            return;
        }

        string defaultResting = (expr == "ZZZ") ? "張嘴1" : "小嘴巴";
        bool severeUncomfy = _browUncomfy && _browNeg > 0.65f;
        string want = nearWant ?? (smiling ? "笑" : (severeUncomfy ? "抖動嘴" : defaultResting));

        bool wantVisible = Layers.TryGetValue(want, out var wl) && wl != null && wl.Visible;
        if (_mouthShown != want || !wantVisible)
        {
            ShowOnlyMouth(want);
        }

        // 只有極度不舒服變成抖動嘴時，才做內縮
        if (want == "抖動嘴")
        {
            float uncomfy = Mathf.Clamp(_browNeg, 0f, 1f);
            float tgt = Mathf.Lerp(1f, MouthMinScaleX, uncomfy);
            _mouthScaleX = Mathf.Lerp(_mouthScaleX, tgt, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, MouthSqueezeSec)));
            ApplyMouthScale("抖動嘴", new Vector2(_mouthScaleX, 1f));
        }
        else
        {
            ApplyMouthScale(want, Vector2.One);
        }
    }

    void ShowOnlyMouth(string name)
    {
        foreach (var n in _mouthLayerNames)
        {
            if (Layers.TryGetValue(n, out var node) && node != null)
            {
                node.Visible = (n == name);
                if (n != name) ResetMouthScale(node);
            }
        }
        _mouthShown = name;
    }

    /// <summary>停手：還原成目前表情本來指定的嘴巴（沒有指定就用小嘴巴）。</summary>
    void ReleaseMouth(string expr)
    {
        string orig = FaceController.GetExpressionMouth(expr);
        if (string.IsNullOrEmpty(orig) || orig == "小嘴")
        {
            orig = (expr == "ZZZ") ? "張嘴1" : "小嘴巴";
        }
        if (Layers.TryGetValue("抖動嘴", out var shake) && shake != null) ResetMouthScale(shake);
        ShowOnlyMouth(orig);
        _mouthShown = null;
        _mouthScaleX = 1f;
    }

    void ApplyMouthScale(string layerName, Vector2 scale)
    {
        if (!Layers.TryGetValue(layerName, out var node) || node is not Control c) return;
        Vector2 center = _mouthCenters.TryGetValue(layerName, out var ctr) ? ctr : new Vector2(964f, 754f);
        c.PivotOffset = center * c.Size / new Vector2(2000f, 3200f);
        c.Scale = scale;
    }

    static void ResetMouthScale(Node node)
    {
        if (node is Control c) c.Scale = Vector2.One;
    }
}
