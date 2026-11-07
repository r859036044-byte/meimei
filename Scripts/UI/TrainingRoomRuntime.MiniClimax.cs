using Godot;

/// <summary>
/// TrainingRoomRuntime — 濕了之後被摸：偶爾來一次「小高潮」（身體小抖一下、嘴巴抖動嘴、噴一點點水），
/// 內褲還穿著時，內褲下面會慢慢濕開（濕痕往下擴大），內褲和床的交界處也會慢慢出現一塊水漬。
///   觸發：小穴（摸 B／舔 B）正在被碰、濕度 ≥ MiniClimaxWetAt、還沒插入、不在大高潮中。
///   不會增加高潮值，也不會取代正式高潮。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("濕了的小高潮")]
    [Export] public bool  MiniClimaxEnabled = true;
    [Export(PropertyHint.Range, "0,100,1")] public float MiniClimaxWetAt = 70f;          // 濕度到多少以上，被摸時偶爾會小高潮
    [Export] public Vector2 MiniClimaxInterval = new Vector2(6f, 12f);                  // 兩次小高潮的間隔（秒，隨機）
    [Export] public float MiniShakeSec     = 0.7f;                                      // 身體小抖幾秒
    [Export(PropertyHint.Range, "0,1,0.05")] public float MiniShakeAmpK = 0.35f;        // 小抖的幅度＝大高潮的幾成
    [Export] public int   MiniSquirtDrops  = 7;                                         // 噴一點水：水滴數
    [Export(PropertyHint.Range, "0.2,1.5,0.05")] public float MiniSquirtPower = 0.6f;   // 噴水力道／距離（跟大高潮第一波比的倍率）

    [ExportGroup("內褲濕開")]
    [Export(PropertyHint.Range, "0,100,1")] public float PantyLeakWetAt = 60f;          // 內褲還穿著、濕度到多少以上開始慢慢濕開
    [Export] public float PantyLeakPerSec  = 0.02f;                                      // 濕開速度（0~1／秒；小高潮會再加一點）
    [Export(PropertyHint.Range, "1,4,0.1")] public float PantyWetSpreadMul = 2.2f;      // 濕開到最大時，濕痕上下範圍放大的倍率
    [Export] public float PantyWetSpreadDownUV = 0.02f;                                  // 濕開到最大時，濕痕中心往下移多少（UV）
    [Export] public float LeakStainOffsetY = 95f;                                        // 內褲和床交界處的水漬：在洞口下方多遠（螢幕 px）
    [Export] public float LeakStainMaxPx   = 70f;                                        // 水漬最大寬度（螢幕 px）
    [Export] public Color LeakStainColor   = new Color(0.45f, 0.52f, 0.68f, 0.38f);      // 水漬顏色（比床深一點的灰藍）

    float _miniT = -1f, _miniShakeT;
    float _pantyUnder;   // 內褲下面濕開的程度 0~1（Panty 濕痕範圍用）
    TextureRect _leakStain;

    /// <summary>每幀由 UpdatePlayValues 呼叫。pussy＝摸 B／舔 B 進行中。</summary>
    void UpdateMiniClimax(float dt, bool pussy)
    {
        bool wet = Wetness >= MiniClimaxWetAt;
        bool can = MiniClimaxEnabled && pussy && wet && !BratInserted && !_spasmsRunning && _jijiPhase < 3;

        // 小抖：只在沒有大高潮痙攣時寫入身體抖動
        if (_miniShakeT > 0f)
        {
            _miniShakeT -= dt;
            if (!_spasmsRunning)
            {
                float amp = SpasmsShakeAmplitude * MiniShakeAmpK * Mathf.Clamp(_miniShakeT / Mathf.Max(0.05f, MiniShakeSec), 0f, 1f);
                _spasmsBodyShake = new Vector2((float)GD.RandRange(-amp, amp), (float)GD.RandRange(-amp * 0.5f, amp * 0.5f));
                if (_miniShakeT <= 0f) _spasmsBodyShake = Vector2.Zero;
            }
        }

        if (can)
        {
            if (_miniT < 0f) _miniT = (float)GD.RandRange(MiniClimaxInterval.X, MiniClimaxInterval.Y);
            _miniT -= dt;
            if (_miniT <= 0f) { _miniT = -1f; TriggerMiniClimax(); }
        }
        else _miniT = -1f;

        // 內褲下面慢慢濕開（還穿著、夠濕才會；脫掉之後慢慢收回）
        bool leaking = MiniClimaxEnabled && !_pantyGone && Wetness >= PantyLeakWetAt;
        _pantyUnder = Mathf.Clamp(_pantyUnder + (leaking ? PantyLeakPerSec : -PantyLeakPerSec * 0.5f) * dt, 0f, 1f);
        UpdateLeakStain();
    }

    void TriggerMiniClimax()
    {
        _miniShakeT = MiniShakeSec;
        _mouthClimaxShakeT = Mathf.Max(_mouthClimaxShakeT, 0.8f);   // 嘴巴抖動嘴一下（Mouth.cs），之後回到原本的嘴型
        TriggerMouthSmile();
        if (!_pantyGone) _pantyUnder = Mathf.Min(1f, _pantyUnder + 0.15f);   // 小高潮：內褲再濕開一點
        GD.Print("[MiniClimax] 小高潮");
        if (!_pantyGone) return;   // 內褲還穿著：水被內褲擋住，只會濕開，不會噴出來
        TaskLogger.Run(MiniSquirtAsync(), "MiniSquirt");
    }

    async System.Threading.Tasks.Task MiniSquirtAsync()
    {
        var w = SquirtWaveDefs[0];
        for (int i = 0; i < MiniSquirtDrops; i++)
        {
            if (!IsInstanceValid(this)) return;
            SpawnSquirtDropPhysics(GetSquirtOrigin(), w.spd * MiniSquirtPower, w.cone, w.dist * MiniSquirtPower);
            await ToSignal(GetTree().CreateTimer(0.06), SceneTree.SignalName.Timeout);
        }
    }

    /// <summary>內褲和床的交界處：水漬慢慢變大（畫在角色後面、床上）。</summary>
    void UpdateLeakStain()
    {
        if (_canvas == null) return;
        float w = _pantyUnder * LeakStainMaxPx;
        if (w < 6f)
        {
            if (_leakStain != null && IsInstanceValid(_leakStain)) _leakStain.Visible = false;
            return;
        }
        Vector2 center = HoleBottomWorld() + new Vector2(0f, LeakStainOffsetY);
        var sz = new Vector2(w, w * 0.5f);
        if (_leakStain == null || !IsInstanceValid(_leakStain))
        {
            _leakStain = MakeBedStainBlob(center, w, LeakStainColor);
            _ejacDebris.Remove(_leakStain);   // 不跟精液血漬一起被清掉
        }
        _leakStain.Visible = true;
        _leakStain.SelfModulate = LeakStainColor;
        _leakStain.Size = sz;
        _leakStain.Position = center - sz * 0.5f;
    }

    /// <summary>濕開之後的濕痕半徑（內褲貼圖 UV）：上下範圍放大。</summary>
    Vector2 PantyWetSpreadRadius(Vector2 r) => new Vector2(r.X * Mathf.Lerp(1f, 1.4f, _pantyUnder), r.Y * Mathf.Lerp(1f, PantyWetSpreadMul, _pantyUnder));
    Vector2 PantyWetSpreadOffset() => new Vector2(0f, PantyWetSpreadDownUV * _pantyUnder);
}
