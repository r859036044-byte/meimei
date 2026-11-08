using Godot;

/// <summary>
/// TrainingRoomRuntime — 嘴巴外觀與說話動作控制系統：
///   1. 教程（操作說明面板開啟）或尚未對莉奈進行任何身體互動前，不更換原本的嘴巴（張嘴1）。
///   2. 插入過程三段式反應：
///      ① 剛抵住入口時（Stage 0）：先是「小嘴巴」（正常大小 1.0x）
///      ② 按住推擠破處阻力時（_resistBlocking）：換成「抖動嘴」
///      ③ 突破瞬間與插入完成（Stage >= 1）：換成「小嘴巴放大」（200% 大叫，之後保持微張大嘴）
///   3. 說台詞時使用「笑」倒過來壓扁（X=0.45, Y=-0.38）搭配「小嘴巴」開合，說完話動作中絕不變回原本的嘴巴。
///   4. 叫聲很大時（破處、高潮、小高潮、內射、激烈抽插），使用「小嘴巴」放大 200%（Scale = 2.0, 2.0）。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("被碰時的嘴巴")]
    [Export] public bool  MouthReactOnOperate = true;
    [Export] public float MouthBratSmileAt    = 60f;
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float MouthMinScaleX = 0.20f;
    [Export] public float MouthSqueezeSec     = 1.5f;
    [Export] public Vector2 MouthCenterTex    = new Vector2(965.5f, 753.5f);

    [ExportGroup("快高潮時的嘴巴")]
    [Export(PropertyHint.Range, "0,100,1")] public float MouthNearStart  = 30f;
    [Export(PropertyHint.Range, "0,100,1")] public float MouthTongueAt   = 70f;   // 高潮值到多少換成小嘴吐舌
    [Export(PropertyHint.Range, "1,3,0.05")] public float MouthNearMaxScale = 1.35f;
    [Export] public float MouthNearSec       = 1.0f;
    [Export] public float MouthClimaxShakeSec = 1.5f;
    float _mouthClimaxShakeT, _mouthNearTarget = 1f;

    [ExportGroup("說話嘴巴開合動畫")]
    [Export] public bool  MouthTalkAnimEnabled = true;
    [Export] public float MouthFlapDuration    = 0.38f; // 放慢開合節奏，避免動得太頻繁

    float _talkTimer = 0f;
    int   _targetFlaps = 0;
    bool  _isTalking = false;
    bool  _hasBeenTouched = false;
    float _loudCryMouthT = 0f;

    /// <summary>目前是否在開啟教程（操作說明）面板或劇情對話中</summary>
    bool IsInTutorialOrDialogue()
    {
        if (_helpPanel != null && IsInstanceValid(_helpPanel) && _helpPanel.Visible) return true;
        if (UiRoot != null && IsInstanceValid(UiRoot) && UiRoot.HasNode("PendingDialogueContainer")) return true;
        return false;
    }

    /// <summary>目前是否有任何身體互動（被摸或被插）正在進行</summary>
    bool IsBodyActionActive()
    {
        return _touchAnimRunningLeft || _touchAnimRunningRight || NippleHandCount > 0 ||
               _bTouchRunning || _lickOn || _patOn || _tongueOn ||
               _jijiPhase != 0 || _spasmsRunning || _insertEjacActive || _ejacRunning;
    }

    /// <summary>當叫聲很大時觸發：使用「小嘴巴」放大 200%</summary>
    public void TriggerLoudCryMouth(float duration = 1.8f)
    {
        if (IsInTutorialOrDialogue()) return;
        _hasBeenTouched = true;
        _loudCryMouthT = Mathf.Max(_loudCryMouthT, duration);
        // 大叫時以放大 200% 的小嘴巴呈現，不切換成說話碎動
        _isTalking = false;
    }

    /// <summary>通知嘴巴開始說話開合動作（僅用於真正說台詞；若只是教程或未互動則不換嘴）</summary>
    public void NotifyVoiceSpeak(int flapCount = 1)
    {
        if (IsInTutorialOrDialogue()) return;
        if (!_hasBeenTouched && !IsBodyActionActive()) return;
        if (_loudCryMouthT > 0f) return;

        _targetFlaps = Mathf.Clamp(flapCount, 1, 3);
        _talkTimer = 0f;
        _isTalking = true;
    }

    /// <summary>依台詞字數長度換算開合次數（若只是擬聲詞／嬌喘叫聲則不換嘴！）</summary>
    public void NotifyVoiceSpeakForText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (IsInTutorialOrDialogue()) return;
        // 如果只是「嗯…」「哈啊…」「啊、啊…」這類嬌喘／叫聲（非實際台詞），不要換嘴開合
        if (IsSoundOnly(text)) return;
        if (!_hasBeenTouched && !IsBodyActionActive()) return;

        int len = text.Length;
        int flaps = 1;
        if (len > 10) flaps = 3;
        else if (len > 5) flaps = 2;
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

    [Export] public float MouthBigScale = 2.0f;
    [Export] public float MouthBigSec   = 0.25f;

    static readonly System.Collections.Generic.Dictionary<string, Vector2> _mouthCenters = new()
    {
        ["小嘴"] = new Vector2(964f, 753.5f),
        ["抖動嘴"] = new Vector2(965.5f, 753.5f),
        ["小嘴巴"] = new Vector2(963f, 757f),
        ["表情/嘴巴"] = new Vector2(961f, 754.5f),
        ["小嘴吐舌"] = new Vector2(965.5f, 761f),
        ["張嘴1"] = new Vector2(962.5f, 753.5f),
        ["笑"] = new Vector2(965f, 765.5f),
        ["笑2"] = new Vector2(965f, 765.5f),
    };
    float _mouthBigK = 1f;

    void UpdateMouthBig(float dt)
    {
        if (_loudCryMouthT > 0f) _loudCryMouthT = Mathf.Max(0f, _loudCryMouthT - dt);
    }

    [Export] public float MouthSmileSec = 2.5f;
    float _mouthSmileT;
    bool  _mouthSmileHold;
    [Export(PropertyHint.Range, "0,1,0.05")] public float MouthPatFastAt = 0.6f;

    public void TriggerMouthSmile() { }

    string _mouthShown;
    bool   _pinchMouthOn;   // 捏臉中嘴巴正顯示倒笑（放手時要還原）
    [Export(PropertyHint.Range, "0.1,1.5,0.01")] public float PinchFrownScaleX = 0.45f;   // 倒笑嘴左右壓縮（0.45＝跟小嘴巴同寬）
    [Export(PropertyHint.Range, "0.1,1.5,0.01")] public float PinchFrownScaleY = 1f;      // 倒笑嘴上下大小（1＝翻轉後不壓扁）
    [Export] public float PinchFrownCenterY = 757f;   // 倒笑嘴中心的貼圖 Y（757＝小嘴巴的中心，兩者位置對齊；往上減、往下加）

    /// <summary>倒笑：「笑」上下翻轉＋左右壓縮，並讓翻轉後的嘴巴中心落在小嘴巴的位置。</summary>
    void ApplyFrownMouth()
    {
        if (!Layers.TryGetValue("笑", out var node) || node is not Control c) return;
        var ctr = _mouthCenters["笑"];
        float sy = Mathf.Max(0.01f, PinchFrownScaleY);
        // 以 pivot 為軸縮放：中心 cy 翻轉後落在 target → pivot = (target + sy*cy) / (1 + sy)
        float py = (PinchFrownCenterY + sy * ctr.Y) / (1f + sy);
        c.PivotOffset = new Vector2(ctr.X, py) * c.Size / new Vector2(2000f, 3200f);
        c.Scale = new Vector2(PinchFrownScaleX, -sy);
    }
    float  _mouthScaleX = 1f;

    void UpdateMouthOperate(float dt)
    {
        UpdateMouthBig(dt);
        UpdateMouthOperateCore(dt);
    }

    /// <summary>被摸或被插時的標準基礎嘴巴：小嘴巴，或高快感時的「小嘴吐舌」，絕不會是原本的嘴巴（張嘴1）</summary>
    string GetActiveBaseMouth()
    {
        if (_mouthTongueOut || MouthNearLevel() >= MouthTongueAt || ClimaxLevel >= MouthTongueAt)
        {
            return "小嘴吐舌";
        }
        return "小嘴巴";
    }

    void UpdateMouthOperateCore(float dt)
    {
        _mouthSmileT -= dt;
        _mouthNearTarget = 1f;
        if (!MouthReactOnOperate || FaceController == null || !FaceController.IsReady) return;

        // 捏臉（按住臉頰拖動）時：嘴巴換成倒笑（「笑」上下翻轉，跟說話張嘴用的是同一招）；放手就還原
        if (_pinchDrag)
        {
            _pinchMouthOn = true;
            ShowOnlyMouth("笑");
            ApplyFrownMouth();
            return;
        }
        if (_pinchMouthOn)
        {
            _pinchMouthOn = false;
            ReleaseMouth("");
        }

        // 若正在用舌頭親嘴（_tongueOn），嘴巴圖層由 TongueOverlay 遮蓋，不顯示底層嘴巴
        if (_tongueOn)
        {
            _isTalking = false;
            return;
        }

        bool bodyActing = IsBodyActionActive();
        if (bodyActing)
        {
            _hasBeenTouched = true;
        }

        // 0. 如果只是看教程（操作說明面板開啟）且還沒開始動作，或進房後從未觸碰過莉奈：不要換嘴，保持原本的嘴巴
        if (!_hasBeenTouched && !bodyActing)
        {
            return;
        }
        if (IsInTutorialOrDialogue() && !bodyActing)
        {
            _isTalking = false;
            return;
        }

        string baseMouth = GetActiveBaseMouth();

        // ════════════════════════════════════════════════════════════
        //  1. 插入過程三段式嘴巴反應：先是小嘴巴 -> 抖動嘴 -> 小嘴巴放大
        // ════════════════════════════════════════════════════════════
        if (_jijiPhase == 3)
        {
            // ① 剛抵住入口、還沒頂到底或未進入阻力推擠時（Stage 0）：先是小嘴巴
            if (_jijiStage == 0 && !_resistBlocking)
            {
                ShowOnlyMouth("小嘴巴");
                ApplyMouthScale("小嘴巴", Vector2.One);
                return;
            }

            // ② 按住左鍵用力推擠破處阻力時（_resistBlocking）：換成抖動嘴
            if (_resistBlocking)
            {
                ShowOnlyMouth("抖動嘴");
                ApplyMouthScale("抖動嘴", Vector2.One);
                return;
            }

            // ③ 突破瞬間（OnBreakthroughWetCheck）或處於破處高潮叫聲中：小嘴巴放大 200%
            if (_loudCryMouthT > 0f)
            {
                ShowOnlyMouth("小嘴巴");
                ApplyMouthScale("小嘴巴", new Vector2(2.0f, 2.0f));
                return;
            }

            // ④ 突破後 / Stage >= 1 插入中：小嘴巴放大（依抽插深度與速度脈動放大 1.35x ~ 1.85x）
            bool thrustingNow = _jijiStage >= 1 && !_s2Paused;
            float normSpeedNow = S2SpeedLvMax > 0f ? Mathf.Clamp(_s2SpeedLv / S2SpeedLvMax, 0f, 1f) : 0f;
            if (normSpeedNow >= 0.65f || ClimaxLevel >= 85f)
            {
                ShowOnlyMouth("小嘴巴");
                ApplyMouthScale("小嘴巴", new Vector2(2.0f, 2.0f));
                return;
            }

            // 說台詞時才開合，沒說話時保持小嘴巴放大
            if (!(_isTalking && MouthTalkAnimEnabled))
            {
                string insertMouth = (baseMouth == "小嘴吐舌") ? "小嘴吐舌" : "小嘴巴";
                ShowOnlyMouth(insertMouth);
                float insertScale = thrustingNow ? Mathf.Lerp(1.35f, 1.80f, normSpeedNow) : 1.45f;
                ApplyMouthScale(insertMouth, new Vector2(insertScale, insertScale));
                return;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  2. 叫聲很大時：用「小嘴巴」放大 200%（Scale = 2.0, 2.0）
        // ════════════════════════════════════════════════════════════
        if (_loudCryMouthT > 0f || _mouthClimaxShakeT > 0f || _spasmsRunning || _insertEjacActive || _ejacRunning)
        {
            if (_mouthClimaxShakeT > 0f) _mouthClimaxShakeT = Mathf.Max(0f, _mouthClimaxShakeT - dt);
            ShowOnlyMouth("小嘴巴");
            ApplyMouthScale("小嘴巴", new Vector2(2.0f, 2.0f));
            return;
        }

        // ════════════════════════════════════════════════════════════
        //  3. 說台詞時的張嘴閉嘴動畫：用「笑」倒過來壓扁替代張嘴閉嘴效果
        //     說完話直接回到小嘴巴（或小嘴吐舌），絕不變回原本的嘴巴
        // ════════════════════════════════════════════════════════════
        if (MouthTalkAnimEnabled && _isTalking)
        {
            _talkTimer += dt;
            float flapDur = Mathf.Max(0.30f, MouthFlapDuration);
            int currentFlap = (int)(_talkTimer / flapDur);

            if (currentFlap < _targetFlaps)
            {
                float flapPhase = (_talkTimer % flapDur) / flapDur;
                if (flapPhase < 0.48f)
                {
                    // 張嘴階段：「笑」倒過來（Y 負值翻轉）並上下左右壓扁（X=0.45 與小嘴巴同寬，Y=-0.38 壓扁）
                    ShowOnlyMouth("笑");
                    ApplyMouthScale("笑", new Vector2(0.45f, -0.38f));
                }
                else
                {
                    // 閉嘴階段：小嘴巴（或小嘴吐舌）
                    ShowOnlyMouth(baseMouth);
                    ApplyMouthScale(baseMouth, Vector2.One);
                }
                return;
            }
            else
            {
                // 說完台詞：普通摸頭回到笑，其他回到小嘴巴（或小嘴吐舌），不會變回原本的嘴巴
                _isTalking = false;
                string talkEndMouth = (_patOn && _jijiPhase != 3) ? "笑" : baseMouth;
                ShowOnlyMouth(talkEndMouth);
                ApplyMouthScale(talkEndMouth, Vector2.One);
                return;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  4. 一般被摸或待機（互動後）：
        //     若為普通摸頭（_patOn && _jijiPhase != 3）：顯示正常的「笑」
        //     其他情況一律用「小嘴巴」或「小嘴吐舌」
        // ════════════════════════════════════════════════════════════
        if (_patOn && _jijiPhase != 3)
        {
            ShowOnlyMouth("笑");
            ApplyMouthScale("笑", Vector2.One);
            return;
        }

        ShowOnlyMouth(baseMouth);
        ApplyMouthScale(baseMouth, Vector2.One);
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

    /// <summary>還原嘴巴：只要已經被摸或被插過，一律維持小嘴巴（或小嘴吐舌），不會變回原本的嘴巴。</summary>
    void ReleaseMouth(string expr)
    {
        string target = (_hasBeenTouched || IsBodyActionActive()) ? GetActiveBaseMouth() : ((expr == "ZZZ") ? "張嘴1" : "小嘴巴");
        ShowOnlyMouth(target);
        ApplyMouthScale(target, Vector2.One);
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
