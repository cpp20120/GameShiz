using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotFramework.Contracts.Tenancy;

internal abstract class OpaqueIdJsonConverter<T> : JsonConverter<T>
    where T : struct, IParsable<T>
{
    protected abstract string GetValue(T value);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a string for {typeToConvert.Name}.");

        var value = reader.GetString();
        if (value is null || !T.TryParse(value, null, out var result))
            throw new JsonException($"Invalid {typeToConvert.Name}.");

        return result;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(GetValue(value));
}
