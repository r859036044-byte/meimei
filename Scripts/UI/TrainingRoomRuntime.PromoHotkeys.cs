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
    // 1 號鍵的鏡頭＝使用者影片裡「親吻的臉鏡頭」：用影片逐格量出來——臉在畫面中央偏上（水平 0.5、垂直 0.39），
    // 臉的大小是「畫面 1920 寬時，每個貼圖像素佔 0.74 個畫面像素」（相當於比影片的遠景再拉近約 1.38 倍）。
    [Export(PropertyHint.Range, "0.2,1.5,0.01")] public float PromoFacePxPerTex = 0.74f;   // 臉前鏡頭的大小（畫面像素／貼圖像素）；會自動換算成目前的鏡頭倍率，不受 CharScale 影響
    [Export] public Vector2 PromoFaceScreenPos = new Vector2(0.5f, 0.39f);                  // 臉要停在畫面的哪裡（0~1）
    [Export] public Vector2 PromoFacePointTex = new Vector2(960f, 700f);                    // 臉中心在角色貼圖（2000×3200）上的位置
    [Export(PropertyHint.Range, "0,10,0.1")] public float PromoCamSmooth = 1.5f;            // 鏡頭移動速度：越小越慢（1.5≈約 2 秒慢慢移過去）；0＝直接切到位（影片裡是直接切）
    [Export] public bool PromoHotkeyToast = false;         // 按下時在畫面左上角短暫顯示動作名稱（錄影時建議關掉，只留在輸出的 log）

    // 預設對應：數字 → 動作代號（目前只有 1 號鍵；其他鍵等你指定。想先試別的動作可在 promo_hotkeys.json 加）
    static readonly (string key, string action)[] PromoDefaultMap =
    {
        ("1", "cam_face"),       // 鏡頭移到莉奈的臉前（再按一次拉回原本的鏡頭）
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
    bool _promoCamOnFace;
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
        ["cam_face"]    = ("鏡頭：臉前", () => PromoCameraFace()),
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
        ["ui_toggle"]   = ("UI 顯示切換", () => { PromoHideUiAlways = !PromoHideUiAlways; PromoSetUiHidden(PromoHideUiAlways); }),
        ["reset"]       = ("全部重置",   () => PromoReset()),
    };

    [Export] public bool PromoHideUiAlways = true;   // 開始遊戲後 UI 一律隱藏（左側工具鈕、「?」、「☰ 選單」、HUD；錄影用）。要把 UI 叫回來：改成 false，或把 promo_hotkeys.json 某個鍵綁 ui_toggle
    bool _promoUiHidden;                // 目前是不是由本系統隱藏著
    CanvasItem _promoMenuBtn;
    Color _promoMenuBtnModulate = Colors.White;
    Control.MouseFilterEnum _promoMenuBtnMouse = Control.MouseFilterEnum.Stop;
    double _promoFindMenuT;

    /// <summary>隱藏／還原所有 UI（場景裡的東西不動）。隱藏時每幀都會呼叫來「釘住」，避免別的程式又把它顯示出來。</summary>
    void PromoSetUiHidden(bool hide)
    {
        if (_uiCanvas != null && IsInstanceValid(_uiCanvas)) _uiCanvas.Visible = !hide;   // HUD、側邊鈕、「?」都在這一層
        // 右上角「☰ 選單」是 NovelReader 建的，而且 NovelReader 每幀都會自己改它的 Visible（NovelReader.cs:337），
        // 所以不能用 Visible 隱藏：改成「完全透明＋不接滑鼠」，NovelReader 怎麼改 Visible 都看不到、也點不到
        if (_promoMenuBtn == null || !IsInstanceValid(_promoMenuBtn))
        {
            double now = Time.GetTicksMsec() / 1000.0;
            if (now - _promoFindMenuT < 0.5) goto afterMenu;   // 找不到時別每幀都搜整棵樹
            _promoFindMenuT = now;
            _promoMenuBtn = GetTree()?.Root?.FindChild("NovelMenuButton", true, false) as CanvasItem;
            if (_promoMenuBtn == null) goto afterMenu;
        }
        if (hide)
        {
            if (!_promoUiHidden)
            {   // 第一次隱藏：記住原本的透明度與滑鼠設定
                _promoMenuBtnModulate = _promoMenuBtn.Modulate;
                if (_promoMenuBtn is Control c0) _promoMenuBtnMouse = c0.MouseFilter;
            }
            _promoMenuBtn.Modulate = new Color(1f, 1f, 1f, 0f);
            if (_promoMenuBtn is Control c1) c1.MouseFilter = Control.MouseFilterEnum.Ignore;
        }
        else if (_promoUiHidden)
        {
            _promoMenuBtn.Modulate = _promoMenuBtnModulate;
            if (_promoMenuBtn is Control c2) c2.MouseFilter = _promoMenuBtnMouse;
        }
    afterMenu:
        _promoUiHidden = hide;
    }

    /// <summary>每幀呼叫（TrainingRoomRuntime._Process）：PromoHideUiAlways 開著時，UI 從開始遊戲就一直隱藏。</summary>
    void PromoUiTick()
    {
        if (!PromoHotkeysEnabled) return;
        if (PromoHideUiAlways) PromoSetUiHidden(true);
        else if (_promoUiHidden && !_promoCamOnFace) PromoSetUiHidden(false);
    }

    /// <summary>1 號鍵：UI 隱藏、被子蓋回身上、鏡頭慢慢移到莉奈臉前（親吻的臉鏡頭）；再按一次：UI 還原、鏡頭慢慢回原位。
    /// PromoCamSmooth＝0 時鏡頭直接切換。</summary>
    void PromoCameraFace()
    {
        if (_canvas == null) return;
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        bool instant = PromoCamSmooth <= 0.01f;
        _camSmoothOverride = instant ? -1f : PromoCamSmooth;
        float zoom; Vector2 anchorScreen, anchorWorld;
        if (_promoCamOnFace)
        {
            _promoCamOnFace = false;
            if (!PromoHideUiAlways) PromoSetUiHidden(false);   // UI 一律隱藏時，離開臉前鏡頭也不把 UI 叫回來
            zoom = 1.1f;
            Vector2 defOff = new Vector2(0f, -vp.Y * (1.1f - 1f) * 0.5f + CamDefaultOffsetY);   // 1.1 倍時的預設鏡頭位置
            anchorScreen = vp * 0.5f;
            anchorWorld  = (vp * 0.5f - defOff) / 1.1f;
        }
        else
        {
            if (!Layers.TryGetValue("臉", out var fl) || fl == null) { GD.PushWarning("[Promo] 找不到「臉」圖層，鏡頭不動"); return; }
            // 注意：Control.GetGlobalRect() 給的已經是「場景座標」（不含 CanvasLayer 的鏡頭縮放／平移），不能再經過 ScreenToWorld
            // （之前這裡多轉了一次，臉就偏到畫面右下；現在直接當場景座標用）
            Rect2 r = fl.GetGlobalRect();
            anchorWorld  = r.Position + PromoFacePointTex / new Vector2(2000f, 3200f) * r.Size;
            float charScale = _charRoot != null ? Mathf.Max(0.01f, _charRoot.Scale.X) : CharScale;
            zoom = Mathf.Clamp(PromoFacePxPerTex / charScale, 1.1f, Mathf.Max(1.1f, CamZoomMax));
            anchorScreen = vp * PromoFaceScreenPos;
            _promoCamOnFace = true;
            PromoSetUiHidden(true);
            if (DuvetState != 0)
            {   // 被子蓋回身上（等於玩家把踢開的被子蓋回去）
                DuvetState = 0;
                if (_duvet != null && IsInstanceValid(_duvet)) { _duvet.FoldSnap = true; _duvet.FoldTarget = 0f; }
            }
        }
        _camZoomTarget = zoom; _camAnchorScreen = anchorScreen; _camAnchorWorld = anchorWorld;
        if (instant) { _camZoom = zoom; _camOffset = anchorScreen - anchorWorld * zoom; }   // 直接到位（每幀的 UpdateCamera 會再套邊界限制）
    }

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
