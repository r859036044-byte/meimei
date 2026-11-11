using Godot;
using System;

/// <summary>
/// 玩家完整數值與 H 系統管理中心
/// 作為 Autoload 單例，貫穿整個遊戲
/// 整合所有數值、狀態、開關
/// </summary>
public partial class PlayerDataManager : Node
{
    // ═══════════════════════════════════════════════════════════
    //  單例
    // ═══════════════════════════════════════════════════════════
    public static PlayerDataManager Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════
    //  ① 全域開關
    // ═══════════════════════════════════════════════════════════
    /// <summary>R18 成人內容總開關；關閉後遮蔽所有成人相關畫面與 H 邏輯</summary>
    public bool R18ContentEnabled { get; set; } = DemoMode.Enabled; // 預設全年齡；R18 需在設定中手動開啟（試玩版預設 R18）

    // ═══════════════════════════════════════════════════════════
    //  ② 玩家差分開關（影響劇情、CG、對話分支）
    // ═══════════════════════════════════════════════════════════
    /// <summary>是否為處女（初次破處前為 true，破處後永久設為 false）</summary>
    public bool IsVirgin { get; set; } = true;

    /// <summary>是否懷孕</summary>
    public bool IsPregnant { get; set; } = false;

    /// <summary>是否只和男主發生過 H</summary>
    public bool IsOnlyWithProtagonist { get; set; } = true;

    // ═══════════════════════════════════════════════════════════
    //  ③ 基本數值
    // ═══════════════════════════════════════════════════════════

    // ── 淫亂度 (0~100) ──────────────────────────────────────
    public float MaxLewd     = 100f;
    public float CurrentLewd { get; private set; }
    /// <summary>淫亂度是否超過 51（影響多項公式）</summary>
    public bool IsLewdOver51 => CurrentLewd > 51f;

    // ── 性慾值 (0~100) ──────────────────────────────────────
    public float MaxDesire     = 100f;
    public float CurrentDesire { get; private set; }
    public bool IsDesireMaxed => CurrentDesire >= 100f;

    // ── 反抗耐久 (0~100) ────────────────────────────────────
    public float MaxResistance     = 100f;
    public float CurrentResistance { get; private set; }

    // ── 體力值 (0~100) ──────────────────────────────────────
    public float MaxStamina     = 100f;
    public float CurrentStamina { get; private set; }

    // ── 痛苦值 (0~100) ──────────────────────────────────────
    public float MaxPain     = 100f;
    public float CurrentPain { get; private set; }

    // ── 高潮值 (0~100) ──────────────────────────────────────
    public float MaxOrgasmVal     = 100f;
    public float CurrentOrgasmVal { get; private set; }

    // ── 穩定值 (0~100) ──────────────────────────────────────
    public float MaxStability     = 100f;
    public float CurrentStability { get; private set; }

    // ── 衣服耐久 (0~100) ────────────────────────────────────
    public float MaxCloth     = 100f;
    public float CurrentCloth { get; private set; }

