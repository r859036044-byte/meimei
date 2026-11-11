using Godot;
using System.Collections.Generic;

/// <summary>
/// TrainingRoomRuntime — 試玩版暫時封鎖的語音（只是「不觸發」，音檔和程式裡的呼叫都還在，沒有刪任何東西）。
///
/// 一鍵換回來：把下面的 <see cref="DemoVoiceBlockOn"/> 改成 false，這些語音就會恢復正常觸發。
/// （清單本身 <see cref="DemoBlockedVoices"/> 保留著當備份，之後要再封鎖，改回 true 就好。）
///
/// 清單的寫法：「資料夾_檔名」。例如 voice 路徑 moan/mid_4.wav → moan_mid_4；
/// 檔名本來就以資料夾名開頭的不重複，例如 thrust/thrust_mid_2.wav → thrust_mid_2。
/// 只有試玩版（DemoMode.Enabled）才會封鎖，完整版不受影響。
/// </summary>
public partial class TrainingRoomRuntime
{
    /// <summary>總開關：true＝試玩版封鎖下面這些語音；false＝全部恢復（一鍵換回來）。</summary>
    public const bool DemoVoiceBlockOn = true;

    static readonly HashSet<string> DemoBlockedVoices = new()
    {
        "moan_mid_4",
        "thrust_mid_2",
        "moan_high_2",
        "moan_high_4",
        "thrust_fast_1",
        "thrust_fast_3",
        "climax_mini_climax_1",
        "climax_fall_2",
        "climax_fall_3",
        "climax_igu_1",
        "insert_pre_ejac_breath",
        "insert_ejac_climax",
        "talk_sweet_sweet_sokosuki_2",
        "touch_breast_breast_touch_1",
        "touch_breast_breast_touch_2",
        "touch_mouth_mouth_kiss_1",
        "touch_mouth_mouth_kiss_2",
    };

    /// <summary>語音路徑（相對 Voice/TR/）轉成清單用的名字：資料夾_檔名，檔名已以資料夾名開頭就不重複。</summary>
    static string VoiceBlockKey(string relativePath)
    {
        string p = relativePath.Replace('\\', '/').TrimStart('/');
        if (p.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase)) p = p[..^4];
        int slash = p.LastIndexOf('/');
        if (slash < 0) return p;
        string folder = p[..slash].Replace('/', '_'), file = p[(slash + 1)..];
        return file.StartsWith(folder + "_") ? file : folder + "_" + file;
    }

    /// <summary>試玩版：這個語音是否被封鎖（不觸發）。</summary>
    static bool VoiceBlockedInDemo(string relativePath)
        => DemoVoiceBlockOn && DemoMode.Enabled && !string.IsNullOrEmpty(relativePath) && DemoBlockedVoices.Contains(VoiceBlockKey(relativePath));
}
