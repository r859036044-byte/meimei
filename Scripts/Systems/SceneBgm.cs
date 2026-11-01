using Godot;

/// <summary>場景入場時補上配樂；後續劇情仍直接控制同一播放器，stop 不會被自動重啟。</summary>
public partial class SceneBgm : Node
{
    const string MusicBus = "SceneMusic";
    public const string Morning = "amacha_yuruyakanaasayake";
    public const string Gentle = "amacha_kasumisou";
    public const string Night = "amacha_seishishitauchu";

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
        string path = "res://Resources/Audio/bgm/" + track + ".mp3";
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning("找不到場景配樂：" + path);
            return;
        }
        var source = ResourceLoader.Load<AudioStreamMP3>(path);
        if (source == null) return;
        var stream = (AudioStreamMP3)source.Duplicate();
        stream.Loop = true;
        player.Stream = stream;
        player.VolumeDb = 0f;
        player.Play();
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
