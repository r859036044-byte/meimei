using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// TrainingRoomRuntime — 莉奈（みづきゆうか 聲線）語音與呼吸嬌喘系統。
/// 支援：
///   1. 直接讀取 16-bit PCM WAV 音訊（避開舊版快取雜音並停用背景環境底噪循環）
///   2. 重要事件語音優先鎖定（破處、進入階段三、高潮、液體釋放不被一般氣泡台詞覆蓋）
///   3. 觸碰輕柔（0.60x~0.68x）vs 階段三深入互動強烈（1.15x~1.35x）的明顯音量與情緒對比
///   4. 純嬌喘／叫聲不換嘴開合；只有真正說台詞時才用倒笑壓扁開合嘴型，叫聲很大時用小嘴巴放大 200%
/// </summary>
public partial class TrainingRoomRuntime
{
    AudioStreamPlayer _trVoicePlayer;
    AudioStreamPlayer _trAmbientPlayer;
    readonly Dictionary<string, AudioStream> _voiceCache = new();

    float _idleMoanTimer = -1f;
    float _voicePriorityLockTimer = 0f;
    float _resistVoiceCd = 0f;

    /// <summary>當前是否有語音正在播放</summary>
    public bool IsVoicePlaying => _trVoicePlayer != null && IsInstanceValid(_trVoicePlayer) && _trVoicePlayer.Playing;

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

