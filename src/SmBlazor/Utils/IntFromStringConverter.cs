using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmBlazor.Utils;

/// <summary>
/// Google Sheet/Apps-Script JSON ponekad šalje number ili prazan string za numeric polja (npr. peekOrder).
/// Ovaj converter mapira:
/// - number -> int
/// - string ""/null -> 0
/// - string "5" -> 5
/// </summary>
public sealed class IntFromStringConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return 0;

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var i)) return i;
            if (reader.TryGetDouble(out var d)) return (int)d;
            return 0;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s)) return 0;
            if (int.TryParse(s, out var i)) return i;
            if (double.TryParse(s, out var d)) return (int)d;
            return 0;
        }

        reader.Skip();
        return 0;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}

