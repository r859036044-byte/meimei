using Godot;

/// <summary>
/// 摸乳頭：點乳頭本體（奶頭1／奶頭2）→ 出現跟「摸 B」同一隻食指手，在乳頭上蹭（操作方式完全照 B：
/// 再點一次或右鍵取消；按住左鍵往上拖加速、往下拖減速，共用撫摸速度等級）。
/// 手的規則跟其他一樣：只有兩隻手、同一隻手不做兩件事；左下角指定了手就用那一隻。
/// NL＝畫面左邊那顆（奶頭2，跟胸2 同一顆）、NR＝畫面右邊（奶頭1）。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("摸乳頭")]
    [Export] public float   NippleRubPushPx  = 2f;     // 乳頭被蹭時跟著動的量（畫布 px）
    [Export] public Vector2 NippleHandNudge  = new Vector2(0f, 6f);   // 指尖位置微調（場景 px）

    // 兩顆各自的狀態
    sealed class NipTouch
    {
        public bool On; public bool SlotLeft; public int Ord; public float T;
        public TextureRect Hand;
    }
    readonly NipTouch _nipL = new(), _nipR = new();
    NipTouch Nip(string k) => k == "NL" ? _nipL : _nipR;
    static string NipLayer(string k) => k == "NL" ? "奶頭2" : "奶頭1";
    static string NipBreast(string k) => k == "NL" ? "胸2" : "胸1";
    int NippleHandCount => (_nipL.On ? 1 : 0) + (_nipR.On ? 1 : 0);

    bool SlotUsedByNipple(bool left) => (_nipL.On && _nipL.SlotLeft == left) || (_nipR.On && _nipR.SlotLeft == left);
    void CancelNipplesInSlot(bool left)
    {
        if (_nipL.On && _nipL.SlotLeft == left) CancelNipple("NL");
        if (_nipR.On && _nipR.SlotLeft == left) CancelNipple("NR");
    }

    /// <summary>由 _Input 呼叫：點在乳頭本體上（沒選 👅）→ 開關摸乳頭，並吃掉這次點擊（避免底下的胸部綠點也觸發）。</summary>
    bool NippleInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb) return false;
        if (_tongueTool || IsPointerOverUi()) return false;
        Vector2 w = ScreenToWorld(mb.Position);
        string k = IsPointOnLayer("奶頭2", w) ? "NL" : IsPointOnLayer("奶頭1", w) ? "NR" : null;
        if (k == null) return false;
        if (Nip(k).On) CancelNipple(k); else StartNipple(k);
        return true;
    }

    /// <summary>由 _Input 呼叫（不吃掉事件）：滑鼠停在乳頭本體上時，胸部那隻半透明手游標改成「碰奶頭」用的食指手
    /// （跟點下去之後出現在奶頭上的手同一張圖）；移開奶頭但還在胸部綠點內就換回胸部手游標。
    /// 畫面左邊那顆（奶頭2，NL）用水平翻轉的食指手、右邊那顆（奶頭1，NR）用原圖，左右各自朝外。
    /// 如果使用者覺得方向反了，把 NipHoverCursorFlipLeft 取消勾選／或勾選即可（Play 中下次進入奶頭生效）。</summary>
    [Export] public bool NipHoverCursorFlipLeft = true;   // 左邊那顆奶頭的 hover 游標是否水平翻轉
    ImageTexture _roomCursorTexFlipped;
    bool _nipHoverFlip;

    void ApplyNippleHoverCursor(bool flip)
    {
        if (!GreenDotCursorEnabled || ClothDragActive) return;
        ApplyHandCursor();   // 確保 _roomCursorTex 已建立（同時把 kind 歸 0）
        if (_roomCursorTex == null) return;
        Vector2 hotspot = RoomCursorTipRatio * RoomCursorSize;
        Texture2D tex = _roomCursorTex;
        if (flip)
        {
            if (_roomCursorTexFlipped == null)
            {
                var img = _roomCursorTex.GetImage();
                img.FlipX();
                _roomCursorTexFlipped = ImageTexture.CreateFromImage(img);
            }
            tex = _roomCursorTexFlipped;
            hotspot = new Vector2(RoomCursorSize - hotspot.X, hotspot.Y);
        }
        Input.SetCustomMouseCursor(tex, Input.CursorShape.Arrow, hotspot);
        _hoverCursorKind = 4;
        _nipHoverFlip = flip;
        HoverTipShow(HoverTipNipple);
    }

    void NippleHoverInput(InputEvent e)
    {
        if (e is not InputEventMouseMotion mm || _tongueTool) return;
        if (_hoverCursorKind != 1 && _hoverCursorKind != 2 && _hoverCursorKind != 4) return;   // 只在胸部 hover 手游標（或奶頭游標）時處理
        string nip = IsPointerOverUi() ? null : NippleAt(ScreenToWorld(mm.Position));
        if (nip != null)
        {
            bool flip = (nip == "L") == NipHoverCursorFlipLeft;
            if (_hoverCursorKind != 4 || _nipHoverFlip != flip) ApplyNippleHoverCursor(flip);
        }
        else if (_hoverCursorKind == 4)
        {
            ApplyBreastHoverCursor(_lastBreastHoverLeft, _hoverIsChest);
        }
    }

    void StartNipple(string k)
    {
        var n = Nip(k);
        if (n.On) return;
        // 同一顆胸已經有手在摸／舌頭在舔 → 先拿掉
        if (k == "NL" && _touchAnimRunningLeft)  CancelHandTouchAnim(true);
        if (k == "NR" && _touchAnimRunningRight) CancelHandTouchAnim(false);
        if (_lickOn && _lickTarget == (k == "NL" ? "L" : "R")) StopLick();

        if (_handMode == HandMode.Left || _handMode == HandMode.Right)
        {
            n.SlotLeft = _handMode == HandMode.Right;   // 跟下方同一套對應（「左手」鈕＝右手那隻）
            CancelSlot(n.SlotLeft, k);
            MakeRoomForHand(k);
        }
        else
        {
            bool leftBusy  = SlotBusy(true, k), rightBusy = SlotBusy(false, k);
            bool? oldSlot = null;
            string replaced = MakeRoomForHandPeek(k, out oldSlot);
            if (replaced != null) n.SlotLeft = oldSlot ?? false;
            else if (leftBusy && !rightBusy) n.SlotLeft = false;
            else if (rightBusy && !leftBusy) n.SlotLeft = true;
            else n.SlotLeft = false;
            CancelSlot(n.SlotLeft, k);
        }
        n.Ord = ++_handSeq;

        var tex = ResourceHelper.LoadTexAny("Sprites/游標");
        if (tex == null) return;
        if (_touchHandLayer == null)
        {
            _touchHandLayer = new Control { Name = "TouchHandLayer", MouseFilter = Control.MouseFilterEnum.Ignore };
            _touchHandLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _canvas.AddChild(_touchHandLayer);
        }
        float sz = BTouchCursorSize;
        n.Hand = new TextureRect
        {
            Name = "TouchHand_" + k, Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(sz, sz * tex.GetHeight() / Mathf.Max(1f, tex.GetWidth())),
            FlipH = n.SlotLeft,
        };
        n.Hand.PivotOffset = n.Hand.Size * NipTipRatio(n);
        _touchHandLayer.AddChild(n.Hand);
        n.On = true; n.T = 0f;
        HadBreastTouch = true;
        PlayerDataManagerTouch();
    }

    Vector2 NipTipRatio(NipTouch n) => n.SlotLeft ? new Vector2(1f - RoomCursorTipRatio.X, RoomCursorTipRatio.Y) : RoomCursorTipRatio;

    void CancelNipple(string k)
    {
        var n = Nip(k);
        if (!n.On) return;
        n.On = false;
        if (n.Hand != null && IsInstanceValid(n.Hand)) n.Hand.QueueFree();
        n.Hand = null;
        if (!(_lickOn && _lickTarget == (k == "NL" ? "L" : "R"))) TouchAnimOffset.Remove(NipLayer(k));
    }

    /// <summary>每幀（HeadPatLate）呼叫：兩顆各自蹭（動作照摸 B）。</summary>
    void UpdateNipples(float dt)
    {
        foreach (var k in new[] { "NL", "NR" })
        {
            var n = Nip(k);
            if (!n.On || n.Hand == null || !IsInstanceValid(n.Hand)) continue;
            bool holding = Input.IsMouseButtonPressed(MouseButton.Left);
            float speedMult = holding ? (Mathf.Sin(n.T) > 0f ? HandAnimHoldUpMult : HandAnimHoldDownMult) : 1f;
            n.T += dt * HandAnimSpeed * speedMult * TouchLevelMult;

            string layer = NipLayer(k);
            // 乳頭跟著被蹭的方向小幅動
            TouchAnimOffset[layer] = new Vector2(Mathf.Sin(n.T * 0.5f) * NippleRubPushPx * 0.5f, -Mathf.Max(0f, Mathf.Sin(n.T)) * NippleRubPushPx);
            if (!Layers.TryGetValue(layer, out var nc) || nc == null) continue;
            Vector2 center = nc.GetGlobalTransform() * _nippleCenter[layer];
            Vector2 tip = center + new Vector2(Mathf.Sin(n.T * 0.5f) * BTouchMoveX * 0.5f, Mathf.Sin(n.T) * BTouchMoveY);
            n.Hand.GlobalPosition = tip - n.Hand.Size * NipTipRatio(n) + NippleHandNudge + new Vector2(n.SlotLeft ? 2f : 0f, 0f);
            n.Hand.RotationDegrees = Mathf.Sin(n.T) * 4f * (n.SlotLeft ? -1f : 1f);
        }
    }

    // ── 手的分配小工具（給乳頭用；B／摸頭沿用各自原本的邏輯） ──
    /// <summary>某一隻手（left＝左手那隻）現在有沒有在做別的事（排除 self）。</summary>
    bool SlotBusy(bool left, string self)
    {
        if (left  && _touchAnimRunningLeft  && self != "L") return true;
        if (!left && _touchAnimRunningRight && self != "R") return true;
        if (_bTouchRunning && _bSlotLeft == left && self != "B") return true;
        if (_patOn && _patSlotLeft == left && self != "H") return true;
        if (_nipL.On && _nipL.SlotLeft == left && self != "NL") return true;
        if (_nipR.On && _nipR.SlotLeft == left && self != "NR") return true;
        return false;
    }

    /// <summary>把用到 left 那隻手的其他動作都取消（排除 self）。</summary>
    void CancelSlot(bool left, string self)
    {
        if (left  && _touchAnimRunningLeft  && self != "L") CancelHandTouchAnim(true);
        if (!left && _touchAnimRunningRight && self != "R") CancelHandTouchAnim(false);
        if (_bTouchRunning && _bSlotLeft == left && self != "B") CancelBTouch();
        if (_patOn && _patSlotLeft == left && self != "H") CancelPat();
        if (_nipL.On && _nipL.SlotLeft == left && self != "NL") CancelNipple("NL");
        if (_nipR.On && _nipR.SlotLeft == left && self != "NR") CancelNipple("NR");
    }

    /// <summary>跟 MakeRoomForHand 一樣，但同時回傳被取消那個動作原本用的是哪隻手。</summary>
    string MakeRoomForHandPeek(string kind, out bool? slotLeft)
    {
        // 先記下各動作目前的手
        bool bS = _bSlotLeft, pS = _patSlotLeft, nlS = _nipL.SlotLeft, nrS = _nipR.SlotLeft;
        string r = MakeRoomForHand(kind);
        slotLeft = r switch
        {
            "L" => true, "R" => false, "B" => bS, "H" => pS, "NL" => nlS, "NR" => nrS, _ => null,
        };
        return r;
    }
}
