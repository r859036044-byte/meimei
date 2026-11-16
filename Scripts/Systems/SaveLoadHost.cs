using Godot;
using System.Collections.Generic;

/// <summary>
/// 通用存讀檔入口：沒有自己存檔按鈕的場景（調教室、客廳等）由 NovelReader 的「☰ 選單」呼叫。
/// 存：場景檔名 + 玩家進度（夜晚、今日行為旗標、劇情變數、調教室被子狀態）。
/// 讀：還原進度後重新載入該場景（調教室會用還原的心情值／雌小鬼值接著玩）。
/// 序章（主選單）有自己的逐行存讀檔，不走這裡。
/// </summary>
public static class SaveLoadHost
{
    /// <summary>讀檔後要套用到調教室的被子狀態（-1＝沒有）。TrainingRoomRuntime 進場時消費。</summary>
    public static int PendingDuvetState = -1;

    public static void OpenSave(SceneTree tree) => Open(tree, true);
    public static void OpenLoad(SceneTree tree) => Open(tree, false);

    static void Open(SceneTree tree, bool save)
    {
        var scene = tree?.CurrentScene;
        if (scene == null) return;

        var ctrl = scene.GetNodeOrNull<SaveLoadController>("SaveLoadController");
        if (ctrl == null || !GodotObject.IsInstanceValid(ctrl))
        {
            ctrl = new SaveLoadController { Name = "SaveLoadController" };
            scene.AddChild(ctrl);
        }

        // 回調一律在開啟前重新指定（靜態欄位可能還留著別的場景的舊 lambda）
        SaveLoadController.OnGetSaveData = () => Capture(tree);
        SaveLoadController.OnApplyLoadData = sd => Apply(tree, sd);

        if (save) ctrl.CaptureAndOpen();
        else ctrl.Open(SaveLoadController.Mode.Load);
    }

    static SaveLoadController.SaveData Capture(SceneTree tree)
    {
        var pdm = PlayerDataManager.Instance;
        var scene = tree.CurrentScene;
        var sd = new SaveLoadController.SaveData
        {
            sceneName = SceneKey(scene),
            lineIndex = pdm?.CurrentNight ?? 0,
        };
        var extra = new Dictionary<string, int>();
        if (pdm != null)
        {
            extra["night"] = pdm.CurrentNight;
            extra["idleNightCount"] = pdm.IdleNightCount;
            extra["didNothing"] = pdm.TodayDidNothing ? 1 : 0;
            extra["touchedOrgasm"] = pdm.TodayTouchedOrgasm ? 1 : 0;
            extra["insertOrgasm"] = pdm.TodayInsertOrgasm ? 1 : 0;
            extra["insertNoOrg"] = pdm.TodayInsertNoOrg ? 1 : 0;
            extra["inserted"] = pdm.TodayInserted ? 1 : 0;
        }
        var troom = FindNode<TrainingRoomRuntime>(scene);
        if (troom != null)
        {
            troom.BratFlush();   // 先把房間內的心情／雌小鬼值寫進劇情變數（SaveToSlot 之後才會複製變數）
            extra["duvet"] = troom.DuvetState;
        }
        sd.extra = extra;
        return sd;
    }

    static void Apply(SceneTree tree, SaveLoadController.SaveData sd)
    {
        if (sd == null || string.IsNullOrEmpty(sd.sceneName)) return;
        var pdm = PlayerDataManager.Instance;
        if (pdm != null && sd.extra != null)
        {
            int Get(string k, int d = 0) => sd.extra.TryGetValue(k, out var v) ? v : d;
            if (sd.extra.ContainsKey("night")) pdm.CurrentNight = Get("night");
            pdm.IdleNightCount = Get("idleNightCount");
            pdm.TodayDidNothing = Get("didNothing", 1) != 0;
            pdm.TodayTouchedOrgasm = Get("touchedOrgasm") != 0;
            pdm.TodayInsertOrgasm = Get("insertOrgasm") != 0;
            pdm.TodayInsertNoOrg = Get("insertNoOrg") != 0;
            pdm.TodayInserted = Get("inserted") != 0;
            PendingDuvetState = sd.extra.TryGetValue("duvet", out var dv) ? dv : -1;
            // 在房間內存的第 2 晚存檔：進房時的第 2 晚心情／雌小鬼加減早就套用過、而且已包含在存檔的數值裡。
            // 補上「已套用」標記，避免讀檔重新載入調教室時 ApplyAANight2Bias 再加減一次（+20／−25 無限疊加）。
            if (sd.extra.ContainsKey("duvet") && Get("night") == 2 && pdm.GetDialogueVar("aa_n2_bias") == 0)
                pdm.AddDialogueVar("aa_n2_bias", 1);
        }
        string path = sd.sceneName.EndsWith(".tscn") ? sd.sceneName : $"res://Scenes/{sd.sceneName}.tscn";
        tree.ChangeSceneToFile(path);
    }

    /// <summary>目前場景的檔名（不含副檔名），例如 TrainingRoom。</summary>
    static string SceneKey(Node scene)
    {
        string p = scene?.SceneFilePath;
        if (!string.IsNullOrEmpty(p)) return System.IO.Path.GetFileNameWithoutExtension(p);
        return scene?.Name ?? "";
    }

    static T FindNode<T>(Node n) where T : Node
    {
        if (n == null) return null;
        if (n is T t) return t;
        foreach (Node c in n.GetChildren())
        {
            var r = FindNode<T>(c);
            if (r != null) return r;
        }
        return null;
    }
}
