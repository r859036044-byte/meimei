using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// 劇本 JSON 的寬容數值／布林轉換器（搭配 FlexibleStringConverter）。
///
/// 背景：DialogueLine / PrologueLine 裡 autoNext、wait、bgScale 是 float，
/// optionStart 是 int，branchEnd、varEnd、optionEnd 是 bool。
/// System.Text.Json 預設只接受「型別完全相符」的寫法，只要手寫成
///   "wait": "60"、"autoNext": ""、"branchEnd": "true"、"varEnd": 1、"choice": "蓋被子"
/// 就會整份劇本解析失敗、整段劇情播不出來。
/// 這些轉換器讓上面這些寫法都能被接受：
///   - 數字欄位：數字、"60"、"60, 1"（取第一段）、""/null（=0）
///   - 布林欄位：true/false、"true"/"false"、"1"/"0"、1/0、""/null（=false）
///   - 字串陣列：["A","B"] 或單一字串 "A"（自動變成 ["A"]）
/// </summary>
static class FlexNum
{
    public static bool TryParse(string s, out double v)
    {
        v = 0;
        if (string.IsNullOrWhiteSpace(s)) return true;
        s = s.Trim();
        int comma = s.IndexOfAny(new[] { ',', '，' });
        if (comma >= 0) s = s.Substring(0, comma).Trim();
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }

    public static double Read(ref Utf8JsonReader reader, string kind)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number: return reader.GetDouble();
            case JsonTokenType.Null:   return 0;
            case JsonTokenType.True:   return 1;
            case JsonTokenType.False:  return 0;
            case JsonTokenType.String:
                var s = reader.GetString();
                if (TryParse(s, out double v)) return v;
                Godot.GD.PushWarning($"[GameJson] 無法把「{s}」轉成{kind}，視為 0");
                return 0;
            default:
                throw new JsonException($"無法把 JSON token「{reader.TokenType}」轉成{kind}");
        }
    }
}

public class FlexibleFloatConverter : JsonConverter<float>
{
    public override float Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        => (float)FlexNum.Read(ref reader, "數字");
    public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions o)
        => writer.WriteNumberValue(value);
}

public class FlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        => (int)Math.Round(FlexNum.Read(ref reader, "整數"));
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions o)
        => writer.WriteNumberValue(value);
}

public class FlexibleBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:   return true;
            case JsonTokenType.False:  return false;
            case JsonTokenType.Null:   return false;
            case JsonTokenType.Number: return reader.GetDouble() != 0;
            case JsonTokenType.String:
                var s = (reader.GetString() ?? "").Trim().ToLowerInvariant();
                return !(s == "" || s == "false" || s == "0" || s == "no" || s == "off");
            default:
                throw new JsonException($"無法把 JSON token「{reader.TokenType}」轉成 true/false");
        }
    }
    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions o)
        => writer.WriteBooleanValue(value);
}

public class FlexibleStringArrayConverter : JsonConverter<string[]>
{
    public override string[] Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.String) return new[] { reader.GetString() };
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"choice 必須是字串陣列，例如 [\"A\",\"B\"]");
        var list = new System.Collections.Generic.List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String: list.Add(reader.GetString()); break;
                case JsonTokenType.Number: list.Add(reader.GetDouble().ToString(CultureInfo.InvariantCulture)); break;
                case JsonTokenType.Null:   list.Add(""); break;
                default: reader.Skip(); break;
            }
        }
        return list.ToArray();
    }
    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions o)
    {
        writer.WriteStartArray();
        foreach (var s in value) writer.WriteStringValue(s);
        writer.WriteEndArray();
    }
}