    public enum ClothLevel { Normal, Torn, Naked }
    public ClothLevel ClothState
    {
        get
        {
            if (CurrentCloth <= 0f)  return ClothLevel.Naked;
            if (CurrentCloth < 50f)  return ClothLevel.Torn;
            return ClothLevel.Normal;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ④ H 狀態（部位開發次數，最高 1000）
    // ═══════════════════════════════════════════════════════════
    public int MaxPartUseCount  = 1000;
    public int MouthUseCount    { get; private set; }
    public int BreastUseCount   { get; private set; }
    public int VaginaUseCount   { get; private set; }
    public int AnalUseCount     { get; private set; }

    // ── 四大部位開發階段 S0~S5 ──────────────────────────────
    public enum DevStage { S0, S1, S2, S3, S4, S5 }

    public static readonly string[] DevStageNames = {
        "S0 (排斥)", "S1 (抗拒)", "S2 (習慣)",
        "S3 (接受)", "S4 (喜歡)", "S5 (淫亂)"
    };

    /// <summary>將部位使用次數映射到開發階段 S0~S5</summary>
    public static DevStage UseCountToStage(int count)
    {
        if (count >= 500) return DevStage.S5;
        if (count >= 300) return DevStage.S4;
        if (count >= 150) return DevStage.S3;
        if (count >=  60) return DevStage.S2;
        if (count >=  15) return DevStage.S1;
        return DevStage.S0;
    }

    /// <summary>取得下一階段所需的使用次數門檻</summary>
    public static int StageThreshold(DevStage stage)
    {
        return stage switch
        {
            DevStage.S0 => 15,
            DevStage.S1 => 60,
            DevStage.S2 => 150,
            DevStage.S3 => 300,
            DevStage.S4 => 500,
            _           => -1,
        };
    }

    public DevStage MouthStage  => UseCountToStage(MouthUseCount);
    public DevStage BreastStage => UseCountToStage(BreastUseCount);
    public DevStage VaginaStage => UseCountToStage(VaginaUseCount);
    public DevStage AnalStage   => UseCountToStage(AnalUseCount);

    // ── 小穴淫亂度 & 小穴濕度 ──────────────────────────────
    public float MaxVaginaLewd    = 100f;
    public float MaxVaginaWetness = 100f;
    public float CurrentVaginaLewd    { get; private set; }
    public float CurrentVaginaWetness { get; private set; }

    public Action<float> OnVaginaLewdChanged;
    public Action<float> OnVaginaWetnessChanged;

    public void SetVaginaLewd(float v)
    {
        CurrentVaginaLewd = Mathf.Clamp(v, 0f, MaxVaginaLewd);
        OnVaginaLewdChanged?.Invoke(CurrentVaginaLewd);
    }
    public void AddVaginaLewd(float delta) => SetVaginaLewd(CurrentVaginaLewd + delta);

    public void SetVaginaWetness(float v)
    {
        CurrentVaginaWetness = Mathf.Clamp(v, 0f, MaxVaginaWetness);
        OnVaginaWetnessChanged?.Invoke(CurrentVaginaWetness);
    }
    public void AddVaginaWetness(float delta) => SetVaginaWetness(CurrentVaginaWetness + delta);

    // ── 警戒值 (0~100) ──────────────────────────────────────
    public float MaxAlertLevel = 100f;
    public float CurrentAlertLevel { get; private set; }
    public Action<float> OnAlertLevelChanged;

    public void SetAlertLevel(float v)
    {
        float old = CurrentAlertLevel;
        CurrentAlertLevel = Mathf.Clamp(v, 0f, MaxAlertLevel);
        if (!Mathf.IsEqualApprox(old, CurrentAlertLevel))
            OnAlertLevelChanged?.Invoke(CurrentAlertLevel);
    }
    public void AddAlertLevel(float delta) => SetAlertLevel(CurrentAlertLevel + delta);

    // ── meimei 高潮值 (0~100) ───────────────────────────────
    public float MaxMeimeiOrgasm = 100f;
    public float CurrentMeimeiOrgasm { get; private set; }
    public Action<float> OnMeimeiOrgasmChanged;

    /// <summary>是否已觸發高潮（本次 session）</summary>
    public bool MeimeiHasOrgasmed { get; set; }

    public enum MeimeiOrgasmType { None, Normal, Big, Ultimate }
    public MeimeiOrgasmType LastOrgasmType { get; set; } = MeimeiOrgasmType.None;

    public void SetMeimeiOrgasm(float v)
    {
        float old = CurrentMeimeiOrgasm;
        CurrentMeimeiOrgasm = Mathf.Clamp(v, 0f, MaxMeimeiOrgasm);
        if (!Mathf.IsEqualApprox(old, CurrentMeimeiOrgasm))
            OnMeimeiOrgasmChanged?.Invoke(CurrentMeimeiOrgasm);
    }
    public void AddMeimeiOrgasm(float delta) => SetMeimeiOrgasm(CurrentMeimeiOrgasm + delta);

    /// <summary>今日結果追蹤</summary>
    public bool TodayDidNothing    { get; set; } = true;
    public bool TodayTouchedOrgasm { get; set; }
    public bool TodayInsertOrgasm  { get; set; }
    public bool TodayInsertNoOrg   { get; set; }
    public bool TodayInserted      { get; set; }

    // ── 劇情變數（對話 JSON 的 addVar / varStart 使用）──────────
    readonly System.Collections.Generic.Dictionary<string, int> _dialogueVars = new();
    /// <summary>取得劇情變數（沒設定過 = 0）</summary>
    public int GetDialogueVar(string name) => string.IsNullOrEmpty(name) ? 0 : (_dialogueVars.TryGetValue(name, out var v) ? v : 0);
    /// <summary>劇情變數 +delta（預設 +1）</summary>
    public void AddDialogueVar(string name, int delta = 1)
    {
        if (string.IsNullOrEmpty(name)) return;
        _dialogueVars[name] = GetDialogueVar(name) + delta;
        if (delta > 0) GalleryDataManager.Instance?.OnDialogueVar(name);
    }
    /// <summary>存檔用：取得目前所有劇情變數的副本</summary>
    public System.Collections.Generic.Dictionary<string, int> GetAllDialogueVars() => new(_dialogueVars);
    /// <summary>讀檔用：整批還原劇情變數（null = 不動）</summary>
    public void SetAllDialogueVars(System.Collections.Generic.Dictionary<string, int> vars)
    {
        if (vars == null) return;
        _dialogueVars.Clear();
        foreach (var kv in vars) _dialogueVars[kv.Key] = kv.Value;
    }
    /// <summary>清除劇情變數（name 為空 = 全部清除）</summary>
    public void ClearDialogueVar(string name = null)
    {
        if (string.IsNullOrEmpty(name)) _dialogueVars.Clear(); else _dialogueVars.Remove(name);
    }

    /// <summary>目前是第幾晚（1=第一晚, 2=第二晚, 3=第三晚）</summary>
    public int CurrentNight { get; set; } = 0;
    /// <summary>連續什麼都沒做的晚上次數</summary>
    public int IdleNightCount { get; set; } = 0;

    /// <summary>第二天分支類型</summary>
    public enum Day2Branch
    {
        Nothing,
        TouchNoOrgasm,
        TouchOrgasm,
        InsertOrgasm,
        InsertNoOrgasm
    }

    /// <summary>根據今日結果判定第二天分支</summary>
    public Day2Branch GetDay2Branch()
    {
        if (TodayInsertNoOrg)   return Day2Branch.InsertNoOrgasm;
        if (TodayInsertOrgasm)  return Day2Branch.InsertOrgasm;
        if (TodayTouchedOrgasm) return Day2Branch.TouchOrgasm;
        if (!TodayDidNothing)   return Day2Branch.TouchNoOrgasm;
        return Day2Branch.Nothing;
    }

    /// <summary>
    /// 開新遊戲：所有遊玩進度與數值回到初始值（設定類的 R18 開關不動）
    /// </summary>
    public void ResetForNewGame()
    {
        IsVirgin = true; IsPregnant = false; IsOnlyWithProtagonist = true;

        CurrentLewd = 0f; CurrentDesire = 0f; CurrentResistance = 0f;
        CurrentStamina = MaxStamina; CurrentPain = 0f; CurrentOrgasmVal = 0f;
        CurrentStability = MaxStability; CurrentCloth = MaxCloth;

        MouthUseCount = 0; BreastUseCount = 0; VaginaUseCount = 0; AnalUseCount = 0;
        SetVaginaLewd(0f); SetVaginaWetness(0f);

        ResetTodayTracking();          // 今日旗標、警戒值、meimei 高潮值
        _dialogueVars.Clear();         // 劇情變數（day1_blanket 等）
        CurrentNight = 0; IdleNightCount = 0;

        IsAroused = false; ArousedTimer = 0f;
        IsFallen = false; IsInserted = false; IsHappening = false; IsSeedBed = false; IsMilking = false;
        IsCorrupted = false; CorruptTimer = 0f;

        _desireDecayTimer = _painDecayTimer = _stabilityRegenTimer = 0f;
        _orgasmBuildTimer = _hTimer = _milkTimer = 0f;
        _slimeMouthTimer = _slimeBreastTimer = _slimeVaginaTimer = _slimeAnalTimer = 0f;
        _periodicArouseTimer = 0f;
        ResetPeriodicArouseInterval();
    }

    /// <summary>重置今日追蹤（進入新的一天時呼叫）</summary>
    public void ResetTodayTracking()
    {
        TodayDidNothing    = true;
        TodayTouchedOrgasm = false;
        TodayInsertOrgasm  = false;
        TodayInsertNoOrg   = false;
        TodayInserted      = false;
        MeimeiHasOrgasmed  = false;
        LastOrgasmType     = MeimeiOrgasmType.None;
        SetAlertLevel(0f);
        SetMeimeiOrgasm(0f);
        // 注意：TrainingRoomRuntime 的靜態旗標需要在場景腳本中重置
    }

    // ═══════════════════════════════════════════════════════════
    //  ⑤ 附加狀態
    // ═══════════════════════════════════════════════════════════
    public bool  IsAroused    { get; private set; }
    public float ArousedTimer { get; private set; }
    public const float ArousedDuration    = 200f;
    public const float ArousedDecayPerSec = 0.2f;

    public bool IsFallen   { get; private set; }
    public bool IsInserted { get; private set; }
    public bool IsHappening { get; private set; }
    public bool IsSeedBed   { get; private set; }
    public bool IsMilking   { get; private set; }

    public bool  IsCorrupted  { get; private set; }
    public float CorruptTimer { get; private set; }

    // ═══════════════════════════════════════════════════════════
    //  ⑥ 內部計時器
    // ═══════════════════════════════════════════════════════════
    private float _desireDecayTimer    = 0f;
    private float _painDecayTimer      = 0f;
    private float _stabilityRegenTimer = 0f;
    private float _orgasmBuildTimer    = 0f;
    private float _hTimer              = 0f;
    private float _milkTimer           = 0f;

    // 史萊姆部位計時
    private float _slimeMouthTimer  = 0f;
    private float _slimeBreastTimer = 0f;
    private float _slimeVaginaTimer = 0f;
    private float _slimeAnalTimer   = 0f;

    // 定時發情
    private float _periodicArouseTimer    = 0f;
    private float _periodicArouseInterval = 0f;

    // 隨機數生成器
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    // ═══════════════════════════════════════════════════════════
    //  ⑧ 事件
    // ═══════════════════════════════════════════════════════════
    public Action<float> OnLewdChanged;
    public Action<float> OnDesireChanged;
    public Action<float> OnResistanceChanged;
    public Action<float> OnStaminaChanged;
    public Action<float> OnPainChanged;
    public Action<float> OnOrgasmValChanged;
    public Action<float> OnStabilityChanged;
    public Action<float> OnClothChanged;

    public Action        OnArousedStart;
    public Action        OnArousedEnd;
    public Action        OnFallen;
    public Action        OnResistEscape;
    public Action        OnOrgasmTrigger;
    public Action        OnDefeat;
    public Action        OnVirginityLost;
    public Action<bool>  OnR18Toggled;

    // ═══════════════════════════════════════════════════════════
    //  生命週期
    // ═══════════════════════════════════════════════════════════
    public override void _Ready()
    {
        if (Instance != null && Instance != this)
        {
            QueueFree();
            return;
        }
        Instance = this;

        // 讀取全年齡/R18 設定（SettingsManager 比本節點先載入，所以由這裡自己讀；預設 false = 全年齡）
        var _cfg = new ConfigFile();
        if (_cfg.Load("user://settings.cfg") == Error.Ok)
            R18ContentEnabled = (bool)_cfg.GetValue("game", "r18", DemoMode.Enabled);

        _rng.Randomize();

        // 初始數值
        CurrentLewd        = 0f;
        CurrentDesire      = 0f;
        CurrentResistance  = 0f;
        CurrentStamina     = MaxStamina;
        CurrentPain        = 0f;
        CurrentOrgasmVal   = 0f;
        CurrentStability   = MaxStability;
        CurrentCloth       = MaxCloth;

        ResetPeriodicArouseInterval();
    }

    public override void _Process(double delta)
    {
        // R18 關閉時跳過所有 H 邏輯
        if (!R18ContentEnabled) return;

        float dt = (float)delta;
        TickDesire(dt);
        TickPain(dt);
        TickStability(dt);
        TickOrgasm(dt);
        TickAroused(dt);
        TickHAction(dt);
        TickCorruption(dt);
        TickPeriodicArouse(dt);
        TickPregnantEffects(dt);
    }

    // ═══════════════════════════════════════════════════════════
    //  Tick 方法
    // ═══════════════════════════════════════════════════════════

    /// <summary>性慾值衰減</summary>
    void TickDesire(float dt)
    {
        if (IsAroused && IsHappening)
        {
            SetDesire(CurrentDesire - ArousedDecayPerSec * dt);
            if (CurrentDesire <= 0f) EndAroused();
        }
        else if (!IsAroused)
        {
            _desireDecayTimer += dt;
            if (_desireDecayTimer >= 10f)
            {
                _desireDecayTimer = 0f;
                SetDesire(CurrentDesire - 1f);
            }
        }
    }

    /// <summary>痛苦值：每 10 秒自動 -1</summary>
    void TickPain(float dt)
    {
        if (CurrentPain <= 0f) return;
        _painDecayTimer += dt;
        if (_painDecayTimer >= 10f)
        {
            _painDecayTimer = 0f;
            SetPain(CurrentPain - 1f);
        }
    }

    /// <summary>穩定值：未跌倒時每 10 秒回復 1 點</summary>
    void TickStability(float dt)
    {
        if (IsFallen || CurrentStability >= MaxStability) return;
        _stabilityRegenTimer += dt;
        if (_stabilityRegenTimer >= 10f)
        {
            _stabilityRegenTimer = 0f;
            SetStability(CurrentStability + 1f);
        }
    }

    /// <summary>高潮值：H 進行中且（性慾 - 痛苦）> 10 時，每 2 秒 +1</summary>
    void TickOrgasm(float dt)
    {
        if (!IsHappening) return;
        float net = CurrentDesire - CurrentPain;
        if (net > 10f)
        {
            _orgasmBuildTimer += dt;
            if (_orgasmBuildTimer >= 2f)
            {
                _orgasmBuildTimer = 0f;
                SetOrgasm(CurrentOrgasmVal + 1f);
            }
        }
        else
        {
            _orgasmBuildTimer = 0f;
        }
    }

    /// <summary>發情倒計時（200 秒）</summary>
    void TickAroused(float dt)
    {
        if (!IsAroused) return;
        ArousedTimer -= dt;
        if (ArousedTimer <= 0f) EndAroused();
    }

    /// <summary>H 進行中的每秒邏輯</summary>
    void TickHAction(float dt)
    {
        if (!IsHappening) return;
        _hTimer += dt;

        // 體力消耗
        float lewdDiv     = CurrentLewd > 10f ? CurrentLewd / 10f : 1f;
        float staminaCost = _rng.RandfRange(1f, 5f) / lewdDiv * dt;
        SetStamina(CurrentStamina - staminaCost);

        // 每 2 秒互動
        if (_hTimer >= 2f)
        {
            _hTimer = 0f;
            if (!IsAroused)
            {
                if (CurrentDesire < 50f)
                    SetPain(CurrentPain + 1f);
                else if (CurrentDesire > 51f)
                    SetPain(CurrentPain - 1f);

                SetDesire(CurrentDesire + 0.2f);
            }
        }

        // 淫亂度 > 51：痛苦轉換為性慾
        if (IsLewdOver51 && CurrentPain > 0f)
        {
            float converted = CurrentPain * dt * 0.5f;
            SetPain(CurrentPain - converted);
            SetDesire(CurrentDesire + converted);
        }
    }

    /// <summary>腐蝕狀態：持續 5 秒</summary>
    void TickCorruption(float dt)
    {
        if (!IsCorrupted) return;
        CorruptTimer -= dt;
        DamageCloth(_rng.RandfRange(1f, 2f) * dt);
        if (CorruptTimer <= 0f) IsCorrupted = false;
    }

    /// <summary>定時發情：每 5~6 遊戲天觸發一次</summary>
    void TickPeriodicArouse(float dt)
    {
        _periodicArouseTimer += dt;
        if (_periodicArouseTimer >= _periodicArouseInterval)
        {
            _periodicArouseTimer = 0f;
            ResetPeriodicArouseInterval();
            TriggerAroused();
        }
    }

    /// <summary>懷孕狀態效果</summary>
    void TickPregnantEffects(float dt)
    {
        if (!IsPregnant || !IsHappening) return;
        if (CurrentLewd < 50f)
            SetPain(CurrentPain + 0.5f * dt);
        else
            SetDesire(CurrentDesire + 0.5f * dt);
    }

    // ═══════════════════════════════════════════════════════════
    //  公開 Setter（數值）
    // ═══════════════════════════════════════════════════════════

    public void SetLewd(float v)
    {
        CurrentLewd = Mathf.Clamp(v, 0f, MaxLewd);
        OnLewdChanged?.Invoke(CurrentLewd);
    }
    public void AddLewd(float delta) => SetLewd(CurrentLewd + delta);

    public void SetDesire(float v)
    {
        float prev    = CurrentDesire;
        CurrentDesire = Mathf.Clamp(v, 0f, MaxDesire);
        OnDesireChanged?.Invoke(CurrentDesire);
        if (prev < 100f && CurrentDesire >= 100f) TriggerAroused();
    }
    public void AddDesire(float delta) => SetDesire(CurrentDesire + delta);

    public void SetResistance(float v)
    {
        float prev        = CurrentResistance;
        CurrentResistance = Mathf.Clamp(v, 0f, MaxResistance);
        OnResistanceChanged?.Invoke(CurrentResistance);
        if (prev < 100f && CurrentResistance >= 100f) TriggerResistEscape();
    }

    public void SetStamina(float v)
    {
        CurrentStamina = Mathf.Clamp(v, 0f, MaxStamina);
        OnStaminaChanged?.Invoke(CurrentStamina);
        if (CurrentStamina <= 0f && IsHappening) TriggerSeedBed();
    }

    public void SetPain(float v)
    {
        CurrentPain = Mathf.Clamp(v, 0f, MaxPain);
        OnPainChanged?.Invoke(CurrentPain);
    }

    public void SetOrgasm(float v)
    {
        CurrentOrgasmVal = Mathf.Clamp(v, 0f, MaxOrgasmVal);
        OnOrgasmValChanged?.Invoke(CurrentOrgasmVal);
        if (CurrentOrgasmVal >= MaxOrgasmVal) TriggerOrgasm();
    }

    public void SetStability(float v)
    {
        float prev       = CurrentStability;
        CurrentStability = Mathf.Clamp(v, 0f, MaxStability);
        OnStabilityChanged?.Invoke(CurrentStability);
        if (prev > 0f && CurrentStability <= 0f) TriggerFallen();
    }

    /// <summary>對衣服造成傷害</summary>
    public void DamageCloth(float amount)
    {
        CurrentCloth = Mathf.Clamp(CurrentCloth - amount, 0f, MaxCloth);
        OnClothChanged?.Invoke(CurrentCloth);
    }

    // ═══════════════════════════════════════════════════════════
    //  部位使用次數
    // ═══════════════════════════════════════════════════════════

    public void AddMouthUse(int count = 1)
        => MouthUseCount = Math.Min(MouthUseCount + count, MaxPartUseCount);

    public void AddBreastUse(int count = 1)
        => BreastUseCount = Math.Min(BreastUseCount + count, MaxPartUseCount);

    public void AddVaginaUse(int count = 1)
        => VaginaUseCount = Math.Min(VaginaUseCount + count, MaxPartUseCount);

    public void AddAnalUse(int count = 1)
        => AnalUseCount = Math.Min(AnalUseCount + count, MaxPartUseCount);

    /// <summary>史萊姆接觸計時（每 5 秒 +1 次數）</summary>
    public void SlimeMouthTick(float dt)
    {
        _slimeMouthTimer += dt;
        if (_slimeMouthTimer >= 5f) { _slimeMouthTimer = 0f; AddMouthUse(); }
    }
    public void SlimeBreastTick(float dt)
    {
        _slimeBreastTimer += dt;
        if (_slimeBreastTimer >= 5f) { _slimeBreastTimer = 0f; AddBreastUse(); }
    }
    public void SlimeVaginaTick(float dt)
    {
        _slimeVaginaTimer += dt;
        if (_slimeVaginaTimer >= 5f) { _slimeVaginaTimer = 0f; AddVaginaUse(); }
    }
    public void SlimeAnalTick(float dt)
    {
        _slimeAnalTimer += dt;
        if (_slimeAnalTimer >= 5f) { _slimeAnalTimer = 0f; AddAnalUse(); }
    }

    // ═══════════════════════════════════════════════════════════
    //  H 戰鬥動作
    // ═══════════════════════════════════════════════════════════

    /// <summary>左右反抗一次</summary>
    public void DoResist()
    {
        float gain = _rng.RandfRange(5f, 10f) - CurrentLewd / 20f;
        SetResistance(CurrentResistance + Mathf.Max(0f, gain));
    }

    /// <summary>上下配合一次，回傳射精進度倍率</summary>
    public float DoCooperate()
    {
        float mult = 1.5f + (CurrentLewd / 10f) * 0.1f;
        return Mathf.Min(mult, 2.5f);
    }

    /// <summary>開始 H 行為</summary>
    public void StartHAction(bool isInsert = false, bool isOtherPartner = false)
    {
        if (!R18ContentEnabled) return;
        IsInserted  = isInsert;
        IsHappening = true;
        _hTimer     = 0f;
        if (isOtherPartner) IsOnlyWithProtagonist = false;
    }

    /// <summary>結束 H 行為</summary>
    public void EndHAction()
    {
        IsHappening       = false;
        IsInserted        = false;
        _hTimer           = 0f;
        _orgasmBuildTimer = 0f;

        if (IsFallen)
        {
            SetStability(30f);
            IsFallen = false;
        }
    }

    /// <summary>受到普通攻擊</summary>
    public void OnAttacked(bool hasCorruptChance = false)
    {
        float stabDmg = _rng.RandfRange(5f, 10f) * GetStabilityDamageMult();
        SetStability(CurrentStability - stabDmg);
        DamageCloth(_rng.RandfRange(2f, 5f));
        SetLewd(CurrentLewd + _rng.RandfRange(0.1f, 0.2f));

        if (hasCorruptChance && _rng.Randf() < 0.30f)
            ApplyCorruption();
    }

    // ═══════════════════════════════════════════════════════════
    //  差分事件
    // ═══════════════════════════════════════════════════════════

    /// <summary>破處（只執行一次）</summary>
    public void TriggerVirginityLoss()
    {
        if (!IsVirgin) return;
        IsVirgin = false;
        if (!IsAroused) SetPain(CurrentPain + 80f);
        OnVirginityLost?.Invoke();
    }

    /// <summary>懷孕機率判定</summary>
    public void RollPregnancy(bool isAnimalType = false)
    {
        if (IsPregnant) return;
        float rate = isAnimalType ? 0.50f : 0.25f;
        if (_rng.Randf() < rate)
        {
            IsPregnant = true;
            IsMilking  = true;
        }
    }

    /// <summary>外部觸發跌倒</summary>
    public void TriggerFallen_External()
    {
        if (!IsFallen) TriggerFallen();
    }

    /// <summary>附加腐蝕狀態</summary>
    public void ApplyCorruption()
    {
        IsCorrupted  = true;
        CorruptTimer = 5f;
    }

    // ═══════════════════════════════════════════════════════════
    //  開關
    // ═══════════════════════════════════════════════════════════
    public void SetR18(bool enabled)
    {
        R18ContentEnabled = enabled;
        OnR18Toggled?.Invoke(enabled);
    }

    public void ToggleR18() => SetR18(!R18ContentEnabled);

    // ═══════════════════════════════════════════════════════════
    //  輔助工具
    // ═══════════════════════════════════════════════════════════

    /// <summary>衣服狀態對穩定值傷害的倍率</summary>
    public float GetStabilityDamageMult()
    {
        return ClothState switch
        {
            ClothLevel.Torn  => 1.5f,
            ClothLevel.Naked => 2.0f,
            _                => 1.0f,
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  內部觸發
    // ═══════════════════════════════════════════════════════════

    void TriggerAroused()
    {
        IsAroused    = true;
        ArousedTimer = ArousedDuration;
        OnArousedStart?.Invoke();
    }

    void EndAroused()
    {
        IsAroused    = false;
        ArousedTimer = 0f;
        if (CurrentDesire >= 100f) SetDesire(0f);
        OnArousedEnd?.Invoke();
    }

    void TriggerFallen()
    {
        IsFallen          = true;
        CurrentResistance = 0f;
        OnFallen?.Invoke();
    }

    void TriggerResistEscape()
    {
        IsFallen          = false;
        IsInserted        = false;
        IsHappening       = false;
        CurrentResistance = 0f;
        OnResistEscape?.Invoke();
    }

    void TriggerOrgasm()
    {
        float lewdDiv     = CurrentLewd > 10f ? CurrentLewd / 10f : 1f;
        float staminaCost = _rng.RandfRange(60f, 80f) / lewdDiv;
        SetStamina(CurrentStamina - staminaCost);
        SetOrgasm(0f);
        OnOrgasmTrigger?.Invoke();
    }

    void TriggerSeedBed()
    {
        IsSeedBed   = true;
        IsHappening = false;
        OnDefeat?.Invoke();
    }

    void ResetPeriodicArouseInterval()
    {
        const float GameDaySec = 240f;
        _periodicArouseInterval = _rng.RandfRange(5f, 6f) * GameDaySec;
    }
}
