using Godot;

/// <summary>
/// 簡化版 2-骨 IK：手臂（肩膀→手肘）+ 手（手肘→指尖）（Unity → Godot 移植版）
///
/// 使用方式：
///   1. 填好 ArmLayerName / HandLayerName（對應 Layers 字典的 key）
///   2. UseIK = false，進 Play 模式
///   3. 配置 ShoulderPivot、ArmElbow、HandJoint、HandTip 等骨骼參數
///   4. 確認後勾選 UseIK
///   5. 遊戲中點熱點 9 → 拖動即可控制手臂
///
/// Godot 移植說明：
///   - RectTransform.anchoredPosition → Control.Position
///   - localEulerAngles.z → Control.RotationDegrees
///   - pivot → Control.PivotOffset（pixels = normalized * Size）
///   - TransformPoint/InverseTransformPoint → GlobalTransform * / AffineInverse *
///   - Mouse.current.position → GetViewport().GetMousePosition()
///   - RectTransformUtility.ScreenPointToLocalPointInRectangle → 自行轉換（見 ScreenToLocal）
/// </summary>
public partial class ArmIK : Node
{
    // ── 連結 ──────────────────────────────────────────────────────────
    [Export] public NodePath RuntimePath;
    [Export] public string   ArmLayerName  = "手臂2";
    [Export] public string   HandLayerName = "手2";

    // ── 旋轉錨點 ──────────────────────────────────────────────────────
    [Export] public Vector2 ShoulderPivot = new Vector2(0.5f, 1.0f);  // normalized
    [Export] public Vector2 HandPivot     = new Vector2(0.5f, 0.5f);

    // ── 圖片微調 ──────────────────────────────────────────────────────
    [Export] public Vector2 ArmOffset    = Vector2.Zero;
    [Export] public float   ArmRotOffset = 0f;
    [Export] public Vector2 HandOffset   = Vector2.Zero;
    [Export] public float   HandRotOffset= 0f;

    // ── 手動 XY 位置微調 ──────────────────────────────────────────────
    [Export] public Vector2 ArmPosAdjust  = Vector2.Zero;
    [Export] public Vector2 HandPosAdjust = Vector2.Zero;

    // ── 拖動靈敏度 ────────────────────────────────────────────────────
    [Export(PropertyHint.Range, "0,3")] public float DragHandScale      = 1f;
    [Export(PropertyHint.Range, "0,3")] public float DragHandScaleRight = 0.3f;
    [Export(PropertyHint.Range, "0,3")] public float DragHandScaleDown  = 0.3f;
    [Export(PropertyHint.Range, "0,20")] public float ReturnSpeed       = 10f;

    // ── 鏡像 ──────────────────────────────────────────────────────────
    [Export] public bool MirrorX = false;

    // ── 凍結呼吸 ──────────────────────────────────────────────────────
    [Export] public float FrozenBreathY = 0f;

    // ── 吸附凍結偏移 ──────────────────────────────────────────────────
    [Export] public float   FreezeArmRotOffset  = 0f;
    [Export] public Vector2 FreezeHandPosOffset = Vector2.Zero;
    [Export] public float   FreezeHandRotOffset = 0f;

    // ── 旋轉靈敏度 ────────────────────────────────────────────────────
    [Export(PropertyHint.Range, "-2,2")] public float ArmRotPerUp    = -0.3f;
    [Export(PropertyHint.Range, "-2,2")] public float ArmRotPerRight =  0.05f;
    [Export(PropertyHint.Range, "-2,2")] public float ArmRotPerLeft  =  0.05f;
    [Export(PropertyHint.Range, "-2,2")] public float HandRotPerUp   = 0f;
    [Export(PropertyHint.Range, "-2,2")] public float HandRotPerDown = 0f;
    [Export(PropertyHint.Range, "-2,2")] public float HandRotPerRight= 0.2f;
    [Export(PropertyHint.Range, "-2,2")] public float HandRotPerLeft = 0.2f;

    // ── 死區 ──────────────────────────────────────────────────────────
    [Export(PropertyHint.Range, "0,200")] public float DeadzoneX = 0f;
    [Export(PropertyHint.Range, "0,200")] public float DeadzoneY = 0f;

    // ── 曲線補正 ──────────────────────────────────────────────────────
    [Export] public bool    UseDragProjection = false;
    [Export] public Vector2 DragReferenceDir  = new Vector2(-1f, 0.5f);

    // ── ArmLift ──────────────────────────────────────────────────────
    [Export] public bool  ArmLiftEnable      = false;
    [Export(PropertyHint.Range, "-500,500")] public float ArmLiftStartDist = 50f;
    [Export(PropertyHint.Range, "-800,800")] public float ArmLiftFullDist  = 300f;
    [Export(PropertyHint.Range, "-90,90")]   public float ArmLiftMaxRot    = -30f;
    [Export(PropertyHint.Range, "-1,1")]     public float ArmLiftCurve     = 0f;
    [Export(PropertyHint.Range, "-90,90")]   public float ArmLiftProjectAngle = 0f;

