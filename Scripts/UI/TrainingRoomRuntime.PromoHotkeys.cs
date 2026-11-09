using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// TrainingRoomRuntime — 宣傳片錄影用快捷鍵：按一個數字鍵，不用任何前置，直接執行對應的動畫／動作。
///
/// • 對應表（數字鍵 → 動作代號）預設寫在 PromoDefaultMap；也可以用 Resources/Data/promo_hotkeys.json 覆蓋
///   （只要寫要改的鍵，沒寫的沿用預設；值填 "none" 就是這個鍵不做事）。主鍵盤數字列和數字鍵盤都算。
/// • 動作代號：見 BuildPromoActions()。也可以寫 "expr:害羞" 這種格式，直接切到指定表情。
/// • 按住 Ctrl／Alt／Meta 時不觸發（避免跟其他快捷鍵衝突）。
/// • 這支檔案是錄影版專用：想完全關掉，把 PromoHotkeysEnabled 設成 false，或不要合併這個分支。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("宣傳片快捷鍵")]
    [Export] public bool PromoHotkeysEnabled = true;       // 總開關
    [Export] public bool PromoHotkeyToast = false;         // 按下時在畫面左上角短暫顯示動作名稱（錄影時建議關掉，只留在輸出的 log）

    // 預設對應：數字 → 動作代號
    static readonly (string key, string action)[] PromoDefaultMap =
    {
        ("1", "hand_left"),      // 左手觸摸動畫（再按一次收起）
        ("2", "hand_right"),     // 右手觸摸動畫（再按一次收起）
        ("3", "b_touch"),        // 摸 B（再按一次收起）
        ("4", "pat"),            // 摸頭（再按一次收起）
        ("5", "mini_climax"),    // 小高潮
        ("6", "climax"),         // 大高潮（含表情、痙攣）
        ("7", "squirt"),         // 噴水（再按一次停止）
        ("8", "ejaculate"),      // 射精（⚠ 試玩版 DemoMode 開著時會直接進「試玩版已結束」）
        ("9", "expr_next"),      // 下一個表情
        ("0", "reset"),          // 全部收起來、表情回預設
    };

    // 表情順序（跟 FaceExpressionController.DefaultExpressionNames 一樣；那邊是 private 所以這裡抄一份）
    static readonly string[] PromoExprNames =
    {
        "害怕", "有點驚訝", "忍耐", "害羞", "開心", "舒服", "傲嬌", "插入1", "插入2",
        "做愛(半閉)", "做愛(全閉)", "做愛白眼", "做愛3", "做愛4", "高潮1", "高潮2", "高潮", "ZZZ",
    };

    Dictionary<string, string> _promoMap;                                // "1" → "hand_left"
    Dictionary<string, (string label, Action run)> _promoActions;        // 動作代號 → (顯示名稱, 執行)
    int _promoExprIdx = -1;
    bool _promoDotsHidden;
    Label _promoToastLabel;

    /// <summary>在 _Input 最前面呼叫。有處理（吃掉這個按鍵）回傳 true。</summary>
    bool PromoHotkeyInput(InputEvent e)
    {
        if (!PromoHotkeysEnabled) return false;
        if (e is not InputEventKey k || !k.Pressed || k.Echo) return false;
        if (k.CtrlPressed || k.AltPressed || k.MetaPressed) return false;
        string digit = PromoDigitOf(k.Keycode);
        if (digit == null) return false;
        EnsurePromoMap();
        if (!_promoMap.TryGetValue(digit, out var action) || string.IsNullOrEmpty(action) || action == "none") return false;
        RunPromoAction(action);
        return true;
    }

    static string PromoDigitOf(Key code) => code switch
    {
        Key.Key0 or Key.Kp0 => "0", Key.Key1 or Key.Kp1 => "1", Key.Key2 or Key.Kp2 => "2",
        Key.Key3 or Key.Kp3 => "3", Key.Key4 or Key.Kp4 => "4", Key.Key5 or Key.Kp5 => "5",
        Key.Key6 or Key.Kp6 => "6", Key.Key7 or Key.Kp7 => "7", Key.Key8 or Key.Kp8 => "8",
        Key.Key9 or Key.Kp9 => "9", _ => null,
    };

    void EnsurePromoMap()
    {
        if (_promoMap != null) return;
        _promoMap = new Dictionary<string, string>();
        foreach (var (key, action) in PromoDefaultMap) _promoMap[key] = action;
        _promoActions = BuildPromoActions();
        const string path = "res://Resources/Data/promo_hotkeys.json";
        try
        {
            if (FileAccess.FileExists(path))
            {
                var parsed = Json.ParseString(FileAccess.GetFileAsString(path));
                if (parsed.VariantType == Variant.Type.Dictionary)
                    foreach (var kv in parsed.AsGodotDictionary())
                    {
                        string key = kv.Key.AsString();
                        if (key.StartsWith("_")) continue;   // 「_說明」之類的註解欄位
                        _promoMap[key] = kv.Value.AsString();
                    }
            }
        }
        catch (Exception ex) { GD.PushWarning("[Promo] 讀取 promo_hotkeys.json 失敗，改用預設對應：" + ex.Message); }
        GD.Print("[Promo] 快捷鍵對應：" + string.Join("、", _promoMap));
    }

    void RunPromoAction(string action)
    {
        try
        {
            if (action.StartsWith("expr:"))
            {
                string name = action.Substring(5);
                FaceController?.SetExpression(name);
                PromoToast("表情：" + name);
                return;
            }
            if (_promoActions.TryGetValue(action, out var a)) { PromoToast(a.label); a.run(); }
            else GD.PushWarning("[Promo] 不認得的動作代號：" + action);
        }
        catch (Exception ex) { GD.PushWarning($"[Promo] 動作 {action} 執行失敗：{ex.Message}"); }
    }

    Dictionary<string, (string, Action)> BuildPromoActions() => new()
    {
        ["hand_left"]   = ("左手觸摸",   () => ToggleHandTouchAnim(true)),
        ["hand_right"]  = ("右手觸摸",   () => ToggleHandTouchAnim(false)),
        ["b_touch"]     = ("摸 B",       () => ToggleBTouch()),
        ["pat"]         = ("摸頭",       () => { if (_patOn) CancelPat(); else StartPat(); }),
        ["mini_climax"] = ("小高潮",     () => TriggerMiniClimax()),
        ["climax"]      = ("大高潮",     () => TriggerClimax()),
        ["squirt"]      = ("噴水",       () => { if (_squirtActive) StopSquirt(); else StartSquirt(); }),
        ["ejaculate"]   = ("射精",       () => TriggerEjaculation()),
        ["breath"]      = ("嬌喘聲",     () => PlayBreathMoan()),
        ["expr_next"]   = ("下一個表情", () => PromoStepExpression(+1)),
        ["expr_prev"]   = ("上一個表情", () => PromoStepExpression(-1)),
        ["night_filter"]= ("夜晚濾鏡",   () => ToggleNightFilter()),
        ["dots_toggle"] = ("綠點顯示切換", () => { _promoDotsHidden = !_promoDotsHidden; SetHotspotDotsHidden(_promoDotsHidden); }),
        ["reset"]       = ("全部重置",   () => PromoReset()),
    };

    void PromoStepExpression(int dir)
    {
        if (FaceController == null) return;
        _promoExprIdx = ((_promoExprIdx < 0 ? (dir > 0 ? -1 : 0) : _promoExprIdx) + dir + PromoExprNames.Length) % PromoExprNames.Length;
        FaceController.SetExpression(PromoExprNames[_promoExprIdx]);
        PromoToast("表情：" + PromoExprNames[_promoExprIdx]);
    }

    void PromoReset()
    {
        if (_touchAnimRunningLeft)  CancelHandTouchAnim(true);
        if (_touchAnimRunningRight) CancelHandTouchAnim(false);
        if (_bTouchRunning) CancelBTouch();
        if (_patOn) CancelPat();
        if (_squirtActive) StopSquirt();
        _promoExprIdx = -1;
        if (FaceController != null && !string.IsNullOrEmpty(FaceController.DefaultExpression)) FaceController.SetExpression(FaceController.DefaultExpression);
    }

    void PromoToast(string text)
    {
        GD.Print("[Promo] " + text);
        if (!PromoHotkeyToast || _canvas == null) return;
        if (_promoToastLabel == null || !IsInstanceValid(_promoToastLabel))
        {
            _promoToastLabel = new Label { Position = new Vector2(24f, 20f), MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 4000 };
            _promoToastLabel.AddThemeFontSizeOverride("font_size", 28);
            _canvas.AddChild(_promoToastLabel);
        }
        _promoToastLabel.Text = text; _promoToastLabel.Modulate = Colors.White;
        var tw = CreateTween();
        tw.TweenInterval(0.8);
        tw.TweenProperty(_promoToastLabel, "modulate:a", 0f, 0.4);
    }
}
