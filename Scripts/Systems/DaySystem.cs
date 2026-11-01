using Godot;
using System;

/// <summary>
/// 天數系統 + 活動體力系統
/// 作為 Autoload 單例，貫穿整個遊戲
///
/// 【活動體力】與 PlayerDataManager 的 H 體力完全獨立
///   初始值：10　最高：20
///   每 3 個等級 +1（Lv3=11, Lv6=12 … Lv30=20）
/// </summary>
public partial class DaySystem : Node
{
    // ═══════════════════════════════════════════════════════════
    //  單例
    // ═══════════════════════════════════════════════════════════
    public static DaySystem Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════
    //  天數
    // ═══════════════════════════════════════════════════════════
    /// <summary>目前天數（從第 1 天開始）</summary>
    public int CurrentDay { get; private set; } = 1;

    // ═══════════════════════════════════════════════════════════
    //  日夜
    // ═══════════════════════════════════════════════════════════
    public enum TimePhase { Day, Night }

    public TimePhase Phase { get; private set; } = TimePhase.Day;
    public bool IsDay   => Phase == TimePhase.Day;
    public bool IsNight => Phase == TimePhase.Night;

    // ═══════════════════════════════════════════════════════════
    //  活動體力
    // ═══════════════════════════════════════════════════════════
    public int CurrentActivityStamina { get; private set; } = 10;
    public int MaxActivityStamina     { get; private set; } = 10;

    /// <summary>今天是否已進行過戰鬥</summary>
    public bool HasFoughtToday { get; private set; } = false;

    // 用來儲存當前等級
    private int _playerLevel = 1;

    // ═══════════════════════════════════════════════════════════
    //  事件（UI 訂閱用）
    // ═══════════════════════════════════════════════════════════
    public Action<int>       OnDayChanged;
    public Action<TimePhase> OnPhaseChanged;
    public Action<int, int>  OnStaminaChanged;  // (current, max)

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

        RecalcMaxStamina();
        CurrentActivityStamina = MaxActivityStamina;
    }

    // ═══════════════════════════════════════════════════════════
    //  等級同步
    // ═══════════════════════════════════════════════════════════

    /// <summary>同步玩家等級以重算活動體力上限</summary>
    public void SetPlayerLevel(int level)
    {
        _playerLevel = Math.Max(1, level);
        RecalcMaxStamina();
        CurrentActivityStamina = Math.Min(CurrentActivityStamina, MaxActivityStamina);
        OnStaminaChanged?.Invoke(CurrentActivityStamina, MaxActivityStamina);
    }

    void RecalcMaxStamina()
    {
        // 每 3 級 +1，上限 20
        int bonus = _playerLevel / 3;
        MaxActivityStamina = Math.Clamp(10 + bonus, 10, 20);
    }

    // ═══════════════════════════════════════════════════════════
    //  體力消耗方法
    // ═══════════════════════════════════════════════════════════

    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    /// <summary>【戰鬥】消耗 8~10 活動體力</summary>
    public bool TryCombat()
    {
        int cost = _rng.RandiRange(8, 10);
        if (CurrentActivityStamina < cost) return false;

        CurrentActivityStamina -= cost;
        HasFoughtToday = true;
        OnStaminaChanged?.Invoke(CurrentActivityStamina, MaxActivityStamina);
        return true;
    }

    /// <summary>【互動】消耗 1~2 活動體力</summary>
    public bool TryInteract()
    {
        int cost = _rng.RandiRange(1, 2);
        if (CurrentActivityStamina < cost) return false;

        CurrentActivityStamina -= cost;
        OnStaminaChanged?.Invoke(CurrentActivityStamina, MaxActivityStamina);
        return true;
    }

    /// <summary>【白天休息】今日未戰鬥才能回復 5 點</summary>
    public bool TryDayRest()
    {
        if (HasFoughtToday) return false;

        CurrentActivityStamina = Math.Min(CurrentActivityStamina + 5, MaxActivityStamina);
        OnStaminaChanged?.Invoke(CurrentActivityStamina, MaxActivityStamina);
        return true;
    }

    /// <summary>直接增加活動體力</summary>
    public void AddStamina(int amount)
    {
        CurrentActivityStamina = Math.Clamp(CurrentActivityStamina + amount, 0, MaxActivityStamina);
        OnStaminaChanged?.Invoke(CurrentActivityStamina, MaxActivityStamina);
    }

    /// <summary>直接扣除活動體力</summary>
    public void SpendStamina(int amount)
    {
        CurrentActivityStamina = Math.Max(0, CurrentActivityStamina - amount);
        OnStaminaChanged?.Invoke(CurrentActivityStamina, MaxActivityStamina);
    }

    // ═══════════════════════════════════════════════════════════
    //  日夜切換
    // ═══════════════════════════════════════════════════════════

    /// <summary>切換到夜晚</summary>
    public void SwitchToNight()
    {
        if (IsNight) return;
        Phase = TimePhase.Night;
        OnPhaseChanged?.Invoke(Phase);
    }

    /// <summary>結束一天，進入下一天</summary>
    public void AdvanceToNextDay()
    {
        CurrentDay++;
        Phase          = TimePhase.Day;
        HasFoughtToday = false;

        // 重算體力上限
        SetPlayerLevel(_playerLevel);

        // 新的一天體力回滿
        CurrentActivityStamina = MaxActivityStamina;

        OnDayChanged?.Invoke(CurrentDay);
        OnPhaseChanged?.Invoke(Phase);
        OnStaminaChanged?.Invoke(CurrentActivityStamina, MaxActivityStamina);
    }
}
