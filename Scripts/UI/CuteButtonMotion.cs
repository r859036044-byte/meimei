using Godot;
using System;

/// <summary>按鈕彈性互動及原創合成提示音，不使用外部音效。</summary>
public partial class CuteButtonMotion : Node
{
    Button _button;
    bool _hover;
    float _scale = 1f;
    AudioStreamPlayer _audio;
    static AudioStreamWav _hoverSound, _clickSound;
    static ulong _lastHover;

    public static void Attach(Button button)
    {
        if (button.HasNode("CuteMotion")) return;
        button.AddChild(new CuteButtonMotion { Name = "CuteMotion" });
    }
    public override void _Ready()
    {
        _button = GetParent<Button>();
        _audio = new AudioStreamPlayer { MaxPolyphony = 2, Bus = "Master" };
        AddChild(_audio);
        _hoverSound ??= Tone(920f, 1260f, 0.055f);
        _clickSound ??= Tone(740f, 1080f, 0.095f);
        _button.MouseEntered += () => { _hover = true; HoverSound(); };
        _button.MouseExited += () => _hover = false;
        _button.FocusEntered += HoverSound;
        _button.Pressed += () => Play(_clickSound, 0.30f);
    }
    void HoverSound()
    {
        if (_button.Disabled || Time.GetTicksMsec() - _lastHover < 90) return;
        _lastHover = Time.GetTicksMsec();
        Play(_hoverSound, 0.16f);
    }
    void Play(AudioStreamWav stream, float gain)
    {
        float volume = SettingsManager.Instance?.SFXVolume ?? 1f;
        if (volume <= 0f || _button.Disabled) return;
        _audio.Stream = stream;
        _audio.VolumeDb = Mathf.LinearToDb(volume * gain);
        _audio.Play();
    }
    public override void _Process(double delta)
    {
        if (!_button.IsVisibleInTree()) return;
        float target = _button.Disabled ? 1f : _button.IsPressed() ? 0.98f : _hover || _button.HasFocus() ? 1.015f : 1f;
        _scale = Mathf.Lerp(_scale, target, 1f - Mathf.Exp(-10f * (float)delta));
        _button.PivotOffset = _button.Size * 0.5f;
        _button.Scale = Vector2.One * _scale;
    }
    static AudioStreamWav Tone(float from, float to, float seconds)
    {
        const int rate = 22050;
        int count = (int)(rate * seconds);
        var data = new byte[count * 2];
        double phase = 0;
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / count;
            phase += Math.Tau * (from + (to - from) * t) / rate;
            double envelope = Math.Sin(Math.PI * t) * Math.Exp(-3.5 * t);
            short sample = (short)(Math.Sin(phase) * envelope * 24000);
            data[i * 2] = (byte)(sample & 255);
            data[i * 2 + 1] = (byte)((sample >> 8) & 255);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = false, Data = data };
    }
}
