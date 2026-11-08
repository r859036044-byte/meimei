using Godot;

public partial class TrainingRoomRuntime
{
    // 0：蓋著；1：踢到左下；2：隱藏。可在 Inspector 或劇情程式切換。
    [Export(PropertyHint.Enum, "蓋著,踢到左下角,隱藏")]
    public int DuvetState { get; set; } = 0;

    // ── 蓋著狀態的位置／配色（Play 中可即時調）──
    [Export] public float DuvetOffsetY = -20f;     // 蓋著的被子整體往上 20px（背景本地 px，負＝往上）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetBleach = 0f;   // 漂白（降低飽和，去掉偏紫）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetBright = 0f;   // 提亮
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetTintAmount = 0.3f;   // 粉紅（覆蓋混合）「右邊最濃」時的強度（左邊漸層到 0）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetTintFadeU0 = 0f;   // 粉紅濾鏡漸層起點（貼圖左右位置，0＝最左）：這裡以左完全沒有
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetTintFadeU1 = 1f;   // 漸層終點（1＝最右）：這裡以右達到完整強度
    [Export] public Color DuvetTint = new Color(1f, 0.78f, 0.87f);               // 粉紅濾鏡顏色
    // 深藍色「色彩增值」（相乘）：左邊最濃，往右漸層到被子一半就沒有
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetBlueAmount = 0f;   // 最左邊的強度（現在整張一樣，見下面右邊強度）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetBlueAmountRight = 0f;   // 右邊的強度（跟左邊一樣＝整張被子均勻 40%；0＝右半邊沒有）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetBlueFadeU0 = 0.15f;     // 這個位置（0＝最左）以左＝左邊強度
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetBlueFadeU1 = 0.95f;   // 這個位置（1＝最右）以右＝右邊強度（0.5＝漸層到被子一半）
    [Export] public Color DuvetBlueColorBottom = new Color(0.12f, 0.16f, 0.40f);   // 下面偏藍
    [Export] public Color DuvetKickedBlueColorBottom = new Color(0.14f, 0.18f, 0.42f);
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetBlueBottomStart = 0.5f;   // 從這個高度往下加強
    [Export] public Color DuvetLeftTopBlueColor = new Color(0.20f, 0.30f, 0.90f);   // 左邊＋上面再疊一層的偏藍色彩增值（蓋著的被子＋踢開的被子）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetLeftTopBlueAmount = 0f;
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float DuvetLeftTopBlueRange = 0.4f;   // 從邊緣往內漸層的範圍
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetKickedTopBlueScale = 0.1f;   // 踢開的被子「上面」的偏藍增值比例（1＝跟左邊一樣濃，越小越淡）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetKickedCornerAmount = 0.15f;   // 踢開的被子左上角額外加強（原本寫死 0.5，這是上面看起來太濃的主因）
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float DuvetKickedCornerRange = 0.5f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetKickedLeftTopBlueAmount = 0f;   // 踢開的被子左邊＋上面偏藍增值的強度（跟蓋著的分開調）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetBottomBlueAmount = 0f;   // 蓋著的被子底部再加一層藍
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetTopRightScale = 0.3f;   // 蓋著的被子右上角偏藍的比例（1＝跟左上一樣，越小越淡）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetTopBlueScale = 0.5f;   // 蓋著的被子「上面」偏藍增值的比例（只影響上面，1＝原本）
    [Export] public Color DuvetDiagColor = new Color(0.49f, 0.50f, 0.83f);   // 被子左上角斜切變暗的顏色（色彩增值）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetDiagAmount = 0.3f;   // 強度
    [Export(PropertyHint.Range, "0,2,0.01")] public float DuvetDiagPos = 0.7f;      // 斜線的位置（左上角往右下推；0＝沒有，2＝整張）
    [Export(PropertyHint.Range, "0.001,0.5,0.001")] public float DuvetDiagSoft = 0.15f;   // 斜線邊緣柔和程度（小＝俐落的斜切）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetKickedDiagAmount = 0f;   // 踢開的被子的左上角斜切變暗強度（預設 0＝踢開的被子不變）
    [Export] public float DuvetDiagShiftPx = 160f;   // 斜切變暗往左移多少 px（貼圖像素）
    [Export(PropertyHint.Range, "-90,90,0.5")] public float DuvetDiagAngleDeg = 10f;   // 斜線逆時針旋轉幾度（負＝順時針）
    [Export] public Color DuvetBlueColor = new Color(0.30f, 0.08f, 0.24f);          // 深紫紅色（色彩增值）
    // 蓋著的被子是角色的子節點，會被「人物夜晚濾鏡」「人物光影濾鏡」一起染暗；開這個會把那兩層抵銷，讓被子跟床／背景一樣不受影響
    [Export] public bool DuvetCancelCharFilter = true;
    // ── 抓被子上緣往下拉（折疊）──
    [Export(PropertyHint.Range, "0.05,0.6,0.01")] public float DuvetGrabFraction = 0.20f;   // 被子上緣多高的範圍可以抓（佔被子內容高度）
    [Export(PropertyHint.Range, "0.2,1,0.01")] public float DuvetInfluenceFraction = 0.50f; // 影響範圍（佔被子內容高度）：抓取帶以外一直到這裡也會被帶動，離上緣越遠影響越小
    [Export(PropertyHint.Range, "0.05,0.8,0.01")] public float DuvetPullWidth = 0.22f;      // 被拉的點往左右影響的寬度（佔被子寬度）；越大整條越被拉
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetPullLean = 0.5f;             // 左右往被拉的點傾斜、收攏的程度
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetFoldRecover = 0.2f;          // 放手後回彈多少比例（0.2＝只回復 20%，其餘維持拉下的樣子）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetFoldShadow = 0.65f;           // 皺褶線條不透明度上限（0.65＝65%）
    [Export] public Color DuvetFoldLineColor = new Color(0.18f, 0.13f, 0.40f);                // 皺褶線條顏色（深藍紫）
    [Export(PropertyHint.Range, "0.3,4,0.05")] public float DuvetFoldLineWidth = 0.45f;     // 皺褶線條粗細
    [Export(PropertyHint.Range, "0.01,1,0.01")] public float DuvetFadeInTime = 0.12f;        // 切換（蓋著⇄踢掉）時，新的那張快速淡入（秒）
    [Export(PropertyHint.Range, "0.01,2,0.01")] public float DuvetFadeOutTime = 0.35f;       // 切換時，舊的那張淡出（秒）
    [Export] public float DuvetFoldWaves = 3f;                                              // 皺褶密度
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetFoldIrregular = 0.6f;         // 皺褶／折疊邊緣的不規則程度（0＝規則）
    // ── 踢掉的被子（床上、所有人物下面）──
    [Export(PropertyHint.Range, "0.1,2,0.01")] public float DuvetKickedScale = 0.7f;   // 踢掉的被子大小（1＝原圖）
    [Export] public Vector2 DuvetKickedPivot = new Vector2(0.33f, 1.0f);               // 縮放中心（背景 UV）：y＝1 是被子底邊＝床的下緣，縮放時底邊不動
    [Export] public Vector2 DuvetKickedOffsetPx = new Vector2(100f, 50f);               // 踢掉的被子位置偏移（貼圖像素；右、下為正）
    // 踢掉的被子左邊和下面的深藍「色彩增值」漸層（跟蓋著的被子同一種效果）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetKickedBlueAmount = 0.2f;   // 最邊緣的強度（原 0.5，減弱三分之一）
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float DuvetKickedBlueRange = 0.45f; // 從邊緣往內漸層的範圍（佔被子寬／高）
    [Export] public Color DuvetKickedBlueColor = new Color(0.32f, 0.09f, 0.25f);           // 紫紅色（色彩增值）
    [Export] public float DuvetKickedClipPx = 5f;            // 超出床多少 px（貼圖像素）以外裁掉（平均值）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetKickedClipIrregular = 0.8f;   // 裁切邊波動程度（0＝整齊貼著床邊）
    [Export] public float DuvetKickedWavePx = 12f;           // 裁切距離的波動幅度（貼圖像素，±）
    [Export] public float DuvetKickedWaveLength = 90f;       // 波動的波長（貼圖像素）：越大越圓緩
    [Export] public float DuvetKickedRoundPx = 14f;          // 被子和裁切邊相交處的磨圓半徑（貼圖像素）：越大越圓
    [Export] public float DuvetKickedOutlinePx = 4.5f;       // 裁切邊黑框線粗細（貼圖像素）
    [Export] public Color DuvetKickedOutlineColor = new Color(0f, 0f, 0f, 1f);   // 裁切邊框線顏色
    [Export] public float DuvetRestoreDistance = 50f;        // 抓著踢掉的被子往上拖多遠（貼圖像素）就蓋回去

