using Godot;

/// <summary>新手教學的暗幕：全螢幕壓暗，挖一個圓形或方形的亮區（有閃動的粉色邊框）。亮區可設成「點得穿」（讓玩家操作亮區裡的東西）。</summary>
public partial class TutorialDim : ColorRect
{
    const string Code = @"shader_type canvas_item;
uniform vec4 hole = vec4(0.0);      // 亮區中心 x,y（像素）與半徑 rx,ry
uniform float shape = 0.0;          // 0 沒有亮區、1 圓／橢圓、2 方形
uniform vec2 size_px = vec2(1920.0, 1080.0);
void fragment() {
    vec2 p = UV * size_px;
    vec4 dark = vec4(0.0, 0.0, 0.0, 0.7);
    if (shape < 0.5) { COLOR = dark; return; }
    float d;
    vec2 q = p - hole.xy;
    if (shape < 1.5) { d = (length(q / max(hole.zw, vec2(1.0))) - 1.0) * min(hole.z, hole.w); }
    else { vec2 e = abs(q) - hole.zw; d = max(e.x, e.y); }
    float a = smoothstep(0.0, 3.0, d);
    float ring = (1.0 - smoothstep(0.0, 3.0, abs(d - 2.0))) * (0.65 + 0.35 * sin(TIME * 5.0));
    COLOR = mix(vec4(dark.rgb, dark.a * a), vec4(1.0, 0.82, 0.9, 1.0), ring);
}";

    int _shape; Vector2 _c, _r; bool _through;
    readonly ShaderMaterial _mat;

    public TutorialDim()
    {
        Color = Colors.White;
        MouseFilter = MouseFilterEnum.Stop;
        _mat = new ShaderMaterial { Shader = new Shader { Code = Code } };
        Material = _mat;
    }

    /// <summary>shape：0 無、1 圓、2 方；through＝亮區裡的操作點得到。</summary>
    public void SetHole(Rect2 r, int shape, bool through, Vector2 viewport)
    {
        _shape = shape; _through = through;
        Position = Vector2.Zero; Size = viewport;
        _c = r.GetCenter();
        _r = shape == 1 ? new Vector2(1f, 1f) * (Mathf.Max(r.Size.X, r.Size.Y) * 0.5f * 1.08f) : r.Size * 0.5f;
        _mat.SetShaderParameter("hole", new Vector4(_c.X, _c.Y, _r.X, _r.Y));
        _mat.SetShaderParameter("shape", (float)shape);
        _mat.SetShaderParameter("size_px", viewport);
    }

    public override bool _HasPoint(Vector2 p)
    {
        if (_shape == 0 || !_through) return true;
        var q = p - _c;
        bool inside = _shape == 1 ? (q.X * q.X) / (_r.X * _r.X) + (q.Y * q.Y) / (_r.Y * _r.Y) <= 1f
                                  : Mathf.Abs(q.X) <= _r.X && Mathf.Abs(q.Y) <= _r.Y;
        return !inside;
    }
}
