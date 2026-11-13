using System.Collections.Generic;

/// <summary>
/// 故事樹結構資料（自動從劇情工房匯出）
/// </summary>
public static class StoryTreeData
{
    public enum NodeType { Story, Choice, Ending, Challenge, Route, Incomplete }

    public readonly struct StoryEdge
    {
        public readonly string Label;
        public readonly string To;
        public readonly bool Shared;
        public StoryEdge(string label, string to, bool shared = false)
            => (Label, To, Shared) = (label, to, shared);
    }

    public sealed class StoryNode
    {
        public string Id;
        public string Label;
        public NodeType Type;
        public string Time;
        public string File;       // null = 無對話檔
        public int EndingNum;     // 0 = 非結局, -1 = 無編號結局
        public string EndingLabel; // e.g. "結局11b"
        public string Preview;
    }

    public const string TreeRoot = "N008";

    // ═══════════════════════════════════════════════════════════
    //  依全年齡開關切換的「目前」資料（圖鑒／樹狀圖一律用這三個）
    //  全年齡開啟 → AA*；關閉（R18）→ 原本的 Nodes / Edges / TreeRoot，完全不動
    // ═══════════════════════════════════════════════════════════
    public static Dictionary<string, StoryNode> CurNodes => GalleryDataManager.AllAgesMode ? AANodes : Nodes;
    public static Dictionary<string, List<StoryEdge>> CurEdges => GalleryDataManager.AllAgesMode ? AAEdges : Edges;
    public static string CurRoot => GalleryDataManager.AllAgesMode ? AATreeRoot : TreeRoot;

    // ═══════════════════════════════════════════════════════════
    //  全年齡版（5 個結局）
    //  圖鑒顯示的結局名稱就寫在下面 AAEndings 裡（和劇本 JSON 的標題分開）；
    //  Day3_Start.json 結局標題行的 "endNo" 對應這裡的編號
    // ═══════════════════════════════════════════════════════════
    public readonly struct AAEnding
    {
        public readonly string Name, Desc, Day1Var, Day2Var;
        public readonly int Pick; // 第3天深夜選擇框要選第幾個（-1 = 沒有選擇）
        public AAEnding(string name, string desc, string d1, string d2, int pick)
            => (Name, Desc, Day1Var, Day2Var, Pick) = (name, desc, d1, d2, pick);
    }

    // ═══════════════════════════════════════════════════════════
    //  全年齡結局的數值門檻（心情 Mood／雌小鬼值 Brat，0~100）
    //  蓋被子路徑決定「想走哪個結局」，數值決定「達不達標」；沒達標就降級，直到兜底（結局1／2）
    //  之前的版本備份在專案根目錄 backup_before_ending_stats/
    // ═══════════════════════════════════════════════════════════
    public const float E5MoodMin = 75f, E5BratMax = 25f;                  // 結局5 兄妹專屬散步（蓋→蓋）：心情很高、雌小鬼很低
    public const float E3MoodMin = 40f, E3MoodMax = 70f, E3BratMin = 55f; // 結局3 課後輔導（不做→蓋）：心情中等、雌小鬼偏高
    public const float E4MoodMax = 30f, E4BratMin = 35f, E4BratMax = 65f; // 結局4 無聲的拒絕（蓋→不做）：心情很低、雌小鬼中等
    // 結局1／2（不做→不做）：理想是心情 ≤ 15、雌小鬼值 ≥ 80，但這是最底層的兜底，達不到也還是這條路

    /// <summary>依兩晚的蓋被選擇與目前心情／雌小鬼值，算出實際會走的路徑。
    /// 回傳 (day1 蓋被, day2 蓋被, 結局編號)；結局編號 5／3／4，或 0＝兜底（結局1／2，由第3晚深夜選擇決定）。</summary>
    public static (bool d1Cover, bool d2Cover, int ending) ResolveAAEnding(bool d1Cover, bool d2Cover, float mood, float brat)
    {
        if (d1Cover && d2Cover)
        {
            if (mood >= E5MoodMin && brat <= E5BratMax) return (true, true, 5);
            d2Cover = false;   // 降級：蓋→蓋 沒達標 → 當作 蓋→不做
        }
        if (d1Cover && !d2Cover)
        {
            if (mood <= E4MoodMax && brat >= E4BratMin && brat <= E4BratMax) return (true, false, 4);
            d1Cover = false;   // 降級：蓋→不做 沒達標 → 當作 不做→不做
        }
        if (!d1Cover && d2Cover)
        {
            if (mood >= E3MoodMin && mood <= E3MoodMax && brat >= E3BratMin) return (false, true, 3);
            d2Cover = false;   // 降級：不做→蓋 沒達標 → 當作 不做→不做
        }
        return (false, false, 0);
    }

