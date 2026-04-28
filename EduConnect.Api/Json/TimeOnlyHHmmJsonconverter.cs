using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EduConnect.Api.Json;

public sealed class TimeOnlyHHmmJsonConverter : JsonConverter<TimeOnly>
{
    private const string Format = "HH:mm";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Hora deve ser string no formato HH:mm.");

        var s = reader.GetString() ?? "";
        if (!TimeOnly.TryParseExact(s, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            throw new JsonException("Hora inválida. Use HH:mm (ex: 18:30).");

        return t;
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}

public sealed class NullableTimeOnlyHHmmJsonConverter : JsonConverter<TimeOnly?>
{
    private const string Format = "HH:mm";

    public override TimeOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Hora deve ser string no formato HH:mm.");

        var s = reader.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (!TimeOnly.TryParseExact(s, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            throw new JsonException("Hora inválida. Use HH:mm (ex: 18:30).");

        return t;
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly? value, JsonSerializerOptions options)
    {
        if (!value.HasValue) { writer.WriteNullValue(); return; }
        writer.WriteStringValue(value.Value.ToString(Format, CultureInfo.InvariantCulture));
    }
}