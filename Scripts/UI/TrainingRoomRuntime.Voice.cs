using Godot;
using System.Collections.Generic;

/// <summary>
/// TrainingRoomRuntime — 莉奈（みづきゆうか 聲線）語音播放系統。
/// 支援台詞關鍵字配音、部位互動觸碰語音、快感呼吸嬌喘與絕頂高潮語音。
/// 音量依 SettingsManager.Instance.VoiceVolume 自動調節。
/// </summary>
public partial class TrainingRoomRuntime
{
    AudioStreamPlayer _trVoicePlayer;
    readonly Dictionary<string, AudioStream> _voiceCache = new();

    void InitTrVoice()
    {
        if (_trVoicePlayer == null || !IsInstanceValid(_trVoicePlayer))
        {
            _trVoicePlayer = new AudioStreamPlayer
            {
                Name = "TRVoicePlayer",
                Bus = "Master",
            };
            AddChild(_trVoicePlayer);
        }
    }

    /// <summary>播放指定路徑的 TR 語音（相對 res://Resources/Audio/Voice/TR/）</summary>
    public void PlayTrVoice(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        InitTrVoice();

        float vol = SettingsManager.Instance?.VoiceVolume ?? 1f;
        if (vol <= 0.001f) return;

        string fullPath = "res://Resources/Audio/Voice/TR/" + relativePath.TrimStart('/');
        if (!_voiceCache.TryGetValue(fullPath, out var stream))
        {
            if (ResourceLoader.Exists(fullPath))
            {
                stream = GD.Load<AudioStream>(fullPath);
                if (stream != null) _voiceCache[fullPath] = stream;
            }
        }

        if (stream == null) return;
        _trVoicePlayer.Stream = stream;
        _trVoicePlayer.VolumeLinear = vol;
        _trVoicePlayer.Play();
    }

    /// <summary>隨機挑選一首播放</summary>
    public void PlayTrVoiceRandom(params string[] relativePaths)
    {
        if (relativePaths == null || relativePaths.Length == 0) return;
        string pick = relativePaths[GD.Randi() % relativePaths.Length];
        PlayTrVoice(pick);
    }

    /// <summary>依莉奈所說的台詞內容與當前狀態，自動配對最合適的語音</summary>
    public void PlayGirlVoiceForText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 1. 專屬台詞配對
        if (text.Contains("這點本事") || text.Contains("その程度"))
        {
            PlayTrVoice("talk/sono_teido.wav");
            return;
        }
        if (text.Contains("雜魚") || text.Contains("弱雞") || text.Contains("哼"))
        {
            PlayTrVoice("talk/fun.wav");
            return;
        }
        if (text.Contains("好舒服") || text.Contains("舒服") || text.Contains("気持ちいい"))
        {
            PlayTrVoice("talk/kimochii.wav");
            return;
        }
        if (text.Contains("那裡") && text.Contains("喜歡"))
        {
            PlayTrVoice("talk/soko_suki.wav");
            return;
        }
        if (text.Contains("身體") && text.Contains("奇怪"))
        {
            PlayTrVoice("talk/karada_hen.wav");
            return;
        }
        if (text.Contains("再來") || text.Contains("啊哈") || text.Contains("哈啊"))
        {
            PlayTrVoiceRandom("talk/aha_motto.wav", "talk/suki.wav");
            return;
        }
        if (text.Contains("喜歡"))
        {
            PlayTrVoice("talk/suki.wav");
            return;
        }
        if (text.Contains("抱緊我"))
        {
            PlayTrVoice("talk/gyutte.wav");
            return;
        }

        // 2. 狀態類台詞（痛、高潮、插入、誘惑）
        if (text.Contains("痛") || text.Contains("不要") || text.Contains("不行") || text.Contains("慢一點") || text.Contains("別看"))
        {
            PlayTrVoiceRandom("pain/dame.wav", "pain/yamete.wav", "pain/muri.wav", "pain/iyada.wav", "pain/minai_de.wav");
            return;
        }
        if (text.Contains("要到") || text.Contains("要來了") || text.Contains("壞掉") || text.Contains("不行了"))
        {
            PlayTrVoiceRandom("climax/near_1.wav", "climax/near_2.wav", "climax/iku.wav");
            return;
        }
        if (text.Contains("插") || text.Contains("進去") || text.Contains("裡面"))
        {
            PlayTrVoiceRandom("insert/ireteru.wav", "insert/ireru_no.wav", "insert/iiyo.wav", "insert/kite.wav");
            return;
        }
        if (text.Contains("滿足我") || text.Contains("想要") || text.Contains("別停"))
        {
            PlayTrVoiceRandom("wet/wet_1.wav", "wet/wet_2.wav");
            return;
        }

        // 3. 正在被碰觸的部位專屬語音
        var f = _reactFlags;
        if (f.breast) { PlayPartTouchVoice("breast"); return; }
        if (f.nipple) { PlayPartTouchVoice("nipple"); return; }
        if (f.mouth)  { PlayPartTouchVoice("mouth"); return; }
        if (f.pussy)  { PlayPartTouchVoice("pussy"); return; }
        if (f.head)   { PlayPartTouchVoice("head"); return; }
        if (f.thrust) { PlayPartTouchVoice("thrust"); return; }

        // 4. 通用嬌喘反應（依目前穴高潮值層級）
        PlayArousalMoanVoice();
    }

    /// <summary>依快感值層級播放對應呼吸嬌喘語音</summary>
    public void PlayArousalMoanVoice()
    {
        float c = ClimaxLevel;
        if (c < 35f)
        {
            PlayTrVoiceRandom("moan/low_1.wav", "moan/low_2.wav", "moan/low_3.wav", "moan/low_4.wav");
        }
        else if (c < 75f)
        {
            PlayTrVoiceRandom("moan/mid_1.wav", "moan/mid_2.wav", "moan/mid_3.wav", "moan/mid_4.wav");
        }
        else
        {
            PlayTrVoiceRandom("moan/high_1.wav", "moan/high_2.wav", "moan/high_3.wav", "moan/high_4.wav");
        }
    }

    /// <summary>播放部位觸控反應專用語音</summary>
    public void PlayPartTouchVoice(string part)
    {
        switch (part)
        {
            case "breast":
                PlayTrVoiceRandom("touch/breast_1.wav", "touch/breast_2.wav", "touch/breast_3.wav");
                break;
            case "nipple":
                PlayTrVoiceRandom("touch/nipple_1.wav", "touch/nipple_2.wav", "touch/nipple_3.wav");
                break;
            case "mouth":
                PlayTrVoiceRandom("touch/mouth_1.wav", "touch/mouth_2.wav", "touch/mouth_3.wav");
                break;
            case "pussy":
                PlayTrVoiceRandom("touch/pussy_1.wav", "touch/pussy_2.wav", "touch/pussy_3.wav");
                break;
            case "head":
                PlayTrVoiceRandom("touch/head_1.wav", "touch/head_2.wav", "touch/head_3.wav");
                break;
            case "thrust":
                PlayTrVoiceRandom("touch/thrust_1.wav", "touch/thrust_2.wav");
                break;
            default:
                PlayArousalMoanVoice();
                break;
        }
    }

    /// <summary>播放高潮絕頂語音</summary>
    public void PlayTrClimaxVoice()
    {
        PlayTrVoiceRandom(
            "climax/climax_1.wav",
            "climax/climax_2.wav",
            "climax/climax_3.wav",
            "climax/climax_4.wav",
            "climax/iku.wav"
        );
    }
}