    public const string AAEndingBadEndVar = "__aa_badend7";   // 結局7 重播用的標記（不是真的劇情變數）
    public static readonly AAEnding[] AAEndings =
    {
        default, // [0] 不使用
        new("雜魚敗犬",     "第1晚不做 → 第2晚不做 → 忍耐",   "day1_noBlanket", "day2_noBlanket", 0), // 結局1
        new("衝動的代價",       "第1晚不做 → 第2晚不做 → 教訓妹妹", "day1_noBlanket", "day2_noBlanket", 1), // 結局2
        new("課後輔導~❤",   "第1晚不做 → 第2晚蓋被子",        "day1_noBlanket", "day2_blanket",   -1), // 結局3
        new("無聲的拒絕",       "第1晚蓋被子 → 第2晚不做",        "day1_blanket",   "day2_noBlanket", -1), // 結局4
        new("兄妹專屬散步~❤",     "第1晚蓋被子 → 第2晚蓋被子",      "day1_blanket",   "day2_blanket",   -1), // 結局5
        new("雌小鬼大小姐",     "雌小鬼值滿 100（心情沒歸 0）→ 第3天清晨",  "mesugaki100",    "mesugaki100",    -1), // 結局6：重播時把 mesugaki100 設成 1，播 Day3_Start 開頭那段
        new("斷線的風箏",       "心情歸 0 → 莉奈離家出走",        AAEndingBadEndVar, AAEndingBadEndVar, -1), // 結局7：播 Day2_Start(bad end)（重播時特別處理，見 LivingRoomRuntime.ReplayAAEndingAsync）
    };

    public static readonly (string id, string label, string night)[] AABranchDefs =
    {
        ("aa_n1_idle",  "什麼都不做", "第１晚"),
        ("aa_n1_cover", "蓋被子",     "第１晚"),
        ("aa_n2_idle",  "什麼都不做", "第２晚"),
        ("aa_n2_cover", "蓋被子",     "第２晚"),
    };

    public const string AATreeRoot = "A00";

    public static readonly Dictionary<string, StoryNode> AANodes = new()
    {
        ["A00"] = new() { Id="A00", Label="序章 第1天",          Type=NodeType.Story,  Time="D1",   File="Prologue/prologue",   Preview="" },
        ["A01"] = new() { Id="A01", Label="第1天 夜晚：選擇",    Type=NodeType.Choice, Time="D1晚上", File="Prologue/prologue",   Preview="" },
        ["A02"] = new() { Id="A02", Label="第2天（第1晚不做）",  Type=NodeType.Story,  Time="D2",   File="Day2/Day2_Start2",    Preview="" },
        ["A03"] = new() { Id="A03", Label="第2天（第1晚蓋被）",  Type=NodeType.Story,  Time="D2",   File="Day2/Day2_Start2",    Preview="" },
        ["A04"] = new() { Id="A04", Label="第2天 夜晚：選擇",    Type=NodeType.Choice, Time="D2晚上", File="Day2/Day2_Start2",    Preview="" },
        ["A05"] = new() { Id="A05", Label="第2天 夜晚：選擇",    Type=NodeType.Choice, Time="D2晚上", File="Day2/Day2_Start2",    Preview="" },
        ["A06"] = new() { Id="A06", Label="第3天（兩晚都不做）", Type=NodeType.Story,  Time="D3",   File="Day3/Day3_Start",     Preview="" },
        ["A07"] = new() { Id="A07", Label="第3天（第2晚蓋被）",  Type=NodeType.Story,  Time="D3",   File="Day3/Day3_Start",     Preview="" },
        ["A08"] = new() { Id="A08", Label="第3天（第2晚不做）",  Type=NodeType.Story,  Time="D3",   File="Day3/Day3_Start",     Preview="" },
        ["A09"] = new() { Id="A09", Label="第3天（兩晚都蓋被）", Type=NodeType.Story,  Time="D3",   File="Day3/Day3_Start",     Preview="" },
        ["A10"] = new() { Id="A10", Label="第3天 深夜：選擇",    Type=NodeType.Choice, Time="D3深夜", File="Day3/Day3_Start",     Preview="" },
        ["AE1"] = new() { Id="AE1", Label="結局1 雜魚敗犬",   Type=NodeType.Ending, Time="D3", File="Day3/Day3_Start", EndingNum=1, EndingLabel="結局1", Preview="" },
        ["AE2"] = new() { Id="AE2", Label="結局2 衝動的代價",     Type=NodeType.Ending, Time="D3", File="Day3/Day3_Start", EndingNum=2, EndingLabel="結局2", Preview="" },
        ["AE3"] = new() { Id="AE3", Label="結局3 課後輔導~❤", Type=NodeType.Ending, Time="D3", File="Day3/Day3_Start", EndingNum=3, EndingLabel="結局3", Preview="" },
        ["AE4"] = new() { Id="AE4", Label="結局4 無聲的拒絕",     Type=NodeType.Ending, Time="D3", File="Day3/Day3_Start", EndingNum=4, EndingLabel="結局4", Preview="" },
        ["AE5"] = new() { Id="AE5", Label="結局5 兄妹專屬散步~❤",   Type=NodeType.Ending, Time="D3", File="Day3/Day3_Start", EndingNum=5, EndingLabel="結局5", Preview="" },
        ["AE6"] = new() { Id="AE6", Label="結局6 雌小鬼大小姐", Type=NodeType.Ending, Time="D3", File="Day3/Day3_Start", EndingNum=6, EndingLabel="結局6", Preview="" },
        ["AE7"] = new() { Id="AE7", Label="結局7 斷線的風箏",   Type=NodeType.Ending, Time="D2", File="Endings/Day2_Start(bad end)", EndingNum=7, EndingLabel="結局7", Preview="" },
    };