        // 停用背景連續循環氣音，徹底消除底噪與颯颯雜音
        if (_trAmbientPlayer != null && IsInstanceValid(_trAmbientPlayer) && _trAmbientPlayer.Playing)
        {
            _trAmbientPlayer.Stop();
        }
    }

    /// <summary>直接從標準 16-bit PCM WAV 檔案解析載入 AudioStreamWav，避開過期的 .godot/imported 快取</summary>
    AudioStream LoadWavDirect(string resPath)
    {
        try
        {
            if (FileAccess.FileExists(resPath))
            {
                byte[] bytes = FileAccess.GetFileAsBytes(resPath);
                if (bytes != null && bytes.Length > 44 &&
                    bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F' &&
                    bytes[8] == 'W' && bytes[9] == 'A' && bytes[10] == 'V' && bytes[11] == 'E')
                {
                    int pos = 12;
                    int channels = 1;
                    int sampleRate = 44100;
                    int bitsPerSample = 16;
                    byte[] pcmData = null;

                    while (pos + 8 <= bytes.Length)
                    {
                        string chunkId = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                        int chunkSize = BitConverter.ToInt32(bytes, pos + 4);
                        pos += 8;
                        if (chunkSize < 0 || pos + chunkSize > bytes.Length) break;

                        if (chunkId == "fmt " && chunkSize >= 16)
                        {
                            channels = BitConverter.ToInt16(bytes, pos + 2);
                            sampleRate = BitConverter.ToInt32(bytes, pos + 4);
                            bitsPerSample = BitConverter.ToInt16(bytes, pos + 14);
                        }
                        else if (chunkId == "data")
                        {
                            pcmData = new byte[chunkSize];
                            Buffer.BlockCopy(bytes, pos, pcmData, 0, chunkSize);
                            break;
                        }
                        pos += chunkSize + (chunkSize & 1);
                    }

                    if (pcmData != null && bitsPerSample == 16)
                    {
                        return new AudioStreamWav
                        {
                            Format = AudioStreamWav.FormatEnum.Format16Bits,
                            MixRate = sampleRate,
                            Stereo = (channels == 2),
                            Data = pcmData,
                        };
                    }
                }
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[TRVoice] LoadWavDirect failed for {resPath}: {e.Message}");
        }

        if (ResourceLoader.Exists(resPath))
        {
            return GD.Load<AudioStream>(resPath);
        }
        return null;
    }

    /// <summary>每幀更新語音優先鎖定計時器與待機／階段三隨快感上升的嬌喘</summary>
    public void UpdateTrVoice(float dt)
    {
        InitTrVoice();
        if (_voicePriorityLockTimer > 0f) _voicePriorityLockTimer -= dt;
        if (_resistVoiceCd > 0f) _resistVoiceCd -= dt;
        UpdateVoiceSeq();
        UpdateIdleMoan(dt);
    }

    /// <summary>隨著高潮度增加或抽插時沒有說話的時候的嬌喘聲音</summary>
    void UpdateIdleMoan(float dt)
    {
        if (_voicePriorityLockTimer > 0f) return;
        if (IsVoicePlaying || _isTalking) return;

        bool thrusting = _jijiPhase == 3 && _jijiStage >= 1 && !_s2Paused;
        float c = ClimaxLevel;
        if (c < 15f && !thrusting)
        {
            _idleMoanTimer = -1f;
            return;
        }

        if (_idleMoanTimer < 0f)
        {
            if (thrusting)
            {
                float normSpeed = S2SpeedLvMax > 0f ? Mathf.Clamp(_s2SpeedLv / S2SpeedLvMax, 0f, 1f) : 0f;
                if (normSpeed > 0.7f)
                    _idleMoanTimer = (float)GD.RandRange(0.8, 1.5);
                else if (normSpeed > 0.35f)
                    _idleMoanTimer = (float)GD.RandRange(1.4, 2.4);
                else
                    _idleMoanTimer = (float)GD.RandRange(2.0, 3.2);
            }
            else
            {
                // 隨高潮度提升，待機嬌喘間隔縮短（高潮度越高喘得越頻繁）
                if (c < 40f)
                    _idleMoanTimer = (float)GD.RandRange(8.0, 13.0);
                else if (c < 70f)
                    _idleMoanTimer = (float)GD.RandRange(4.0, 7.0);
                else
                    _idleMoanTimer = (float)GD.RandRange(2.2, 4.0);
            }
        }

        _idleMoanTimer -= dt;
        if (_idleMoanTimer <= 0f)
        {
            _idleMoanTimer = -1f;
            if (thrusting)
            {
                PlayThrustMoanVoice();
            }
            else
            {
                PlayBreathMoan();
            }
        }
    }

    /// <summary>播放高優先級關鍵語音（破處、進入階段三、高潮、內射等），鎖定一段時間不被一般反應覆蓋，並觸發小嘴巴放大 200%</summary>
    public void PlayTrVoicePriority(string relativePath, float lockSeconds = 2.5f, float customVolScale = 1.25f)
    {
        _voicePriorityLockTimer = 0f;
        PlayTrVoice(relativePath, forcePriority: true, customVolScale: customVolScale);
        _voicePriorityLockTimer = lockSeconds;
        TriggerLoudCryMouth(Mathf.Min(lockSeconds, 2.2f));
    }

    /// <summary>播放指定路徑的 TR 語音（相對 res://Resources/Audio/Voice/TR/）</summary>
    public void PlayTrVoice(string relativePath, bool forcePriority = false, float customVolScale = -1f)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        if (!_seqAdvancing) { _voiceSeq.Clear(); _seqCurrent = false; }   // 別的語音插進來就取消依序佇列
        InitTrVoice();

        // 若目前正在播放高優先級語音（如破處、高潮、剛插入），一般語音不可打斷覆蓋
        if (!forcePriority && _voicePriorityLockTimer > 0f && IsVoicePlaying)
        {
            return;
        }

        // 依使用者要求：基礎語音音量為原本的 50%
        float baseVol = (SettingsManager.Instance?.VoiceVolume ?? 1f) * 0.50f;
        if (baseVol <= 0.001f) return;

        // 依語音類型做合適的音量調整：
        // 一般觸碰與耳語較輕柔（0.60x~0.70x），插入／破處／抽插／高潮則明顯更強烈（1.15x~1.30x）
        float volScale = 1f;
        if (customVolScale > 0f)
        {
            volScale = customVolScale;
        }
        else if (relativePath.StartsWith("insert/") || relativePath.StartsWith("climax/"))
        {
            volScale = 1.25f;
        }
        else if (relativePath.StartsWith("thrust/") || relativePath.StartsWith("moan/"))
        {
            volScale = 1.15f;
        }
        else if (relativePath.StartsWith("talk_mumble/"))
        {
            volScale = (_jijiPhase == 3) ? 0.95f : 0.70f;
        }
        else if (relativePath.StartsWith("talk_sweet/"))
        {
            volScale = (_jijiPhase == 3) ? 0.95f : 0.72f;
        }
        else if (relativePath.StartsWith("moan_breath/"))
        {
            volScale = 0.62f; // 待機／輕觸時的短促氣音保持輕柔
        }
        else if (relativePath.StartsWith("touch_"))
        {
            volScale = 0.65f; // 一般觸碰比被插時小聲柔和
        }

        string fullPath = "res://Resources/Audio/Voice/TR/" + relativePath.TrimStart('/');
        if (!_voiceCache.TryGetValue(fullPath, out var stream))
        {
            stream = LoadWavDirect(fullPath);
            if (stream != null) _voiceCache[fullPath] = stream;
        }

        if (stream == null) return;
        _trVoicePlayer.Stream = stream;
        _trVoicePlayer.VolumeLinear = Mathf.Clamp(baseVol * volScale, 0f, 1f);
        _trVoicePlayer.Play();
    }

    /// <summary>隨機挑選一首播放</summary>
    public void PlayTrVoiceRandom(params string[] relativePaths)
    {
        if (relativePaths == null || relativePaths.Length == 0) return;
        string pick = relativePaths[GD.Randi() % relativePaths.Length];
        PlayTrVoice(pick);
    }

    /// <summary>隨機挑選一首以高優先級播放</summary>
    public void PlayTrVoicePriorityRandom(float lockSeconds, float customVolScale, params string[] relativePaths)
    {
        if (relativePaths == null || relativePaths.Length == 0) return;
        string pick = relativePaths[GD.Randi() % relativePaths.Length];
        PlayTrVoicePriority(pick, lockSeconds, customVolScale);
    }

    // ── 指定台詞改用「正常音效」5 個檔，每次隨機播其中一個（只有下面 NormalVoiceLines 列的 9 句；其他台詞照舊）──
    [Export] public bool UseNormalVoiceForLines = true;   // true＝下面指定的台詞播正常音效；false＝全部回到依關鍵字配音的舊規則
    [Export(PropertyHint.Range, "0.1,1.5,0.05")] public float NormalVoiceVolScale = 0.7f;   // 這 5 個音效的音量倍率（跟一般觸碰語音差不多）
    static readonly string[] NormalVoicePaths =
    {
        "normal/breast_touch_5.wav",
        "normal/low_1.wav",
        "normal/moan_breath_breath_1.wav",
        "normal/sleep_kiss_2.wav",
        "normal/touch_body_sleep_sigh_2.wav",
    };

    // 指定改用正常音效的台詞：「什麼都沒做」的正常話前 6 句（BratNormalLines）＋ 摸頭反應通用（ReactHead）3 句
    static readonly string[] NormalVoiceLines =
    {
        "今天辛苦了。", "謝謝你。", "嗯，我知道了。", "要不要一起吃點東西？", "今天天氣不錯呢。", "你今天有好好吃飯嗎？",
        "嗯……", "呼嗯…", "嗯…♪",
    };

    /// <summary>台詞（已加工過：可能被加了愛心或「不要…」）是不是等於 line。愛心不比較，後面接的拒絕詞也算。</summary>
    bool LineMatches(string text, string line)
    {
        static string Strip(string t) => t.Replace("\uFE0F", "").Replace("❤", "").Replace("♡", "").Replace("♥", "").Trim();
        string s = Strip(text), l = Strip(line);
        if (s == l) return true;
        if (s.StartsWith(l, System.StringComparison.Ordinal) && GirlRefuseWords != null)
        {   // 不舒服時台詞後面會被自動接上「不要…」之類的拒絕詞
            string rest = s.Substring(l.Length).Trim();
            foreach (var w in GirlRefuseWords) if (rest == w) return true;
        }
        return false;
    }

    /// <summary>這句台詞是不是指定要播正常音效的那 9 句之一。</summary>
    bool IsNormalVoiceLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var line in NormalVoiceLines) if (LineMatches(text, line)) return true;
        return false;
    }

    // 摸頭反應（ReactHeadNormal）6 句：各自指定音檔；寫多個＝依序輪流播（前一個播完才接下一個）
    static readonly (string line, string[] files)[] HeadPatVoiceMap =
    {
        ("哼～笨蛋雜魚♪",                 new[] { "talk_brat/fun.wav", "talk_brat/brat_baka.wav", "talk_brat/brat_zako_1.wav" }),
        ("才沒有覺得很舒服呢…雜魚♡",       new[] { "talk_brat/fun.wav", "talk_brat/brat_zako_2.wav" }),
        ("嘻嘻…雜魚大哥哥摸得還挺舒服的嘛♪", new[] { "talk_brat/brat_taunt.wav" }),
        ("唔…別把我當小孩子啦！",           new[] { "normal/low_1.wav" }),
        ("哼…笨蛋…♪",                    new[] { "talk_brat/fun.wav", "talk_brat/brat_baka.wav" }),
        ("再多摸一下也可以喔…♡",           new[] { "talk_brat/brat_zako_1.wav" }),
    };

    // 摸頭台詞（PatTalk，沒插入時 PatGirlLines）5 句：各自指定音檔（寫多個＝依序輪流播）
    // 注意：「才沒有覺得很舒服呢…雜魚♡」這句在上面的摸頭反應也有，兩邊音檔不同，所以分兩張表、由各自的呼叫端查。
    static readonly (string line, string[] files)[] PatTalkVoiceMap =
    {
        ("哼～別以為摸摸頭我就會聽話喔…笨蛋雜魚♡", new[] { "talk_brat/fun.wav", "talk_brat/brat_baka.wav", "talk_brat/brat_zako_1.wav" }),
        ("才沒有覺得很舒服呢…雜魚♡",             new[] { "talk_brat/brat_zako_2.wav" }),
        ("嘻嘻…再多摸一下也不是不行啦♪",          new[] { "talk_brat/brat_zako_1.wav" }),
        ("唔…別把我當小孩子啦！",                 new[] { "talk_brat/fun.wav" }),
        ("笨蛋雜魚…♪",                        new[] { "talk_brat/brat_baka.wav", "talk_brat/brat_zako_2.wav" }),
    };

    bool PlayMappedVoice(string text, (string line, string[] files)[] map)
    {
        foreach (var (line, files) in map)
        {
            if (!LineMatches(text, line)) continue;
            if (files.Length == 1 && files[0].StartsWith("normal/")) PlayTrVoice(files[0], false, NormalVoiceVolScale);
            else PlayTrVoiceSeq(files);
            return true;
        }
        return false;
    }

    bool TryPlayHeadPatVoice(string text) => PlayMappedVoice(text, HeadPatVoiceMap);

    /// <summary>摸頭台詞（PatTalk）：有指定音檔就播並回傳 true；沒指定回傳 false（呼叫端用原本的摸頭語音）。</summary>
    public bool TryPlayPatTalkVoice(string text) => !string.IsNullOrEmpty(text) && PlayMappedVoice(text, PatTalkVoiceMap);

    /// <summary>從正常音效 5 個檔隨機挑一個播（高優先語音正在播時不蓋掉，由呼叫端先判斷）。</summary>
    void PlayNormalLineVoice()
    {
        string pick = NormalVoicePaths[(int)(GD.Randi() % (uint)NormalVoicePaths.Length)];
        PlayTrVoice(pick, false, NormalVoiceVolScale);
    }

    static readonly int[] BreathIds = { 1,2,3,4,5,6,7,8,9, 11,12, 15, 21,22,23,24,25,26, 30,31, 33, 35,36,37,38, 41,42,43,44,45,46,47,48,49,50 };

    // ── 無觸碰固定語音：依序播放（前一個播完才接下一個，不再隨機）──
    readonly System.Collections.Generic.Queue<string> _voiceSeq = new();
    bool _seqCurrent;      // 目前播的是依序語音
    bool _seqAdvancing;    // 正在由佇列接下一個（不要清佇列）

    /// <summary>依序播放指定語音（同一句內前一個播完才接下一個）；新台詞出現時立刻中斷上一輪、直接播最新的。</summary>
    public void PlayTrVoiceSeq(params string[] paths)
    {
        if (paths == null || paths.Length == 0) return;
        _voiceSeq.Clear();
        PlayTrVoice(paths[0], forcePriority: true);
        _seqCurrent = true;
        for (int i = 1; i < paths.Length; i++) _voiceSeq.Enqueue(paths[i]);
    }

    void UpdateVoiceSeq()
    {
        if (_voiceSeq.Count == 0 || IsVoicePlaying) return;
        _seqAdvancing = true;
        PlayTrVoice(_voiceSeq.Dequeue(), forcePriority: true);
        _seqAdvancing = false;
        _seqCurrent = true;
    }

    /// <summary>無觸碰時的固定語音（台詞→音檔）。回傳 true＝有處理。</summary>
    bool TryPlayIdleFixedVoice(string text)
    {
        const string F = "talk_brat/fun.wav", S = "talk_brat/sono_teido.wav";
        const string Z1 = "talk_brat/brat_zako_1.wav", Z2 = "talk_brat/brat_zako_2.wav";
        const string B = "talk_brat/brat_baka.wav", T = "talk_brat/brat_taunt.wav";
        if (text.Contains("雜魚好沒用"))      { PlayTrVoiceSeq(T); return true; }
        if (text.Contains("雜魚也敢命令我"))  { PlayTrVoiceSeq(F, S); return true; }
        if (text.Contains("才不聽你的"))      { PlayTrVoiceSeq(F, B); return true; }
        if (text.Contains("笨蛋笨蛋"))        { PlayTrVoiceSeq(B); return true; }
        if (text.Contains("弱雞"))            { PlayTrVoiceSeq(Z2); return true; }
        if (text.Contains("哼") && text.Contains("雜魚")) { PlayTrVoiceSeq(F, Z1); return true; }
        if (text.Contains("等好久"))          { PlayTrVoiceSeq(F); return true; }
        return false;
    }


    /// <summary>播放短促動態嬌喘氣音（純氣音不換嘴開合）</summary>
    public void PlayBreathMoan()
    {
        // 只用指定編號：1~9、11~12、15、21~26、30~31、33、35~38、41~50
        int idx = BreathIds[(int)(GD.Randi() % (uint)BreathIds.Length)];
        PlayTrVoice($"moan_breath/breath_{idx}.wav");
    }

    /// <summary>播放中間高潮／小高潮語音（叫聲較大：小嘴巴放大 200%）</summary>
    public void PlayTrMiniClimaxVoice()
    {
        PlayTrVoicePriorityRandom(2.0f, 1.20f,
            "climax/mini_climax_1.wav",
            "climax/mini_climax_2.wav",
            "climax/mini_climax_3.wav",
            "climax/mini_climax_4.wav",
            "climax/mini_climax_5.wav"
        );
        TriggerLoudCryMouth(1.8f);
    }

    /// <summary>播放大高潮絕頂語音（叫聲很大：小嘴巴放大 200%）</summary>
    public void PlayTrClimaxVoice()
    {
        PlayTrVoicePriorityRandom(3.2f, 1.30f,
            "climax/climax_long_1.wav",
            "climax/climax_igu_1.wav",
            "climax/climax_fall_2.wav",
            "climax/climax_fall_3.wav"
        );
        TriggerLoudCryMouth(2.5f);
    }

    /// <summary>當開始進入階段三（抵住入口／準備突破）時的立即語音與氣泡反應</summary>
    public void PlayInsertionStartVoice()
    {
        bool wetEnough = (_jijiAnus ? _anusWet : Wetness) >= BreakWetNeed;
        if (wetEnough)
        {
            PlayTrVoicePriorityRandom(1.8f, 1.15f,
                "talk_mumble/mumble_rame.wav",
                "talk_mumble/mumble_endure.wav"
            );
            ShowBubble(ref _bubbleGirl, ref _bubbleGirlLbl, "啊…要進來了…！", false);
        }
        else
        {
            PlayTrVoicePriorityRandom(1.8f, 1.15f,
                "insert/break_dry.wav",
                "talk_mumble/mumble_endure.wav",
                "talk_mumble/mumble_rame.wav"
            );
            ShowBubble(ref _bubbleGirl, ref _bubbleGirlLbl, "唔…那裡還…！", false);
        }
        TriggerLoudCryMouth(1.5f);
    }

    /// <summary>當頂住入口用力推擠（_resistBlocking）時的反應語音</summary>
    public void PlayInsertionResistVoice()
    {
        if (_resistVoiceCd > 0f || _voicePriorityLockTimer > 0f) return;
        _resistVoiceCd = 1.6f;
        PlayTrVoicePriorityRandom(1.4f, 1.15f,
            "insert/break_dry.wav",
            "talk_mumble/mumble_endure.wav",
            "moan/mid_1.wav",
            "moan/mid_2.wav"
        );
        TriggerLoudCryMouth(1.2f);
    }

    /// <summary>依莉奈所說的台詞內容與當前狀態，自動配對最合適的情景語音；僅在真正說台詞（非純嬌喘叫聲）時才開合嘴巴</summary>
    public void PlayGirlVoiceForText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 通知嘴巴：內部會自動檢查是否為純叫聲（IsSoundOnly）或教程，只有真正台詞才會用倒笑壓扁開合嘴巴
        NotifyVoiceSpeakForText(text);

        // 若高優先級語音正在播放（如破處、高潮、內射），不覆蓋音訊
        if (_voicePriorityLockTimer > 0f && IsVoicePlaying)
        {
            return;
        }

        // 摸頭反應 6 句：各自指定的音檔（見 HeadPatVoiceMap）
        if (TryPlayHeadPatVoice(text)) return;

        // 指定的 9 句台詞：用「正常音效」5 個檔隨機播一個（其他台詞、或 UseNormalVoiceForLines 關掉，走下面原本的關鍵字規則）
        if (UseNormalVoiceForLines && IsNormalVoiceLine(text))
        {
            PlayNormalLineVoice();
            return;
        }

        // 0. 若目前正處於插入／抽插階段（_jijiPhase == 3），優先給予強烈的階段三專屬反應！
        if (_jijiPhase == 3)
        {
            // 高潮或接近高潮台詞
            if (text.Contains("要到") || text.Contains("要來了") || text.Contains("壞掉") || text.Contains("不行了") ||
                text.Contains("高潮") || text.Contains("墮落") || text.Contains("いっちゃう") || text.Contains("らめぇ") ||
                text.Contains("いくっ") || text.Contains("～～♡"))
            {
                PlayTrClimaxVoice();
                return;
            }

            // 口齒不清／承受不住的求饒台詞
            if (text.Contains("不要") || text.Contains("不行") || text.Contains("住手") || text.Contains("慢一點") ||
                text.Contains("痛") || text.Contains("だめ") || text.Contains("らめ") || text.Contains("無理"))
            {
                PlayTrVoiceRandom(
                    "talk_mumble/mumble_rame.wav",
                    "talk_mumble/mumble_moudamedeshu.wav",
                    "talk_mumble/mumble_endure.wav"
                );
                return;
            }

            // 舒服／喜歡台詞
            if (text.Contains("舒服") || text.Contains("喜歡") || text.Contains("那裡") || text.Contains("好棒") ||
                text.Contains("還要") || text.Contains("気持ち") || text.Contains("すき"))
            {
                PlayTrVoiceRandom(
                    "talk_sweet/sweet_kimochi.wav",
                    "talk_sweet/sweet_sokosuki_2.wav",
                    "talk_sweet/sweet_koresuki_1.wav"
                );
                return;
            }

            // 抽插進行中（Stage >= 1）：一律播放依速度分級的激烈本番嬌喘
            if (_jijiStage >= 1)
            {
                PlayThrustMoanVoice();
                return;
            }
            else
            {
                PlayInsertionResistVoice();
                return;
            }
        }

        // 0.5 無觸碰（沒有任何部位被碰）：指定語音、依序播放
        {
            var ff = _reactFlags;
            bool anyTouch = ff.breast || ff.nipple || ff.mouth || ff.head || ff.pussy || ff.thrust;
            if (!anyTouch && TryPlayIdleFixedVoice(text)) return;
        }

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
            PlayTrClimaxVoice();
            return;
        }

        // 7. 正在被碰觸的部位專屬語音（優先於通用氣音判斷，確保各部位音效正確觸發）
        var f = _reactFlags;
        if (f.thrust) { PlayPartTouchVoice("thrust"); return; }
        if (f.pussy)  { PlayPartTouchVoice("pussy"); return; }
        if (f.nipple) { PlayPartTouchVoice("nipple"); return; }
        if (f.breast) { PlayPartTouchVoice("breast"); return; }
        if (f.mouth)  { PlayPartTouchVoice("mouth"); return; }
        if (f.head)   { PlayPartTouchVoice("head"); return; }

        // 8. 氣音嬌喘反應（如「嗯…」、「哈啊…」、「唔嗯…」、「呼…」、「啊…♡」）
        if (text.Contains("嗯") || text.Contains("哈") || text.Contains("啊") || text.Contains("呼") || text.Contains("唔") || text.Contains("…"))
        {
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

        // 9. 通用嬌喘反應
        PlayBreathMoan();
    }

    /// <summary>依快感值層級播放對應呼吸嬌喘語音</summary>
    public void PlayArousalMoanVoice()
    {
        PlayBreathMoan();
    }

    /// <summary>播放部位觸控反應專用語音（純觸碰嬌哼不換嘴開合）</summary>
    public void PlayPartTouchVoice(string part)
    {
        switch (part)
        {
            case "breast":
                PlayTrVoiceRandom(
                    "touch_breast/breast_touch_1.wav",
                    "touch_breast/breast_touch_4.wav",
                    "touch_breast/breast_touch_5.wav"
                );
                break;
            case "nipple":
                PlayTrVoiceRandom(
                    "touch_breast/breast_touch_1.wav",
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
                    "touch_pussy/pussy_touch_3.wav",
                    "touch_pussy/pussy_touch_4.wav",
                    "talk_mumble/mumble_rame.wav"
                );
                break;
            case "head":
                if (_jijiPhase == 3)
                {
                    PlayTrVoiceRandom(
                        "touch_body/sleep_sigh_1.wav",
                        "touch_body/sleep_sigh_2.wav",
                        "talk_sweet/sweet_gyutte.wav"
                    );
                }
                else
                {
                    PlayTrVoiceRandom(
                        "talk_brat/brat_zako_1.wav",
                        "talk_brat/brat_zako_2.wav",
                        "talk_brat/brat_taunt.wav",
                        "talk_sweet/sweet_aha.wav"
                    );
                }
                break;
            case "body":
                PlayTrVoiceRandom(
                    "touch_body/body_touch_1.wav",
                    "touch_body/body_touch_2.wav",
                    "touch_body/sleep_hum_1.wav",
                    "touch_body/sleep_hum_2.wav"
                );
                break;
            case "thrust":
                PlayThrustMoanVoice();
                break;
            default:
                PlayBreathMoan();
                break;
        }
    }

    /// <summary>依抽插速度播放純嬌喘喘息（純嬌喘不換成倒笑碎動；高速激烈嬌喘叫聲很大時將小嘴巴放大 200%）</summary>
    public void PlayThrustMoanVoice()
    {
        float normSpeed = S2SpeedLvMax > 0f ? Mathf.Clamp(_s2SpeedLv / S2SpeedLvMax, 0f, 1f) : 0f;
        if (normSpeed < 0.35f)
        {
            PlayTrVoiceRandom(
                "thrust/thrust_slow_1.wav",
                "thrust/thrust_slow_2.wav",
                "thrust/thrust_slow_3.wav",
                "moan/mid_1.wav",
                "moan/mid_2.wav"
            );
        }
        else if (normSpeed < 0.7f)
        {
            PlayTrVoiceRandom(
                "thrust/thrust_mid_1.wav",
                "thrust/thrust_mid_2.wav",
                "thrust/thrust_mid_3.wav",
                "moan/mid_3.wav",
                "moan/mid_4.wav"
            );
        }
        else
        {
            PlayTrVoiceRandom(
                "thrust/thrust_fast_1.wav",
                "thrust/thrust_fast_2.wav",
                "thrust/thrust_fast_3.wav",
                "moan/high_1.wav",
                "moan/high_2.wav",
                "moan/high_4.wav"
            );
            // 叫聲很大：小嘴巴放大 200%
            TriggerLoudCryMouth(1.4f);
        }
    }

    /// <summary>莉奈被射時的台詞反應：高潮時「要、要去了～！！♡」，沒高潮時「不、不行啊～！！」</summary>
    public void PlayRinaEjacReaction()
    {
        bool rinaClimax = _spasmsRunning || ClimaxLevel >= 50f;
        if (rinaClimax)
        {
            if (ClimaxLevel >= 50f && !_spasmsRunning)
            {
                TriggerClimax();
            }
            PlayTrVoicePriority("insert/ejac_climax.wav", 3.5f, 1.35f);
            ShowBubble(ref _bubbleGirl, ref _bubbleGirlLbl, "要、要去了～！！♡", false);
            TriggerLoudCryMouth(3.0f);
        }
        else
        {
            PlayTrVoicePriority("insert/ejac_normal.wav", 3.5f, 1.35f);
            ShowBubble(ref _bubbleGirl, ref _bubbleGirlLbl, "不、不行啊～！！", false);
            TriggerLoudCryMouth(3.0f);
        }
    }
}
