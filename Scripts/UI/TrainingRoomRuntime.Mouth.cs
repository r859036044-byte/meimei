using Godot;

/// <summary>
/// TrainingRoomRuntime — 嘴巴外觀與說話動作控制系統：
///   1. 教程（操作說明面板開啟）或尚未對莉奈進行任何身體互動前，不更換原本的嘴巴（張嘴1）。
///   2. 一旦被摸或被插（以及互動進行中、說完話後），一律使用「小嘴巴」或高快感時的「小嘴吐舌」，絕不變回原本的嘴巴（張嘴1／表情嘴巴／抖動嘴）。
///   3. 只有真正說台詞時才動嘴巴（純嬌喘／叫聲不換嘴），並使用「笑」倒過來壓扁（X=0.45, Y=-0.38）搭配「小嘴巴」呈現自然的閉嘴張嘴說話效果。
///   4. 當叫聲很大時（破處、高潮、小高潮、內射、激烈抽插高音嬌喘），使用「小嘴巴」放大 200%（Scale = 2.0, 2.0）。
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
        //  1. 叫聲很大時：用「小嘴巴」放大 200%（Scale = 2.0, 2.0）
        // ════════════════════════════════════════════════════════════
        if (_loudCryMouthT > 0f || _mouthClimaxShakeT > 0f || _spasmsRunning || _insertEjacActive || _ejacRunning)
        {
            if (_mouthClimaxShakeT > 0f) _mouthClimaxShakeT = Mathf.Max(0f, _mouthClimaxShakeT - dt);
            ShowOnlyMouth("小嘴巴");
            ApplyMouthScale("小嘴巴", new Vector2(2.0f, 2.0f));
            return;
        }

        // ════════════════════════════════════════════════════════════
        //  2. 說台詞時的張嘴閉嘴動畫：用「笑」倒過來壓扁替代張嘴閉嘴效果
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
                // 說完台詞：保持小嘴巴（或小嘴吐舌），不會變回原本的嘴巴
                _isTalking = false;
                ShowOnlyMouth(baseMouth);
                ApplyMouthScale(baseMouth, Vector2.One);
                return;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  3. 被摸或是被插的時候（以及互動後）：一律用「小嘴巴」或「小嘴吐舌」
        //     若抽插速度很快或高潮將近（叫聲較大），小嘴巴放大至 200%
        // ════════════════════════════════════════════════════════════
        bool thrusting = _jijiPhase == 3 && _jijiStage >= 1 && !_s2Paused;
        if (thrusting)
        {
            float normSpeed = S2SpeedLvMax > 0f ? Mathf.Clamp(_s2SpeedLv / S2SpeedLvMax, 0f, 1f) : 0f;
            if (normSpeed >= 0.65f || ClimaxLevel >= 85f)
            {
                // 激烈抽插叫聲很大：小嘴巴放大 200%
                ShowOnlyMouth("小嘴巴");
                ApplyMouthScale("小嘴巴", new Vector2(2.0f, 2.0f));
                return;
            }
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