    // ── PreXRotBoost ──────────────────────────────────────────────────
    [Export(PropertyHint.Range, "-3,3")]     public float ArmPreXRotBoost     = 0f;
    [Export(PropertyHint.Range, "-800,800")] public float ArmPreXBoostFullDist = 200f;

    // ── HandDragBounds ────────────────────────────────────────────────
    [Export] public bool    HandDragBoundsEnabled = false;
    [Export] public Vector2 HandDragBoundsMin     = new Vector2(-200f, -100f);
    [Export] public Vector2 HandDragBoundsMax     = new Vector2( 100f,  200f);

    // ── HandLift ──────────────────────────────────────────────────────
    [Export] public bool  HandLiftEnable      = false;
    [Export(PropertyHint.Range, "0,500")]   public float HandLiftStartDist   = 50f;
    [Export(PropertyHint.Range, "0,800")]   public float HandLiftFullDist    = 300f;
    [Export(PropertyHint.Range, "-90,90")]  public float HandLiftMaxRot      = -30f;
    [Export(PropertyHint.Range, "-90,90")]  public float HandLiftProjectAngle = 0f;
    [Export(PropertyHint.Range, "-800,800")] public float HandLiftXFullDist  = 300f;

    // ── YOnlyBoost ──────────────────────────────────────────────────
    [Export] public bool  YOnlyBoostEnable   = false;
    [Export(PropertyHint.Range, "-90,90")]  public float YOnlyBoostArmMaxRot  = 0f;
    [Export(PropertyHint.Range, "-90,90")]  public float YOnlyBoostHandMaxRot = 0f;
    [Export(PropertyHint.Range, "0,800")]   public float YOnlyBoostYFullDist  = 200f;
    [Export(PropertyHint.Range, "0,800")]   public float YOnlyBoostXFullDist  = 200f;

    // ── 穩定吸附基準 ──────────────────────────────────────────────────
    [Export] public bool UseStableSnapRef = false;

    // ── 手腕附著 ──────────────────────────────────────────────────────
    [Export] public bool HandFollowWrist = false;

    // ── IK 參數（HideInInspector → 無 [Export]，但仍可程式設定）─────
    public bool    UseIK           = false;
    public Vector2 ArmElbow        = new Vector2(0f, 200f);     // Godot Y 下正，原 (0,-200)
    public Vector2 HandJoint       = new Vector2(0f, -80f);     // 原 (0, 80) → Godot Y 反
    public Vector2 HandTip         = new Vector2(0f, 120f);     // 原 (0,-120) → Godot Y 反
    public Vector2 DragFollowPoint = new Vector2(0f, 120f);
    public Vector2 DragCursorOffset= Vector2.Zero;
    public float   ElbowBias       = 1f;
    public Vector2 RestPosition    = new Vector2(0f, 200f);     // Godot Y 反
    public float   BreathSuppress  = 0.85f;

    // TRR 每幀寫入的靜止位置
    // 靜止姿勢（無呼吸），由 TrainingRoomRuntime.UpdateArmLayerDetails 每幀寫入（Godot 座標：Position / PivotOffset / RotationDegrees）
    public bool    DragArmRestValid  = false;
    public bool    DragHandRestValid = false;
    public Vector2 DragArmRestPivot  = Vector2.Zero;
    public Vector2 DragHandRestPivot = Vector2.Zero;
    public Vector2 DragArmRestPos  = Vector2.Zero;
    public float   DragArmRestRot  = 0f;
    public Vector2 DragHandRestPos = Vector2.Zero;
    public float   DragHandRestRot = 0f;

    // ── 公開狀態 ──────────────────────────────────────────────────────
    public bool IsDragging  { get; private set; }
    public bool IsFrozen    => _isFrozen;
    public bool IsReturning => _isReturning;
    public bool IsActive    => UseIK || IsDragging || _isReturning || _isFrozen;

    // ── 私有 ─────────────────────────────────────────────────────────
    Vector2 _ikTarget;
    bool    _ikTargetInited;
    Vector2 _lastHandDir = new Vector2(0f, 1f);   // Godot Y 下，對應 Unity (0,-1)

    // IK 骨架座標（CharRootRT 空間，供視覺化）
    Vector2 _visShoulder, _visElbow, _visHandJoint, _visHandTip;
    bool    _visValid = false;

    Vector2 _shoulderFixed;
    bool    _shoulderValid = false;

