using Godot;
using System.Collections.Generic;

/// <summary>
/// TrainingRoomRuntime — 莉奈（みづきゆうか 聲線）語音與呼吸嬌喘系統。
/// 支援：
///   1. 50% 柔和基礎音量控制（耳邊低語與弱氣台詞再額外降至 0.70x~0.75x）
///   2. 傲嬌/雌小鬼嘲諷台詞（talk_brat）
///   3. 口齒不清、微弱不堅定、低語小聲台詞（talk_mumble）
///   4. 融化依賴、撒嬌喜歡台詞（talk_sweet）
///   5. 50 款短促動態呼吸氣音與嬌喘（moan_breath）
///   6. 各部位互動觸摸嬌喘（touch_breast, touch_pussy, touch_body, touch_mouth）
///   7. 興奮絕頂與事後高潮語音（climax, insert）
///   8. 動態背景安眠呼吸嬌喘音軌（ambient），依快感值平滑淡入淡出。
///   9. 發聲即時聯動嘴型開合動畫（IsVoicePlaying / NotifyVoiceSpeak）。
/// </summary>
public partial class TrainingRoomRuntime
{
    AudioStreamPlayer _trVoicePlayer;
    AudioStreamPlayer _trAmbientPlayer;
    readonly Dictionary<string, AudioStream> _voiceCache = new();

    float _ambientTargetVol = 0f;

    /// <summary>當前是否有語音正在播放，供嘴型聯動判斷</summary>
    public bool IsVoicePlaying => (_trVoicePlayer != null && IsInstanceValid(_trVoicePlayer) && _trVoicePlayer.Playing) || _voiceMouthT > 0.01f;

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

