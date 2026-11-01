using Godot;

/// <summary>
/// 對話框頂部按鈕列的版面設定
///
/// Unity 的 ScriptableObject → Godot 的 Resource
/// 可在編輯器建立 .tres 資源檔調整數值，或直接用預設值
/// </summary>
[GlobalClass]
public partial class DialogueTopBtnConfig : Resource
{
    // ═══════════════════════════════════════════════════════════
    //  按鈕高度（共用）
    // ═══════════════════════════════════════════════════════════
    [Export] public float BtnHeight = 30f;

    // ═══════════════════════════════════════════════════════════
    //  文字大小（共用）
    // ═══════════════════════════════════════════════════════════
    [Export] public int FontSize = 14;

    // ═══════════════════════════════════════════════════════════
    //  文字描邊（共用）
    // ═══════════════════════════════════════════════════════════
    [Export(PropertyHint.Range, "0,3,0.01")]
    public float OutlineAlpha = 1f;

    /// <summary>描邊大小（套用為四方向偏移）</summary>
    [Export] public float OutlineSize = 3f;

    // ═══════════════════════════════════════════════════════════
    //  文字偏移（共用，x=左右 y=上下）
    // ═══════════════════════════════════════════════════════════
    [Export] public Vector2 TextOffset = Vector2.Zero;

    // ═══════════════════════════════════════════════════════════
    //  自動播放
    // ═══════════════════════════════════════════════════════════
    [Export] public Vector2 AutoPlayPos = new Vector2(0f, 0f);
    [Export] public float   AutoPlayW   = 110f;

    // ═══════════════════════════════════════════════════════════
    //  快速播放
    // ═══════════════════════════════════════════════════════════
    [Export] public Vector2 FastFwdPos = new Vector2(-118f, 0f);
    [Export] public float   FastFwdW   = 110f;

    // ═══════════════════════════════════════════════════════════
    //  跳過
    // ═══════════════════════════════════════════════════════════
    [Export] public Vector2 SkipPos = new Vector2(-236f, 0f);
    [Export] public float   SkipW   = 70f;

    // ═══════════════════════════════════════════════════════════
    //  保存
    // ═══════════════════════════════════════════════════════════
    [Export] public Vector2 SavePos = new Vector2(-314f, 0f);
    [Export] public float   SaveW   = 110f;

    // ═══════════════════════════════════════════════════════════
    //  讀檔
    // ═══════════════════════════════════════════════════════════
    [Export] public Vector2 LoadPos = new Vector2(-432f, 0f);
    [Export] public float   LoadW   = 110f;
}
