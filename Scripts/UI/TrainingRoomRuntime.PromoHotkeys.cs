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
        ("9", "outside_ejac"),   // 外射精：去掉親嘴、摸胸，鏡頭對準臉（可自由縮放），精液一發一發射到頭和臉的範圍（避開眼睛嘴巴）；不需要雞雞
        ("8", "zoom_jiji"),      // 用 7 秒慢慢縮小鏡頭，縮到原版 7 號鍵（臉前）的位置；再按一次中途停止
        ("7", "face_kiss_chest"), // 鏡頭換成臉部特寫的倍率（不鎖臉）並慢慢往上移一小段、鎖住滾輪縮放＋親嘴＋摸胸（左手摸胸、右手摸右邊乳頭）＋身體上下搖擺＋抽插狀態的外觀（嘴型、汗、眉毛、表情），鏡頭始終在臉前；再按一次全部停止（沒有雞雞、沒有抽插）
        ("5", "pussy_touch"),    // 摸小穴（影片裡腿張開、一根手指摸小穴的那段；鏡頭跟 4 號鍵同一個；再按一次收手）
        ("4", "chest_touch"),    // 摸胸（影片裡躺床、被子在左下角、左手摸胸、右手摸乳頭的那段；再按一次收手）
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
        ["pussy_touch"] = ("摸小穴", () => PromoPussyTouch()),
        ["insert"]      = ("插入", () => PromoInsert()),
        ["thrust"]      = ("抽插", () => PromoThrust()),
        ["face_kiss_chest"] = ("臉部特寫＋親嘴＋摸胸", () => PromoFaceKissChest()),
        ["outside_ejac"] = ("外射精（射到頭）", () => PromoOutsideEjac()),
        ["zoom_jiji"]   = ("慢慢縮小到臉前鏡頭", () => PromoZoomJiji()),
        ["tongue_kiss"] = ("親嘴", () => PromoKissToggle()),
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
        PromoSwayTick((float)GetProcessDeltaTime());
        if (_promoDeepT > 0f && _jijiPhase == 0) { _promoDeepT = 0f; _promoInsertOn = false; _promoForceErect = false; _promoThrustOn = false; _promo7On = false; }   // 雞雞被收起來：清掉 6／7 號鍵的狀態
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
        if (!instant && Mathf.Abs(zoom - _camZoom) < 0.02f)
        {   // 倍率幾乎沒變（例如已經在 4 號鍵的鏡頭再按 5）：UpdateCamera 只在倍率變動時才重算位置，所以這裡自己讓位移平順走過去
            _camZoom = zoom; _camAnchorScreen = anchorScreen; _promoCamAnimating = false;
            _promoCamOffTo = anchorScreen - anchorWorld * zoom; _promoCamOffAnim = true;
            return;
        }
        _promoCamOffAnim = false;
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
    bool _promoCamOffAnim; Vector2 _promoCamOffTo;
    void PromoCamTick()
    {
        if (_promoZoomOn) { _promo7HoldT = 0f; _promoPanOn = false; PromoZoomStep((float)GetProcessDeltaTime()); return; }
        if (_promoPanOn)
        {   // 7 號鍵：鏡頭從目前位置慢慢往上移一小段（倍率不變）
            Vector2 vp = GetViewport().GetVisibleRect().Size;
            _promoPanT += (float)GetProcessDeltaTime();
            float p = Mathf.Clamp(_promoPanT / Mathf.Max(0.3f, PromoPanSec), 0f, 1f);
            float e = p * p * (3f - 2f * p);   // 慢慢開始、慢慢停
            _camOffset.Y = Mathf.Lerp(_promoPanFromY, _promoPanToY, e);
            _camAnchorScreen = new Vector2(vp.X * 0.5f, vp.Y * 0.5f + (_camOffset.Y - _promoPanFromY));
            _camAnchorWorld = (_camAnchorScreen - _camOffset) / _camZoom;
            if (p >= 1f) _promoPanOn = false;
            return;
        }
        if (_promo7HoldT > 0f) { _promo7HoldT -= (float)GetProcessDeltaTime(); PromoFaceShot(true); return; }   // 7 號鍵：鏡頭釘在臉部特寫
        if (_promoCamOffAnim)
        {
            float k = 1f - Mathf.Exp(-Mathf.Max(0.3f, PromoCamSmooth) * (float)GetProcessDeltaTime());
            _camOffset = _camOffset.Lerp(_promoCamOffTo, k);
            if ((_camOffset - _promoCamOffTo).Length() < 0.5f) { _camOffset = _promoCamOffTo; _promoCamOffAnim = false; }
            return;
        }
        if (!_promoCamAnimating) return;
        float denom = _camZoomTarget - _promoCamZ0;
        float p = Mathf.Abs(denom) < 0.001f ? 1f : Mathf.Clamp((_camZoom - _promoCamZ0) / denom, 0f, 1f);
        if (p >= 0.999f) { _camAnchorScreen = _promoCamS1; _promoCamAnimating = false; return; }
        _camAnchorScreen = _promoCamS0.Lerp(_promoCamS1, p);
    }


    // 4 號鍵（摸胸）和 5 號鍵（摸小穴）共用影片裡「躺床整張床」的鏡頭：影片兩段的構圖一樣——
    // 莉奈的臉在畫面上方（垂直約 0.19）、胸在 0.37 左右。胸中心用貼圖座標換算（胸1／胸2 圖層是整張 2000×3200 的畫布，不能拿圖層外框中心當胸的位置）。
    // 移動方式／速度共用 PromoCamSmooth。
    [Export] public Vector2 PromoChestScreenPos = new Vector2(0.5f, 0.37f);                  // 胸中心要停在畫面的哪裡（0~1）
    [Export] public Vector2 PromoChestPointTex = new Vector2(925f, 993f);                    // 兩個胸的中心在角色貼圖（2000×3200）上的位置（胸1 977,998／胸2 873,988 的中點）
    [Export(PropertyHint.Range, "0.2,1.5,0.01")] public float PromoBedPxPerTex = 0.66f;     // 床上鏡頭的大小（畫面像素／貼圖像素；從影片量的估計值，想再近／遠就調這個）
    [Export] public bool PromoChestHands = true;                                              // 4 號鍵同時讓左右手碰胸

    bool _promoChestOn;

    /// <summary>4 號鍵：被子拿掉（左下角）→ 左手摸胸＋右手摸乳頭（肩帶、衣服不動） → 鏡頭（同 1 號鍵的倍率與移動方式）移到胸前。
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
            if (_nipR.On) CancelNipple("NR");
            return;
        }

        _promoChestOn = true;

        // 1) 被子拿掉（踢到左下角；直接到位，不演拉的手）
        DuvetState = 1;
        if (_duvet != null && IsInstanceValid(_duvet)) { _duvet.FoldSnap = true; _duvet.FoldTarget = 0f; }
        if (_duvetKicked != null && IsInstanceValid(_duvetKicked)) { _duvetKicked.Drag = Vector2.Zero; _duvetKicked.DragSnap = true; }

        // 2) 左手摸胸、右手摸乳頭（右邊那顆乳頭，食指；沒在動才開，避免變成取消）
        if (PromoChestHands)
        {
            if (!_touchAnimRunningLeft) StartHandTouchAnim(true);
            if (_touchAnimRunningRight) CancelHandTouchAnim(false);   // 右手不是摸胸，是摸乳頭
            if (!_nipR.On) StartNipple("NR");
        }

        // 3) 鏡頭：影片的床上鏡頭（5 號鍵共用）
        PromoBedCamera(instant);
    }


    /// <summary>床上鏡頭（4、5 號鍵共用）：胸中心停在畫面 PromoChestScreenPos，倍率 PromoBedPxPerTex。</summary>
    void PromoBedCamera(bool instant)
    {
        if (!Layers.TryGetValue("臉", out var fl) || fl == null) { GD.PushWarning("[Promo] 找不到「臉」圖層，鏡頭不動"); return; }
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        Rect2 r = fl.GetGlobalRect();   // 已是場景座標（同 PromoCameraFace）
        Vector2 anchorWorld = r.Position + PromoChestPointTex / new Vector2(2000f, 3200f) * r.Size;
        float charScale = _charRoot != null ? Mathf.Max(0.01f, _charRoot.Scale.X) : CharScale;
        float zoom = Mathf.Clamp(PromoBedPxPerTex / charScale, 1.1f, Mathf.Max(1.1f, CamZoomMax));
        _camSmoothOverride = instant ? -1f : PromoCamSmooth;
        PromoSetUiHidden(true);
        PromoCamGoto(zoom, anchorWorld, vp * PromoChestScreenPos, instant);
        GD.Print($"[Promo] 床上鏡頭：胸中心 場景{anchorWorld}；倍率 {zoom:F2}（目前倍率 {_camZoom:F2}）");
    }


    /// <summary>床上的前置（5、6 號鍵共用）：被子拿掉（左下角）、內褲脫掉、進 M 腿（腿張開）。ToggleMLegModeAsync 開頭會自己把鏡頭拉到 M 腿鏡頭，呼叫端之後再蓋成影片鏡頭。</summary>
    void PromoBedSetup()
    {
        DuvetState = 1;
        if (_duvet != null && IsInstanceValid(_duvet)) { _duvet.FoldSnap = true; _duvet.FoldTarget = 0f; }
        if (_duvetKicked != null && IsInstanceValid(_duvetKicked)) { _duvetKicked.Drag = Vector2.Zero; _duvetKicked.DragSnap = true; }
        if (!_pantyGone) { _pantyGone = true; _pantyPullT = PantyDoneT; _pantyFadeA = 0f; }   // 跟玩家把內褲拉到底放手同一組狀態
        if (!_mLegOpen && !_mLegTransitioning) TaskLogger.Run(ToggleMLegModeAsync(), "TrainingRoom.ToggleMLegMode");
    }

    bool _promoPussyOn;

    /// <summary>5 號鍵：被子拿掉、內褲脫掉、進 M 腿（腿張開）、一根手指摸小穴；鏡頭＝影片的床上鏡頭（同 4 號鍵）。
    /// 再按一次：只收手（取消摸小穴），鏡頭、被子、腿都不動。不需要任何前置。</summary>
    void PromoPussyTouch()
    {
        if (_canvas == null) return;
        if (_promoPussyOn)
        {
            _promoPussyOn = false;
            if (_bTouchRunning) CancelBTouch();
            return;
        }
        if (_mLegTransitioning) { GD.Print("[Promo] 摸小穴：M 腿切換中，請稍後再按"); return; }
        _promoPussyOn = true;
        if (_touchAnimRunningLeft)  CancelHandTouchAnim(true);   // 影片裡只有摸小穴的那一根手指
        if (_touchAnimRunningRight) CancelHandTouchAnim(false);
        if (_patOn) CancelPat();

        PromoBedSetup();
        PromoBedCamera(PromoCamSmooth <= 0.01f);

        // 4) M 腿淡入完成後，開始摸小穴（切換一開始會把摸 B 取消，所以要等切換完）
        float wait = _mLegOpen && !_mLegTransitioning ? 0.05f : MLegFadeDuration + 0.15f;
        GetTree().CreateTimer(wait).Timeout += () =>
        {
            if (!_promoPussyOn || !_mLegOpen) return;
            if (!_bTouchRunning) StartBTouch();
        };
    }


    // 6 號鍵（插入）的鏡頭＝影片裡插入那段：比床上鏡頭再拉近約 1.6 倍，以小穴為中心，小穴在畫面中央偏下（水平 0.5、垂直 0.46）。
    // 影片的倍率換算出來比 CamZoomMax（預設 3）還大一點，會被擋在 CamZoomMax；想再近就把 CamZoomMax 調大。
    [Export] public Vector2 PromoInsertScreenPos = new Vector2(0.5f, 0.46f);                 // 小穴要停在畫面的哪裡（0~1）
    [Export(PropertyHint.Range, "0.2,2,0.01")] public float PromoInsertPxPerTex = 1.05f;     // 插入鏡頭的大小（畫面像素／貼圖像素；從影片量的估計值）
    [Export(PropertyHint.Range, "0.5,12,0.5")] public float PromoInsertSec = 2f;            // 雞雞從入口推進到突破點的時間（秒）
    [Export(PropertyHint.Range, "0.5,12,0.5")] public float PromoDeepSec = 3f;               // 突破後「往上多拉」的時間（秒）
    [Export(PropertyHint.Range, "0,1,0.05")] public float PromoInsertDepth = 0f;          // 突破後再往上多拉多深（0＝不多拉，只看到插進去；越大越深，但男人身體後面的空隙會露出來，建議保持 0）
    bool _promoInsertAuto, _promoForceErect, _promoInsertOn;
    float _promoDeepT;   // 突破後往上多拉的比例（Jiji.cs 的深插中心位置用）

    /// <summary>6 號鍵：被子拿掉、內褲脫掉、進 M 腿 → 鏡頭拉近到小穴 → 雞雞出現、靠近、插進去（直接突破到深插，不用按住滑鼠）。
    /// 再按一次：雞雞收起來（鏡頭不動）。不需要任何前置。</summary>
    void PromoInsert()
    {
        if (_canvas == null) return;
        if (_promoInsertOn)
        {
            _promoInsertOn = false; _promoInsertAuto = false; _promoForceErect = false; _promoDeepT = 0f;
            if (_jijiPhase > 0) HideJiji();
            return;
        }
        if (_mLegTransitioning) { GD.Print("[Promo] 插入：M 腿切換中，請稍後再按"); return; }
        _promoInsertOn = true; _promoDeepT = 0f;
        TaskLogger.Run(PromoInsertAsync(), "TrainingRoom.PromoInsert");
    }

    /// <summary>插入鏡頭（6 號鍵）：影片裡插入那段的鏡頭，以小穴為中心；男人身體後面的空隙由鏡頭護欄（Camera.cs BellyGapGuard）擋住。</summary>
    void PromoInsertShot(bool instant = false)
    {
        if (_hotspotRTs != null && _hotspotRTs.Length > 4 && _hotspotRTs[4] != null && IsInstanceValid(_hotspotRTs[4]))
        {
            Vector2 vp = GetViewport().GetVisibleRect().Size;
            Vector2 pussyWorld = _hotspotRTs[4].GetGlobalRect().GetCenter();   // 已是場景座標
            float charScale = _charRoot != null ? Mathf.Max(0.01f, _charRoot.Scale.X) : CharScale;
            float zoom = Mathf.Clamp(PromoInsertPxPerTex / charScale, 1.1f, Mathf.Max(1.1f, CamZoomMax));
            bool inst = instant || PromoCamSmooth <= 0.01f;
            _camSmoothOverride = inst ? -1f : PromoCamSmooth;
            PromoSetUiHidden(true);
            PromoCamGoto(zoom, pussyWorld, vp * PromoInsertScreenPos, inst);
            GD.Print($"[Promo] 插入鏡頭：小穴 場景{pussyWorld}；倍率 {zoom:F2}");
        }
    }

    async System.Threading.Tasks.Task PromoInsertAsync(bool moveCamera = true, bool thenThrust = false, bool extras = true)
    {
        async System.Threading.Tasks.Task Wait(float sec) => await ToSignal(GetTree().CreateTimer(sec), SceneTreeTimer.SignalName.Timeout);
        try
        {
            if (_touchAnimRunningLeft)  CancelHandTouchAnim(true);
            if (_touchAnimRunningRight) CancelHandTouchAnim(false);
            if (_nipR.On) CancelNipple("NR");
            if (_nipL.On) CancelNipple("NL");
            if (_bTouchRunning) CancelBTouch();
            if (_patOn) CancelPat();
            _promoChestOn = false; _promoPussyOn = false;

            PromoBedSetup();
            for (float t = 0f; (_mLegTransitioning || !_mLegOpen) && t < 4f; t += 0.05f) await Wait(0.05f);   // 等 M 腿淡入完成
            if (!_promoInsertOn || !_mLegOpen) { _promoInsertOn = false; return; }
            if (!moveCamera) { PromoFaceShot(true); _promo7HoldT = 1.5f; }   // 7 號鍵：進 M 腿時遊戲會把鏡頭搶去 M 腿鏡頭，這裡立刻切回臉前並再釘住

            if (moveCamera) PromoInsertShot();

            // 雞雞：出現 → 靠近 → 插入（勃起度不夠也強制勃起，不然遊戲會把雞雞收起來）
            _promoForceErect = true; _erectOn = true;
            if (_jijiPhase == 0) ShowJiji();
            if (_jijiPhase != 1) { GD.Print("[Promo] 插入：雞雞沒出現（ShowJiji 被擋）"); _promoInsertOn = false; _promoForceErect = false; return; }
            await Wait(JijiAppearDuration + 0.4f);
            if (!_promoInsertOn) return;
            OnJijiHotspotDown();   // 階段 1 → 2：滑到小穴
            await Wait(JijiAppearDuration / Mathf.Max(JijiPhase2SpeedMult, 0.1f) + 0.4f);
            if (!_promoInsertOn) return;
            GD.Print($"[Promo] 插入：靠近完成 phase={_jijiPhase} animating={_jijiAnimating} mLeg={_mLegOpen} pantyGone={_pantyGone}");
            if (_jijiPhase == 1) { OnJijiHotspotDown(); await Wait(JijiAppearDuration + 0.4f); }   // 保險：第一次沒進階段 2 就再來一次
            if (_jijiPhase != 2) { GD.Print($"[Promo] 插入：沒進到靠近階段（phase={_jijiPhase}）"); _promoInsertOn = false; return; }
            // 不走 OnJijiHotspotDown（它在小穴位置有偏差時只會重新對位、不插入）：自己把雞雞對準現在的小穴，再直接開始插入
            _jijiAnimCts?.Cancel(); _jijiAnimating = false;
            _jijiPhase2Pos = CalcPhase2Offset(); _jijiOffset = _jijiPhase2Pos;
            _promoInsertAuto = true;   // 先接管插入深度再開始插入：不然遊戲會當作沒按住，把雞雞退回靠近階段
            StartInsertion();
            await Wait(0.2f);
            if (!_promoInsertOn || _jijiPhase != 3) { GD.Print($"[Promo] 插入：沒進到插入階段（phase={_jijiPhase}；M 腿={_mLegOpen}；內褲擋住={MPantyBlocksInsert}）"); _promoInsertOn = false; return; }

            // 推進去：自己把插入深度從 0 推到突破點（不長按、不用滑鼠），突破後再慢慢往上多拉到更深
            float from = _jijiInsertY, to = JijiStage1MaxY, dur = Mathf.Max(0.1f, PromoInsertSec);
            for (float t = 0f; t < dur && _promoInsertOn && _jijiPhase == 3; )
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                t += (float)GetProcessDeltaTime();
                _jijiInsertY = Mathf.Lerp(from, to, Mathf.Clamp(t / dur, 0f, 1f));   // 勻速
            }
            if (!_promoInsertOn || _jijiPhase != 3) return;
            _promoInsertAuto = false;   // 交回遊戲原本的深插邏輯
            Breakthrough();
            for (float t = 0f; t < PromoDeepSec && _promoInsertOn && _jijiPhase == 3; )
            {   // 往上多拉：深插的「中心位置」從最淺慢慢升到 PromoInsertDepth（0＝最淺、1＝最深）
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                t += (float)GetProcessDeltaTime();
                float p = Mathf.Clamp(t / Mathf.Max(0.1f, PromoDeepSec), 0f, 1f);
                _promoDeepT = PromoInsertDepth * p * p * (3f - 2f * p);
            }
            GD.Print($"[Promo] 插入：完成 phase={_jijiPhase} stage={_jijiStage} 深度={_promoDeepT:F2}");
            if (!moveCamera) { PromoFaceShot(true); _promo7HoldT = 1.5f; }   // 7 號鍵：插入過程中遊戲／護欄可能動過鏡頭，抽插開始前再切一次並釘住，一定要在臉部特寫
            if (thenThrust && _promoInsertOn && (!extras || _promo7On) && _jijiPhase == 3 && _jijiStage >= 1) PromoStartThrust(extras);   // 7 自動插入途中又按了 7（＝取消）就不要再開始動作
        }
        finally { _promoInsertAuto = false; if (extras && thenThrust && !_promoThrustOn) _promo7On = false; }   // 7 的自動插入失敗／被取消：清掉「7 在動」旗標，下次按 7 才會重新開始
    }


    // 7 號鍵：鏡頭「即時」（不緩動）切到莉奈臉部特寫，同時抽插＋親嘴＋摸胸一起開始；鏡頭之後一直留在臉前（不會被 M 腿／插入的鏡頭蓋掉）。
    [Export(PropertyHint.Range, "0,5,0.5")] public float PromoThrustLevel = 2f;   // 抽插速度等級（0＝最慢待機，5＝最快；太快男人身體後面的空隙可能露出來）
    // 7 號鍵的臉前鏡頭＝使用者給的截圖：臉的特寫，鼻子在畫面（0.5、0.39）、兩隻拳頭在畫面兩側、胸口蝴蝶結在下方。
    // 倍率用兩隻拳頭的間距跟床上鏡頭比（約 2.9 倍）換算出來，所以比 CamZoomMax 還大，7 號鍵不受 CamZoomMax 限制。
    // 臉的位置沿用 PromoFacePointTex／PromoFaceScreenPos（截圖裡的鼻子位置跟它一樣）。
    [Export(PropertyHint.Range, "0.5,4,0.01")] public float PromoFace7PxPerTex = 1.89f;   // 7 號鍵臉部特寫的大小（畫面像素／貼圖像素）；想再近／遠就調這個
    bool _promoThrustOn;   // 抽插正在動（6 或 7 啟動的都算）
    float _promo7HoldT;    // >0＝7 號鍵剛按下／剛插好：這段時間每幀都把鏡頭釘在臉部特寫，一定到得了
    bool _promo7On;        // 7 號鍵的整組動作（臉部特寫＋抽插＋親嘴＋摸胸）正在進行

    /// <summary>臉部特寫鏡頭（7 號鍵；8 號鍵從這裡開始放大；對準點同 1 號鍵、倍率用 PromoFace7PxPerTex；不動 UI、不動被子）。</summary>
    void PromoFaceShot(bool instant)
    {
        if (!Layers.TryGetValue("臉", out var fl) || fl == null) { GD.PushWarning("[Promo] 找不到「臉」圖層，鏡頭不動"); return; }
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        Rect2 r = fl.GetGlobalRect();   // 已是場景座標（同 PromoCameraFace）
        Vector2 anchorWorld = r.Position + PromoFacePointTex / new Vector2(2000f, 3200f) * r.Size;
        float charScale = _charRoot != null ? Mathf.Max(0.01f, _charRoot.Scale.X) : CharScale;
        float zoom = Mathf.Max(1.1f, PromoFace7PxPerTex / charScale);   // 不受 CamZoomMax 限制（特寫比一般放大上限還近）
        _camSmoothOverride = instant ? -1f : PromoCamSmooth;
        PromoSetUiHidden(true);
        PromoCamGoto(zoom, anchorWorld, vp * PromoFaceScreenPos, instant);
        _promoCamOnFace = true;
        GD.Print($"[Promo] 臉部特寫鏡頭：倍率 {zoom:F2}");
    }

    void PromoStartThrust(bool extras = true)
    {
        if (_jijiPhase != 3 || _jijiStage < 1) return;
        _promoThrustOn = true;
        _s2Paused = false; _pullActive = false;
        _s2SpeedLv = Mathf.Clamp(PromoThrustLevel, 0f, S2SpeedLvMax);
        GD.Print($"[Promo] 抽插開始：速度等級 {_s2SpeedLv:F1}");
        if (!extras) return;   // 6 號鍵：只有抽插
        // 同時：親嘴（舌頭動畫，同 3 號鍵）＋摸胸（左手摸胸＋右手摸右邊乳頭，同 4 號鍵）
        PromoKissStart();
        if (!_touchAnimRunningLeft) StartHandTouchAnim(true);
        if (_touchAnimRunningRight) CancelHandTouchAnim(false);
        if (!_nipR.On) StartNipple("NR");
        _promoChestOn = true;
    }

    /// <summary>停止 7 號鍵的全部動作：抽插、親嘴、摸胸（鏡頭不動）。</summary>
    void PromoStopThrust()
    {
        _promoThrustOn = false; _promo7On = false; _promoSwayOn = false;
        _promoWheelLock = false; _promoPanOn = false;   // 9 號鍵等也會停掉 7：一併解鎖
        PromoThrustLookStop();
        if (_jijiPhase == 3 && _jijiStage >= 1) { _s2Paused = true; _s2SpeedLv = 0f; }
        PromoKissStop();
        if (_tongueOn) StopTongue();
        if (_touchAnimRunningLeft)  CancelHandTouchAnim(true);
        if (_touchAnimRunningRight) CancelHandTouchAnim(false);
        if (_nipR.On) CancelNipple("NR");
        _promoChestOn = false;
    }

    /// <summary>7 號鍵：鏡頭即時切到臉前＋抽插開始。已經插入就直接抽；還沒插入先自動插入（鏡頭不動）再抽。再按一次：抽插、親嘴、摸胸全部停止，鏡頭不動。</summary>
    // 7 號鍵的上下搖擺：整個身體（所有圖層，跟高潮痙攣／插入推擠同一個 BodyShake）一起上下搖，特寫鏡頭裡臉、胸都會跟著動。沒有雞雞、沒有抽插，只有身體在搖。
    [Export(PropertyHint.Range, "0,60,0.5")] public float PromoSwayPx = 12f;             // 上下搖擺的幅度（角色畫布 px；往上往下各這麼多）
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float PromoSwayHz = 0.9f;          // 搖擺的快慢（每秒幾次）
    [Export(PropertyHint.Range, "0,1,0.05")] public float PromoSwayRise = 0.15f;         // 搖擺的起伏不對稱程度：0＝上下一樣、越大越像「往上頂、慢慢落回」（跟抽插的節奏像）
    bool _promoSwayOn; float _promoSwayT; Vector2 _promoSway;

    // 7 號鍵的「抽插狀態」外觀：沒有雞雞、沒有抽插，但莉奈身上抽插時才有的東西都在——插入的嘴型（小嘴巴放大）、汗一直增加、眉毛插入時的反應、表情「插入2」。
    // 只有錄影按 7 才會為 true，遊玩時永遠是 false（各處只是多一個「|| _promoThrustLook」的判斷）。
    [Export(PropertyHint.Range, "0,5,0.5")] public float PromoThrustLookLevel = 2f;   // 抽插狀態外觀的「速度等級」（影響嘴巴放大程度；0 最小、5 最大）
    [Export] public string PromoThrustLookExpr = "插入2";                              // 7 號鍵的表情（抽插突破後那個）；留空＝不換表情
    bool _promoThrustLook; string _promoPrevExpr;

    void PromoThrustLookStart()
    {
        if (_promoThrustLook) return;
        _promoThrustLook = true;
        if (FaceController != null && !string.IsNullOrEmpty(PromoThrustLookExpr))
        {
            _promoPrevExpr = FaceController.CurrentExpression;
            FaceController.SetExpression(PromoThrustLookExpr);
        }
    }

    void PromoThrustLookStop()
    {
        if (!_promoThrustLook) return;
        _promoThrustLook = false;
        if (FaceController != null && !string.IsNullOrEmpty(_promoPrevExpr) && FaceController.CurrentExpression == PromoThrustLookExpr)
            FaceController.SetExpression(_promoPrevExpr);   // 還原成按 7 之前的表情
        _promoPrevExpr = null;
    }

    /// <summary>每幀（PromoUiTick）：算上下搖擺的位移。停止時平順收回 0。</summary>
    void PromoSwayTick(float dt)
    {
        if (_promoSwayOn)
        {
            _promoSwayT += dt * PromoSwayHz;
            float ph = _promoSwayT % 1f;
            float w = Mathf.Sin(ph * Mathf.Tau);                                   // -1 ~ 1：上下對稱
            float u = Mathf.Abs(Mathf.Sin(ph * Mathf.Pi)) * 2f - 1f;                // |sin|：快速往上、慢慢落回（像抽插）
            _promoSway = new Vector2(0f, -Mathf.Lerp(w, u, PromoSwayRise / 1f) * PromoSwayPx);
        }
        else if (_promoSway != Vector2.Zero)
        {
            _promoSway = _promoSway.Lerp(Vector2.Zero, 1f - Mathf.Exp(-10f * dt));
            if (_promoSway.Length() < 0.05f) _promoSway = Vector2.Zero;
        }
    }

    // 7 號鍵的鏡頭：大小（縮放倍率）＝臉部特寫那個倍率（PromoFace7PxPerTex），但「不再鎖定臉」——倍率直接換上去（以目前畫面中央為中心，位置不跳），
    // 然後鏡頭從目前的位置慢慢往上移一小段（PromoPanUpPx 螢幕 px，PromoPanSec 秒）。按下 7 之後鎖住滑鼠滾輪縮放，直到再按 7 停止（或按 9）。只有錄影才有，遊玩的滾輪不受影響。
    [Export(PropertyHint.Range, "0,600,5")] public float PromoPanUpPx = 100f;       // 鏡頭往上移多少（螢幕 px；不多，預設 100）
    [Export(PropertyHint.Range, "0.5,15,0.5")] public float PromoPanSec = 4f;        // 往上移花幾秒
    [Export] public bool PromoLockWheelOn7 = true;                                    // 按 7 之後鎖住滾輪縮放
    bool _promoWheelLock, _promoPanOn; float _promoPanT, _promoPanFromY, _promoPanToY;

    void PromoCam7Start()
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float charScale = _charRoot != null ? Mathf.Max(0.01f, _charRoot.Scale.X) : CharScale;
        float z7 = Mathf.Max(1.1f, PromoFace7PxPerTex / charScale);                  // 倍率照舊（不受 CamZoomMax 限制）
        Vector2 world = (vp * 0.5f - _camOffset) / _camZoom;                          // 目前畫面中央的場景點：以它為中心換倍率，畫面不跳
        _promoCamAnimating = false; _promoCamOffAnim = false; _promoZoomOn = false; _promo7HoldT = 0f;
        _camSmoothOverride = -1f;
        _camZoom = z7; _camZoomTarget = z7;
        _camOffset = vp * 0.5f - world * z7;
        _camAnchorWorld = world; _camAnchorScreen = vp * 0.5f;
        _promoPanFromY = _camOffset.Y; _promoPanToY = _camOffset.Y + PromoPanUpPx;   // offset.Y 變大＝畫面內容往下＝鏡頭往上看
        _promoPanT = 0f; _promoPanOn = PromoPanUpPx > 0.5f;
        _promoWheelLock = PromoLockWheelOn7;
        PromoSetUiHidden(true);
        GD.Print($"[Promo] 7 號鍵鏡頭：倍率 {z7:F2}（不鎖臉），往上移 {PromoPanUpPx:F0}px／{PromoPanSec:F1}秒；滾輪縮放鎖定={_promoWheelLock}");
    }

    void PromoFaceKissChest()
    {
        if (_canvas == null) return;
        // 7 自己已經在動：再按一次＝全部停止（親嘴、摸胸；鏡頭不動）
        if (_promo7On && (PromoKissOn || _nipR.On || _touchAnimRunningLeft)) { PromoStop7(); return; }
        // （旗標還亮著、但其實什麼都沒在動＝卡住的舊旗標，不能當成「停止」，要重新開始）
        _promo7On = true;
        PromoCam7Start();      // 鏡頭：倍率＝臉部特寫的大小，但不鎖臉；從目前位置慢慢往上移一小段。同時鎖住滾輪縮放

        // 被子拿掉（左下角），胸才看得到；跟 4 號鍵一樣直接到位
        DuvetState = 1;
        if (_duvet != null && IsInstanceValid(_duvet)) { _duvet.FoldSnap = true; _duvet.FoldTarget = 0f; }
        if (_duvetKicked != null && IsInstanceValid(_duvetKicked)) { _duvetKicked.Drag = Vector2.Zero; _duvetKicked.DragSnap = true; }

        // 親嘴（舌頭用舔的動作，同 3 號鍵）＋摸胸（左手摸胸＋右手摸右邊乳頭，同 4 號鍵）
        PromoKissStart();
        if (!_touchAnimRunningLeft) StartHandTouchAnim(true);
        if (_touchAnimRunningRight) CancelHandTouchAnim(false);   // 右手不是摸胸，是摸乳頭
        if (!_nipR.On) StartNipple("NR");
        _promoChestOn = true;
        _promoSwayOn = true; _promoSwayT = 0f;   // 上下搖擺
        PromoThrustLookStart();                  // 抽插狀態的外觀：嘴型、汗、眉毛、表情
    }

    /// <summary>停止 7 號鍵的全部動作：親嘴、摸胸、上下搖擺（鏡頭不動）。</summary>
    void PromoStop7()
    {
        _promo7On = false; _promoChestOn = false; _promo7HoldT = 0f; _promoSwayOn = false;
        _promoWheelLock = false; _promoPanOn = false;   // 解除滾輪縮放鎖、停止往上移
        PromoThrustLookStop();
        PromoKissStop();
        if (_touchAnimRunningLeft)  CancelHandTouchAnim(true);
        if (_touchAnimRunningRight) CancelHandTouchAnim(false);
        if (_nipR.On) CancelNipple("NR");
    }


    // 8 號鍵：用 PromoZoomSec（7）秒慢慢「縮小」鏡頭，從目前的畫面（通常是 7 號鍵的臉部特寫）縮到原版 7 號鍵的臉前位置
    // （PromoFacePxPerTex 倍率、臉停在 PromoFaceScreenPos；就是 1 號鍵那個臉前鏡頭）。臉的位置平順移到終點，不會瞬移。
    [Export(PropertyHint.Range, "0.1,1,0.05")] public float PromoZoomOutFrac = 0.55f;   // 縮小的比例：從目前鏡頭往「原版 7 號鍵臉前位置」縮多少（1＝縮到底＝原本的行為，0.55＝只縮一半多一點，停在中間）；想縮更少就調小
    [Export(PropertyHint.Range, "1,15,0.5")] public float PromoZoomSec = 3f;               // 縮小花的時間（秒）
    bool _promoZoomOn; float _promoZoomT, _promoZ0, _promoZEnd;
    Vector2 _promoZWorld, _promoZScreen0, _promoZScreen1;

    /// <summary>8 號鍵：慢慢縮小到原版 7 號鍵（臉前）的鏡頭位置。再按一次：中途停止（鏡頭停在那裡）。</summary>
    void PromoZoomJiji()
    {
        if (_canvas == null) return;
        if (_promoZoomOn) { _promoZoomOn = false; GD.Print("[Promo] 縮小：手動停止"); return; }
        if (!Layers.TryGetValue("臉", out var fl) || fl == null) { GD.PushWarning("[Promo] 找不到「臉」圖層，鏡頭不動"); return; }
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        Rect2 r = fl.GetGlobalRect();   // 已是場景座標（同 PromoCameraFace）
        _promoZWorld = r.Position + PromoFacePointTex / new Vector2(2000f, 3200f) * r.Size;   // 臉的位置（場景座標）
        float charScale = _charRoot != null ? Mathf.Max(0.01f, _charRoot.Scale.X) : CharScale;
        _promoZ0 = _camZoom;
        float zFull = Mathf.Clamp(PromoFacePxPerTex / charScale, 1.1f, Mathf.Max(1.1f, CamZoomMax));   // 原版 7 號鍵（1 號鍵臉前鏡頭）的倍率＝縮到底
        float frac = Mathf.Clamp(PromoZoomOutFrac, 0.05f, 1f);
        _promoZEnd = Mathf.Lerp(_camZoom, zFull, frac);   // 只縮 frac 的比例，不縮到底
        _promoZScreen0 = _promoZWorld * _camZoom + _camOffset;   // 臉現在在畫面上的位置（第一幀畫面完全不動）
        _promoZScreen1 = _promoZScreen0.Lerp(vp * PromoFaceScreenPos, frac);   // 臉的位置也只移 frac 的比例
        _promoZoomT = 0f;
        _promoCamAnimating = false; _promoCamOffAnim = false;
        PromoSetUiHidden(true);
        _promoZoomOn = true;
        GD.Print($"[Promo] 縮小開始：{_promoZ0:F2} → {_promoZEnd:F2}，{PromoZoomSec:F1} 秒");
    }

    void PromoZoomStep(float dt)
    {
        _promoZoomT += dt;
        float p = Mathf.Clamp(_promoZoomT / Mathf.Max(0.5f, PromoZoomSec), 0f, 1f);
        float e = p * p * (3f - 2f * p);   // 慢慢開始、慢慢停
        float z = Mathf.Lerp(_promoZ0, _promoZEnd, e);
        Vector2 screen = _promoZScreen0.Lerp(_promoZScreen1, e);
        _camZoom = z; _camZoomTarget = z;
        _camAnchorWorld = _promoZWorld; _camAnchorScreen = screen;
        _camOffset = screen - _promoZWorld * z;
        if (p >= 1f) { _promoZoomOn = false; GD.Print($"[Promo] 縮小完成：倍率 {_camZoom:F2}"); }
    }


    // 9 號鍵（外射精）：射到頭那裡
    [Export(PropertyHint.Range, "2,14,1")] public int PromoEjacWaves = 7;                  // 一次一起射幾發
    [Export(PropertyHint.Range, "0,0.4,0.01")] public float PromoEjacFaceMargin = 0.1f;   // 落點離「頭和臉」邊緣至少留多少（占寬／高的比例）：越大越集中在中間、越不可能射到外面
    [Export] public Vector2 PromoEjacFaceY = new Vector2(0.08f, 0.7f);                      // 落點在頭的哪一段高度（0＝頭頂最上緣、1＝下巴；預設避開下巴那一段）
    [Export] public bool PromoEjacSimultaneous = false;                                     // true＝7 發同一瞬間齊射；false＝一發一發慢慢射（預設）
    [Export] public Vector2 PromoEjacGap = new Vector2(0.55f, 0.9f);                       // 一發一發射的間隔（秒；後面越射越慢）
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float PromoEjacStagger = 0f;          // 每發之間錯開幾秒（0＝全部同一瞬間一起射；想要有一點先後就調大，例如 0.05）
    [Export(PropertyHint.Range, "0.5,3,0.1")] public float PromoEjacPower = 2f;            // 力道（飛行速度倍率；越大射越遠、越快）

    /// <summary>9 號鍵：去掉親嘴、摸胸（還有抽插）→ 鏡頭對準臉（可以自由縮放）→ 把精液射到頭和臉的範圍。不需要雞雞（有的話先退出來）。射的時候再按 9 沒有作用。</summary>
    void PromoOutsideEjac()
    {
        if (_canvas == null) return;
        if (_ejacRunning || _insertEjacActive) { GD.Print("[Promo] 外射精：還在射，請稍後"); return; }
        // 不需要雞雞：沒有雞雞時精液從小穴口的位置往臉飛（雞雞頂端的位置在沒有雞雞時會退回用小穴熱點）
        TaskLogger.Run(PromoOutsideEjacAsync(), "TrainingRoom.PromoOutsideEjac");
    }

    async System.Threading.Tasks.Task PromoOutsideEjacAsync()
    {
        // 1) 去掉 7 號鍵的全部動作：抽插停、親嘴停、摸胸停（鏡頭不動）
        PromoStopThrust();
        PromoKissStop();
        if (_tongueOn) StopTongue();
        if (_touchAnimRunningLeft)  CancelHandTouchAnim(true);
        if (_touchAnimRunningRight) CancelHandTouchAnim(false);
        if (_nipL.On) CancelNipple("NL");
        if (_nipR.On) CancelNipple("NR");
        _promoChestOn = false;
        _promoInsertAuto = false; _promoZoomOn = false;

        // 鏡頭：即時對準臉（7 號鍵的臉部特寫大小），之後不釘住、不鎖滾輪，可以隨意縮放
        PromoFaceShot(true);
        _promo7HoldT = 0f; _promoPanOn = false; _promoWheelLock = false;

        // 2) 雞雞退出來（外射：雞雞在外面，從小穴口往上射）
        if (_jijiPhase == 3)
        {
            BackToPhase2("宣傳片外射精：拔出");
            await ToSignal(GetTree().CreateTimer(JijiAppearDuration + 0.5f), SceneTreeTimer.SignalName.Timeout);
        }

        // 3) 射：全部射向頭（劉海／頭頂／臉的上半部），沿用遊戲的禁射區（避開眼睛、嘴巴）
        _ejacRunning = true; _ejacStop = false; _ejacMul = 1f;
        ShowJijiSemen();
        _sweat = Mathf.Min(1f, _sweat + SweatOnEjac);
        try
        {
            PlayRinaEjacReaction();
            int n = Mathf.Max(2, PromoEjacWaves);
            // 預設一發一發慢慢射（PromoEjacSimultaneous 打開才是 7 發齊射）。每一發各自一道射流，從雞雞頂端飛向頭和臉上不同的落點
            var shots = new System.Collections.Generic.List<System.Threading.Tasks.Task>();
            Vector2 hair = LayerCanvasCenter("前髮"), face = LayerCanvasCenter("臉");
            // 落點分散，但全部落在「頭和臉」的範圍裡：左右均勻攤開（每發佔一格、順序打亂），高低在頭的上段隨機；再避開眼睛嘴巴的禁射區，最後一定夾回頭和臉的範圍內
            Rect2 fr = PromoHeadRectTex();   // 頭和臉（臉＋前髮）貼圖實際畫出來的範圍（貼圖座標）
            Layers.TryGetValue("臉", out var faceNode);
            var order = new System.Collections.Generic.List<int>();
            for (int q = 0; q < n; q++) order.Add(q);
            for (int q = n - 1; q > 0; q--) { int r = (int)GD.RandRange(0, q); (order[q], order[r]) = (order[r], order[q]); }
            float mx = fr.Size.X * PromoEjacFaceMargin, my = fr.Size.Y * PromoEjacFaceMargin;
            float xLo = fr.Position.X + mx, xHi = fr.End.X - mx;
            float yLo = fr.Position.Y + Mathf.Max(my, fr.Size.Y * PromoEjacFaceY.X), yHi = fr.Position.Y + Mathf.Min(fr.Size.Y - my, fr.Size.Y * PromoEjacFaceY.Y);
            if (yHi < yLo) yHi = yLo;
            for (int i = 0; i < n; i++)
            {
                Vector2 tipC = JijiTipCanvas();
                float slot = n <= 1 ? 0.5f : order[i] / (float)(n - 1);                       // 0（臉最左）~ 1（臉最右）
                Vector2 texPt = new Vector2(Mathf.Lerp(xLo, xHi, slot) + Rf(-1f, 1f) * (xHi - xLo) / Mathf.Max(2, n) * 0.4f, Rf(yLo, yHi));
                texPt.X = Mathf.Clamp(texPt.X, xLo, xHi);
                Vector2 tgt = faceNode != null ? faceNode.GetTransform() * texPt : LayerCanvasCenter("臉") + (texPt - fr.GetCenter());
                tgt = PushOutOfNoShootZone(tgt);
                // 被推出禁射區後可能掉到頭外面：換算回貼圖座標，夾回頭和臉的範圍內
                if (faceNode != null)
                {
                    Vector2 back = faceNode.GetTransform().AffineInverse() * tgt;
                    back = new Vector2(Mathf.Clamp(back.X, fr.Position.X + mx * 0.5f, fr.End.X - mx * 0.5f), Mathf.Clamp(back.Y, fr.Position.Y + my * 0.5f, fr.End.Y - my * 0.5f));
                    tgt = faceNode.GetTransform() * back;
                }
                if (PromoEjacSimultaneous)
                {   // 齊射：7 發同時，力道差不多
                    float power = PromoEjacPower * Rf(0.9f, 1.1f);
                    shots.Add(ShootSemenAsync(tipC, tgt, EjacFarSegCount, power, EjacFarBlobScale, addDrip: i >= n - 3));
                    if (PromoEjacStagger > 0f && i < n - 1) await ToSignal(GetTree().CreateTimer(PromoEjacStagger), SceneTreeTimer.SignalName.Timeout);
                }
                else
                {   // 一發一發慢慢射：射完這一發（落地）才射下一發，越射越弱、間隔越來越長，最後幾發只是流出來並滴落
                    float k = n <= 1 ? 0f : i / (float)(n - 1);
                    float power = Mathf.Lerp(PromoEjacPower, PromoEjacPower * 0.55f, k * k) * Rf(0.92f, 1.08f);
                    float blob = Mathf.Lerp(EjacFarBlobScale, EjacFarBlobScale * 0.7f, k);
                    int seg = Mathf.Max(8, Mathf.RoundToInt(Mathf.Lerp(EjacFarSegCount, EjacFarSegCount * 0.6f, k)));
                    await ShootSemenAsync(tipC, tgt, seg, power, blob, addDrip: k > 0.55f);
                    if (i < n - 1) await ToSignal(GetTree().CreateTimer(Rf(PromoEjacGap.X, PromoEjacGap.Y) * Mathf.Lerp(1f, 1.6f, k)), SceneTreeTimer.SignalName.Timeout);
                }
            }
            if (shots.Count > 0) await System.Threading.Tasks.Task.WhenAll(shots);   // 齊射時：等全部落地
            if (_jijiPhase > 0) SpawnLandingBlob(CanvasToScreen(JijiTipCanvas()), large: true);   // 雞雞頂端的精液團（沒有雞雞就不用）
        }
        finally { _ejacRunning = false; }
    }


    // 親嘴（3、7 號鍵）：直接用遊戲的「舔」（舔乳頭、舔小穴同一套動作，Tongue.cs 的 StartLick／UpdateLick），舔的目標是嘴巴（"M"）。
    // 舌頭圖、大小、速度、往上舔再慢慢回來、微微旋轉，全部跟舔乳頭一模一樣；之後舔的動作怎麼調，親嘴會一起變。不再是「舌頭1→2→3→2」上下換圖。
    [Export(PropertyHint.Range, "0.1,1.5,0.05")] public float PromoKissScale = 0.7f;   // 親嘴舌頭的大小：舔乳頭舌頭大小（LickTongueSize）的幾倍；太大就調小
    [Export] public Vector2 PromoKissNudge = new Vector2(0f, -3f);   // 舌頭相對嘴巴中心的微調（場景 px）；還沒對準嘴巴就調這個（Y 正＝往下）

    bool PromoKissOn => _lickOn && _lickTarget == "M";
    void PromoKissToggle() { if (PromoKissOn) PromoKissStop(); else PromoKissStart(); }
    void PromoKissStart()
    {
        if (PromoKissOn || _canvas == null) return;
        if (_tongueOn) StopTongue();   // 原本那套換圖的舌頭不用
        StartLick("M");
    }
    void PromoKissStop() { if (PromoKissOn) StopLick(); }


    readonly System.Collections.Generic.Dictionary<string, Rect2> _promoLayerRects = new();
    /// <summary>某個圖層貼圖實際畫出來的範圍（貼圖座標，2000×3200 畫布上的一個框）。略過零星淡雜點：一列／一行至少要有 20 個明顯不透明的點才算。算一次就快取；找不到回傳 false。</summary>
    bool PromoLayerRectTex(string name, out Rect2 rect)
    {
        if (_promoLayerRects.TryGetValue(name, out rect)) return rect.Size.X > 0f;
        rect = default;
        if (Layers.TryGetValue(name, out var l) && l is TextureRect tr && tr.Texture != null)
        {
            var img = tr.Texture.GetImage();
            if (img != null)
            {
                if (img.IsCompressed()) img.Decompress();
                img.Convert(Image.Format.Rgba8);
                byte[] d = img.GetData(); int w = img.GetWidth(), h = img.GetHeight();
                int[] rows = new int[h], cols = new int[w];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (d[(y * w + x) * 4 + 3] >= 26) { rows[y]++; cols[x]++; }
                int y0 = -1, y1 = -1, x0 = -1, x1 = -1;
                for (int y = 0; y < h; y++) if (rows[y] >= 20) { if (y0 < 0) y0 = y; y1 = y; }
                for (int x = 0; x < w; x++) if (cols[x] >= 20) { if (x0 < 0) x0 = x; x1 = x; }
                if (y0 >= 0 && x0 >= 0) rect = new Rect2(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            }
        }
        _promoLayerRects[name] = rect;
        if (rect.Size.X > 0f) GD.Print($"[Promo] 「{name}」圖層範圍（貼圖座標）：{rect}");
        return rect.Size.X > 0f;
    }

    /// <summary>頭和臉的範圍＝「臉」和「前髮」兩個範圍的聯集（貼圖座標）。都找不到時用臉中心附近一個合理大小的框。</summary>
    Rect2 PromoHeadRectTex()
    {
        bool hasFace = PromoLayerRectTex("臉", out var f), hasHair = PromoLayerRectTex("前髮", out var hr);
        if (hasFace && hasHair) return f.Merge(hr);
        if (hasFace) return f;
        if (hasHair) return hr;
        return new Rect2(990f - 330f, 604f - 330f, 660f, 760f);
    }


    /// <summary>6 號鍵：抽插。不演「靠近、推進去」的過程，按下去就直接是已經插進去、在抽插的畫面。鏡頭＝插入鏡頭（以小穴為中心，即時切過去）；男人身體後面的空隙由鏡頭護欄擋住。
    /// 只有抽插，沒有親嘴和摸胸（那是 7 號鍵）。再按一次：停止抽插，鏡頭不動。速度在 PromoThrustLevel。</summary>
    void PromoThrust()
    {
        if (_canvas == null) return;
        if (_promoThrustOn)
        {   // 再按一次：只停抽插（7 的親嘴、摸胸若在動就留著），鏡頭不動
            _promoThrustOn = false;
            if (_jijiPhase == 3 && _jijiStage >= 1) { _s2Paused = true; _s2SpeedLv = 0f; }
            return;
        }
        if (_jijiPhase == 3 && _jijiStage >= 1) { PromoInsertShot(true); PromoStartThrust(false); return; }   // 已經插入：鏡頭切到插入鏡頭，直接抽
        if (_mLegTransitioning) { GD.Print("[Promo] 抽插：M 腿切換中，請稍後再按"); return; }
        _promoInsertOn = true; _promoDeepT = 0f;
        TaskLogger.Run(PromoInstantThrustAsync(), "TrainingRoom.PromoThrust");
    }


    /// <summary>6 號鍵還沒插入時：不演出現／靠近／推進，雞雞直接就位在「插入完成」的狀態再開始抽插。（被子拿掉、內褲脫掉、進 M 腿還是要的，M 腿淡入約 0.3 秒。）</summary>
    async System.Threading.Tasks.Task PromoInstantThrustAsync()
    {
        async System.Threading.Tasks.Task Wait(float sec) => await ToSignal(GetTree().CreateTimer(sec), SceneTreeTimer.SignalName.Timeout);
        async System.Threading.Tasks.Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        try
        {
            if (_touchAnimRunningLeft)  CancelHandTouchAnim(true);
            if (_touchAnimRunningRight) CancelHandTouchAnim(false);
            if (_nipR.On) CancelNipple("NR");
            if (_nipL.On) CancelNipple("NL");
            if (_lickOn) StopLick();
            if (_bTouchRunning) CancelBTouch();
            if (_patOn) CancelPat();
            _promoChestOn = false; _promoPussyOn = false;

            PromoBedSetup();
            for (float t = 0f; (_mLegTransitioning || !_mLegOpen) && t < 4f; t += 0.05f) await Wait(0.05f);   // 等 M 腿淡入完成
            if (!_promoInsertOn || !_mLegOpen) { _promoInsertOn = false; return; }
            await Frame(); await Frame();   // 讓小穴熱點先跟上 M 腿位置，雞雞才對得準

            PromoInsertShot(instant: true);   // 鏡頭即時切到插入鏡頭（跟「插入」那個鏡頭一樣）；男人身體後面的空隙由鏡頭護欄（Camera.cs BellyGapGuard）擋住

            // 雞雞：直接就位（跳過淡入、靠近）
            _promoForceErect = true; _erectOn = true;
            if (_jijiPhase == 0) ShowJiji();
            if (_jijiPhase != 1) { GD.Print("[Promo] 抽插：雞雞沒出現（ShowJiji 被擋）"); _promoInsertOn = false; _promoForceErect = false; return; }
            _jijiAnimCts?.Cancel(); _jijiAnimating = false; _jijiAlpha = 1f;   // 取消淡入動畫，直接完全顯示
            _jijiPhase2Pos = CalcPhase2Offset(); _jijiOffset = _jijiPhase2Pos; _jijiPhase = 2;

            // 直接「插入完成」：開始插入 → 深度直接放到突破點 → 突破到深插（由遊戲原本的深插抽動接手）
            _promoInsertAuto = true;
            StartInsertion();
            if (_jijiPhase != 3) { GD.Print($"[Promo] 抽插：沒進到插入階段（M 腿={_mLegOpen}；內褲擋住={MPantyBlocksInsert}）"); _promoInsertOn = false; return; }
            _jijiInsertY = JijiStage1MaxY;
            _promoInsertAuto = false;
            Breakthrough();
            PromoStartThrust(false);   // 只有抽插
        }
        finally { _promoInsertAuto = false; }
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
