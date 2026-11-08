using Godot;

/// <summary>
/// TrainingRoomRuntime — 嘴巴外觀與說話動作控制系統：
/// 支援：
///   1. 常態基礎嘴型：說完台詞或平時皆為可愛的「小嘴巴」（ZZZ 睡覺表情時為「張嘴1」）。
///   2. 依照台詞字數節奏開合：短台詞僅開合 1 次，長台詞最多 4 次，絕不頻繁抖動。
///   3. 倒笑寬度壓縮：翻轉倒笑（Y 軸 -1.25）時，水平左右壓縮（X 軸 0.48），使其寬度與小嘴巴一致。
///   4. 說完台詞後立刻平穩變回「小嘴巴」。
///   5. 快感與快高潮反應：隨高潮度提升小嘴巴漸進放大，滿了轉為「小嘴吐舌」。
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
    [Export] public float MouthFlapDuration    = 0.28f; // 每次開合持續時間（秒）：開 0.15s + 閉 0.13s

    float _talkTimer = 0f;
    int   _targetFlaps = 0;
    bool  _isTalking = false;

    /// <summary>通知嘴巴依台詞字數開始說話開合動作（字少僅 1 次，最多 4 次，說完還原小嘴巴）</summary>
    public void NotifyVoiceSpeak(int flapCount = 1)
    {
        _targetFlaps = Mathf.Clamp(flapCount, 1, 4);
        _talkTimer = 0f;
        _isTalking = true;
    }

    /// <summary>依台詞字數長度換算開合次數</summary>
    public void NotifyVoiceSpeakForText(string text)
    {
        int len = string.IsNullOrEmpty(text) ? 1 : text.Length;
        int flaps = 1;
        if (len > 7) flaps = 4;
        else if (len > 5) flaps = 3;
        else if (len > 3) flaps = 2;
        else flaps = 1;

        NotifyVoiceSpeak(flaps);
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

    [Export] public float MouthBigScale = 2.0f;
    [Export] public float MouthBigSec   = 0.25f;

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
            _isTalking = false;
            return;
        }

        // ════════════════════════════════════════════════════════════
        //  1. 依照台詞開合說話動畫（頂多4次，少則1次，說完必成小嘴巴）
        // ════════════════════════════════════════════════════════════
        if (MouthTalkAnimEnabled && _isTalking)
        {
            _talkTimer += dt;
            float flapDur = Mathf.Max(0.18f, MouthFlapDuration);
            int currentFlap = (int)(_talkTimer / flapDur);

            if (currentFlap < _targetFlaps)
            {
                float flapPhase = (_talkTimer % flapDur) / flapDur;
                if (flapPhase < 0.55f)
                {
                    // 張嘴階段：倒笑壓縮左右（X=0.48 寬度與小嘴巴約32px一致，Y=-1.25 翻轉成開口拱形）
                    ShowOnlyMouth("笑");
                    ApplyMouthScale("笑", new Vector2(0.48f, -1.25f));
                }
                else
                {
                    // 閉嘴階段：小嘴巴
                    ShowOnlyMouth("小嘴巴");
                    ApplyMouthScale("小嘴巴", Vector2.One);
                }
                return;
            }
            else
            {
                // 開合次數已滿：說完台詞都會變成小嘴巴
                _isTalking = false;
                ShowOnlyMouth("小嘴巴");
                ApplyMouthScale("小嘴巴", Vector2.One);
                return;
            }
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

    /// <summary>停手或還原：回到小嘴巴（ZZZ 表情時為張嘴1）。</summary>
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
