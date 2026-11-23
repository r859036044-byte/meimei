using Godot;

/// <summary>
/// MainMenuRuntime — OP 音樂：循環播放＋音量平滑補償。
/// meimei_op.ogg 後半（約 30 秒之後）比前半大聲約 6 dB。這裡走一條專用音軌（bus）加上增益，
/// 依播放位置用折線內插把後半壓回跟前半差不多，變化是連續的，不會突然變小聲。
/// 循環：播完不直接無縫接回開頭（開頭比結尾大聲很多），而是先靜音一小段，再從頭淡入。
/// </summary>
public partial class MainMenuRuntime
{
    const string OpBus = "MenuOpBgm";
    AudioEffectAmplify _opAmp;
    AudioStream _opStream;
    bool _opFinishedHooked;

    [ExportGroup("開場BGM循環")]
    [Export] public float OpLoopGap    = 2f;   // 每次循環回到開頭前，先安靜幾秒
    [Export] public float OpLoopFadeIn = 3f;   // 回到開頭後，音量淡入的秒數

    // (播放秒數, 補償 dB)：依實測響度（30s 起逐漸變大、130s 起最大約 +6 dB）反向補償
    static readonly (float t, float db)[] OpEq =
    {
        (0f, 0f), (20f, 0f), (30f, -1.0f), (40f, -2.5f), (50f, -3.5f), (60f, -4.4f),
        (70f, -4.6f), (90f, -4.8f), (110f, -5.5f), (130f, -6.2f), (200f, -6.2f),
    };

    /// <summary>開場曲載入後呼叫：開啟循環，並掛上補償音軌。</summary>
    void SetupOpBgm(AudioStream clip)
    {
        if (clip == null || _bgmSource == null) return;
        // 不用引擎的無縫循環：改成播完自己接（見 OnOpFinished），才能在開頭前加空白與淡入
        SetStreamLoop(clip, false);
        _opStream = clip;
        if (!_opFinishedHooked)
        {
            _bgmSource.Finished += OnOpFinished;
            _opFinishedHooked = true;
        }

        if (AudioServer.GetBusIndex(OpBus) < 0)
        {
            AudioServer.AddBus();
            int idx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(idx, OpBus);
            AudioServer.SetBusSend(idx, "Master");
            _opAmp = new AudioEffectAmplify();
            AudioServer.AddBusEffect(idx, _opAmp);
        }
        else
        {
            _opAmp = AudioServer.GetBusEffect(AudioServer.GetBusIndex(OpBus), 0) as AudioEffectAmplify;
        }
    }

    static void SetStreamLoop(AudioStream clip, bool loop)
    {
        if (clip is AudioStreamOggVorbis ogg) ogg.Loop = loop;
        else if (clip is AudioStreamMP3 mp3) mp3.Loop = loop;
        else if (clip is AudioStreamWav wav)
            wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
    }

    /// <summary>OP 曲播完：靜音一段時間後，從頭重播並淡入。</summary>
    void OnOpFinished()
    {
        if (_bgmSource == null || !IsInstanceValid(_bgmSource)) return;
        if (_opStream == null || _bgmSource.Stream != _opStream) return;   // 已換成別首就不處理
        TaskLogger.Run(RestartOpLoop(_lifeCts.Token), "MainMenu.OpLoopRestart");
    }

    async System.Threading.Tasks.Task RestartOpLoop(System.Threading.CancellationToken ct)
    {
        _bgmFading = true;                 // 淡入期間不讓 ApplyVolumes 覆蓋音量
        SetVol(_bgmSource, 0f);
        if (OpLoopGap > 0f)
            await ToSignal(GetTree().CreateTimer(OpLoopGap), SceneTreeTimer.SignalName.Timeout);
        if (ct.IsCancellationRequested || _bgmSource == null || !IsInstanceValid(_bgmSource)
            || _bgmSource.Stream != _opStream)
        {
            _bgmFading = false;
            return;
        }
        _bgmSource.Play();
        await FadeInBGM(OpLoopFadeIn, ct);   // 結束時會自己清掉 _bgmFading
    }

    void UpdateOpBgmEq()
    {
        if (_bgmSource == null || !IsInstanceValid(_bgmSource) || _opAmp == null) return;
        bool isOp = _bgmSource.Stream != null && _bgmSource.Stream == _opStream;
        string want = isOp ? OpBus : "Master";
        if (_bgmSource.Bus != want) _bgmSource.Bus = want;
        if (!isOp) return;

        float p = _bgmSource.GetPlaybackPosition();
        float db = OpEq[OpEq.Length - 1].db;
        for (int i = 1; i < OpEq.Length; i++)
        {
            if (p <= OpEq[i].t)
            {
                float k = Mathf.Clamp((p - OpEq[i - 1].t) / Mathf.Max(0.01f, OpEq[i].t - OpEq[i - 1].t), 0f, 1f);
                db = Mathf.Lerp(OpEq[i - 1].db, OpEq[i].db, k);
                break;
            }
        }
        _opAmp.VolumeDb = db;
    }

    void CleanupOpBgmBus()
    {
        int idx = AudioServer.GetBusIndex(OpBus);
        if (idx >= 0) AudioServer.RemoveBus(idx);
        if (_bgmSource != null && IsInstanceValid(_bgmSource))
        {
            _bgmSource.Bus = "Master";
            if (_opFinishedHooked) _bgmSource.Finished -= OnOpFinished;
        }
        _opFinishedHooked = false;
        if (_opStream != null) SetStreamLoop(_opStream, true);   // 還原成一般循環，避免影響別處用到同一個資源
    }
}
