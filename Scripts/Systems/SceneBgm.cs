using Godot;

/// <summary>場景入場時補上配樂；後續劇情仍直接控制同一播放器，stop 不會被自動重啟。</summary>
public partial class SceneBgm : Node
{
    const string MusicBus = "SceneMusic";
    public const string Morning = "amacha_yuruyakanaasayake";
    public const string Gentle = "amacha_kasumisou";
    public const string Night = "amacha_seishishitauchu";
    public const string MiniGame = "amacha_picopicodisco";

    public static void Install(Node scene, string track, AudioStreamPlayer player = null)
    {
        if (scene.GetNodeOrNull<SceneBgm>("SceneBgm") != null) return;
        if (player == null)
        {
            player = new AudioStreamPlayer { Name = "SceneBgmPlayer" };
            scene.AddChild(player);
        }
        var controller = new SceneBgm { Name = "SceneBgm" };
        scene.AddChild(controller);
        player.Bus = MusicBus;
        // 僅補入沒有配樂的播放器，不覆寫已指定曲目。
        if (player.Stream != null) return;
        var stream = LoadLoopingStream(track);
        if (stream == null)
        {
            GD.PushWarning("找不到場景配樂：" + track);
            return;
        }
        player.Stream = stream;
        player.VolumeDb = 0f;
        player.Play();
    }

    /// <summary>載入 BGM 並確保開啟循環播放（支援 .mp3 / .ogg / .wav）。</summary>
    public static AudioStream LoadLoopingStream(string trackOrRelPath)
    {
        if (string.IsNullOrEmpty(trackOrRelPath)) return null;
        string trimmed = trackOrRelPath.Trim();
        string basePath = trimmed.StartsWith("res://") ? trimmed
            : trimmed.StartsWith("Audio/") ? "res://Resources/" + trimmed
            : "res://Resources/Audio/bgm/" + trimmed;
        string[] exts = { "", ".mp3", ".ogg", ".wav" };
        foreach (string ext in exts)
        {
            string full = basePath + ext;
            if (ResourceLoader.Exists(full))
            {
                var source = ResourceLoader.Load<AudioStream>(full);
                if (source != null)
                {
                    var copy = source.Duplicate() as AudioStream ?? source;
                    EnableLoop(copy);
                    return copy;
                }
            }
            if (full.EndsWith(".mp3") && FileAccess.FileExists(full))
            {
                var bytes = FileAccess.GetFileAsBytes(full);
                if (bytes != null && bytes.Length > 0)
                {
                    var mp3 = new AudioStreamMP3 { Data = bytes, Loop = true };
                    return mp3;
                }
            }
        }
        return null;
    }

    /// <summary>為音訊串流開啟循環播放。</summary>
    public static void EnableLoop(AudioStream stream)
    {
        if (stream is AudioStreamOggVorbis ogg) ogg.Loop = true;
        else if (stream is AudioStreamMP3 mp3) mp3.Loop = true;
        else if (stream is AudioStreamWav wav) wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
    }

    public override void _Ready()
    {
        if (AudioServer.GetBusIndex(MusicBus) < 0)
        {
            AudioServer.AddBus();
            int index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, MusicBus);
            AudioServer.SetBusSend(index, "Master");
        }
        SettingsManager.OnVolumeChanged += ApplyVolume;
        ApplyVolume();
    }

    public override void _ExitTree() => SettingsManager.OnVolumeChanged -= ApplyVolume;

    static void ApplyVolume()
    {
        int index = AudioServer.GetBusIndex(MusicBus);
        if (index < 0) return;
        float volume = (SettingsManager.Instance?.MusicVolume ?? 0.8f) * 0.35f;
        AudioServer.SetBusMute(index, volume <= 0f);
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(volume, 0.0001f)));
    }
}