    [Export] public bool DuvetBreathEnabled = true;                                  // 蓋著的被子跟著呼吸起伏
    [Export] public float DuvetBreathPx = 3f;                                        // 吸氣時胸口處被子往上鼓起多少 px（貼圖 px）
    [Export(PropertyHint.Range, "0,0.1,0.001")] public float DuvetBreathWide = 0.004f;   // 鼓起時左右微微撐寬的比例
    [Export] public Vector2 DuvetBreathCenterUV = new Vector2(0.5f, 0.55f);          // 起伏最明顯的位置（背景 UV）：胸口／腹部
    [Export] public Vector2 DuvetBreathRadiusUV = new Vector2(0.4f, 0.3f);         // 起伏範圍（背景 UV，左右／上下）
    [Export(PropertyHint.Range, "0,1,0.01")] public float DuvetBreathAsym = 0.18f;   // 吸氣、吐氣節奏不對稱程度（0＝標準正弦）

    BedDuvetOverlay _duvet;          // 蓋著的被子（角色圖層裡、前髮前面）
    BedDuvetOverlay _duvetKicked;    // 踢掉的被子（床上、角色下面）
    bool _duvetDrag;
    float _duvetDragStartLocalY;
    float _foldStart;                // 按下時的折疊進度（上次放手留下的拉痕）
    bool _pullStarted;               // 這次按住拉扯是否已經新增了一組皺褶線條
    bool _kickDrag;
    Vector2 _kickGrabOffset;         // 按下時「被子位移 − 滑鼠 UV」，拖曳時保持不變，被子才不會跳
    Image _kickedImg;

