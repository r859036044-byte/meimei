using Godot;
using System;

/// <summary>
/// DL 版開關：雞雞、開（開1／開上）、插入 這幾張圖，改成載入「12px 馬賽克版」。
///
/// 馬賽克版放在 Resources/Sprites/DL/（檔名跟原圖相同，由 Tools/make_dl_mosaic.py 產生）；
/// 原圖完全保留在 Resources/Sprites/，DL 關掉時照舊載入原圖。
///
/// 開啟的條件（任一個）：
///   • 改下面的 DefaultEnabled（true／false）。
///   • 試玩版（DemoMode.Enabled）一律用 DL 版。
///   • 匯出時在 Export Preset → Features → Custom 加上自訂 feature tag「dl」。
///
/// 對應關係（圖層名稱 → 圖檔）：雞雞 → 雞雞.png、開1 → xiaoxuekai.png、開上 → xiaoxuekaishang.png、插入 → xiaoxuekai2.png、
///                              洞 → xiaoxuekaidi.png、m腿/小穴（M 腿小穴）→ 小穴_M.png。
/// 某張馬賽克圖不存在（例如還沒產生／還沒匯入）時，自動退回載入原圖，不會缺圖。
/// </summary>
public static class DlMode
{
    /// <summary>DL 版預設開關：true＝開啟。</summary>
    public const bool DefaultEnabled = false;

    static bool? _cached;

    public static bool Enabled => _cached ??= DefaultEnabled || DemoMode.Enabled || OS.HasFeature("dl");

    // 有馬賽克版的圖（Sprites/ 底下的檔名，不含副檔名）
    static readonly string[] MosaicSprites = { "雞雞", "xiaoxuekai", "xiaoxuekaishang", "xiaoxuekai2", "xiaoxuekaidi", "小穴_M" };

    static bool HasMosaic(string name) =>
        Array.IndexOf(MosaicSprites, name) >= 0 && ResourceLoader.Exists("res://Resources/Sprites/DL/" + name + ".png");

    /// <summary>給 ResourceHelper.LoadTexAny 用的相對路徑（不含副檔名），例如 "Sprites/雞雞"。DL 開啟且有馬賽克版就回傳 "Sprites/DL/雞雞"，否則原樣回傳。</summary>
    public static string Resolve(string relPath)
    {
        if (!Enabled || string.IsNullOrEmpty(relPath) || !relPath.StartsWith("Sprites/")) return relPath;
        string name = relPath.Substring("Sprites/".Length);
        return HasMosaic(name) ? "Sprites/DL/" + name : relPath;
    }

    /// <summary>給直接用 ResourceLoader.Load 的地方用：傳入圖檔名（不含副檔名），回傳完整的 res:// 路徑（.png）。</summary>
    public static string ResolvePath(string baseName)
    {
        if (Enabled && HasMosaic(baseName)) return "res://Resources/Sprites/DL/" + baseName + ".png";
        return "res://Resources/Sprites/" + baseName + ".png";
    }
}
