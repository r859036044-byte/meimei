using Godot;

/// <summary>
/// 進調教室前的背景預讀（只做一次）：立繪／背景貼圖、調教室場景檔、夜晚 BGM、常用的小語音資料夾。
/// 在標題畫面一開始（吉太 LOGO 亮的那段等待時間）就呼叫，進房間的黑屏才會短。
/// 進房間時 _Ready 會印 [TRLoad] 各階段毫秒數（Build／整個 _Ready），哪段慢看輸出就知道。
/// </summary>
public partial class TrainingRoomRuntime
{
    static bool _warmed;
    static readonly string[] WarmVoiceDirs = { "talk", "talk_brat", "talk_sweet", "talk_mumble", "normal", "pain", "touch_body", "touch_breast", "touch_mouth", "touch_pussy", "thrust", "wet", "insert", "moan" };

    public static void WarmUp()
    {
        if (_warmed) return;
        _warmed = true;
        foreach (var rel in GetPreloadSpritePaths()) ResourceHelper.PreloadTexAny(rel);
        ResourceLoader.LoadThreadedRequest("res://Scenes/TrainingRoom.tscn");
        ResourceLoader.LoadThreadedRequest("res://Resources/Audio/bgm/" + SceneBgm.Night + ".mp3");
        foreach (var d in WarmVoiceDirs)
        {
            string dir = "res://Resources/Audio/Voice/TR/" + d;
            foreach (var f in DirAccess.GetFilesAt(dir))
            {
                string name = f.Replace(".import", "").Replace(".remap", "");
                if (!(name.EndsWith(".wav") || name.EndsWith(".ogg") || name.EndsWith(".mp3"))) continue;
                ResourceLoader.LoadThreadedRequest(dir + "/" + name);
            }
        }
        GD.Print("[TRLoad] 背景預讀已送出");
    }
}
