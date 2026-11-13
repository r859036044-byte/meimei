using Godot;
using System.Collections.Generic;

/// <summary>
/// 圖鑒資料管理器（Autoload 單例）
/// 負責記錄已解鎖的結局、CG、分支劇情、故事節點，並持久化至 user://gallery.json
///
/// 【開發者全解鎖開關】
/// DebugUnlockAllOverride 是一個純顯示層的開關，只會影響
/// IsEndingUnlocked / IsCGUnlocked / IsBranchUnlocked / IsStoryNodeUnlocked 的回傳值，
/// 完全不會寫入或更動底層存檔資料。
/// </summary>
public partial class GalleryDataManager : Node
{
    public static GalleryDataManager Instance { get; private set; }

    // 結局 [1..19]，index 0 不使用
    private readonly bool[] _endings = new bool[20];

    // CG 解鎖（以資源 id 為 key）
    private readonly HashSet<string> _cgs = new();

    // 分支解鎖（以分支 id 為 key，night1_idle 等語意 id）
    private readonly HashSet<string> _branches = new();

    // 故事樹節點解鎖（以 "Nxxx" 為 key）
    private readonly HashSet<string> _storyNodes = new();

    // 全年齡版結局 [1..AAEndingCount]（與 R18 的 18 個結局完全分開記錄）
    public const int AAEndingCount = 7;
    private readonly bool[] _aaEndings = new bool[AAEndingCount + 1];

    /// <summary>目前是否為全年齡模式（設定裡的全年齡開關）。圖鑒、分支、樹狀圖依此切換</summary>
    public static bool AllAgesMode => !(PlayerDataManager.Instance?.R18ContentEnabled ?? false);

    private const string FilePath = "user://gallery.json";

    /// <summary>
    /// 開發者工具「全解鎖模式」開關（僅供除錯測試用，不影響真實存檔）。
    /// </summary>
    public bool DebugUnlockAllOverride { get; set; } = true;

    public override void _Ready()
    {
        Instance = this;
        Load();
    }

    // ─── 查詢（會受開發者全解鎖開關影響）───────────────────────
    public bool IsEndingUnlocked(int n)
        => DebugUnlockAllOverride || (n is >= 1 and <= 19 && _endings[n]);

    public bool IsAAEndingUnlocked(int n)
        => DebugUnlockAllOverride || (n >= 1 && n <= AAEndingCount && _aaEndings[n]);

    public bool IsCGUnlocked(string id)
        => DebugUnlockAllOverride || _cgs.Contains(id);

    public bool IsBranchUnlocked(string id)
        => DebugUnlockAllOverride || _branches.Contains(id);

    public bool IsStoryNodeUnlocked(string id)
        => DebugUnlockAllOverride || _storyNodes.Contains(id);

    // ─── 解鎖（玩家真實進度，會寫入存檔）────────────────────────
    public void UnlockEnding(int n)
    {
        if (n < 1 || n > 19 || _endings[n]) return;
        _endings[n] = true;
        Save();
    }

    public void UnlockAAEnding(int n)
    {
        if (n < 1 || n > AAEndingCount || _aaEndings[n]) return;
        _aaEndings[n] = true;
        Save();
    }

    public void LockAAEnding(int n)
    {
        if (n < 1 || n > AAEndingCount || !_aaEndings[n]) return;
        _aaEndings[n] = false;
        Save();
    }

    /// <summary>
    /// 劇情變數被設定時呼叫（PlayerDataManager.AddDialogueVar）：
    /// 全年齡的選擇（第1晚／第2晚 蓋不蓋被子）順便解鎖對應的分支與樹狀圖節點
    /// </summary>
    public void OnDialogueVar(string name)
    {
        var (branch, nodes) = StoryTreeData.AAUnlocksFor(name);
        if (branch != null) UnlockBranch(branch);
        if (nodes != null) foreach (var n in nodes) UnlockStoryNode(n);
    }

    public void UnlockCG(string id)
    {
        if (_cgs.Add(id)) Save();
    }

    public void UnlockBranch(string id)
    {
        if (_branches.Add(id)) Save();
    }

    public void UnlockStoryNode(string id)
    {
        if (_storyNodes.Add(id)) Save();
    }

    // ─── 鎖定（僅供開發者工具使用）──────────────────────────────
    public void LockEnding(int n)
    {
        if (n < 1 || n > 19 || !_endings[n]) return;
        _endings[n] = false;
        Save();
    }

    public void LockStoryNode(string id)
    {
        if (_storyNodes.Remove(id)) Save();
    }

    // ─── 儲存 ───────────────────────────────────────────────────
    void Save()
    {
        var d = new Godot.Collections.Dictionary();

        var ea = new Godot.Collections.Array();
        for (int i = 1; i <= 19; i++) if (_endings[i]) ea.Add(i);
        d["endings"] = ea;

        var aa = new Godot.Collections.Array();
        for (int i = 1; i <= AAEndingCount; i++) if (_aaEndings[i]) aa.Add(i);
        d["aaEndings"] = aa;

        var ca = new Godot.Collections.Array();
        foreach (var c in _cgs) ca.Add(c);
        d["cgs"] = ca;

        var ba = new Godot.Collections.Array();
        foreach (var b in _branches) ba.Add(b);
        d["branches"] = ba;

        var sna = new Godot.Collections.Array();
        foreach (var s in _storyNodes) sna.Add(s);
        d["storyNodes"] = sna;

        using var f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
        f?.StoreString(Json.Stringify(d));
    }

    // ─── 讀取 ───────────────────────────────────────────────────
    void Load()
    {
        if (!FileAccess.FileExists(FilePath)) return;
        using var f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read);
        if (f == null) return;

        var parsed = Json.ParseString(f.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary) return;
        var d = parsed.AsGodotDictionary();

        if (d.TryGetValue("endings", out var ev) && ev.VariantType == Variant.Type.Array)
            foreach (var e in ev.AsGodotArray())
            {
                int n = e.AsInt32();
                if (n is >= 1 and <= 19) _endings[n] = true;
            }

        if (d.TryGetValue("aaEndings", out var av) && av.VariantType == Variant.Type.Array)
            foreach (var e in av.AsGodotArray())
            {
                int n = e.AsInt32();
                if (n >= 1 && n <= AAEndingCount) _aaEndings[n] = true;
            }

        if (d.TryGetValue("cgs", out var cv) && cv.VariantType == Variant.Type.Array)
            foreach (var c in cv.AsGodotArray()) _cgs.Add(c.AsString());

        if (d.TryGetValue("branches", out var bv) && bv.VariantType == Variant.Type.Array)
            foreach (var b in bv.AsGodotArray()) _branches.Add(b.AsString());

        if (d.TryGetValue("storyNodes", out var snv) && snv.VariantType == Variant.Type.Array)
            foreach (var s in snv.AsGodotArray()) _storyNodes.Add(s.AsString());
    }
}
