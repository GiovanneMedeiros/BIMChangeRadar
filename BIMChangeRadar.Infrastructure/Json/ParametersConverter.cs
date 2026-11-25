using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BIMChangeRadar.Infrastructure.Json;

/// <summary>Aceita parâmetros com valores string, número, booleano ou null e converte tudo para string.</summary>
public class ParametersConverter : JsonConverter<Dictionary<string, string>>
{
    public override Dictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (reader.TokenType == JsonTokenType.Null) return result;
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("'parameters' deve ser um objeto.");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString()!;
            reader.Read();
            var value = reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.GetDouble().ToString("0.####", CultureInfo.InvariantCulture),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                JsonTokenType.Null => null,
                _ => throw new JsonException($"Valor não suportado no parâmetro '{key}'.")
            };
            if (value is not null) result[key] = value;
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (k, v) in value) writer.WriteString(k, v);
        writer.WriteEndObject();
    }
}
