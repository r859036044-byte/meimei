using Godot;
using System;

/// <summary>
/// EyeFollowController — Unity 版移植
/// 讓眼睛/瞳孔圖層跟隨滑鼠移動，並模擬雙眼視差。
///
/// 移植說明：
///   RectTransform       → Control
///   Canvas local space  → 以根 Control（1920×1080）的 local 座標系為準
///   LateUpdate          → _Process
///   enabled             → SetProcess(bool)
/// </summary>
public partial class EyeFollowController : Node
{
    // ════════════════════════════════════════════════════════════
    //  Export
    // ════════════════════════════════════════════════════════════

    /// <summary>根畫布 Control 路徑（全螢幕 1920×1080 Control，用於 mouse 座標轉換）</summary>
    [Export] public NodePath CanvasRootPath;

    /// <summary>臉部中心 Control 路徑（留空則用各眼睛基準位置的平均值）</summary>
    [Export] public NodePath FaceCenterPath;

    [Export] public EyeLayerConfig[] Eyes = new EyeLayerConfig[]
    {
        new EyeLayerConfig { LayerName = "表情/眼球1", MaxMoveX = 8f, MaxMoveY = 4f },
        new EyeLayerConfig { LayerName = "表情/眼球2", MaxMoveX = 8f, MaxMoveY = 4f },
    };

    [ExportGroup("靈敏度")]
    [Export] public float SensitivityX = 300f;
    [Export] public float SensitivityY = 200f;

    [ExportGroup("視差")]
    [Export] public float ParallaxFactor = 0.25f;

    [ExportGroup("平滑")]
    [Export] public float SmoothSpeed = 8f;

    [ExportGroup("觸發區域")]
    // 數值取自 Unity 場景檔 TrainingRoom.unity（fileID 903285459, EyeFollowController）
    // 的真實 Inspector 調校值：TriggerCenter (-35, 150)、TriggerSize (300, 500)。
    // 這裡的座標系是「Unity 慣例：Y 軸朝上」（_Process() 裡滑鼠座標已翻轉成同一慣例再比對），
    // 所以可以直接照抄 Unity 數值，不需要再額外翻轉 Y。
    // 範圍大約落在「上半身到頭」，往下在小穴（腰部以下）之前就會離開觸發區，符合原設計。
    [Export] public Vector2 TriggerCenter = new Vector2(-35f, 150f);
    [Export] public Vector2 TriggerSize   = new Vector2(300f, 500f);

    /// <summary>由 TrainingRoomRuntime 在互動時設為 true，強制眼睛歸位。</summary>
    public bool SuppressTracking = false;

    /// <summary>執行期由 TrainingRoomRuntime 注入，供圖層字典查找（LayerName 內含「/」的 key 需靠此字典解析，
    /// 因為場景樹是扁平結構，並非真正的節點階層，GetNodeOrNull 無法直接解析）。</summary>
    public TrainingRoomRuntime Runtime;

    /// <summary>由 TrainingRoomRuntime 每幀寫入的「頭部呼吸」Y 偏移量（Godot 座標）。
    /// 眼球已經完全交給這個控制器管位置，不會再經過通用呼吸迴圈，所以呼吸時的自然起伏
    /// 要靠這裡疊加，而不是外部直接改 Position（避免又變成兩邊搶位置）。</summary>
    public float BreathOffsetY = 0f;
    public Vector2 ShakeOffset = Vector2.Zero;  // 高潮痙攣抖動（同 EyeCloseController.ShakeOffset）

    // ════════════════════════════════════════════════════════════
    //  私有狀態
    // ════════════════════════════════════════════════════════════

    private Control _canvasRoot;
    private Control _faceCenter;
    private EyeState[] _eyeStates;
    private bool _ready = false;

    private sealed class EyeState
    {
        public Control Node;
        public Vector2 BasePos;        // 初始 Position（父空間）
        public Vector2 BasePosCanvas;  // 初始在 canvas root local 空間的位置
        public Vector2 CurrentOffset;
    }

    // ════════════════════════════════════════════════════════════
    //  生命週期
    // ════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        SetProcess(false); // 等初始化完成再開啟

