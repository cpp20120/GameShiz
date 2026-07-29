using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatAdministration.Telegram.Infrastructure;

internal sealed class ReadOnlySetJsonConverter<T> : JsonConverter<IReadOnlySet<T>>
{
    public override IReadOnlySet<T> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<HashSet<T>>(ref reader, options) ?? new HashSet<T>();

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlySet<T> value,
        JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.ToArray(), options);
}