    bool    _dragInited;
    bool    _isReturning;
    bool    _isFrozen;
    Control _handParentCtrl;
    Vector2 _dragStartMouseInParent;
    float   _origArmRot;
    float   _origHandRot;
    Vector2 _shoulderInParent;
    Vector2 _shoulderPos;          // 拖動中肩膀點（Godot 父座標），固定不動
    Vector2 _origHandPivInParent;
    float   _retArmRot;
    Vector2 _retHandPivInParent;
    float   _retHandRot;
    float   _returnSpeedMult = 1f;

    bool    _snapRefValid            = false;
    float   _snapRefArmRot           = 0f;
    float   _snapRefHandRot          = 0f;
    Vector2 _snapRefHandPivInParent;

    TrainingRoomRuntime _runtime;

    // ════════════════════════════════════════════════════════════════════
    //  生命週期
    // ════════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        if (RuntimePath != null && !RuntimePath.IsEmpty)
            _runtime = GetNodeOrNull<TrainingRoomRuntime>(RuntimePath);
        // 往上找父節點（ArmIK 是由 TrainingRoomRuntime 動態 AddChild 建立的）。
        // 原本只用名稱 "TrainingRoomRuntime" 搜尋，但場景根節點名稱是 "TrainingRoom"，
        // 會找不到 → _runtime 為 null → 整個拖動系統完全不動作。
        for (Node n = GetParent(); _runtime == null && n != null; n = n.GetParent())
            _runtime = n as TrainingRoomRuntime;
        if (_runtime == null)
            _runtime = GetTree().Root.FindChild("TrainingRoomRuntime", true, false) as TrainingRoomRuntime;
    }

    // ════════════════════════════════════════════════════════════════════
    //  座標慣例（復刻版）
    //  • 所有「角度」欄位（_origArmRot/_retArmRot/_origHandRot/_retHandRot/_snapRef*Rot、
    //    以及 Inspector 的 Freeze*RotOffset）都用 Unity 慣例：度、逆時針為正。
    //    寫回 Godot 時一律 RotationDegrees = -角度。
    //  • 所有「滑鼠位移 / 手位移 / 各種 Bounds、Offset」都用 Unity 慣例：Y 向上為正。
    //    Godot 的位移先 ToU() 翻轉 Y，計算完再 ToU() 翻回。
    //  • 所有「位置」欄位（_shoulderPos、_origHandPivInParent…）存的是 Godot 父節點座標。
    //  • ShoulderPivot / HandPivot 是 Unity「小切圖」上的 0~1 比例，
    //    透過 TrainingRoomRuntime.UnityPivotToLocal 換算成整張畫布上的像素點。
    // ════════════════════════════════════════════════════════════════════
    static Vector2 ToU(Vector2 v) => new Vector2(v.X, -v.Y);

    static float LerpAngleDeg(float from, float to, float t)
    {
        float d = Mathf.PosMod(to - from + 180f, 360f) - 180f;
        return from + d * t;
    }
    static float DeltaAngleDeg(float a, float b) => Mathf.PosMod(b - a + 180f, 360f) - 180f;

    /// <summary>Control 本地像素點 local → 父節點座標（考慮 PivotOffset 與旋轉，Scale 視為 1）</summary>
    static Vector2 LocalToParent(Control c, Vector2 local)
        => c.Position + c.PivotOffset + (local - c.PivotOffset).Rotated(Mathf.DegToRad(c.RotationDegrees));

    /// <summary>要讓本地點 local 落在父座標 target（旋轉 godotRotDeg）時，Control 應有的 Position</summary>
    static Vector2 PositionForPoint(Control c, Vector2 local, Vector2 target, float godotRotDeg)
        => target - c.PivotOffset - (local - c.PivotOffset).Rotated(Mathf.DegToRad(godotRotDeg));

    /// <summary>更換 PivotOffset 但保持畫面不動（Godot 改 PivotOffset 在有旋轉時會讓圖片位移）</summary>
    static void SetPivotKeepVisual(Control c, Vector2 newPivot)
    {
        Vector2 keep = LocalToParent(c, newPivot);
        c.PivotOffset = newPivot;
        c.Position    = PositionForPoint(c, newPivot, keep, c.RotationDegrees);
    }

    Vector2 ShoulderLocal(Control armRT) => _runtime != null && _runtime.HasArmSpriteRect(ArmLayerName)
        ? TrainingRoomRuntime.UnityPivotToLocal(ArmLayerName, ShoulderPivot)
        : new Vector2(ShoulderPivot.X, 1f - ShoulderPivot.Y) * armRT.Size;
    Vector2 HandPivotLocal(Control handRT) => _runtime != null && _runtime.HasArmSpriteRect(HandLayerName)
        ? TrainingRoomRuntime.UnityPivotToLocal(HandLayerName, HandPivot)
        : new Vector2(HandPivot.X, 1f - HandPivot.Y) * handRT.Size;

    // ── 公開 API（由 TrainingRoomRuntime 的熱點 9 / 10 呼叫）──────────────
    public void BeginDrag()
    {
        _isFrozen   = false;
        _dragInited = false;
        IsDragging  = true;
    }

    public void EndDrag()
    {
        _shoulderValid = false;
        if (!UseIK && _dragInited) { IsDragging = false; _isReturning = true; }
        else                       { IsDragging = false; _dragInited = false; }
    }

    public void CancelDragAndRelease()
    {
        if (!_dragInited) { _isFrozen = false; _isReturning = false; _shoulderValid = false; return; }
        _retArmRot          += FreezeArmRotOffset;
        _retHandPivInParent += ToU(FreezeHandPosOffset);
        _retHandRot         += FreezeHandRotOffset;
        IsDragging       = false;
        _isFrozen        = false;
        _isReturning     = true;
        _returnSpeedMult = 2f;
        _shoulderValid   = false;
    }

    public void FreezeAtDragPos()
    {
        if (!_dragInited) { EndDrag(); return; }
        IsDragging = false; _isReturning = false; _isFrozen = true;
    }

    /// <summary>計算游標位於 screenPos 時的手臂姿勢並凍結（吸附到熱點圓心用）</summary>
    public void FreezeAtScreenPos(Vector2 screenPos)
    {
        if (!_dragInited || _handParentCtrl == null) { EndDrag(); return; }
        Vector2 curMouse = _handParentCtrl.GetGlobalTransformWithCanvas().AffineInverse() * screenPos;

        Vector2 refHandPiv  = (UseStableSnapRef && _snapRefValid) ? _snapRefHandPivInParent : _origHandPivInParent;
        float   baseArmRot  = (UseStableSnapRef && _snapRefValid) ? _snapRefArmRot  : _origArmRot;
        float   baseHandRot = (UseStableSnapRef && _snapRefValid) ? _snapRefHandRot : _origHandRot;

        Vector2 posDelta = ToU(curMouse - refHandPiv);   // Unity 慣例（Y 上正）

        float snapX = posDelta.X >= 0f ? posDelta.X * DragHandScaleRight : posDelta.X * DragHandScale;
        float snapY = posDelta.Y <= 0f ? posDelta.Y * DragHandScaleDown  : posDelta.Y * DragHandScale;
        _retHandPivInParent = refHandPiv + ToU(new Vector2(snapX, snapY));

        Vector2 rotDelta = new Vector2(MirrorX ? -posDelta.X : posDelta.X, posDelta.Y);
        if (HandDragBoundsEnabled)
            rotDelta = new Vector2(Mathf.Clamp(rotDelta.X, HandDragBoundsMin.X, HandDragBoundsMax.X),
                                   Mathf.Clamp(rotDelta.Y, HandDragBoundsMin.Y, HandDragBoundsMax.Y));

        ComputeDragRotations(rotDelta, posDelta.X, posDelta.X, baseArmRot, baseHandRot);

        IsDragging = false; _isReturning = false; _isFrozen = true;
    }

    public void Unfreeze()
    {
        _isFrozen = false; _dragInited = false; _shoulderValid = false;
    }

    /// <summary>
    /// 忠實復刻 Unity ArmIK 的旋轉公式（拖動與吸附共用）。
    /// delta = 已做 MirrorX / Bounds 的位移（Unity 慣例）；rawX = 未鏡像的 X（選參數用）；
    /// boostX = YOnlyBoost 用的 X（Unity 拖動用 posEffX、吸附用 posDelta.x）。
    /// 結果寫入 _retArmRot / _retHandRot（Unity 角度）。
    /// </summary>
    void ComputeDragRotations(Vector2 delta, float rawX, float boostX, float baseArmRot, float baseHandRot)
    {
        Vector2 eff = new Vector2(
            Mathf.Abs(delta.X) > DeadzoneX ? delta.X - Mathf.Sign(delta.X) * DeadzoneX : 0f,
            Mathf.Abs(delta.Y) > DeadzoneY ? delta.Y - Mathf.Sign(delta.Y) * DeadzoneY : 0f);

        if (UseDragProjection && DragReferenceDir.LengthSquared() > 0.01f)
        {
            Vector2 refNorm = DragReferenceDir.Normalized();
            float   proj    = eff.Dot(refNorm);
            eff = proj > 0f ? refNorm * proj : Vector2.Zero;
        }

        // ── 手臂 ──
        float yRotBase = Mathf.Max(0f, eff.Y);
        if (ArmPreXBoostFullDist != 0f)
        {
            float xProgress = Mathf.Clamp(-eff.X / ArmPreXBoostFullDist, 0f, 1f);
            yRotBase *= 1f + (1f - xProgress) * ArmPreXRotBoost;
        }
        float armRotDelta = yRotBase * ArmRotPerUp
                          + (eff.X >= 0f ? eff.X * ArmRotPerRight : -eff.X * ArmRotPerLeft);

        if (ArmLiftEnable && ArmLiftFullDist > ArmLiftStartDist)
        {
            float upDist = Mathf.Max(0f, LiftProjection(eff, ArmLiftProjectAngle));
            float t      = Mathf.Clamp(Mathf.InverseLerp(ArmLiftStartDist, ArmLiftFullDist, upDist), 0f, 1f);
            float curved = ArmLiftCurve >= 0f
                ? Mathf.Pow(t, 1f + ArmLiftCurve * 3f)
                : 1f - Mathf.Pow(1f - t, 1f - ArmLiftCurve * 3f);
            armRotDelta += curved * ArmLiftMaxRot;
        }
        if (YOnlyBoostEnable && YOnlyBoostXFullDist > 0f && YOnlyBoostYFullDist > 0f)
        {
            float xb = Mathf.Clamp(boostX / YOnlyBoostXFullDist, 0f, 1f);
            float yb = Mathf.Clamp(Mathf.Max(0f, eff.Y) / YOnlyBoostYFullDist, 0f, 1f);
            armRotDelta += (1f - xb) * yb * YOnlyBoostArmMaxRot;
        }
        _retArmRot = baseArmRot + (MirrorX ? -armRotDelta : armRotDelta);

        // ── 手 ──
        float handRotYSign = MirrorX ? 1f : -1f;
        float handRotX = rawX >= 0f ? eff.X * HandRotPerRight : eff.X * HandRotPerLeft;
        float handRotY = eff.Y >= 0f ? eff.Y * HandRotPerUp : eff.Y * HandRotPerDown;
        _retHandRot = baseHandRot + handRotYSign * handRotY - handRotX;

        if (HandLiftEnable && HandLiftFullDist > HandLiftStartDist)
        {
            float upDist    = Mathf.Max(0f, LiftProjection(eff, HandLiftProjectAngle));
            float t         = Mathf.Clamp(Mathf.InverseLerp(HandLiftStartDist, HandLiftFullDist, upDist), 0f, 1f);
            float xProgress = HandLiftXFullDist != 0f ? Mathf.Clamp(-eff.X / HandLiftXFullDist, 0f, 1f) : 0f;
            _retHandRot += (MirrorX ? -1f : 1f) * t * (1f - xProgress) * HandLiftMaxRot;
        }
        if (YOnlyBoostEnable && YOnlyBoostXFullDist > 0f && YOnlyBoostYFullDist > 0f)
        {
            float xb = Mathf.Clamp(boostX / YOnlyBoostXFullDist, 0f, 1f);
            float yb = Mathf.Clamp(Mathf.Max(0f, eff.Y) / YOnlyBoostYFullDist, 0f, 1f);
            _retHandRot += (MirrorX ? -1f : 1f) * (1f - xb) * yb * YOnlyBoostHandMaxRot;
        }
    }

    public override void _Process(double delta)
    {
        if (IsDragging)
        {
            bool released = !Input.IsMouseButtonPressed(MouseButton.Left);
            if (released)
            {
                IsDragging = false;
                if (!UseIK && _dragInited) _isReturning = true;
            }
        }
    }

    // 由 TrainingRoomRuntime 在呼吸/手臂圖層更新之後呼叫（等同 Unity LateUpdate）
    public void LateProcess(double delta)
    {
        if (_runtime == null) return;

        if (!UseIK && !IsDragging && !_isReturning && !_isFrozen)
        {
            _shoulderValid  = false;
            _ikTargetInited = false;
            _dragInited     = false;
            return;   // 待機：完全交給 TrainingRoomRuntime.UpdateArmLayerDetails（HandFollowWrist 在 Unity 也是關閉）
        }

        if (!_runtime.Layers.TryGetValue(ArmLayerName,  out var armNode)  || armNode  is not Control armRT)  return;
        if (!_runtime.Layers.TryGetValue(HandLayerName, out var handNode) || handNode is not Control handRT) return;
        var armParentRT  = armRT .GetParent() as Control ?? armRT;
        var handParentRT = handRT.GetParent() as Control ?? armParentRT;

        Vector2 shoulderLocal = ShoulderLocal(armRT);
        Vector2 handPivLocal  = HandPivotLocal(handRT);

        void ApplyArm(Vector2 shoulderPos, float unityRot)
        {
            armRT.PivotOffset     = shoulderLocal;
            armRT.RotationDegrees = -unityRot;
            // 軸心 = 肩膀；Position 使肩膀點固定在 shoulderPos
            armRT.Position        = PositionForPoint(armRT, shoulderLocal, shoulderPos, -unityRot);
        }
        void ApplyHand(Vector2 pivPos, float unityRot)
        {
            handRT.RotationDegrees = -unityRot;
            handRT.Position        = PositionForPoint(handRT, handPivLocal, pivPos, -unityRot);
        }

        // ── 凍結 ──
        if (_isFrozen && !IsDragging && !UseIK)
        {
            Vector2 breathOff = ToU(new Vector2(0f, FrozenBreathY));
            ApplyArm(_shoulderPos + breathOff, _retArmRot + FreezeArmRotOffset);
            ApplyHand(_retHandPivInParent + ToU(FreezeHandPosOffset) + breathOff, _retHandRot + FreezeHandRotOffset);
            return;
        }

        // ── 回彈 ──
        if (_isReturning && !IsDragging && !UseIK)
        {
            if (!_dragInited) { _isReturning = false; return; }
            float t = Mathf.Clamp(ReturnSpeed > 0f ? (float)delta * ReturnSpeed * _returnSpeedMult : 1f, 0f, 1f);
            float   returnArmRot  = (UseStableSnapRef && _snapRefValid) ? _snapRefArmRot  : _origArmRot;
            float   returnHandRot = (UseStableSnapRef && _snapRefValid) ? _snapRefHandRot : _origHandRot;
            Vector2 returnHandPiv = (UseStableSnapRef && _snapRefValid) ? _snapRefHandPivInParent : _origHandPivInParent;

            _retArmRot          = LerpAngleDeg(_retArmRot, returnArmRot, t);
            _retHandPivInParent = _retHandPivInParent.Lerp(returnHandPiv, t);
            _retHandRot         = LerpAngleDeg(_retHandRot, returnHandRot, t);
            ApplyArm(_shoulderPos, _retArmRot);
            ApplyHand(_retHandPivInParent, _retHandRot);

            if (Mathf.Abs(DeltaAngleDeg(_retArmRot, returnArmRot)) < 0.5f &&
                _retHandPivInParent.DistanceTo(returnHandPiv) < 1f &&
                Mathf.Abs(DeltaAngleDeg(_retHandRot, returnHandRot)) < 0.5f)
            {
                _isReturning = false; _dragInited = false; _returnSpeedMult = 1f;
            }
            return;
        }

        // ── 簡易拖動（UseIK=false）──
        if (IsDragging && !UseIK)
        {
            if (!_dragInited)
            {
                // 先把手臂/手放回「無呼吸」的靜止姿勢，再捕捉基準（TrainingRoomRuntime 每幀計算）
                if (DragArmRestValid)
                {
                    armRT.PivotOffset = DragArmRestPivot; armRT.Position = DragArmRestPos; armRT.RotationDegrees = DragArmRestRot;
                }
                if (DragHandRestValid)
                {
                    handRT.PivotOffset = DragHandRestPivot; handRT.Position = DragHandRestPos; handRT.RotationDegrees = DragHandRestRot;
                }
                _dragInited  = true;
                _origArmRot  = -armRT .RotationDegrees;
                _origHandRot = -handRT.RotationDegrees;
                _retArmRot   = _origArmRot;
                _retHandRot  = _origHandRot;

                _shoulderPos          = LocalToParent(armRT, shoulderLocal);
                _origHandPivInParent  = LocalToParent(handRT, handPivLocal);
                _retHandPivInParent   = _origHandPivInParent;

                if (UseStableSnapRef && !_snapRefValid)
                {
                    _snapRefArmRot          = _origArmRot;
                    _snapRefHandRot         = _origHandRot;
                    _snapRefHandPivInParent = _origHandPivInParent;
                    _snapRefValid           = true;
                }
                _handParentCtrl         = handParentRT;
                _dragStartMouseInParent = handParentRT.GetLocalMousePosition();
            }

            Vector2 d = ToU(handParentRT.GetLocalMousePosition() - _dragStartMouseInParent); // Unity 慣例
            float rawDeltaX = d.X;
            d.X = MirrorX ? -d.X : d.X;
            if (HandDragBoundsEnabled)
            {
                d = new Vector2(Mathf.Clamp(d.X, HandDragBoundsMin.X, HandDragBoundsMax.X),
                                Mathf.Clamp(d.Y, HandDragBoundsMin.Y, HandDragBoundsMax.Y));
                rawDeltaX = Mathf.Clamp(rawDeltaX, HandDragBoundsMin.X, HandDragBoundsMax.X);
            }
            float posEffX = Mathf.Abs(rawDeltaX) > DeadzoneX ? rawDeltaX - Mathf.Sign(rawDeltaX) * DeadzoneX : 0f;
            float effY    = Mathf.Abs(d.Y) > DeadzoneY ? d.Y - Mathf.Sign(d.Y) * DeadzoneY : 0f;
            if (UseDragProjection && DragReferenceDir.LengthSquared() > 0.01f)
            {
                // Unity：投影只作用在 eff（旋轉），手位置的 Y 也用投影後的 eff.y
                Vector2 eff0 = new Vector2(Mathf.Abs(d.X) > DeadzoneX ? d.X - Mathf.Sign(d.X) * DeadzoneX : 0f, effY);
                Vector2 refNorm = DragReferenceDir.Normalized();
                float proj = eff0.Dot(refNorm);
                effY = (proj > 0f ? refNorm * proj : Vector2.Zero).Y;
            }

            ComputeDragRotations(d, rawDeltaX, posEffX, _origArmRot, _origHandRot);

            float handX = posEffX >= 0f ? posEffX * DragHandScaleRight : posEffX * DragHandScale;
            float handY = effY    <= 0f ? effY    * DragHandScaleDown  : effY    * DragHandScale;
            _retHandPivInParent = _origHandPivInParent + ToU(new Vector2(handX, handY));

            ApplyArm(_shoulderPos, _retArmRot);
            ApplyHand(_retHandPivInParent, _retHandRot);
            return;
        }

        // ── UseIK 模式（Unity 場景兩隻手都是 UseIK=0，此段保留原移植版未改動）──
        _dragInited = false;

        // 設定手臂 pivot（旋轉中心 = 肩膀）
        SetPivotNormalized(armRT, ShoulderPivot);

        if (!_shoulderValid)
        {
            _shoulderFixed = armRT.Position;
            _shoulderValid = true;

            // 預計算初始手骨方向
            float iL1 = Mathf.Max(1f, ArmElbow.Length());
            float iL2 = Mathf.Max(1f, HandJoint.DistanceTo(HandTip));
            float iD  = Mathf.Clamp(RestPosition.Length(), Mathf.Abs(iL1 - iL2) + 1f, iL1 + iL2 - 1f);
            if (iD > 0.01f)
            {
                float cosA0 = Mathf.Clamp((iL1*iL1 + iD*iD - iL2*iL2) / (2f*iL1*iD), -1f, 1f);
                float eAng0 = Mathf.Atan2(RestPosition.Y, RestPosition.X) + Mathf.Acos(cosA0) * ElbowBias;
                Vector2 initElbow = _shoulderFixed + new Vector2(Mathf.Cos(eAng0), Mathf.Sin(eAng0)) * iL1;
                Vector2 initHand  = (_shoulderFixed + RestPosition) - initElbow;
                if (initHand.LengthSquared() > 0.01f) _lastHandDir = initHand.Normalized();
            }
        }
        Vector2 shoulder = _shoulderFixed;

        float L1 = Mathf.Max(1f, ArmElbow.Length());
        float L2 = Mathf.Max(1f, HandJoint.DistanceTo(HandTip));

        Vector2 rawTarget = shoulder + RestPosition;
        if (IsDragging)
        {
            rawTarget = ScreenToLocal(armParentRT, GetViewport().GetMousePosition());
            rawTarget += DragCursorOffset;
            Vector2 dpToTip = HandTip - DragFollowPoint;
            if (dpToTip.LengthSquared() > 0.01f)
            {
                float localBoneAng = Mathf.Atan2((HandTip - HandJoint).Y, (HandTip - HandJoint).X);
                float curHandAng   = Mathf.Atan2(_lastHandDir.Y, _lastHandDir.X) - localBoneAng;
                float c = Mathf.Cos(curHandAng), s = Mathf.Sin(curHandAng);
                rawTarget += new Vector2(dpToTip.X*c - dpToTip.Y*s, dpToTip.X*s + dpToTip.Y*c);
            }
        }

        float dt3 = (float)GetProcessDeltaTime();
        if (!_ikTargetInited) { _ikTarget = rawTarget; _ikTargetInited = true; }
        _ikTarget = ReturnSpeed > 0f
            ? _ikTarget.Lerp(rawTarget, dt3 * ReturnSpeed)
            : rawTarget;

        // 2-骨 IK
        Vector2 toTarget = _ikTarget - shoulder;
        float D = Mathf.Clamp(toTarget.Length(), Mathf.Abs(L1 - L2) + 1f, L1 + L2 - 1f);
        float cosA      = Mathf.Clamp((L1*L1 + D*D - L2*L2) / (2f * L1 * D), -1f, 1f);
        float baseAngle = Mathf.Atan2(toTarget.Y, toTarget.X);
        float elbowAng  = baseAngle + Mathf.Acos(cosA) * ElbowBias;
        Vector2 elbow   = shoulder + new Vector2(Mathf.Cos(elbowAng), Mathf.Sin(elbowAng)) * L1;

        Vector2 handVec = _ikTarget - elbow;
        if (handVec.LengthSquared() > 0.01f)
            _lastHandDir = _lastHandDir.Lerp(handVec.Normalized(), dt3 * 20f);

        // 套用手臂旋轉
        float localAng = Mathf.Atan2(ArmElbow.Y, ArmElbow.X);
        float worldAng = Mathf.Atan2(elbow.Y - shoulder.Y, elbow.X - shoulder.X);
        armRT.RotationDegrees = Mathf.RadToDeg(worldAng - localAng) + ArmRotOffset;
        armRT.Position        = shoulder + ArmOffset;

        // 套用手的位置和旋轉
        {
            Vector2 localBoneVec = HandTip - HandJoint;
            float   localBoneAng = Mathf.Atan2(localBoneVec.Y, localBoneVec.X);
            float   worldBoneAng = Mathf.Atan2(_lastHandDir.Y, _lastHandDir.X);
            float   handAngle    = worldBoneAng - localBoneAng;

            float theta = handAngle;
            float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
            Vector2 rotJoint = new Vector2(HandJoint.X*c - HandJoint.Y*s, HandJoint.X*s + HandJoint.Y*c);

            // HandPivot 補償
            Vector2 handPivNorm    = HandPivot - new Vector2(0.5f, 0.5f);
            Vector2 handPivShift   = handPivNorm * handRT.Size;
            Vector2 rotHandPivShift= new Vector2(handPivShift.X*c - handPivShift.Y*s,
                                                  handPivShift.X*s + handPivShift.Y*c);

            Vector2 elbowWorld    = armParentRT.GetGlobalTransform() * elbow;
            Vector2 elbowInHand   = handParentRT.GetGlobalTransform().AffineInverse() * elbowWorld;

            SetPivotNormalized(handRT, HandPivot);
            handRT.Position        = elbowInHand - rotJoint + rotHandPivShift + HandOffset;
            handRT.RotationDegrees = Mathf.RadToDeg(handAngle) + HandRotOffset;
        }
    }


    /// <summary>
    /// 手肘目前的畫面座標（給 TrainingRoomRuntime.TryGrabElbow 判斷滑鼠是否按在手肘附近）。
    /// 復刻版：取手臂切圖「下緣中點」(Unity 比例 0.5, 0.05) 經手臂目前的 Transform 換算，
    /// 會跟著呼吸/拖動即時移動。ElbowGrabPivot 可在 Inspector 微調。
    /// </summary>
    [Export] public Vector2 ElbowGrabPivot = new Vector2(0.5f, 0.05f);
    public Vector2? GetElbowGlobalPos()
    {
        if (_runtime == null) return null;
        if (!_runtime.Layers.TryGetValue(ArmLayerName, out var armNode) || armNode is not Control armRT) return null;
        Vector2 local = _runtime.HasArmSpriteRect(ArmLayerName)
            ? TrainingRoomRuntime.UnityPivotToLocal(ArmLayerName, ElbowGrabPivot)
            : ArmElbow;
        return armRT.GetGlobalTransform() * local;
    }

    /// <summary>
    /// 手圖層現在的「手腕連接點」世界座標（HandJoint 經 手圖層 Transform 換算）。
    /// 跟 GetElbowGlobalPos() 應該是同一個點——Unity 原版在 Scene View 用紅圓（ArmElbow）
    /// 疊在橘圓（HandJoint）上校準，兩點完全重疊 = 連接點正確。這裡沒有 Scene View 手動拖拉，
    /// 改成畫兩個小圓點讓你在遊戲畫面上直接比對兩點是否重疊（見 TrainingRoomRuntime 的
    /// ShowIKDebugDots）。
    /// </summary>
    public Vector2? GetHandJointGlobalPos()
    {
        if (_runtime == null) return null;
        if (!_runtime.Layers.TryGetValue(HandLayerName, out var handNode) || handNode is not Control handRT) return null;
        return handRT.GetGlobalTransform() * HandJoint;
    }

    // ════════════════════════════════════════════════════════════════════
    //  輔助方法
    // ════════════════════════════════════════════════════════════════════

    /// <summary>（Unity 慣例，eff.Y 向上為正）把 eff 投影到以 +Y 為基準、順時針 angleDeg 的方向</summary>
    float LiftProjection(Vector2 eff, float angleDeg)
    {
        if (angleDeg == 0f) return eff.Y;
        float rad = Mathf.DegToRad(angleDeg);
        float sx  = Mathf.Sin(rad);
        float sy  = Mathf.Cos(rad);
        float ex  = MirrorX ? -sx : sx;
        return eff.X * ex + eff.Y * sy;
    }

    /// <summary>設定 Control 的 PivotOffset（用 0~1 normalized 值）— 僅 UseIK 模式使用</summary>
    static void SetPivotNormalized(Control ctrl, Vector2 normalizedPivot)
    {
        ctrl.PivotOffset = normalizedPivot * ctrl.Size;
    }

    /// <summary>把螢幕座標轉換到 Control 的本地座標 — 僅 UseIK 模式使用</summary>
    static Vector2 ScreenToLocal(Control ctrl, Vector2 screenPos)
    {
        return ctrl.GetGlobalTransformWithCanvas().AffineInverse() * screenPos;
    }
}
