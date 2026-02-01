using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmBlazor.Utils;

/// <summary>
/// Google Sheet/Apps-Script JSON ponekad šalje number ili prazan string za numeric polja (npr. fixedDay).
/// Ovaj converter prihvata:
/// - number -> int?
/// - string ""/null -> null
/// - string "5" -> 5
/// </summary>
public sealed class NullableIntFromStringConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var i)) return i;
            // fallback: if it's a number but not int32, try as double and cast if safe
            if (reader.TryGetDouble(out var d))
                return (int)d;
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (int.TryParse(s, out var i)) return i;
            return null;
        }

        // Unexpected token -> skip gracefully
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}

