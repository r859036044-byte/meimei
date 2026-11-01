using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// 遊戲內所有劇本／對話 JSON 的統一解析設定。
///
/// 【為什麼要有這個檔案】
/// 專案裡原本有三個地方各自 new 了一份 JsonSerializerOptions：
///   DialogueSystem.ParseDialogueData、MainMenuRuntime.LoadJsonData、
///   TrainingRoomRuntime 的備援解析。
/// 之前只在 DialogueSystem 那一份加上 FlexibleStringConverter，
/// 結果序章（prologue.json）走的是 MainMenuRuntime.LoadJsonData 那一份、沒有轉換器，
/// 一樣被「裸數字塞進 string 欄位」噴例外，導致點「觀看劇情」整個沒反應。
///
/// 所以統一成這一份設定，之後不管哪裡要解析劇本 JSON 都用 GameJson.Options()，
/// 不會再發生「修了一個地方、另一個地方還是舊行為」的問題。
/// </summary>
public static class GameJson
{
    /// <summary>
    /// 劇本 JSON 專用的寬容解析設定：
    /// - IncludeFields：PrologueLine 等類別用的是 public 欄位而非屬性，必須開啟
    /// - PropertyNameCaseInsensitive / AllowTrailingCommas / 允許註解：容忍手寫 JSON 的小瑕疵
    /// - FlexibleStringConverter：容忍 "chX": 350 這種裸數字寫進 string 欄位（Unity 端的舊習慣）
    /// </summary>
    public static JsonSerializerOptions Options()
    {
        var opts = new JsonSerializerOptions
        {
            IncludeFields               = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling         = JsonCommentHandling.Skip,
            AllowTrailingCommas         = true,
            DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        };
        opts.Converters.Add(new FlexibleStringConverter());
        opts.Converters.Add(new FlexibleFloatConverter());
        opts.Converters.Add(new FlexibleIntConverter());
        opts.Converters.Add(new FlexibleBoolConverter());
        opts.Converters.Add(new FlexibleStringArrayConverter());
        return opts;
    }
}