    public static readonly Dictionary<string, List<StoryEdge>> AAEdges = new()
    {
        ["A00"] = new() { new("繼續", "A01"), new("雌小鬼值滿100", "AE6"), new("心情歸0", "AE7") },
        ["A01"] = new() { new("什麼都不做", "A02"), new("蓋被子", "A03") },
        ["A02"] = new() { new("繼續", "A04") },
        ["A03"] = new() { new("繼續", "A05") },
        ["A04"] = new() { new("什麼都不做", "A06"), new("蓋被子", "A07") },
        ["A05"] = new() { new("什麼都不做", "A08"), new("蓋被子", "A09") },
        ["A06"] = new() { new("繼續", "A10") },
        ["A10"] = new() { new("忍耐", "AE1"), new("教訓妹妹", "AE2") },
        ["A07"] = new() { new("結局", "AE3") },
        ["A08"] = new() { new("結局", "AE4") },
        ["A09"] = new() { new("結局", "AE5") },
    };

    /// <summary>全年齡劇情變數 → 要解鎖的分支 id 與樹狀圖節點</summary>
    public static (string branch, string[] nodes) AAUnlocksFor(string varName)
    {
        var pdm = PlayerDataManager.Instance;
        bool d1Cover = (pdm?.GetDialogueVar("day1_blanket") ?? 0) > 0;
        return varName switch
        {
            "day1_noBlanket" => ("aa_n1_idle",  new[] { "A00", "A01", "A02", "A04" }),
            "day1_blanket"   => ("aa_n1_cover", new[] { "A00", "A01", "A03", "A05" }),
            "day2_noBlanket" => ("aa_n2_idle",  d1Cover ? new[] { "A08" } : new[] { "A06", "A10" }),
            "day2_blanket"   => ("aa_n2_cover", d1Cover ? new[] { "A09" } : new[] { "A07" }),
            _ => (null, null),
        };
    }