    void BuildDuvetLayer()
    {
        var bg = _canvas.GetNode<Control>("BG");
        _duvet = new BedDuvetOverlay
        {
            Name = "DuvetForeground",
            Target = bg,
            StateSource = () => DuvetState,
            Covered = ResourceHelper.LoadTexAny("Backgrounds/bg_LinaiBed_night_duvet_covered"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        // 放進角色圖層裡、「前髮」前面：蓋在身體／衣服／手腳上面，前髮（和之後的眉毛等）在被子上面。
        if (_charRoot != null && Layers.TryGetValue("前髮", out var hair) && hair != null && hair.GetParent() == _charRoot)
        {
            _charRoot.AddChild(_duvet);
            _charRoot.MoveChild(_duvet, hair.GetIndex());
        }
        else _canvas.AddChild(_duvet);   // 找不到前髮時退回原本做法（角色後、HUD 前）
        _duvet.Setup();

        // 踢掉的被子：直接接在背景（BG，床是它的子節點）後面 → 在床和背景上面、所有角色圖層下面。
        _duvetKicked = new BedDuvetOverlay
        {
            Name = "DuvetKicked",
            KickedMode = true,
            Target = bg,
            StateSource = () => DuvetState,
            Kicked = ResourceHelper.LoadTexAny("Backgrounds/bg_LinaiBed_night_duvet_kicked"),
            BedMask = ResourceHelper.LoadTexAny("Backgrounds/bg_LinaiBed_night_bed"),   // 床的範圍（只當踢被子的裁切遮罩，不再畫出來），用來裁切
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _canvas.AddChild(_duvetKicked);
        _canvas.MoveChild(_duvetKicked, bg.GetIndex() + 1);

        UpdateDuvet();
        _duvet.Sync();
        _duvetKicked.Sync();
    }

    /// <summary>每幀把 Inspector 的被子參數推給疊層（Play 中即時生效）。</summary>
    void UpdateDuvet()
    {
        if (_duvet != null && IsInstanceValid(_duvet))
        {
            _duvet.OffsetY = DuvetOffsetY;
            _duvet.Bleach = DuvetBleach; _duvet.Bright = DuvetBright;
            _duvet.TintAmt = DuvetTintAmount; _duvet.Tint = DuvetTint;
            _duvet.TintU0 = DuvetTintFadeU0; _duvet.TintU1 = DuvetTintFadeU1;
            _duvet.BlueAmt = DuvetBlueAmount; _duvet.BlueAmtR = DuvetBlueAmountRight; _duvet.BlueCol = DuvetBlueColor;
            _duvet.DgCol = DuvetDiagColor; _duvet.DgAmt = DuvetDiagAmount; _duvet.DgPos = DuvetDiagPos; _duvet.DgSoft = DuvetDiagSoft; _duvet.DgShiftPx = DuvetDiagShiftPx; _duvet.DgAngleDeg = DuvetDiagAngleDeg; _duvet.LtTopK = DuvetTopBlueScale; _duvet.LtBotAmt = DuvetBottomBlueAmount; _duvet.LtTopRight = DuvetTopRightScale; _duvet.LtCol = DuvetLeftTopBlueColor; _duvet.LtAmt = DuvetLeftTopBlueAmount; _duvet.LtRange = DuvetLeftTopBlueRange; _duvet.BlueColBot = DuvetBlueColorBottom; _duvet.BlueV0 = DuvetBlueBottomStart; _duvet.BlueU0 = DuvetBlueFadeU0; _duvet.BlueU1 = DuvetBlueFadeU1;
            _duvet.FoldShadow = DuvetFoldShadow; _duvet.FoldWaves = DuvetFoldWaves;
            _duvet.FoldIrregular = DuvetFoldIrregular;
            _duvet.FoldLineColor = DuvetFoldLineColor; _duvet.FoldLineWidth = DuvetFoldLineWidth;
            _duvet.FadeInTime = DuvetFadeInTime; _duvet.FadeOutTime = DuvetFadeOutTime;
            _duvet.PullW = DuvetPullWidth; _duvet.PullLean = DuvetPullLean;
            _duvet.SetGrabFraction(DuvetGrabFraction);
            _duvet.SetInfluenceFraction(DuvetInfluenceFraction);
            // 抵銷人物濾鏡：夜晚乘色（_charRoot.Modulate）＋人物光影群組 shader
            {   // 呼吸：跟人物同一個呼吸時間，吸氣慢一點、吐氣放鬆一點，不是死板的正弦
                float ph = _breathTime * Mathf.Pi * 2f;
                float b = Mathf.Sin(ph) + DuvetBreathAsym * Mathf.Sin(2f * ph - 0.6f);
                float inh = DuvetBreathEnabled ? Mathf.Clamp(b * 0.5f + 0.5f, 0f, 1f) : 0f;
                inh = inh * inh * (3f - 2f * inh);
                _duvet.BreathPx = DuvetBreathPx * (inh - 0.5f) * 2f * (DuvetBreathEnabled ? 1f : 0f);
                _duvet.BreathWide = DuvetBreathEnabled ? DuvetBreathWide * (inh - 0.5f) * 2f : 0f;
                _duvet.BreathC = new Vector4(DuvetBreathCenterUV.X, DuvetBreathCenterUV.Y, Mathf.Max(0.02f, DuvetBreathRadiusUV.X), Mathf.Max(0.02f, DuvetBreathRadiusUV.Y));
            }
            _duvet.CancelCharFilter = DuvetCancelCharFilter;
            _duvet.CharShadeMat = _charShadeMat;
            _duvet.NightMul = _charRoot != null ? _charRoot.Modulate : Colors.White;
        }
        if (_duvetKicked != null && IsInstanceValid(_duvetKicked))
        {
            _duvetKicked.FadeInTime = DuvetFadeInTime; _duvetKicked.FadeOutTime = DuvetFadeOutTime;
            _duvetKicked.KickedBlueAmt = DuvetKickedBlueAmount; _duvetKicked.KickedBlueRange = DuvetKickedBlueRange;
            _duvetKicked.KickedBlueCol = DuvetKickedBlueColor; _duvetKicked.KickedBlueColBot = DuvetKickedBlueColorBottom; _duvetKicked.KickedBlueCornerAmt = DuvetKickedCornerAmount; _duvetKicked.KickedBlueCornerRange = DuvetKickedCornerRange; _duvetKicked.DgCol = DuvetDiagColor; _duvetKicked.DgAmt = DuvetKickedDiagAmount; _duvetKicked.DgPos = DuvetDiagPos; _duvetKicked.DgSoft = DuvetDiagSoft; _duvetKicked.DgShiftPx = DuvetDiagShiftPx; _duvetKicked.DgAngleDeg = DuvetDiagAngleDeg; _duvetKicked.KickedTopK = DuvetKickedTopBlueScale; _duvetKicked.LtCol = DuvetLeftTopBlueColor; _duvetKicked.LtAmt = DuvetKickedLeftTopBlueAmount; _duvetKicked.LtRange = DuvetLeftTopBlueRange;
            _duvetKicked.KickedScale = DuvetKickedScale;
            _duvetKicked.KickedPivot = DuvetKickedPivot;
            _duvetKicked.OffsetPx = DuvetKickedOffsetPx;
            _duvetKicked.ClipPx = DuvetKickedClipPx;
            _duvetKicked.ClipIrregular = DuvetKickedClipIrregular;
            _duvetKicked.ClipWavePx = DuvetKickedWavePx;
            _duvetKicked.ClipWaveLength = DuvetKickedWaveLength;
            _duvetKicked.RoundPx = DuvetKickedRoundPx;
            _duvetKicked.OutlinePx = DuvetKickedOutlinePx;
            _duvetKicked.OutlineColor = DuvetKickedOutlineColor;
        }
    }

    /// <summary>滑鼠（場景座標）換算成被子的本地 Y（被子本地座標，單位＝Size）。</summary>
    bool DuvetLocal(Vector2 world, out Vector2 local)
    {
        local = _duvet.GetGlobalTransform().AffineInverse() * world;
        return true;
    }

    /// <summary>由 _Input 呼叫：蓋著時抓上緣往下拉；踢掉時抓著被子拖。回傳 true＝吃掉事件。</summary>
    bool DuvetInput(InputEvent e)
    {
        if (DuvetState == 1) { _duvetDrag = false; return KickedInput(e); }
        if (_kickDrag) { _kickDrag = false; if (_duvetKicked != null) { _duvetKicked.DragSnap = false; _duvetKicked.Drag = Vector2.Zero; } }
        return CoveredInput(e);
    }

    /// <summary>蓋著狀態：按在被子上緣 DuvetGrabFraction 範圍內（且貼圖不透明）時開始；
    /// 只有被抓的那一點往下變形，左右順勢往那一點傾斜（不是整片往下平移）；
    /// 往下拖多少，那一點就被拉下多少（20%~50% 的範圍依距離遞減地跟著動）；
    /// 抓滿就切成「踢到左下角」；放手只回彈 DuvetFoldRecover（預設 20%），其餘維持，再抓可以接著拉。</summary>
    bool CoveredInput(InputEvent e)
    {
        if (_duvet == null || !IsInstanceValid(_duvet) || DuvetState != 0 || !_duvet.Visible) { if (_duvetDrag) _duvetDrag = false; return false; }
        float sizeY = Mathf.Max(_duvet.Size.Y, 1f);
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed)
            {
                if (IsPointerOverUi() || _tongueTool) return false;
                DuvetLocal(ScreenToWorld(mb.Position), out var lp);
                float v = lp.Y / sizeY, u = lp.X / Mathf.Max(_duvet.Size.X, 1f);
                // 已經有殘留的拉痕時，可抓的範圍跟著往下放寬
                if (u < 0f || u > 1f || v < _duvet.TopV || v > _duvet.TopV + _duvet.GrabBandV * (1f + _duvet.Fold)) return false;
                var img = _duvet.Covered?.GetImage();
                if (img == null) return false;
                if (img.IsCompressed()) img.Decompress();
                int px = (int)(u * img.GetWidth()), py = (int)(v * img.GetHeight());
                if (px < 0 || py < 0 || px >= img.GetWidth() || py >= img.GetHeight() || img.GetPixel(px, py).A < 0.1f) return false;
                _duvetDrag = true;
                _duvetDragStartLocalY = lp.Y;
                _foldStart = _duvet.Fold;                 // 從上次放手留下的拉痕接著拉
                _pullStarted = false;
                _duvet.PullX = u;                         // 被抓的點
                _duvet.FoldSnap = true; _duvet.FoldTarget = _foldStart;
                return true;
            }
            if (_duvetDrag)
            {
                _duvetDrag = false;
                // 放手：只回彈 DuvetFoldRecover（預設 20%），其餘維持拉下的樣子
                _duvet.FoldSnap = false;
                _duvet.FoldTarget = _duvet.FoldTarget * (1f - Mathf.Clamp(DuvetFoldRecover, 0f, 1f));
                return true;
            }
        }
        else if (e is InputEventMouseMotion mm && _duvetDrag)
        {
            DuvetLocal(ScreenToWorld(mm.Position), out var lp);
            float band = Mathf.Max(_duvet.GrabBandV * sizeY, 1f);
            float p = Mathf.Clamp(_foldStart + (lp.Y - _duvetDragStartLocalY) / band, 0f, 1f);
            _duvet.FoldTarget = p;
            // 這次拉扯真的往下拉了一點，才新增一組皺褶線條（只點一下不動不算；舊的線條保留）
            if (!_pullStarted && p > _foldStart + 0.02f) { _pullStarted = true; _duvet.BeginPull(_duvet.PullX); }
            if (p >= 1f)
            {
                // 抓滿 → 被踢開
                _duvetDrag = false;
                DuvetState = 1;
                _duvet.FoldSnap = true; _duvet.FoldTarget = 0f;
                if (_duvetKicked != null) { _duvetKicked.Drag = Vector2.Zero; _duvetKicked.DragSnap = true; }
            }
            return true;
        }
        return false;
    }

    // ── 踢掉的被子：抓著往右上拖 → 蓋回去 ──────────────────────────────

    // 這些圖層上的點，視為「點到角色」：被子在角色下面，所以角色優先，不抓被子。
    static readonly string[] _kickCharLayers =
    {
        "身體", "臉", "胸1", "胸2", "手臂1", "手臂2", "手1", "手2",
        "腿/腿1", "腿/腿2", "m腿/腿1", "m腿/腿2", "男人腿1", "男人腿2", "男人肚子",
    };

    bool PointerOverCharacter(Vector2 world)
    {
        foreach (var n in _kickCharLayers) if (IsPointOnLayer(n, world)) return true;
        return false;
    }

    bool KickedOpaque(Vector2 uv)
    {
        if (uv.X < 0f || uv.X >= 1f || uv.Y < 0f || uv.Y >= 1f) return false;
        if (_kickedImg == null)
        {
            _kickedImg = _duvetKicked.Kicked?.GetImage();
            if (_kickedImg == null) return false;
            if (_kickedImg.IsCompressed()) _kickedImg.Decompress();
        }
        int px = (int)(uv.X * _kickedImg.GetWidth()), py = (int)(uv.Y * _kickedImg.GetHeight());
        if (px < 0 || py < 0 || px >= _kickedImg.GetWidth() || py >= _kickedImg.GetHeight()) return false;
        return _kickedImg.GetPixel(px, py).A >= 0.1f;
    }

    Vector2 KickedMouseUV(Vector2 world)
    {
        var lp = _duvetKicked.GetGlobalTransform().AffineInverse() * world;
        return new Vector2(lp.X / Mathf.Max(_duvetKicked.Size.X, 1f), lp.Y / Mathf.Max(_duvetKicked.Size.Y, 1f));
    }

    /// <summary>踢掉狀態：在被子（不透明處，且沒點到角色）按住左鍵拖動；
    /// 往右上拖超過 DuvetRestoreDistance 就蓋回去；沒拖到就彈回原位。</summary>
    bool KickedInput(InputEvent e)
    {
        if (_duvetKicked == null || !IsInstanceValid(_duvetKicked) || !_duvetKicked.Visible || _duvetKicked.Kicked == null)
        { _kickDrag = false; return false; }

        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed)
            {
                if (IsPointerOverUi() || _tongueTool) return false;
                var world = ScreenToWorld(mb.Position);
                if (PointerOverCharacter(world)) return false;
                var uv = KickedMouseUV(world);
                if (!KickedOpaque(_duvetKicked.SrcUV(uv))) return false;   // 要點在被子本體上（扣掉位移、還原縮放）
                _kickDrag = true;
                _kickGrabOffset = _duvetKicked.DragCur - uv;
                _duvetKicked.DragSnap = true;
                _duvetKicked.Drag = _duvetKicked.DragCur;
                return true;
            }
            if (_kickDrag)
            {
                _kickDrag = false;
                _duvetKicked.DragSnap = false; _duvetKicked.Drag = Vector2.Zero;   // 沒拖到：平滑彈回
                return true;
            }
        }
        else if (e is InputEventMouseMotion mm && _kickDrag)
        {
            var uv = KickedMouseUV(ScreenToWorld(mm.Position));
            var drag = uv + _kickGrabOffset;
            _duvetKicked.Drag = drag;
            // 往上拖了多少（貼圖像素；相對於踢掉時的位置）：往上拖滿 DuvetRestoreDistance（預設 50）就蓋回去
            var tex = _duvetKicked.Kicked;
            float up = -drag.Y * tex.GetHeight();
            if (up >= Mathf.Max(DuvetRestoreDistance, 1f))
            {
                // 拖到位 → 蓋回去（踢掉的被子留在被放開的位置淡出，蓋著的被子快速淡入）
                _kickDrag = false;
                DuvetState = 0;
                if (_duvet != null) { _duvet.FoldSnap = true; _duvet.FoldTarget = 0f; }
            }
            return true;
        }
        return false;
    }
}
