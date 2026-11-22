using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

/// <summary>
/// LivingRoom 劇情演出：鏡頭縮放（camZoom）＋衝擊特效（impact）
///
/// 劇本 JSON 用法（同一行可以一起寫，會同時進行；都在該行台詞出現「之前」播完）：
///   "camZoom": "倍率,幀數,焦點X,焦點Y"   鏡頭推近（只縮放背景＋立繪，不動台詞框、CG）
///                                         焦點 0~1（0.5,0.5＝畫面中心），省略就沿用上一次；
///                                         "1" 或 "1,1" ＝ 復原（幀數 1 ＝ 瞬間）
///   "impact":  "強度,幀數"                白閃＋畫面震動＋鏡頭衝撞，強度 1＝標準（可 0.3~2），幀數預設 30
///
/// 例：{ "text": "砰！", "se": "maoudamashii_impact09", "impact": "1.4,36", "camZoom": "1.2,14,0.5,0.42" }
/// 每段劇本播完（PlayDialogueAsync 結束）會自動把鏡頭復原。
/// </summary>
public partial class LivingRoomRuntime
{
    float   _camZoom  = 1f;                     // 目前鏡頭倍率
    float   _camPunch = 1f;                     // 衝擊時額外的瞬間放大
    Vector2 _camFocus = new(0.5f, 0.5f);        // 縮放焦點（0~1）
    int     _impactSerial;                      // 新的衝擊會中斷舊的

    static float ParseF(string s, float def)
        => float.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : def;

    List<Control> CameraTargets()
    {
        var list = new List<Control>();
        if (_bgImage != null) list.Add(_bgImage);
        if (_trueEndSpine != null) list.Add(_trueEndSpine);
        var pr = _panel?.GetNodeOrNull<Control>("PortraitFloatRoot");
        if (pr != null) list.Add(pr);
        return list;
    }

    void ApplyCamera()
    {
        float z = Mathf.Max(0.1f, _camZoom * _camPunch);
        foreach (var c in CameraTargets())
        {
            c.PivotOffset = new Vector2(c.Size.X * _camFocus.X, c.Size.Y * _camFocus.Y);
            c.Scale = new Vector2(z, z);
        }
    }

    /// <summary>劇本播完時呼叫：鏡頭、震動全部歸位。</summary>
    void ResetCameraFx()
    {
        _impactSerial++;
        _camZoom = 1f; _camPunch = 1f; _camFocus = new Vector2(0.5f, 0.5f);
        ApplyCamera();
        if (_panel != null) _panel.Position = Vector2.Zero;
        _vignetteA = 0f;
        if (_vignette != null && GodotObject.IsInstanceValid(_vignette)) _vignette.Modulate = new Color(1, 1, 1, 0);
    }

    async Task HandleCameraFxAsync(DialogueSystem.DialogueLine line)
    {
        Task zoom = Task.CompletedTask, hit = Task.CompletedTask;
        if (!string.IsNullOrWhiteSpace(line.camZoom)) zoom = CamZoomAsync(line.camZoom);
        if (!string.IsNullOrWhiteSpace(line.impact))  hit  = ImpactAsync(line.impact);
        Task vig = string.IsNullOrWhiteSpace(line.vignette) ? Task.CompletedTask : VignetteAsync(line.vignette);
        await Task.WhenAll(zoom, hit, vig);
    }

