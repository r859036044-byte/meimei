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
        ("2", "duvet_pull"),     // 一隻手把被單往下拉開、被子拿掉（影片裡拉被單的那段；不需要前置）
        ("3", "tongue_kiss"),    // 親嘴（影片裡嘴巴張開、舌頭動的那段；再按一次停止；不需要先選 👅）
        ("4", "chest_touch"),    // 摸胸（影片裡躺床、被子在左下角、雙手碰胸的那段；再按一次收手）
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
        ["duvet_pull"]  = ("拉開被子", () => PromoPullDuvet()),
        ["chest_touch"] = ("摸胸", () => PromoChestTouch()),
        ["tongue_kiss"] = ("親嘴", () => { if (_tongueOn) StopTongue(); else StartTongue(); }),
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
    [Export] public bool PromoHideBubbles = true;    // 開始遊戲後莉奈與男主的台詞氣泡一律隱藏（錄影用；台詞照常運作，只是看不到）
    [Export(PropertyHint.Range, "0.05,0.5,0.01")] public float PromoHandRelSize = 0.16f;   // 2 號鍵拉被子的那隻手的寬度＝被子實際寬度的幾倍（跟鏡頭倍率無關；影片約 0.2，這裡再小一點）
    [Export(PropertyHint.Range, "0.3,0.7,0.01")] public float PromoPullU = 0.58f;         // 手抓被子上緣的水平位置（以整張被子圖的寬度算：被子實際佔 0.33~0.68，0.58＝被子偏右）
    [Export(PropertyHint.Range, "0,1.5,0.05")] public float PromoPullLeftRatio = 0.6f;   // 往下拉的同時往左移多少（以下拉距離為 1；0＝垂直往下，0.6＝往左下角）
    [Export(PropertyHint.Range, "0.3,6,0.1")] public float PromoPullSec = 1.1f;      // 往下拉的時間（秒）
    [Export] public bool PromoSilent = true;         // 錄影模式完全安靜：所有聲音（語音、音效、背景音樂）都靜音（錄好影片再後製配音）
    bool _promoMuted, _promoMuteHooked;
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

    /// <summary>錄影模式靜音：把 Master 音訊匯流排靜音（所有聲音都在這條上，包含 TR 語音、音效、BGM）。每幀檢查，避免被別的程式（設定、音量滑桿）解除。
    /// 離開調教室時自動解除，不影響標題畫面等其他場景。</summary>
    void PromoSilenceTick()
    {
        int bus = AudioServer.GetBusIndex("Master");
        if (bus < 0) return;
        if (PromoSilent)
        {
            if (!AudioServer.IsBusMute(bus)) AudioServer.SetBusMute(bus, true);
            _promoMuted = true;
            if (!_promoMuteHooked)
            {
                _promoMuteHooked = true;
                TreeExiting += () => { int b = AudioServer.GetBusIndex("Master"); if (b >= 0) AudioServer.SetBusMute(b, false); };
            }
        }
        else if (_promoMuted)
        {
            AudioServer.SetBusMute(bus, false);   // 把 PromoSilent 關掉：還原聲音
            _promoMuted = false;
        }
    }

    /// <summary>每幀呼叫（TrainingRoomRuntime._Process）：PromoHideUiAlways 開著時，UI 從開始遊戲就一直隱藏。</summary>
    void PromoUiTick()
    {
        if (!PromoHotkeysEnabled) return;
        PromoSilenceTick();
        if (PromoHideBubbles)
        {   // 台詞氣泡：別的程式顯示它就每幀再藏起來（沒有閃一下：藏在同一幀畫面繪製之前）
            if (_bubbleGirl != null && IsInstanceValid(_bubbleGirl)) _bubbleGirl.Visible = false;
            if (_bubblePlayer != null && IsInstanceValid(_bubblePlayer)) _bubblePlayer.Visible = false;
        }
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
        PromoCamGoto(zoom, anchorWorld, anchorScreen, instant);
    }

    /// <summary>鏡頭移到「anchorWorld 這個場景點停在畫面 anchorScreen、倍率 zoom」。1 號鍵（臉前）和 4 號鍵（胸）共用，所以移動方式、速度完全一樣。</summary>
    void PromoCamGoto(float zoom, Vector2 anchorWorld, Vector2 anchorScreen, bool instant)
    {
        _camZoomTarget = zoom; _camAnchorWorld = anchorWorld;
        if (instant)
        {
            _camAnchorScreen = anchorScreen; _promoCamAnimating = false;
            _camZoom = zoom; _camOffset = anchorScreen - anchorWorld * zoom;   // 直接到位（每幀的 UpdateCamera 會再套邊界限制）
            return;
        }
        // 慢慢移動：錨點先設在「目標那個點現在在畫面上的位置」→ 第一幀畫面完全不動（不會瞬移），
        // 之後每幀 PromoCamTick() 依縮放進度把錨點從這裡平順移到最終位置，邊放大邊走到定位
        _promoCamS0 = _camZoom * anchorWorld + _camOffset;
        _promoCamS1 = anchorScreen;
        _promoCamZ0 = _camZoom;
        _camAnchorScreen = _promoCamS0;
        _promoCamAnimating = true;
    }

    bool _promoCamAnimating;
    Vector2 _promoCamS0, _promoCamS1;
    float _promoCamZ0;

    /// <summary>每幀在 UpdateCamera 之前呼叫：鏡頭慢慢移動時，讓錨點跟著縮放進度從起點平順走到終點。</summary>
    void PromoCamTick()
    {
        if (!_promoCamAnimating) return;
        float denom = _camZoomTarget - _promoCamZ0;
        float p = Mathf.Abs(denom) < 0.001f ? 1f : Mathf.Clamp((_camZoom - _promoCamZ0) / denom, 0f, 1f);
        if (p >= 0.999f) { _camAnchorScreen = _promoCamS1; _promoCamAnimating = false; return; }
        _camAnchorScreen = _promoCamS0.Lerp(_promoCamS1, p);
    }


    // 4 號鍵的鏡頭＝影片裡「摸胸」那段：倍率跟臉前鏡頭一樣（影片逐格量出來也是約 0.74～0.75 畫面px／貼圖px），
    // 只是對準點換成兩個胸的中心，停在畫面中央偏上（水平 0.5、垂直 0.38）。移動方式／速度共用 PromoCamSmooth。
    [Export] public Vector2 PromoChestScreenPos = new Vector2(0.5f, 0.38f);                  // 胸中心要停在畫面的哪裡（0~1）
    [Export] public bool PromoChestHands = true;                                              // 4 號鍵同時讓左右手碰胸

    bool _promoChestOn;

    /// <summary>4 號鍵：被子拿掉（左下角）→ 左右手碰胸（肩帶、衣服不動） → 鏡頭（同 1 號鍵的倍率與移動方式）移到胸前。
    /// 再按一次：只收手（取消摸胸動作），鏡頭和被子不動。不需要任何前置。</summary>
    void PromoChestTouch()
    {
        if (_canvas == null) return;
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        bool instant = PromoCamSmooth <= 0.01f;
        _camSmoothOverride = instant ? -1f : PromoCamSmooth;
        if (_promoChestOn)
        {   // 再按一次：只取消摸胸動作（收手）；鏡頭、被子都維持原樣不動
            _promoChestOn = false;
            if (_touchAnimRunningLeft)  CancelHandTouchAnim(true);
            if (_touchAnimRunningRight) CancelHandTouchAnim(false);
            return;
        }

        // 胸中心（場景座標）：胸1／胸2 圖層外框中心的平均。GetGlobalRect 已是場景座標（同 PromoCameraFace 的註解），不再經過 ScreenToWorld
        Vector2 sum = Vector2.Zero; int n = 0;
        foreach (var name in new[] { "胸1", "胸2" })
            if (Layers.TryGetValue(name, out var c) && c != null) { sum += c.GetGlobalRect().GetCenter(); n++; }
        if (n == 0) { GD.PushWarning("[Promo] 摸胸：找不到「胸1／胸2」圖層，鏡頭不動"); return; }
        _promoChestOn = true;

        // 1) 被子拿掉（踢到左下角；直接到位，不演拉的手）
        DuvetState = 1;
        if (_duvet != null && IsInstanceValid(_duvet)) { _duvet.FoldSnap = true; _duvet.FoldTarget = 0f; }
        if (_duvetKicked != null && IsInstanceValid(_duvetKicked)) { _duvetKicked.Drag = Vector2.Zero; _duvetKicked.DragSnap = true; }

        // 2) 左右手碰胸（沒在動才開，避免變成取消）
        if (PromoChestHands)
        {
            if (!_touchAnimRunningLeft)  StartHandTouchAnim(true);
            if (!_touchAnimRunningRight) StartHandTouchAnim(false);
        }

        // 3) 鏡頭：倍率與臉前鏡頭相同
        float charScale = _charRoot != null ? Mathf.Max(0.01f, _charRoot.Scale.X) : CharScale;
        float zoom = Mathf.Clamp(PromoFacePxPerTex / charScale, 1.1f, Mathf.Max(1.1f, CamZoomMax));
        PromoSetUiHidden(true);
        PromoCamGoto(zoom, sum / n, vp * PromoChestScreenPos, instant);
        GD.Print($"[Promo] 摸胸：胸中心 場景{sum / n}；倍率 {zoom:F2}");
    }

    bool _promoPulling;

    /// <summary>2 號鍵：影片裡「手把被單拉下去」——一隻手出現在被子上緣右側，抓著往下拉，被子被拉開（拿掉，踢到左下角）。
    /// 不需要前置：目前不是蓋著的狀態，會先把被子蓋回去再演。
    /// 手畫在獨立的最上層（CanvasLayer 12、螢幕座標），位置每幀由「被子上的點 → 螢幕」換算，所以不受鏡頭、UI 隱藏、圖層順序影響。</summary>
    void PromoPullDuvet()
    {
        if (_duvet == null || !IsInstanceValid(_duvet) || _canvas == null) { GD.PushWarning("[Promo] 拉被子：找不到被子圖層，沒有動作"); return; }
        if (_promoPulling) { GD.Print("[Promo] 拉被子：上一次還沒演完"); return; }
        _promoPulling = true;
        GetTree().CreateTimer(8.0).Timeout += () => _promoPulling = false;   // 保險：不管中間出什麼狀況，8 秒後一定可以再按

        // 前置：被子蓋回去、清掉殘留的拉痕
        DuvetState = 0;
        _duvet.FoldSnap = true; _duvet.FoldTarget = 0f;
        if (_duvetKicked != null && IsInstanceValid(_duvetKicked)) { _duvetKicked.Drag = Vector2.Zero; _duvetKicked.DragSnap = true; }

        // 被子上「抓點」的場景座標：用被子控制項自己的全域變換（含縮放／旋轉），再用目前鏡頭換成螢幕座標
        Transform2D xf = _duvet.GetGlobalTransform();
        Vector2 sz = _duvet.Size;
        float u = PromoPullU;
        _duvet.PullX = u;
        Vector2 startW = xf * new Vector2(u * sz.X, (_duvet.TopV + 0.03f) * sz.Y);
        Vector2 bandW  = xf.BasisXform(new Vector2(0f, Mathf.Max(8f, _duvet.GrabBandV * sz.Y)));   // 抓滿要往下拉的距離（場景座標）
        Vector2 W2S(Vector2 w) => _canvas.Transform * w;
        GD.Print($"[Promo] 拉被子：抓點 場景{startW} → 螢幕{W2S(startW)}；下拉距離 {bandW}；被子 Size={sz} TopV={_duvet.TopV:F2} GrabBandV={_duvet.GrabBandV:F2}；鏡頭倍率 {_camZoom:F2}");

        var tex = ResourceHelper.LoadTexAny("Sprites/右手");   // 影片裡逐格比對是「右手」圖
        CanvasLayer layer = null; TextureRect hand = null;
        Vector2 handSize = Vector2.Zero;
        if (tex != null)
        {
            layer = new CanvasLayer { Layer = 12, Name = "PromoHandLayer" };
            AddChild(layer);
            // 手的寬度＝被子實際內容寬度（約佔整張被子圖的 0.353）在畫面上的寬度 × PromoHandRelSize；所以不管鏡頭拉多近，手跟被子的比例都一樣
            float duvetScreenW = (xf.BasisXform(new Vector2(0.353f * sz.X, 0f)) * _camZoom).Length();
            float w = Mathf.Max(24f, duvetScreenW * PromoHandRelSize);
            handSize = new Vector2(w, w * tex.GetHeight() / Mathf.Max(1, tex.GetWidth()));
            hand = new TextureRect
            {
                Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0f), Size = handSize,
            };
            layer.AddChild(hand);
        }
        else GD.PushWarning("[Promo] 拉被子：載入不到 Sprites/右手，沒有手的圖（被子還是會被拉開）");

        Vector2 vp = GetViewport().GetVisibleRect().Size;
        void PlaceHand(Vector2 worldPoint, float wiggleX = 0f)
        {
            if (hand == null || !IsInstanceValid(hand)) return;
            Vector2 sp = W2S(worldPoint) + new Vector2(wiggleX, 0f) - handSize * new Vector2(0.15f, 0.2f);   // 手指尖（右手圖左上）對準抓點
            sp.X = Mathf.Clamp(sp.X, -handSize.X * 0.5f, vp.X - handSize.X * 0.5f);   // 萬一算出來在畫面外，至少讓手露出來（也方便看出算錯）
            sp.Y = Mathf.Clamp(sp.Y, -handSize.Y * 0.5f, vp.Y - handSize.Y * 0.5f);
            hand.Position = sp;
        }
        // 手從被子右邊外側「移動到被子上」：起點在抓點右方一個被子寬度的 0.45、稍微往下
        Vector2 approachW = startW + xf.BasisXform(new Vector2(0.45f * 0.353f * sz.X, 0f)) + bandW * 0.5f;
        PlaceHand(approachW);

        bool pullStarted = false;
        var tw = CreateTween();
        if (hand != null) tw.TweenProperty(hand, "modulate:a", 0.92f, 0.3);                           // 手淡入
        tw.TweenMethod(Callable.From<float>(v =>                                                      // 手移動到被子上緣的抓點
        {
            float e = v * v * (3f - 2f * v);
            PlaceHand(approachW.Lerp(startW, e));
        }), 0f, 1f, 0.7);
        tw.TweenMethod(Callable.From<float>(v => PlaceHand(startW, Mathf.Sin(v * Mathf.Pi * 2f) * 6f)), 0f, 1f, 0.35);   // 抓住前輕輕動一下
        tw.TweenMethod(Callable.From<float>(v =>                                                      // 抓著往下拉
        {
            float p = v * v * (3f - 2f * v);   // 緩入緩出
            if (_duvet == null || !IsInstanceValid(_duvet)) return;
            _duvet.FoldSnap = true; _duvet.FoldTarget = p;
            if (!pullStarted && p > 0.02f) { pullStarted = true; _duvet.BeginPull(_duvet.PullX); }   // 新增一組皺褶線（跟玩家拉的時候一樣）
            PlaceHand(startW + bandW * p + new Vector2(-bandW.Length() * PromoPullLeftRatio * p, 0f));   // 往下拉的同時往左移＝往左下角拉
        }), 0f, 1f, PromoPullSec);
        tw.TweenCallback(Callable.From(() =>
        {   // 抓滿：被子被踢到左下角（跟玩家拉滿時一樣）
            if (_duvet != null && IsInstanceValid(_duvet))
            {
                DuvetState = 1;
                _duvet.FoldSnap = true; _duvet.FoldTarget = 0f;
                if (_duvetKicked != null && IsInstanceValid(_duvetKicked)) { _duvetKicked.Drag = Vector2.Zero; _duvetKicked.DragSnap = true; }
            }
        }));
        if (hand != null) tw.TweenProperty(hand, "modulate:a", 0f, 0.4);                              // 手淡出
        tw.TweenCallback(Callable.From(() => { if (layer != null && IsInstanceValid(layer)) layer.QueueFree(); _promoPulling = false; }));
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
