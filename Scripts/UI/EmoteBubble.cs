using Godot;
using System;

/// <summary>
/// 莉奈頭旁的氣泡表情（RPG Maker 風）：全部用程式畫，不需要圖檔。
///   Heart    好感：幾顆愛心往上飄、左右搖、淡出
///   Tangle   不好：一團毛線（亂麻），彈出來會抖一下
///   Exclaim  驚嘆號　　Question  問號
/// 用法：EmoteBubble.Spawn(parent, EmoteBubble.Kind.Heart, () => 螢幕座標);
/// 座標＝氣泡「底部中心」（愛心＝飄出的起點）；每幀重新呼叫 Func，所以可以跟著頭走。
/// </summary>
public partial class EmoteBubble : Control
{
    public enum Kind { Heart, Tangle, Exclaim, Question }

    public Kind EmoteKind;
    public Func<Vector2> Anchor;
    public float HoldSec = 1.2f;      // 表情氣泡停留秒數（愛心不用，飄完就消失）

    static readonly Color Outline = new Color("4a3a4f");
    static readonly Color HeartFill = new Color("ed70a3");
    static readonly Color HeartLine = new Color("e85b95");

    float _age;
    Vector2[] _scribble;
    // 愛心
    HeartDecoration[] _hearts;
    float[] _hDelay, _hDur, _hX, _hRise, _hSway, _hPhase, _hSize;

    public static EmoteBubble Spawn(Node parent, Kind kind, Func<Vector2> anchor, float holdSec = 1.2f)
    {
        var e = new EmoteBubble { EmoteKind = kind, Anchor = anchor, HoldSec = holdSec };
        parent.AddChild(e);
        return e;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 50;
        var rng = new RandomNumberGenerator(); rng.Randomize();

        if (EmoteKind == Kind.Heart)
        {
            Size = Vector2.Zero;
            int n = 5;
            _hearts = new HeartDecoration[n];
            _hDelay = new float[n]; _hDur = new float[n]; _hX = new float[n];
            _hRise = new float[n]; _hSway = new float[n]; _hPhase = new float[n]; _hSize = new float[n];
            for (int i = 0; i < n; i++)
            {
                _hSize[i] = rng.RandfRange(22f, 34f);
                _hDelay[i] = i * 0.18f + rng.RandfRange(0f, 0.1f);
                _hDur[i] = rng.RandfRange(1.5f, 2.1f);
                _hX[i] = rng.RandfRange(-28f, 28f);
                _hRise[i] = rng.RandfRange(90f, 140f);
                _hSway[i] = rng.RandfRange(8f, 16f);
                _hPhase[i] = rng.RandfRange(0f, Mathf.Tau);
                var h = new HeartDecoration { Size = new Vector2(_hSize[i], _hSize[i]), Visible = false, PivotOffset = new Vector2(_hSize[i], _hSize[i]) * 0.5f };
                AddChild(h);
                _hearts[i] = h;
            }
        }
        else
        {
            Size = new Vector2(72f, 76f);
            PivotOffset = new Vector2(36f, 72f);   // 從尾巴尖端彈出來
            if (EmoteKind == Kind.Tangle)
            {
                // 一團毛線：亂數但固定的幾圈環繞線
                _scribble = new Vector2[70];
                for (int i = 0; i < _scribble.Length; i++)
                {
                    float t = i / (float)(_scribble.Length - 1);
                    float a = t * Mathf.Tau * 3.2f + rng.RandfRange(-0.25f, 0.25f);
                    float r = 6f + 15f * Mathf.Abs(Mathf.Sin(t * 7.3f + 0.6f)) + rng.RandfRange(-2.5f, 2.5f);
                    _scribble[i] = new Vector2(36f, 32f) + new Vector2(Mathf.Cos(a) * r * 1.05f, Mathf.Sin(a * 1.17f + 0.8f) * r);
                }
            }
            Scale = new Vector2(0.3f, 0.3f);
        }
        Reposition();
    }