        if (CanvasRootPath != null && !CanvasRootPath.IsEmpty)
            _canvasRoot = GetNodeOrNull<Control>(CanvasRootPath);

        if (_canvasRoot == null && Runtime != null)
        {
            // 由 TrainingRoomRuntime 注入時，眼睛圖層實際上是 CharRoot 的子節點，
            // 座標轉換應以 CharRoot 為基準（而非整個畫面的 CanvasLayer）
            _canvasRoot = Runtime.CharRoot;
        }

        if (_canvasRoot == null)
        {
            // fallback：往上找第一個全螢幕 Control
            _canvasRoot = GetParent<Control>();
        }

        if (FaceCenterPath != null && !FaceCenterPath.IsEmpty)
            _faceCenter = GetNodeOrNull<Control>(FaceCenterPath);

        InitEyeStates();
        _ready = true;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!_ready || _eyeStates == null || _canvasRoot == null) return;

        // 滑鼠在 canvas root local 空間的位置
        // Godot Y 軸朝下，Unity Y 軸朝上 → 計算時翻轉 Y 使語意一致
        Vector2 mouseScreen = GetViewport().GetMousePosition();
        Vector2 mouseCanvas = ScreenToCanvasLocal(mouseScreen);
        // 轉為 Unity 慣例：Y 軸朝上（上=正）
        mouseCanvas.Y = -mouseCanvas.Y;

        // 臉部中心（canvas local，Unity 慣例）
        Vector2 faceCenter;
        if (_faceCenter != null)
        {
            Vector2 fc = ScreenToCanvasLocal(_faceCenter.GetGlobalRect().GetCenter());
            fc.Y = -fc.Y;
            faceCenter = fc;
        }
        else
        {
            faceCenter = Vector2.Zero;
            int cnt = 0;
            foreach (var st in _eyeStates)
            {
                if (st == null) continue;
                faceCenter += st.BasePosCanvas;
                cnt++;
            }
            if (cnt > 0) faceCenter /= cnt;
        }

        // 觸發區域
        var triggerRect = new Rect2(
            TriggerCenter.X - TriggerSize.X * 0.5f,
            TriggerCenter.Y - TriggerSize.Y * 0.5f,
            TriggerSize.X, TriggerSize.Y);
        bool inZone = !SuppressTracking && triggerRect.HasPoint(mouseCanvas);

        // 正規化偏移
        Vector2 d = mouseCanvas - faceCenter;
        float normX = Mathf.Clamp(d.X / SensitivityX, -1f, 1f);
        float normY = Mathf.Clamp(d.Y / SensitivityY, -1f, 1f);

        float dt = (float)delta;

        for (int i = 0; i < _eyeStates.Length; i++)
        {
            var st = _eyeStates[i];
            if (st == null || st.Node == null) continue;

            Vector2 targetOffset;
            if (inZone)
            {
                float eyeX = st.BasePosCanvas.X - faceCenter.X;
                float parallaxMult = 1f;
                if (normX != 0f && eyeX != 0f)
                    parallaxMult = 1f - Mathf.Sign(eyeX) * Mathf.Sign(normX) * ParallaxFactor;

                float cfg_maxX = Eyes.Length > i ? Eyes[i].MaxMoveX : 8f;
                float cfg_maxY = Eyes.Length > i ? Eyes[i].MaxMoveY : 4f;

                // canvas 偏移轉回 Godot（Y 翻轉）
                targetOffset = new Vector2(normX * cfg_maxX * parallaxMult, -normY * cfg_maxY);
            }
            else
            {
                targetOffset = Vector2.Zero;
            }

            st.CurrentOffset = SmoothSpeed > 0f
                ? st.CurrentOffset.Lerp(targetOffset, dt * SmoothSpeed)
                : targetOffset;

            st.Node.Position = st.BasePos + st.CurrentOffset + new Vector2(0f, BreathOffsetY) + ShakeOffset;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  公開 API
    // ════════════════════════════════════════════════════════════

    public void RefreshBasePositions()
    {
        if (_eyeStates == null) return;
        foreach (var st in _eyeStates)
        {
            if (st == null || st.Node == null) continue;
            st.Node.Position   = st.BasePos;
            st.CurrentOffset   = Vector2.Zero;
            st.BasePos         = st.Node.Position;
            Vector2 basePosCanvas = GlobalToCanvasLocal(st.Node.GetGlobalRect().GetCenter());
            basePosCanvas.Y = -basePosCanvas.Y; // 轉 Unity 慣例（與 InitEyeStates 一致，修正原本漏掉的 Y 翻轉）
            st.BasePosCanvas   = basePosCanvas;
        }
    }

    /// <summary>將所有眼睛立即歸位到基準位置（供切換待機狀態時呼叫，
    /// 對應 Unity 版 OnDisable() 停用時自動歸位的行為，Godot 的 SetProcess(false) 沒有這個副作用，需手動呼叫）。</summary>
    public void ResetToCenter()
    {
        if (_eyeStates == null) return;
        foreach (var st in _eyeStates)
        {
            if (st == null || st.Node == null) continue;
            st.CurrentOffset = Vector2.Zero;
            st.Node.Position = st.BasePos;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  內部工具
    // ════════════════════════════════════════════════════════════

    private void InitEyeStates()
    {
        if (Eyes == null) { _eyeStates = Array.Empty<EyeState>(); return; }
        _eyeStates = new EyeState[Eyes.Length];

        for (int i = 0; i < Eyes.Length; i++)
        {
            var cfg = Eyes[i];
            Control ctrl = cfg.DirectNode;

            // 場景樹是扁平結構（圖層 key 例如「表情/眼球1」內的「/」只是命名慣例，
            // 實際節點名稱是底線版本，且都是 _charRoot 的平行子節點），
            // 因此優先透過 Runtime.Layers 字典查找，與 Unity 原版 Runtime.Layers.TryGetValue 邏輯一致
            if (ctrl == null && !string.IsNullOrEmpty(cfg.LayerName) && Runtime != null)
                Runtime.Layers.TryGetValue(cfg.LayerName, out ctrl);

            if (ctrl == null && !string.IsNullOrEmpty(cfg.LayerName) && _canvasRoot != null)
            {
                ctrl = _canvasRoot.GetNodeOrNull<Control>(cfg.LayerName);
            }

            if (ctrl == null)
            {
                GD.PrintErr($"[EyeFollow] 找不到圖層節點「{cfg.LayerName}」");
                continue;
            }

            if (ctrl == null) continue;

            Vector2 basePos       = ctrl.Position;
            Vector2 basePosCanvas = GlobalToCanvasLocal(ctrl.GetGlobalRect().GetCenter());
            basePosCanvas.Y = -basePosCanvas.Y; // 轉 Unity 慣例

            _eyeStates[i] = new EyeState
            {
                Node          = ctrl,
                BasePos       = basePos,
                BasePosCanvas = basePosCanvas,
                CurrentOffset = Vector2.Zero,
            };
        }
    }

    /// <summary>螢幕像素座標 → canvas root local 座標（Godot Y 軸朝下）</summary>
    private Vector2 ScreenToCanvasLocal(Vector2 screenPos)
    {
        if (_canvasRoot == null) return screenPos;
        var xform = _canvasRoot.GetGlobalTransform().AffineInverse();
        // 先用 viewport transform 把螢幕座標轉成全域 canvas 座標
        var canvasXform = GetViewport().GetScreenTransform().AffineInverse();
        Vector2 globalPos = canvasXform * screenPos;
        return xform * globalPos;
    }

    /// <summary>全域座標 → canvas root local 座標（Godot Y 軸朝下）</summary>
    private Vector2 GlobalToCanvasLocal(Vector2 globalPos)
    {
        if (_canvasRoot == null) return globalPos;
        return _canvasRoot.GetGlobalTransform().AffineInverse() * globalPos;
    }
}

// ════════════════════════════════════════════════════════════════════════
//  資料結構
// ════════════════════════════════════════════════════════════════════════

[Serializable]
public partial class EyeLayerConfig : Godot.Resource
{
    /// <summary>直接指定眼睛 Control 節點（比 LayerName 優先）</summary>
    public Control DirectNode;

    /// <summary>節點路徑（從 CanvasRoot 起，DirectNode 為空時使用）</summary>
    [Export] public string LayerName;

    [Export] public float MaxMoveX = 8f;
    [Export] public float MaxMoveY = 4f;
}
