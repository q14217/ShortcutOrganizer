using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShortcutOrganizer
{
    /// <summary>
    /// 宽松的 DateTime 转换器：兼容多种常见格式。
    /// 反序列化时若无法解析，返回 DateTime.Now。
    /// </summary>
    public class FlexibleDateTimeConverter : JsonConverter<DateTime>
    {
        private static readonly string[] Formats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy/MM/dd",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-ddTHH:mm:ss.FFFFFFFK",
        };

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return DateTime.Now;

                // 1) 直接 TryParse（兼容大部分情况）
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    return dt;

                if (DateTime.TryParse(s, CultureInfo.CurrentCulture,
                        DateTimeStyles.None, out dt))
                    return dt;

                // 2) 逐个精确格式
                foreach (var f in Formats)
                {
                    if (DateTime.TryParseExact(s, f, CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out dt))
                        return dt;
                }
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                // Unix 时间戳（秒）
                try
                {
                    long sec = reader.GetInt64();
                    if (sec > 0)
                        return DateTimeOffset.FromUnixTimeSeconds(sec).LocalDateTime;
                }
                catch { }
            }
            else if (reader.TokenType == JsonTokenType.Null)
            {
                return DateTime.Now;
            }

            return DateTime.Now;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }

    /// <summary>
    /// 宽松的 Nullable DateTime 转换器。
    /// </summary>
    public class FlexibleNullableDateTimeConverter : JsonConverter<DateTime?>
    {
        private static readonly FlexibleDateTimeConverter _inner = new FlexibleDateTimeConverter();

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                string s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return null;
            }

            try
            {
                return _inner.Read(ref reader, typeof(DateTime), options);
            }
            catch
            {
                return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            else
                writer.WriteNullValue();
        }
    }

    /// <summary>
    /// 统一的 JSON 序列化选项，供备份 / 恢复共用。
    /// </summary>
    public static class JsonHelpers
    {
        public static JsonSerializerOptions Default { get; } = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            var opt = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };
            opt.Converters.Add(new FlexibleDateTimeConverter());
            opt.Converters.Add(new FlexibleNullableDateTimeConverter());
            return opt;
        }
    }
}