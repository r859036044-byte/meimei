using Godot;

/// <summary>
/// MiniGameNight3 圖層設定（Unity ScriptableObject → Godot Resource）
/// 掛到 LivingRoomRuntime 的 MgConfig 欄位
/// </summary>
[GlobalClass]
public partial class MiniGameNight3Config : Resource
{
    [ExportGroup("底層 (mg_bottom)")]
    [Export] public Vector2 BottomPos  = new Vector2(0f, 0f);
    [Export] public Vector2 BottomSize = new Vector2(0f, 0f);

    [ExportGroup("子宮層 (mg_uterus)")]
    [Export] public Vector2 UterusPos  = new Vector2(0f, 0f);
    [Export] public Vector2 UterusSize = new Vector2(0f, 0f);

    [ExportGroup("外框 (mg_frame)")]
    [Export] public Vector2 FramePos   = new Vector2(0f, 0f);
    [Export] public Vector2 FrameSize  = new Vector2(0f, 0f);

    [ExportGroup("雞雞各深度 Y 位置（正=上，負=下）")]
    [Export] public float JijiY_G      = -200f;
    [Export] public float JijiY_Near   =  -80f;
    [Export] public float JijiY_Far    =   50f;
    [Export] public float JijiY_Uterus =  150f;

    [ExportGroup("雞雞各深度旋轉角（正=逆時針）")]
    [Export] public float JijiRot_G      =   0f;
    [Export] public float JijiRot_Near   =  -5f;
    [Export] public float JijiRot_Far    = -10f;
    [Export] public float JijiRot_Uterus = -15f;

    [ExportGroup("雞雞偏移與大小")]
    [Export] public float   JijiOffX    = 0f;
    [Export] public float   JijiOffsetY = 0f;
    [Export] public Vector2 JijiSize    = new Vector2(0f, 0f);

    [ExportGroup("測試")]
    [Export] public float TestMarkerX = -1f;
}
