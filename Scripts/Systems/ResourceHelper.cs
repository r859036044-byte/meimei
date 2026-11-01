using Godot;

/// <summary>
/// 共用資源載入輔助（避免各檔案各自硬編 .png，導致 .jpg/.jpeg/.webp 圖片載入失敗）
/// 對應 Unity 的 Resources.Load&lt;Sprite&gt; 會自動比對副檔名的行為。
/// </summary>
public static class ResourceHelper
{
    /// <summary>
    /// 依 res://Resources/ 為底，嘗試 .png / .jpg / .jpeg / .webp 各副檔名載入貼圖。
    /// relPath 不含副檔名，例如 "Backgrounds/bg_LinaiRoom(day)"。
    /// </summary>
    public static Texture2D LoadTexAny(string relPath)
    {
        if (string.IsNullOrEmpty(relPath)) return null;

        string basePath = "res://Resources/" + relPath;
        string[] exts = { ".png", ".jpg", ".jpeg", ".webp" };

        foreach (var ext in exts)
        {
            string full = basePath + ext;
            if (ResourceLoader.Exists(full))
            {
                var tex = ResourceLoader.Load<Texture2D>(full);
                if (tex != null) return tex;
            }
        }

        GD.PushWarning($"[ResourceHelper] 找不到圖片資源：{relPath}");
        return null;
    }

    /// <summary>
    /// 跟 LoadTexAny 用同一套副檔名探測規則，但改成呼叫 Godot 的「背景執行緒載入」API
    /// （ResourceLoader.LoadThreadedRequest），先在背景執行緒把檔案讀進 Godot 內建的資源
    /// 快取，不會卡住主執行緒、也不用等它完成。之後真正呼叫 LoadTexAny（＝同步的
    /// ResourceLoader.Load）時，只要背景載入已經完成，就會直接命中快取、不用重新讀檔／
    /// 解壓縮——用來提前藏掉「切換場景瞬間，一次同步載入大量貼圖造成長時間黑畫面」的問題
    /// （見 TrainingRoomRuntime.GetPreloadSpritePaths() 的呼叫端說明）。
    /// 純粹是「順便先預熱」的提示，就算沒探測到檔案、或載入還沒完成也不影響後續行為，
    /// 所以這裡不用回傳值、也不處理例外。
    /// </summary>
    public static void PreloadTexAny(string relPath)
    {
        if (string.IsNullOrEmpty(relPath)) return;

        string basePath = "res://Resources/" + relPath;
        string[] exts = { ".png", ".jpg", ".jpeg", ".webp" };

        foreach (var ext in exts)
        {
            string full = basePath + ext;
            if (ResourceLoader.Exists(full))
            {
                // 如果同一個路徑已經在背景載入中或已經載入完成，這裡會回傳非 Ok 的狀態，
                // 直接忽略即可，不影響之後照舊呼叫 LoadTexAny。
                ResourceLoader.LoadThreadedRequest(full);
                return;
            }
        }
    }
}
