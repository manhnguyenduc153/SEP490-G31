using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace sep490_be.Helpers
{
    public class DateTimeJsonConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.Parse(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            }
            else
            {
                // Treat Unspecified (EF Core default) and Local as UTC values,
                // and append 'Z' so the frontend correctly shifts to local browser timezone.
                writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            }
        }
    }

    public class NullableDateTimeJsonConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (string.IsNullOrEmpty(str)) return null;
            return DateTime.Parse(str);
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
            }
            else
            {
                var val = value.Value;
                if (val.Kind == DateTimeKind.Utc)
                {
                    writer.WriteStringValue(val.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                }
                else
                {
                    writer.WriteStringValue(DateTime.SpecifyKind(val, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                }
            }
        }
    }
}
