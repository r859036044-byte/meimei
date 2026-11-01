using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// 讓 JSON 反序列化時，字串欄位可以同時接受「字串」和「純數字/布林」兩種寫法。
///
/// 背景：劇本 JSON（從 Unity 專案同步過來）裡很多欄位——chX、chY、chScale、bgX、bgY、
/// bgScale、cgX、cgY、cgScale 等——在 C# 這邊的 DialogueLine 定義是 string
/// （因為值可能是 "60, 1" 這種「數值,附加旗標」的複合格式），但實際資料檔裡大量寫成
/// 裸數字，例如 "chX": 350 而不是 "chX": "350"。
///
/// Unity 那邊用的 JSON 讀取方式對這種型別不符很寬容，會自動轉換；Godot 這邊用的
/// System.Text.Json 是嚴格型別比對，JSON 給數字、C# 要字串會直接丟例外中斷解析——
/// 而且因為對話流程是用「射後不理」的 async Task 寫法（見 TaskLogger.cs 的說明），
/// 這個例外會被吞掉、完全沒有任何錯誤訊息，只會讓遊戲停在當下畫面，非常難抓。
///
/// 這個轉換器讓所有 string 欄位都能接受數字/布林寫法，自動轉成字串（例如
/// 350 → "350"、0.9 → "0.9"），從根本解決這一整類問題，不需要一一修改每個
/// JSON 檔案，未來從 Unity 同步新劇本也不會再犯。
/// </summary>
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out long l)) return l.ToString(CultureInfo.InvariantCulture);
                return reader.GetDouble().ToString(CultureInfo.InvariantCulture);
            case JsonTokenType.True:
            case JsonTokenType.False:
                return reader.GetBoolean().ToString(CultureInfo.InvariantCulture);
            default:
                throw new JsonException($"無法把 JSON token「{reader.TokenType}」轉成字串欄位");
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