    async Task CamZoomAsync(string raw)
    {
        var p = raw.Split(',');
        float target = Mathf.Max(0.1f, ParseF(p[0], 1f));
        float frames = p.Length > 1 ? ParseF(p[1], 20f) : 20f;
        if (p.Length > 3) _camFocus = new Vector2(Mathf.Clamp(ParseF(p[2], 0.5f), 0f, 1f), Mathf.Clamp(ParseF(p[3], 0.5f), 0f, 1f));
        else if (target <= 1.0001f) _camFocus = new Vector2(0.5f, 0.5f);

        float dur = frames / 60f;
        if (dur <= 0.02f) { _camZoom = target; ApplyCamera(); return; }
        float start = _camZoom, t = 0f;
        while (t < dur)
        {
            t += (float)GetProcessDeltaTime();
            float k = Mathf.Clamp(t / dur, 0f, 1f);
            k = 1f - Mathf.Pow(1f - k, 3f);            // ease-out：一開始衝得快，最後慢慢停
            _camZoom = Mathf.Lerp(start, target, k);
            ApplyCamera();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _camZoom = target; ApplyCamera();
    }

    async Task ImpactAsync(string raw)
    {
        if (_panel == null) return;
        var p = raw.Split(',');
        float s   = Mathf.Clamp(ParseF(p[0], 1f), 0.05f, 3f);
        float dur = (p.Length > 1 ? ParseF(p[1], 30f) : 30f) / 60f;
        if (dur < 0.1f) dur = 0.1f;
        int serial = ++_impactSerial;

        // 白閃
        var flash = new ColorRect
        {
            Color = new Color(1f, 1f, 1f, Mathf.Clamp(0.55f + 0.25f * s, 0f, 0.9f)),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _panel.AddChild(flash);
        if (_fadeOverlay != null && _fadeOverlay.GetParent() == _panel) _panel.MoveChild(_fadeOverlay, _panel.GetChildCount() - 1);
        float flashA = flash.Color.A, flashDur = Mathf.Min(0.28f, dur);

        var rng = new RandomNumberGenerator(); rng.Randomize();
        float amp = 34f * s;            // 震動幅度（px）
        float punch = 0.07f * s;        // 衝撞放大量
        float t = 0f;
        while (t < dur)
        {
            if (serial != _impactSerial) break;
            t += (float)GetProcessDeltaTime();
            float k = Mathf.Clamp(t / dur, 0f, 1f);
            float decay = (1f - k) * (1f - k);

            _panel.Position = new Vector2(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f)) * amp * decay;
            // 衝撞：瞬間放大後彈回
            _camPunch = 1f + punch * Mathf.Exp(-7f * k) * Mathf.Cos(k * 18f);
            ApplyCamera();
            if (GodotObject.IsInstanceValid(flash))
                flash.Color = new Color(1f, 1f, 1f, flashA * Mathf.Clamp(1f - t / flashDur, 0f, 1f));

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (GodotObject.IsInstanceValid(flash)) flash.QueueFree();
        if (serial == _impactSerial)
        {
            _panel.Position = Vector2.Zero;
            _camPunch = 1f; ApplyCamera();
        }
    }

    // ── 視角黑霧（四周變暗的暈影）─────────────────────────────
    // 劇本："vignette": "強度,幀數"   強度 0~1（0＝關掉），幀數預設 30
    TextureRect _vignette;
    float _vignetteA;

    void EnsureVignette()
    {
        if (_vignette != null && GodotObject.IsInstanceValid(_vignette)) return;
        var g = new Gradient();
        g.SetColor(0, new Color(0, 0, 0, 0));
        g.SetOffset(0, 0.38f);
        g.SetColor(1, new Color(0, 0, 0, 1));
        g.SetOffset(1, 1.0f);
        g.AddPoint(0.72f, new Color(0, 0, 0, 0.55f));
        var tex = new GradientTexture2D
        {
            Gradient = g, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1.08f, 1.0f),
            Width = 512, Height = 288,
        };
        _vignette = new TextureRect
        {
            Texture = tex, StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0),
        };
        _vignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _panel.AddChild(_vignette);
        // 放在台詞框下面（蓋住背景／立繪／CG，但不蓋台詞）
        if (_dialogueBox != null && _dialogueBox.GetParent() == _panel)
            _panel.MoveChild(_vignette, _dialogueBox.GetIndex());
    }

    async Task VignetteAsync(string raw)
    {
        if (_panel == null) return;
        var p = raw.Split(',');
        float target = Mathf.Clamp(ParseF(p[0], 0.85f), 0f, 1f);
        float dur = (p.Length > 1 ? ParseF(p[1], 30f) : 30f) / 60f;
        EnsureVignette();
        if (_dialogueBox != null && _dialogueBox.GetParent() == _panel && _vignette.GetIndex() > _dialogueBox.GetIndex())
            _panel.MoveChild(_vignette, _dialogueBox.GetIndex());
        float start = _vignetteA, t = 0f;
        while (t < dur)
        {
            t += (float)GetProcessDeltaTime();
            _vignetteA = Mathf.Lerp(start, target, Mathf.Clamp(t / dur, 0f, 1f));
            _vignette.Modulate = new Color(1, 1, 1, _vignetteA);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        _vignetteA = target; _vignette.Modulate = new Color(1, 1, 1, target);
    }
}
