using Godot;

/// <summary>
/// TrainingRoomRuntime — 嘴巴外觀與說話動作控制系統：
/// 支援：
///   1. 常態基礎嘴型：被摸之後就應該會一直保持小嘴巴，在做切換（ZZZ 睡覺表情時為「張嘴1」）。
///   2. 依照台詞字數節奏開合：短台詞僅開合 1 次，長台詞最多 4 次，說完台詞都會變成小嘴巴。
///   3. 倒笑寬度壓縮：翻轉倒笑（Y 軸 -1.25~-1.35）時，水平左右壓縮（X 軸 0.48~0.52），使其寬度與小嘴巴一致。
///   4. 被插與抽插時有明顯比被摸更大的反應：抵住推進時緊張喘息、抽插時隨每次深入張嘴大喘氣與小嘴巴動態脈動。
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
    [Export(PropertyHint.Range, "1,3,0.05")] public float MouthNearMaxScale = 1.35f;  // 被摸時小嘴巴放到最大的倍率（保持比被插時溫和）
    [Export] public float MouthNearSec       = 1.0f;   // 放大跟著高潮值走的平滑時間
    [Export] public float MouthClimaxShakeSec = 1.5f;  // 高潮瞬間：先抖動嘴幾秒，之後換小嘴吐舌
    float _mouthClimaxShakeT, _mouthNearTarget = 1f;

    [ExportGroup("說話嘴巴開合動畫")]
    [Export] public bool  MouthTalkAnimEnabled = true;
    [Export] public float MouthFlapDuration    = 0.28f; // 每次開合持續時間（秒）：開 0.15s + 閉 0.13s

    float _talkTimer = 0f;
    int   _targetFlaps = 0;
    bool  _isTalking = false;
    bool  _hasBeenTouched = false;

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
        { "", "ZZZ", "害怕", "有點驚訝", "忍耐", "害羞", "開心", "舒服", "傲嬌", "插入1", "插入2", "做愛", "做愛(半閉)", "做愛(全閉)", "做愛3", "做愛4", "高潮", "高潮1", "高潮2" };

    [Export] public float MouthBigScale = 1.65f;
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
        _mouthBigK = Mathf.Lerp(_mouthBigK, tgt, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, sec)));
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
        UpdateMouthBig(dt);
        UpdateMouthOperateCore(dt);
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
            ApplyMouthScale("抖動嘴", new Vector2(1.25f, 1.25f));
            if (_mouthClimaxShakeT <= 0f) ReleaseMouth(expr);
            return;
        }

        bool free = System.Array.IndexOf(_mouthFreeExprs, expr) >= 0;
        bool busy = _tongueOn || _lickOn;   // 只有舔嘴或舌頭在嘴巴上時才算嘴巴忙碌
        bool inPhase3 = _jijiPhase == 3;
        bool thrusting = _jijiPhase == 3 && _jijiStage >= 1 && !_s2Paused;
        bool operating = _browLingerT > 0f || inPhase3;

        if (operating || _patOn || _touchAnimRunningLeft || _touchAnimRunningRight || _bTouchRunning)
        {
            _hasBeenTouched = true;
        }

        if (_mouthSmileHold && (!free || busy || (_browUncomfy && _browLingerT > 0f) || CurrentGirlTalkState() == GirlTalkState.Pain))
        {
            _mouthSmileHold = false;
        }

        bool patFast = _patOn && HeadPatSpeedLvMax > 0f && _patSpeedLv / HeadPatSpeedLvMax >= MouthPatFastAt;
        bool smiling = (_mouthSmileT > 0f || _mouthSmileHold) && !patFast;

        // 若嘴巴真正被佔用（例如舔嘴），交還表情系統
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
                    // 張嘴階段：倒笑壓縮左右（X=0.48 寬度與小嘴巴一致，Y=-1.25 翻轉成開口拱形；被插時張得更大）
                    ShowOnlyMouth("笑");
                    float openY = inPhase3 ? -1.42f : -1.22f;
                    float openX = inPhase3 ? 0.52f : 0.48f;
                    ApplyMouthScale("笑", new Vector2(openX, openY));
                }
                else
                {
                    // 閉嘴階段：小嘴巴
                    ShowOnlyMouth("小嘴巴");
                    float closeScale = inPhase3 ? 1.30f : 1.0f;
                    ApplyMouthScale("小嘴巴", new Vector2(closeScale, closeScale));
                }
                return;
            }
            else
            {
                // 開合次數已滿：說完台詞都會變成小嘴巴
                _isTalking = false;
                ShowOnlyMouth("小嘴巴");
                float endScale = inPhase3 ? 1.25f : 1.0f;
                ApplyMouthScale("小嘴巴", new Vector2(endScale, endScale));
                return;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  2. 快高潮時的嘴巴（隨快感逐漸放大）
        // ════════════════════════════════════════════════════════════
        string nearWant = null;
        float nearLv = MouthNearLevel();
        if (operating && nearLv >= MouthNearStart)
        {
            float tN = Mathf.Clamp((nearLv - MouthNearStart) / Mathf.Max(1f, MouthTongueAt - MouthNearStart), 0f, 1f);
            _mouthNearTarget = Mathf.Lerp(1f, MouthNearMaxScale, tN);
            nearWant = nearLv >= MouthTongueAt ? "小嘴吐舌" : "小嘴巴";
        }

        // ════════════════════════════════════════════════════════════
        //  3. 被插與突破時的大反應（明顯比一般被摸更劇烈）
        // ════════════════════════════════════════════════════════════
        if (inPhase3)
        {
            // Stage 0：剛抵住入口或正在用力頂開阻力（_resistBlocking）
            if (_jijiStage == 0)
            {
                if (_resistBlocking && _jijiHeld)
                {
                    // 用力頂開破處阻力時：張嘴喘息（倒笑壓縮）與抖動嘴交替，呈現明顯承受感
                    bool gaspOpen = ((int)(Time.GetTicksMsec() / 220) % 2) == 0;
                    if (gaspOpen)
                    {
                        ShowOnlyMouth("笑");
                        ApplyMouthScale("笑", new Vector2(0.50f, -1.35f));
                    }
                    else
                    {
                        ShowOnlyMouth("抖動嘴");
                        ApplyMouthScale("抖動嘴", new Vector2(0.85f, 1.15f));
                    }
                }
                else
                {
                    ShowOnlyMouth("小嘴巴");
                    ApplyMouthScale("小嘴巴", new Vector2(1.30f, 1.30f));
                }
                return;
            }

            // Stage >= 1：突破後或抽插進行中
            if (thrusting)
            {
                float normSpeed = S2SpeedLvMax > 0f ? Mathf.Clamp(_s2SpeedLv / S2SpeedLvMax, 0f, 1f) : 0f;
                if (nearWant == "小嘴吐舌" && _s2Sin <= 0.25f)
                {
                    ShowOnlyMouth("小嘴吐舌");
                    float tongueScale = Mathf.Lerp(1.25f, 1.50f, normSpeed);
                    ApplyMouthScale("小嘴吐舌", new Vector2(tongueScale, tongueScale));
                }
                else if (_s2Sin > 0.20f || (normSpeed >= 0.40f && _s2Sin > 0.0f))
                {
                    // 每次往上深入頂入時：張大嘴喘氣（倒笑壓縮寬度，大張喘息）
                    ShowOnlyMouth("笑");
                    float openY = Mathf.Lerp(-1.28f, -1.48f, normSpeed);
                    ApplyMouthScale("笑", new Vector2(0.52f, openY));
                }
                else
                {
                    // 抽插回撤間隙的小嘴巴：依速度與深度動態放大脈動（1.25x ~ 1.60x，比一般被摸的 1.0x 大得多）
                    ShowOnlyMouth("小嘴巴");
                    float thrustScale = Mathf.Lerp(1.25f, 1.60f, normSpeed) * (1f + 0.15f * Mathf.Max(0f, _s2Sin));
                    ApplyMouthScale("小嘴巴", new Vector2(thrustScale, thrustScale));
                }
                return;
            }
            else
            {
                // 插在裡面但暫停抽插時：保持微張放大的小嘴巴（或小嘴吐舌）
                string restIn = (nearWant == "小嘴吐舌") ? "小嘴吐舌" : "小嘴巴";
                ShowOnlyMouth(restIn);
                ApplyMouthScale(restIn, new Vector2(1.28f, 1.28f));
                return;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  4. 常態基礎嘴型（一般被摸或待機）：保持輕柔的小嘴巴（1.0x~1.15x）
        // ════════════════════════════════════════════════════════════
        string defaultResting = (expr == "ZZZ") ? "張嘴1" : "小嘴巴";

        if (!operating && !smiling)
        {
            // 被摸過之後，平常待機一律穩定保持可愛的小嘴巴
            if (_hasBeenTouched || _mouthShown != null)
            {
                ShowOnlyMouth(defaultResting);
                ApplyMouthScale(defaultResting, Vector2.One);
            }
            return;
        }

        bool severeUncomfy = _browUncomfy && _browNeg > 0.65f;
        string want = nearWant ?? (smiling ? "笑" : (severeUncomfy ? "抖動嘴" : defaultResting));

        bool wantVisible = Layers.TryGetValue(want, out var wl) && wl != null && wl.Visible;
        if (_mouthShown != want || !wantVisible)
        {
            ShowOnlyMouth(want);
        }

        // 只有極度不舒服變成抖動嘴時，才做內縮；一般被摸時僅微幅縮放（1.0x~1.15x），不會比被插反應大
        if (want == "抖動嘴")
        {
            float uncomfy = Mathf.Clamp(_browNeg, 0f, 1f);
            float tgt = Mathf.Lerp(1f, MouthMinScaleX, uncomfy);
            _mouthScaleX = Mathf.Lerp(_mouthScaleX, tgt, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, MouthSqueezeSec)));
            ApplyMouthScale("抖動嘴", new Vector2(_mouthScaleX, 1f));
        }
        else if (want == "小嘴巴" || want == "小嘴吐舌")
        {
            float touchScale = Mathf.Clamp(_mouthNearTarget, 1f, 1.18f);
            ApplyMouthScale(want, new Vector2(touchScale, touchScale));
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
        string target = (expr == "ZZZ") ? "張嘴1" : "小嘴巴";
        if (Layers.TryGetValue("抖動嘴", out var shake) && shake != null) ResetMouthScale(shake);
        ShowOnlyMouth(target);
        _mouthShown = target;
        _mouthScaleX = 1f;
    }

    void ApplyMouthScale(string layerName, Vector2 scale)
    {
        if (!Layers.TryGetValue(layerName, out var node) || node is not Control c) return;
        var center = _mouthCenters.TryGetValue(layerName, out var mc) ? mc : MouthCenterTex;
        c.PivotOffset = center * c.Size / new Vector2(2000f, 3200f);
        c.Scale = scale;
    }

    static void ResetMouthScale(CanvasItem node)
    {
        if (node is Control c) c.Scale = Vector2.One;
    }
}
