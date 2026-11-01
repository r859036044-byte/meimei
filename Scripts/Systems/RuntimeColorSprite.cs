using Godot;

/// <summary>
/// 執行時生成純色 Sprite 佔位符（Unity RuntimeColorSprite 的 Godot 移植版）
/// 掛在 Sprite2D 節點上，_Ready 時若無 Texture 則自動產生色塊。
/// Spine 插件安裝後可改用 SpineSprite，移除此腳本。
/// </summary>
public partial class RuntimeColorSprite : Sprite2D
{
    [Export] public Color SpriteColor = Colors.White;
    [Export] public int   PixelWidth  = 80;
    [Export] public int   PixelHeight = 80;

    public override void _Ready()
    {
        if (Texture != null) return;
        var img = Image.CreateEmpty(PixelWidth, PixelHeight, false, Image.Format.Rgba8);
        img.Fill(SpriteColor);
        Texture = ImageTexture.CreateFromImage(img);
    }
}