    public static readonly Dictionary<string, StoryNode> Nodes = new()
    {
        ["N001"] = new() { Id="N001", Label="第一天夜晚：選擇", Type=NodeType.Choice, Time="D1晚上", File="Prologue/Day1LinaiFirst（12）", EndingNum=0, EndingLabel=null, Preview="嗯？莉奈的房門怎麼沒關緊？\n莉奈……？妳睡著了嗎？\n（房間裡沒有任何回應）" },
        ["N002"] = new() { Id="N002", Label="路線A", Type=NodeType.Route, Time="D1晚上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N003"] = new() { Id="N003", Label="第2天 早上（摸 · 高潮）", Type=NodeType.Story, Time="D2早上", File="Day2/N003", EndingNum=0, EndingLabel=null, Preview="早、早啊，莉奈。昨天睡得還好嗎？\n哼，還算可以啦。睡得挺香的❤" },
        ["N004"] = new() { Id="N004", Label="第3天 早上（摸 · 沒高潮）", Type=NodeType.Story, Time="D2早上", File="Day2/N004", EndingNum=0, EndingLabel=null, Preview="（沒高潮分支的早上）" },
        ["N006"] = new() { Id="N006", Label="什麼都不做後（NothingRoute）", Type=NodeType.Story, Time="D2早上", File="Day2/N006", EndingNum=0, EndingLabel=null, Preview="（什麼都不做，進入NothingRoute）" },
        ["N007"] = new() { Id="N007", Label="什麼都不做後", Type=NodeType.Story, Time="D3早上", File="Day3/N007", EndingNum=0, EndingLabel=null, Preview="（第3天早上，Nothing路線）" },
        ["N008"] = new() { Id="N008", Label="第1天 早上", Type=NodeType.Incomplete, Time="D1早上", File=null, EndingNum=0, EndingLabel=null, Preview="（尚未填寫）" },
        ["N009"] = new() { Id="N009", Label="第2天 傍晚", Type=NodeType.Story, Time="D2傍晚", File="Day2/N009", EndingNum=0, EndingLabel=null, Preview="（第2天傍晚）" },
        ["N010"] = new() { Id="N010", Label="第2天 晚上：選擇", Type=NodeType.Choice, Time="D2晚上", File="Day2/N010", EndingNum=0, EndingLabel=null, Preview="（第2天晚上，三路分歧）" },
        ["N011"] = new() { Id="N011", Label="第3天 傍晚", Type=NodeType.Story, Time="D3傍晚", File="Day3/N011", EndingNum=0, EndingLabel=null, Preview="（第3天傍晚）" },
        ["N012"] = new() { Id="N012", Label="逃走後", Type=NodeType.Story, Time="D3晚上", File="Day3/N012", EndingNum=0, EndingLabel=null, Preview="（逃走後的劇情）" },
        ["N013"] = new() { Id="N013", Label="雜魚結局", Type=NodeType.Ending, Time="後續", File=null, EndingNum=-1, EndingLabel="保留段落", Preview="（尚未填寫）" },
        ["N017"] = new() { Id="N017", Label="結局4 雜魚結局", Type=NodeType.Ending, Time="後續", File=null, EndingNum=4, EndingLabel="結局4", Preview="（尚未填寫）" },
        ["N018"] = new() { Id="N018", Label="教訓妹妹", Type=NodeType.Story, Time="D3晚上", File="Day3/N018", EndingNum=0, EndingLabel=null, Preview="（教訓妹妹的劇情）" },
        ["N019"] = new() { Id="N019", Label="第3天 晚上：選擇", Type=NodeType.Choice, Time="D3晚上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N020"] = new() { Id="N020", Label="挑戰結果（勝敗判定）", Type=NodeType.Challenge, Time="D4早上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N021"] = new() { Id="N021", Label="結局3 雌小鬼的專屬暗號", Type=NodeType.Ending, Time=" — ", File=null, EndingNum=3, EndingLabel="結局3", Preview="（尚未填寫）" },
        ["N022"] = new() { Id="N022", Label="結局2 衝動的代價", Type=NodeType.Ending, Time=" — ", File=null, EndingNum=2, EndingLabel="結局2", Preview="（尚未填寫）" },
        ["N023"] = new() { Id="N023", Label="插入（路由，Nothing線）", Type=NodeType.Route, Time="D2晚上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N024"] = new() { Id="N024", Label="結局1 斷線的風箏", Type=NodeType.Ending, Time="後續", File=null, EndingNum=1, EndingLabel="結局1", Preview="（尚未填寫）" },
        ["N025"] = new() { Id="N025", Label="插入（路由）", Type=NodeType.Route, Time="D1晚上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N026"] = new() { Id="N026", Label="情況一高潮後", Type=NodeType.Story, Time="D2早上", File="Day2/N026", EndingNum=0, EndingLabel=null, Preview="（插入高潮後，Day2）" },
        ["N027"] = new() { Id="N027", Label="情況二沒高潮（路由）", Type=NodeType.Route, Time="D2早上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N028"] = new() { Id="N028", Label="情況一高潮", Type=NodeType.Incomplete, Time="D3早上", File=null, EndingNum=0, EndingLabel=null, Preview="（尚未設定後續）" },
        ["N029"] = new() { Id="N029", Label="情況二沒高潮（路由）", Type=NodeType.Route, Time="D3早上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N030"] = new() { Id="N030", Label="傍晚（B線）", Type=NodeType.Story, Time="D2傍晚", File="Day2/N030", EndingNum=0, EndingLabel=null, Preview="（B線傍晚劇情）" },
        ["N031"] = new() { Id="N031", Label="第2天晚上：選擇（B線）", Type=NodeType.Choice, Time="D2晚上", File="Day2/N031", EndingNum=0, EndingLabel=null, Preview="（B線第2天晚上選擇）" },
        ["N032"] = new() { Id="N032", Label="摸摸後（無高潮線）", Type=NodeType.Story, Time="D3早上", File="Day3/N032", EndingNum=0, EndingLabel=null, Preview="（第三天早上，持續摸摸線）" },
        ["N033"] = new() { Id="N033", Label="插入（路由，B線）", Type=NodeType.Route, Time="D3早上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N034"] = new() { Id="N034", Label="傍晚（無高潮線）", Type=NodeType.Story, Time="D3傍晚", File="Day3/N034", EndingNum=0, EndingLabel=null, Preview="（傍晚劇情）" },
        ["N035"] = new() { Id="N035", Label="摸摸（路由，B線）", Type=NodeType.Route, Time="D3傍晚", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N036"] = new() { Id="N036", Label="晚上（無高潮線）", Type=NodeType.Story, Time="D3晚上", File="Day3/N036", EndingNum=0, EndingLabel=null, Preview="（晚上劇情）" },
        ["N037"] = new() { Id="N037", Label="結局5 無聲的拒絕", Type=NodeType.Ending, Time="D3", File=null, EndingNum=5, EndingLabel="結局5", Preview="（尚未填寫）" },
        ["N038"] = new() { Id="N038", Label="第2天晚上後", Type=NodeType.Incomplete, Time="D2傍晚", File=null, EndingNum=0, EndingLabel=null, Preview="（尚未填寫）" },
        ["N039"] = new() { Id="N039", Label="情況一高潮", Type=NodeType.Story, Time="D3晚上", File="Day3/N039", EndingNum=0, EndingLabel=null, Preview="（高潮後翌日）" },
        ["N040"] = new() { Id="N040", Label="情況二沒高潮", Type=NodeType.Story, Time="D3晚上", File="Day3/N040", EndingNum=0, EndingLabel=null, Preview="（情況二沒高潮後）" },
        ["N041"] = new() { Id="N041", Label="插入（路由）", Type=NodeType.Route, Time="後續", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N042"] = new() { Id="N042", Label="摸摸（路由）", Type=NodeType.Route, Time="後續", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N043"] = new() { Id="N043", Label="什麼都沒做後", Type=NodeType.Story, Time="後續", File="Day3/N043", EndingNum=0, EndingLabel=null, Preview="（什麼都沒做後）" },
        ["N044"] = new() { Id="N044", Label="進入後：再次選擇", Type=NodeType.Choice, Time="後續", File="Day3/N044", EndingNum=0, EndingLabel=null, Preview="（進入房間後的選擇）" },
        ["N045"] = new() { Id="N045", Label="結局6 恰如其分的距離", Type=NodeType.Ending, Time="後續", File=null, EndingNum=6, EndingLabel="結局6", Preview="（尚未填寫）" },
        ["N046"] = new() { Id="N046", Label="不進入後", Type=NodeType.Story, Time="後續", File="Day3/N046", EndingNum=0, EndingLabel=null, Preview="（選擇不進入後的劇情）" },
        ["N047"] = new() { Id="N047", Label="高潮後", Type=NodeType.Route, Time="後續", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N048"] = new() { Id="N048", Label="沒高潮後", Type=NodeType.Route, Time="後續", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N049"] = new() { Id="N049", Label="高潮後", Type=NodeType.Story, Time="後續", File="Day3/N049", EndingNum=0, EndingLabel=null, Preview="（摸摸高潮後）" },
        ["N050"] = new() { Id="N050", Label="被抓包的逃兵", Type=NodeType.Story, Time="後續", File="Day3/N050", EndingNum=0, EndingLabel=null, Preview="站住！雜魚哥哥！\n莉奈！\n誰被一個變態連續三天晚上在身上亂摸，還能睡得著啊！" },
        ["N051"] = new() { Id="N051", Label="結局7 無盡的榨取", Type=NodeType.Ending, Time="後續", File=null, EndingNum=7, EndingLabel="結局7", Preview="（尚未填寫）" },
        ["N052"] = new() { Id="N052", Label="結局8 雌小鬼的專屬早餐", Type=NodeType.Ending, Time="後續", File=null, EndingNum=8, EndingLabel="結局8", Preview="（尚未填寫）" },
        ["N053"] = new() { Id="N053", Label="B線 · 情況一高潮", Type=NodeType.Story, Time="D3早上", File="Day3/N053", EndingNum=0, EndingLabel=null, Preview="（B線高潮後）" },
        ["N054"] = new() { Id="N054", Label="B線 · 情況二沒高潮（路由）", Type=NodeType.Route, Time="D3早上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N055"] = new() { Id="N055", Label="B線 · 傍晚", Type=NodeType.Story, Time="D3早上", File="Day3/N055", EndingNum=0, EndingLabel=null, Preview="（B線傍晚）" },
        ["N056"] = new() { Id="N056", Label="結局11 淪為妹妹的專屬玩具", Type=NodeType.Ending, Time="D3", File="Endings/N056", EndingNum=11, EndingLabel="結局11a", Preview="【第三日夜晚 · 莉奈房間】" },
        ["N057"] = new() { Id="N057", Label="傍晚（路線B沒高潮）", Type=NodeType.Story, Time="後續", File="Day3/N057", EndingNum=0, EndingLabel=null, Preview="（傍晚劇情）" },
        ["N058"] = new() { Id="N058", Label="結局9 最熟悉的陌生人", Type=NodeType.Ending, Time="後續", File=null, EndingNum=9, EndingLabel="結局9", Preview="（尚未填寫）" },
        ["N059"] = new() { Id="N059", Label="傍晚（進入選擇）", Type=NodeType.Choice, Time="後續", File="Day3/N059", EndingNum=0, EndingLabel=null, Preview="傍晚劇情，選擇是否進入莉奈房間" },
        ["N060"] = new() { Id="N060", Label="結局10 雌小鬼妹妹的專屬早餐2", Type=NodeType.Ending, Time="後續", File=null, EndingNum=10, EndingLabel="結局10", Preview="（尚未填寫）" },
        ["N061"] = new() { Id="N061", Label="傍晚後", Type=NodeType.Incomplete, Time="D2傍晚", File=null, EndingNum=0, EndingLabel=null, Preview="（尚未填寫）" },
        ["N062"] = new() { Id="N062", Label="晚上後", Type=NodeType.Incomplete, Time="D2傍晚", File=null, EndingNum=0, EndingLabel=null, Preview="（尚未填寫）" },
        ["N063"] = new() { Id="N063", Label="結局後（前段）", Type=NodeType.Incomplete, Time="D2傍晚", File=null, EndingNum=0, EndingLabel=null, Preview="（尚未填寫）" },
        ["N064"] = new() { Id="N064", Label="結局後（後段）", Type=NodeType.Story, Time="D2傍晚", File="Endings/N064", EndingNum=0, EndingLabel=null, Preview="第三日夜晚 · 浴室。雜魚哥哥，幫我擦背❤" },
        ["N065"] = new() { Id="N065", Label="傍晚劇情（蛋糕）", Type=NodeType.Story, Time="D2傍晚", File="Day2/N065", EndingNum=0, EndingLabel=null, Preview="手裡提著排隊買來的限量版草莓千層蛋糕……" },
        ["N066"] = new() { Id="N066", Label="晚上：第2次夜晚選擇", Type=NodeType.Choice, Time="D2晚上", File="Day2/N066", EndingNum=0, EndingLabel=null, Preview="準備回到自己的房間……沒鎖。這臭小鬼，今天居然又沒關門" },
        ["N067"] = new() { Id="N067", Label="摸摸（路由）", Type=NodeType.Route, Time="D2晚上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N069"] = new() { Id="N069", Label="什麼都沒做（路由）", Type=NodeType.Route, Time="D2晚上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N070"] = new() { Id="N070", Label="高潮後・老鼠隱喻", Type=NodeType.Story, Time="D2晚上", File="Day2/N070", EndingNum=0, EndingLabel=null, Preview="莉奈，早安。\n早安呀，哥哥～❤" },
        ["N071"] = new() { Id="N071", Label="沒高潮（路由）", Type=NodeType.Route, Time="D2晚上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N072"] = new() { Id="N072", Label="傍晚選擇", Type=NodeType.Choice, Time="D2傍晚", File="Day2/N072", EndingNum=0, EndingLabel=null, Preview="（傍晚再次選擇）" },
        ["N073"] = new() { Id="N073", Label="結局11 錯失的時機", Type=NodeType.Ending, Time="D2", File=null, EndingNum=0, EndingLabel="結局11b", Preview="（尚未填寫）" },
        ["N074"] = new() { Id="N074", Label="插入後", Type=NodeType.Story, Time="D2", File="Day2/N074", EndingNum=0, EndingLabel=null, Preview="（插入後）" },
        ["N075"] = new() { Id="N075", Label="插入（路由）", Type=NodeType.Route, Time="D2晚上", File=null, EndingNum=0, EndingLabel=null, Preview="（此處不需要台詞）" },
        ["N076"] = new() { Id="N076", Label="結局13 沉淪的課後輔導", Type=NodeType.Ending, Time="D2", File="Endings/N076", EndingNum=13, EndingLabel="結局13", Preview="莉奈：唔……嗯……好奇怪的感覺……" },
        ["N077"] = new() { Id="N077", Label="結局12 粗暴的代價", Type=NodeType.Ending, Time="D2", File="Endings/N077", EndingNum=12, EndingLabel="結局12", Preview="莉奈：好痛！太深了……快拔出去！你這白痴，快拔出去啊！" },
        ["N080"] = new() { Id="N080", Label="插入 · 高潮後", Type=NodeType.Story, Time="D2晚上", File="Day2/N080", EndingNum=0, EndingLabel=null, Preview="（插入高潮後早上）" },
        ["N081"] = new() { Id="N081", Label="插入 · 沒高潮後", Type=NodeType.Story, Time="D2晚上", File="Day2/N081", EndingNum=0, EndingLabel=null, Preview="（插入沒高潮後）" },
        ["N082"] = new() { Id="N082", Label="晚上：浴室邀請", Type=NodeType.Choice, Time="D2晚上", File="Day2/N082", EndingNum=0, EndingLabel=null, Preview="今天體育課流了好多汗，背後自己洗不到。雜魚哥哥，進來幫我洗澡❤" },
        ["N083"] = new() { Id="N083", Label="浴室高潮後", Type=NodeType.Choice, Time="D2晚上", File="Day2/N083", EndingNum=0, EndingLabel=null, Preview="我顫抖著手拿起海綿……\n哈啊……嗯……❤" },
        ["N084"] = new() { Id="N084", Label="結局15 笨拙的代價", Type=NodeType.Ending, Time="D2晚上", File="Endings/N084", EndingNum=15, EndingLabel="結局15", Preview="【男主獨白】笨拙的代價" },
        ["N085"] = new() { Id="N085", Label="結局14 浴室裡的沉淪", Type=NodeType.Ending, Time="D2晚上", File="Endings/N085", EndingNum=14, EndingLabel="結局14", Preview="【男主獨白】浴室裡的沉淪" },
        ["N086"] = new() { Id="N086", Label="結局16 笨拙的做愛", Type=NodeType.Ending, Time="D2晚上", File="Endings/N086", EndingNum=16, EndingLabel="結局16", Preview="莉奈：好痛！（笨拙的做愛）" },
        ["N087"] = new() { Id="N087", Label="傍晚：主從分歧", Type=NodeType.Choice, Time="D2傍晚", File="Day2/N087", EndingNum=0, EndingLabel=null, Preview="（傍晚，兩個結局分歧）" },
        ["N088"] = new() { Id="N088", Label="結局18 沉迷於主從遊戲的兄妹", Type=NodeType.Ending, Time="D2", File="Endings/N088", EndingNum=18, EndingLabel="結局18", Preview="這次在莉奈的挑釁下，男主從背後深深插入。" },
        ["N089"] = new() { Id="N089", Label="結局17 被榨取的雜魚玩具", Type=NodeType.Ending, Time="D2", File="Endings/N089", EndingNum=17, EndingLabel="結局17", Preview="莉奈：……喂。" },
    };

    public static readonly Dictionary<string, List<StoryEdge>> Edges = new()
    {
        ["N001"] = new() { new("摸摸", "N002", false), new("什麼都不做", "N006", false), new("插入", "N025", false) },
        ["N002"] = new() { new("高潮", "N003", false), new("沒高潮", "N004", false) },
        ["N003"] = new() { new("傍晚劇情", "N065", false) },
        ["N004"] = new() { new("傍晚劇情", "N030", false) },
        ["N006"] = new() { new("繼續", "N009", false) },
        ["N007"] = new() { new("繼續", "N011", false) },
        ["N008"] = new() { new("繼續", "N001", false) },
        ["N009"] = new() { new("繼續", "N010", false) },
        ["N010"] = new() { new("什麼都不做", "N007", false), new("插入", "N023", false), new("摸摸", "N032", true) },
        ["N011"] = new() { new("繼續", "N019", false) },
        ["N012"] = new() { new("結局", "N013", false) },
        ["N014"] = new() { new("繼續", "N012", true) },
        ["N015"] = new() { new("繼續", "N014", true) },
        ["N018"] = new() { new("進入挑戰", "N020", false) },
        ["N019"] = new() { new("算了，早點睡吧", "N017", false), new("教訓妹妹", "N018", false) },
        ["N020"] = new() { new("勝利", "N021", false), new("失敗", "N022", false) },
        ["N023"] = new() { new("情況一高潮", "N028", false), new("情況二沒高潮", "N029", false) },
        ["N025"] = new() { new("情況二沒高潮", "N027", false), new("情況1高潮", "N026", false) },
        ["N026"] = new() { new("第二天晚上劇情", "N038", false) },
        ["N027"] = new() { new("進入結局", "N024", true) },
        ["N029"] = new() { new("進入結局", "N024", true) },
        ["N030"] = new() { new("第2天晚上劇情", "N031", false) },
        ["N031"] = new() { new("插入", "N033", false), new("摸摸", "N035", false), new("什麼都沒做", "N032", true) },
        ["N032"] = new() { new("傍晚劇情", "N034", false) },
        ["N033"] = new() { new("情況一高潮", "N053", false), new("情況二沒高潮", "N054", false) },
        ["N034"] = new() { new("晚上劇情", "N036", false) },
        ["N035"] = new() { new("情況一高潮", "N039", true), new("情況二沒高潮", "N040", false) },
        ["N036"] = new() { new("結局", "N037", false) },
        ["N038"] = new() { new("傍晚劇情", "N061", false) },
        ["N039"] = new() { new("傍晚劇情", "N059", false) },
        ["N040"] = new() { new("傍晚劇情", "N057", false) },
        ["N041"] = new() { new("高潮", "N047", false), new("沒高潮", "N048", false) },
        ["N042"] = new() { new("高潮", "N049", false), new("沒高潮", "N050", false) },
        ["N043"] = new() { new("結局", "N045", true) },
        ["N044"] = new() { new("插入", "N041", false), new("摸摸", "N042", false), new("什麼都沒做", "N043", false) },
        ["N046"] = new() { new("結局", "N045", false) },
        ["N047"] = new() { new("結局", "N060", false) },
        ["N048"] = new() { new("進入結局", "N024", false) },
        ["N049"] = new() { new("結局", "N052", false) },
        ["N050"] = new() { new("結局", "N051", false) },
        ["N053"] = new() { new("傍晚劇情", "N055", false) },
        ["N054"] = new() { new("進入結局", "N024", true) },
        ["N055"] = new() { new("結局", "N056", false) },
        ["N057"] = new() { new("結局", "N058", false) },
        ["N059"] = new() { new("不進入", "N046", false), new("進入", "N044", false) },
        ["N061"] = new() { new("晚上劇情", "N062", false) },
        ["N062"] = new() { new("結局", "N063", false) },
        ["N063"] = new() { new("結局", "N064", false) },
        ["N065"] = new() { new("晚上劇情", "N066", false) },
        ["N066"] = new() { new("摸摸", "N067", false), new("什麼都沒做", "N069", false), new("插入", "N075", false) },
        ["N067"] = new() { new("高潮", "N070", false), new("沒高潮", "N071", false) },
        ["N069"] = new() { new("什麼都不做", "N032", false) },
        ["N070"] = new() { new("晚上劇情", "N082", false) },
        ["N071"] = new() { new("繼續", "N039", false) },
        ["N072"] = new() { new("什麼都沒做", "N073", false), new("插入", "N074", false) },
        ["N074"] = new() { new("高潮", "N076", false), new("沒高潮", "N077", false) },
        ["N075"] = new() { new("高潮", "N080", false), new("沒高潮", "N081", false) },
        ["N080"] = new() { new("傍晚劇情", "N087", false) },
        ["N081"] = new() { new("繼續", "N072", false) },
        ["N082"] = new() { new("高潮", "N083", false), new("沒高潮", "N084", false) },
        ["N083"] = new() { new("高潮", "N085", false), new("沒高潮", "N086", false) },
        ["N087"] = new() { new("結局A", "N088", false), new("結局B", "N089", false) },
    };
}