        if (_trAmbientPlayer == null || !IsInstanceValid(_trAmbientPlayer))
        {
            _trAmbientPlayer = new AudioStreamPlayer
            {
                Name = "TRAmbientBreathPlayer",
                Bus = "Master",
            };
            AddChild(_trAmbientPlayer);

            // 優先使用精選溫柔安眠喘息音軌
            string ambPath = "res://Resources/Audio/Voice/TR/ambient/slow_gentle_breath.wav";
            if (!ResourceLoader.Exists(ambPath))
            {
                ambPath = "res://Resources/Audio/Voice/TR/ambient/loli_aegi.wav";
            }
            if (ResourceLoader.Exists(ambPath))
            {
                var s = GD.Load<AudioStream>(ambPath);
                if (s is AudioStreamWav wav)
                {
                    wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                }
                _trAmbientPlayer.Stream = s;
                _trAmbientPlayer.VolumeLinear = 0f;
                _trAmbientPlayer.Play();
            }
        }
    }

    /// <summary>每幀更新背景嬌喘呼吸音軌音量</summary>
    public void UpdateTrVoice(float dt)
    {
        InitTrVoice();
        if (_trAmbientPlayer == null || !IsInstanceValid(_trAmbientPlayer)) return;

        // 語音整體音量設為原本的 50%
        float masterVol = (SettingsManager.Instance?.VoiceVolume ?? 1f) * 0.50f;
        if (masterVol <= 0.001f)
        {
            _trAmbientPlayer.VolumeLinear = 0f;
            return;
        }

        // 當穴高潮值 > 15 或有任何部位互動/抽插時，呼吸嬌喘逐漸升溫淡入
        float c = ClimaxLevel;
        bool anyTouch = _touchAnimRunningLeft || _touchAnimRunningRight || _bTouchRunning || _tongueOn || _patOn;
        bool thrusting = _jijiPhase == 3 && _jijiStage >= 1 && !_s2Paused;

        if (c > 15f || anyTouch || thrusting)
        {
            float intensity = Mathf.Clamp((c - 15f) / 75f, 0f, 1f);
            float baseVol = (thrusting || anyTouch) ? 0.18f : 0.09f;
            _ambientTargetVol = (baseVol + intensity * 0.18f) * masterVol;
        }
        else
        {
            _ambientTargetVol = 0f;
        }

        // 平滑漸層淡入淡出
        _trAmbientPlayer.VolumeLinear = Mathf.MoveToward(_trAmbientPlayer.VolumeLinear, _ambientTargetVol, dt * 0.35f);
        if (_ambientTargetVol > 0.001f && !_trAmbientPlayer.Playing)
        {
            _trAmbientPlayer.Play();
        }
    }

    /// <summary>播放指定路徑的 TR 語音（相對 res://Resources/Audio/Voice/TR/）</summary>
    public void PlayTrVoice(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        InitTrVoice();

        // 依使用者要求：基礎語音音量為原本的 50%
        float baseVol = (SettingsManager.Instance?.VoiceVolume ?? 1f) * 0.50f;
        if (baseVol <= 0.001f) return;

        // 依語音類型做合適的音量調整：口齒不清、弱氣與耳邊低語更加輕柔小聲
        float volScale = 1f;
        if (relativePath.StartsWith("talk_mumble/"))
        {
            volScale = 0.70f; // 口齒不清、動搖、小聲低語
        }
        else if (relativePath.StartsWith("talk_sweet/"))
        {
            volScale = 0.75f; // 撒嬌溫存
        }
        else if (relativePath.StartsWith("moan_breath/"))
        {
            volScale = 0.75f; // 嬌喘短促氣音
        }
        else if (relativePath.StartsWith("touch_"))
        {
            volScale = 0.80f; // 觸碰嬌哼
        }

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
        _trVoicePlayer.VolumeLinear = baseVol * volScale;
        _trVoicePlayer.Play();

        // 觸發嘴巴說話/開合動畫
        NotifyVoiceSpeak(0.65f);
    }

    /// <summary>隨機挑選一首播放</summary>
    public void PlayTrVoiceRandom(params string[] relativePaths)
    {
        if (relativePaths == null || relativePaths.Length == 0) return;
        string pick = relativePaths[GD.Randi() % relativePaths.Length];
        PlayTrVoice(pick);
    }

    /// <summary>播放短促動態嬌喘氣音（從 50 款真實素材隨機挑選）</summary>
    public void PlayBreathMoan()
    {
        int idx = (int)(GD.Randi() % 50) + 1;
        PlayTrVoice($"moan_breath/breath_{idx}.wav");
    }

    /// <summary>依莉奈所說的台詞內容與當前狀態，自動配對最合適的情景語音</summary>
    public void PlayGirlVoiceForText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 1. 雌小鬼調侃、嘲諷、罵人
        if (text.Contains("雜魚") || text.Contains("弱雞") || text.Contains("哼") || text.Contains("這點本事") ||
            text.Contains("その程度") || text.Contains("笨蛋") || text.Contains("ばーか") || text.Contains("ざこ"))
        {
            PlayTrVoiceRandom(
                "talk_brat/brat_zako_1.wav",
                "talk_brat/brat_zako_2.wav",
                "talk_brat/brat_taunt.wav",
                "talk_brat/brat_baka.wav",
                "talk_brat/brat_kimochi.wav"
            );
            return;
        }

        // 2. 雌小鬼吃癟、動搖被擊倒
        if (text.Contains("唔…！") || text.Contains("ふえ") || text.Contains("やるじゃない"))
        {
            PlayTrVoiceRandom("talk_brat/brat_defeat_1.wav", "talk_brat/brat_defeat_2.wav");
            return;
        }

        // 3. 口齒不清、舌頭打結、微弱不堅定、耳邊低語小聲（らめ、もうだめでしゅ、みないでぇ、やだ等）
        if (text.Contains("不要") || text.Contains("不行") || text.Contains("住手") || text.Contains("慢一點") ||
            text.Contains("別看") || text.Contains("痛") || text.Contains("みないで") || text.Contains("無理") ||
            text.Contains("むり") || text.Contains("だめ") || text.Contains("らめ") || text.Contains("打結") ||
            text.Contains("口齒不清") || text.Contains("放開") || text.Contains("壞心") || text.Contains("討厭") ||
            text.Contains("やだ"))
        {
            PlayTrVoiceRandom(
                "talk_mumble/mumble_rame.wav",
                "talk_mumble/mumble_ramenanoni.wav",
                "talk_mumble/mumble_moudamedeshu.wav",
                "talk_mumble/mumble_minaidea.wav",
                "talk_mumble/mumble_minaideyo.wav",
                "talk_mumble/mumble_kannanomuri.wav",
                "talk_mumble/mumble_sonnanomuri.wav",
                "talk_mumble/mumble_moudame_1.wav",
                "talk_mumble/mumble_moudame_2.wav",
                "talk_mumble/mumble_damea.wav",
                "talk_mumble/mumble_iyanano.wav",
                "talk_mumble/mumble_yada.wav",
                "talk_mumble/mumble_sleep_rame_1.wav",
                "talk_mumble/mumble_sleep_rame_2.wav",
                "talk_mumble/mumble_endure.wav"
            );
            return;
        }

        // 4. 舒服、撒嬌、融化依賴
        if (text.Contains("舒服") || text.Contains("喜歡") || text.Contains("那裡") || text.Contains("抱緊") ||
            text.Contains("身體") || text.Contains("気持ち") || text.Contains("すき") || text.Contains("エッチ") ||
            text.Contains("あは") || text.Contains("好棒") || text.Contains("還要") || text.Contains("再來"))
        {
            PlayTrVoiceRandom(
                "talk_sweet/sweet_kimochi.wav",
                "talk_sweet/sweet_sokosuki_1.wav",
                "talk_sweet/sweet_sokosuki_2.wav",
                "talk_sweet/sweet_koresuki_1.wav",
                "talk_sweet/sweet_koresuki_2.wav",
                "talk_sweet/sweet_gyutte.wav",
                "talk_sweet/sweet_sukisuki.wav",
                "talk_sweet/sweet_ecchi.wav",
                "talk_sweet/sweet_aha.wav",
                "talk_sweet/sweet_sleep_gyutte.wav",
                "talk_sweet/sweet_sleep_soko.wav"
            );
            return;
        }

        // 5. 插入與準備
        if (text.Contains("插") || text.Contains("進去") || text.Contains("裡面") || text.Contains("いれる") ||
            text.Contains("きて") || text.Contains("良いよ"))
        {
            PlayTrVoiceRandom(
                "insert/insert_ask.wav",
                "insert/insert_ready.wav",
                "insert/insert_come.wav"
            );
            return;
        }

        // 6. 高潮絕頂墮落
        if (text.Contains("要到") || text.Contains("要來了") || text.Contains("壞掉") || text.Contains("不行了") ||
            text.Contains("高潮") || text.Contains("墮落") || text.Contains("いっちゃう") || text.Contains("らめぇ") ||
            text.Contains("堕ちちゃう") || text.Contains("いくっ"))
        {
            PlayTrVoiceRandom(
                "climax/climax_fall_1.wav",
                "climax/climax_fall_2.wav",
                "climax/climax_fall_3.wav",
                "climax/climax_iku_1.wav",
                "climax/climax_iku_2.wav",
                "talk_mumble/mumble_mada.wav"
            );
            return;
        }

        // 7. 氣音嬌喘反應（如「嗯…」、「哈啊…」、「唔嗯…」、「呼…」、「啊…♡」）
        if (text.Contains("嗯") || text.Contains("哈") || text.Contains("啊") || text.Contains("呼") || text.Contains("唔") || text.Contains("…"))
        {
            // 高快感值且帶愛心時，有機率穿插耳邊恍惚或口齒不清喘息
            if (ClimaxLevel > 65f && text.Contains("♡") && (GD.Randi() % 100 < 35))
            {
                PlayTrVoiceRandom(
                    "talk_mumble/mumble_mada.wav",
                    "talk_mumble/mumble_moudamedeshu.wav",
                    "talk_sweet/sweet_kimochi.wav"
                );
                return;
            }
            PlayBreathMoan();
            return;
        }

        // 8. 正在被碰觸的部位專屬語音
        var f = _reactFlags;
        if (f.breast) { PlayPartTouchVoice("breast"); return; }
        if (f.nipple) { PlayPartTouchVoice("nipple"); return; }
        if (f.mouth)  { PlayPartTouchVoice("mouth"); return; }
        if (f.pussy)  { PlayPartTouchVoice("pussy"); return; }
        if (f.head)   { PlayPartTouchVoice("head"); return; }
        if (f.thrust) { PlayPartTouchVoice("thrust"); return; }

        // 9. 通用嬌喘反應
        PlayBreathMoan();
    }

    /// <summary>依快感值層級播放對應呼吸嬌喘語音</summary>
    public void PlayArousalMoanVoice()
    {
        PlayBreathMoan();
    }

    /// <summary>播放部位觸控反應專用語音</summary>
    public void PlayPartTouchVoice(string part)
    {
        switch (part)
        {
            case "breast":
                PlayTrVoiceRandom(
                    "touch_breast/breast_touch_1.wav",
                    "touch_breast/breast_touch_2.wav",
                    "touch_breast/breast_touch_3.wav",
                    "touch_breast/breast_touch_4.wav",
                    "touch_breast/breast_touch_5.wav"
                );
                break;
            case "nipple":
                PlayTrVoiceRandom(
                    "touch_breast/breast_touch_2.wav",
                    "touch_breast/breast_touch_4.wav",
                    "talk_sweet/sweet_sokosuki_2.wav"
                );
                break;
            case "mouth":
                PlayTrVoiceRandom(
                    "touch_mouth/mouth_kiss_1.wav",
                    "touch_mouth/mouth_kiss_2.wav",
                    "touch_mouth/mouth_kiss_3.wav",
                    "touch_mouth/sleep_kiss_1.wav",
                    "touch_mouth/sleep_kiss_2.wav",
                    "touch_mouth/sleep_kiss_3.wav"
                );
                break;
            case "pussy":
                PlayTrVoiceRandom(
                    "touch_pussy/pussy_touch_1.wav",
                    "touch_pussy/pussy_touch_2.wav",
                    "touch_pussy/pussy_touch_3.wav",
                    "touch_pussy/pussy_touch_4.wav",
                    "talk_mumble/mumble_rame.wav"
                );
                break;
            case "head":
            case "body":
                PlayTrVoiceRandom(
                    "touch_body/body_touch_1.wav",
                    "touch_body/body_touch_2.wav",
                    "touch_body/sleep_hum_1.wav",
                    "touch_body/sleep_hum_2.wav",
                    "touch_body/sleep_sigh_1.wav",
                    "touch_body/sleep_sigh_2.wav"
                );
                break;
            case "thrust":
                if (GD.Randi() % 2 == 0)
                {
                    PlayTrVoiceRandom("touch/thrust_1.wav", "touch/thrust_2.wav");
                }
                else
                {
                    PlayBreathMoan();
                }
                break;
            default:
                PlayBreathMoan();
                break;
        }
    }

    /// <summary>播放高潮絕頂語音</summary>
    public void PlayTrClimaxVoice()
    {
        PlayTrVoiceRandom(
            "climax/climax_fall_1.wav",
            "climax/climax_fall_2.wav",
            "climax/climax_fall_3.wav",
            "climax/climax_iku_1.wav",
            "climax/climax_iku_2.wav"
        );
    }
}
