using Godot;

/// <summary>
/// 名字框背景貼圖產生器：
/// 白色底 + 水平漸層（左透明 → 右不透明）+ 圓角（含抗鋸齒）。
/// 顏色由節點的 Modulate 染色，因此可沿用依說話者換色的邏輯。
///
/// Unity 用 Texture2D.SetPixels + Sprite.Create，
/// Godot 改用 Image.SetPixel + ImageTexture.CreateFromImage。
/// </summary>
public static class NameBarSprite
{
    // 產生後快取，避免每次重畫
    private static ImageTexture _cached;

    private const int   W          = 260;    // 與名字框大小相同
    private const int   H          = 42;
    private const float Radius     = 12f;    // 圓角半徑（像素）
    private const float AlphaLeft  = 0.25f;  // 左側透明度
    private const float AlphaRight = 1.0f;   // 右側透明度

    /// <summary>取得名字框貼圖（第一次呼叫時產生並快取）</summary>
    public static Texture2D Get()
    {
        if (_cached != null) return _cached;

        // 建立 RGBA8 影像
        var img = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                // 水平漸層：左邊淡、右邊濃
                float a = Mathf.Lerp(AlphaLeft, AlphaRight, x / (float)(W - 1));

                // 圓角遮罩（帶 1px 抗鋸齒）
                float mask = 1f;
                float cx = -1f, cy = -1f;

                if (x < Radius && y < Radius)                 { cx = Radius;         cy = Radius; }
                else if (x >= W - Radius && y < Radius)        { cx = W - 1 - Radius; cy = Radius; }
                else if (x < Radius && y >= H - Radius)        { cx = Radius;         cy = H - 1 - Radius; }
                else if (x >= W - Radius && y >= H - Radius)   { cx = W - 1 - Radius; cy = H - 1 - Radius; }

                if (cx >= 0f)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    mask = Mathf.Clamp(Radius - d + 0.5f, 0f, 1f);
                }

                img.SetPixel(x, y, new Color(1f, 1f, 1f, a * mask));
            }
        }

        _cached = ImageTexture.CreateFromImage(img);
        return _cached;
    }
}