    void Reposition()
    {
        if (Anchor == null) return;
        Vector2 a = Anchor();
        Position = EmoteKind == Kind.Heart ? a : a - new Vector2(Size.X * 0.5f, Size.Y - 4f);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;
        Reposition();

        if (EmoteKind == Kind.Heart) { ProcessHearts(); return; }

        // 彈出：0.3 → 1.15 → 1；停留 HoldSec；最後 0.3 秒淡出並往上飄一點
        float s = _age < 0.16f ? Mathf.Lerp(0.3f, 1.15f, _age / 0.16f)
                : _age < 0.28f ? Mathf.Lerp(1.15f, 1f, (_age - 0.16f) / 0.12f) : 1f;
        Scale = new Vector2(s, s);
        float outT = Mathf.Clamp((_age - HoldSec) / 0.3f, 0f, 1f);
        Modulate = new Color(1, 1, 1, 1f - outT);
        if (EmoteKind == Kind.Tangle && _age < 0.9f)
            Rotation = Mathf.Sin(_age * 45f) * 0.09f * (1f - _age / 0.9f);   // 毛線團彈出來時抖一下
        else
            Rotation = 0f;
        if (_age >= HoldSec + 0.3f) QueueFree();
        QueueRedraw();
    }

    void ProcessHearts()
    {
        bool allDone = true;
        for (int i = 0; i < _hearts.Length; i++)
        {
            float lt = _age - _hDelay[i];
            var h = _hearts[i];
            if (lt < 0f) { allDone = false; h.Visible = false; continue; }
            float p = lt / _hDur[i];
            if (p >= 1f) { h.Visible = false; continue; }
            allDone = false;
            h.Visible = true;
            float x = _hX[i] + Mathf.Sin(p * Mathf.Tau * 1.2f + _hPhase[i]) * _hSway[i];
            float y = -p * _hRise[i];
            h.Position = new Vector2(x, y) - h.Size * 0.5f;
            float sc = 0.5f + 0.5f * Mathf.Min(1f, p * 6f);
            h.Scale = new Vector2(sc, sc);
            float fade = p < 0.12f ? p / 0.12f : 1f - Mathf.SmoothStep(0.55f, 1f, p);
            h.Modulate = new Color(1, 1, 1, fade);
        }
        if (allDone) QueueFree();
    }

    public override void _Draw()
    {
        if (EmoteKind == Kind.Heart) return;

        var c = new Vector2(36f, 32f);
        // 氣泡：白底圓＋深色描邊＋左下小尾巴
        DrawColoredPolygon(new[] { new Vector2(c.X - 10f, 58f), new Vector2(c.X + 6f, 60f), new Vector2(c.X - 14f, 72f) }, Outline);
        DrawColoredPolygon(new[] { new Vector2(c.X - 8f, 57f), new Vector2(c.X + 4f, 58f), new Vector2(c.X - 11f, 67f) }, Colors.White);
        DrawCircle(c, 31f, Outline);
        DrawCircle(c, 28f, Colors.White);

        switch (EmoteKind)
        {
            case Kind.Exclaim:
            {
                var col = new Color("e8456b");
                DrawColoredPolygon(new[] { new Vector2(c.X - 5f, c.Y - 19f), new Vector2(c.X + 5f, c.Y - 19f), new Vector2(c.X + 3f, c.Y + 4f), new Vector2(c.X - 3f, c.Y + 4f) }, col);
                DrawCircle(new Vector2(c.X, c.Y + 14f), 3.8f, col);
                break;
            }
            case Kind.Question:
            {
                var col = new Color("5b8fd9");
                var arc = new Vector2[22];
                for (int i = 0; i < arc.Length; i++)
                {
                    float a = Mathf.DegToRad(Mathf.Lerp(-185f, 40f, i / (float)(arc.Length - 1)));
                    arc[i] = new Vector2(c.X, c.Y - 9f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 11f;
                }
                DrawPolyline(arc, col, 5f, true);
                DrawLine(arc[^1], new Vector2(c.X, c.Y + 3f), col, 5f, true);
                DrawCircle(new Vector2(c.X, c.Y + 15f), 3.6f, col);
                break;
            }
            case Kind.Tangle:
                DrawPolyline(_scribble, new Color("3a3a42"), 2.4f, true);
                break;
        }
    }
}
