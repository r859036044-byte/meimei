using Godot;

/// <summary>原生繪製深藍魚形圖示，用於男主對話框名字欄。</summary>
public partial class FishDecoration : Control
{
    public FishDecoration() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Draw()
    {
        float w = Size.X;
        float h = Size.Y;

        // ── 魚身（橢圓）──────────────────────────────────────────
        const int segs = 44;
        var body = new Vector2[segs];
        float cx = w * 0.42f;  // 圓心偏左以留空間給魚尾
        float cy = h * 0.50f;
        float rx = w * 0.36f;
        float ry = h * 0.40f;
        for (int i = 0; i < segs; i++)
        {
            float t = Mathf.Tau * i / segs;
            body[i] = new Vector2(cx + rx * Mathf.Cos(t), cy + ry * Mathf.Sin(t));
        }
        DrawColoredPolygon(body, new Color("3a72c4"));

        // ── 魚尾（兩個三角形）────────────────────────────────────
        float tailBase = cx + rx * 0.55f;
        float tipX     = w * 0.99f;
        float spread   = h * 0.42f;
        var tailTop = new Vector2[]
        {
            new Vector2(tailBase, cy - h * 0.06f),
            new Vector2(tailBase, cy - ry * 0.50f),
            new Vector2(tipX,     cy - spread),
        };
        var tailBot = new Vector2[]
        {
            new Vector2(tailBase, cy + h * 0.06f),
            new Vector2(tailBase, cy + ry * 0.50f),
            new Vector2(tipX,     cy + spread),
        };
        DrawColoredPolygon(tailTop, new Color("2a5faa"));
        DrawColoredPolygon(tailBot, new Color("2a5faa"));

        // ── 輪廓 ─────────────────────────────────────────────────
        var outline = new Vector2[segs + 1];
        body.CopyTo(outline, 0);
        outline[segs] = body[0];
        DrawPolyline(outline, new Color("1a3f80"), 0.8f, true);

        // ── 眼睛 ─────────────────────────────────────────────────
        const int eyeSegs = 14;
        float ex = cx - rx * 0.38f;
        float ey = cy - ry * 0.18f;
        float er = Mathf.Min(w, h) * 0.10f;
        var eye = new Vector2[eyeSegs];
        for (int i = 0; i < eyeSegs; i++)
        {
            float t = Mathf.Tau * i / eyeSegs;
            eye[i] = new Vector2(ex + er * Mathf.Cos(t), ey + er * Mathf.Sin(t));
        }
        DrawColoredPolygon(eye, Colors.White);
        // 瞳孔
        float pr = er * 0.48f;
        var pupil = new Vector2[eyeSegs];
        for (int i = 0; i < eyeSegs; i++)
        {
            float t = Mathf.Tau * i / eyeSegs;
            pupil[i] = new Vector2(ex + pr * Mathf.Cos(t), ey + pr * Mathf.Sin(t));
        }
        DrawColoredPolygon(pupil, new Color("1a3060"));

        // ── 魚鰭（背鰭）─────────────────────────────────────────
        var fin = new Vector2[]
        {
            new Vector2(cx - rx * 0.10f, cy - ry * 0.88f),
            new Vector2(cx + rx * 0.20f, cy - ry * 0.88f),
            new Vector2(cx + rx * 0.25f, cy - ry * 1.05f),
            new Vector2(cx - rx * 0.05f, cy - ry * 1.10f),
        };
        DrawColoredPolygon(fin, new Color("2a5faa"));
    }
}
