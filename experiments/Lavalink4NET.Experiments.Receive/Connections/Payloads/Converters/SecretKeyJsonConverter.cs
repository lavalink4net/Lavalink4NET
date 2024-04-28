namespace Lavalink4NET.Experiments.Receive.Connections.Payloads.Converters;

using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class SecretKeyJsonConverter : JsonConverter<ReadOnlyMemory<byte>>
{
    public override ReadOnlyMemory<byte> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var byteData = JsonSerializer.Deserialize(
            reader: ref reader,
            jsonTypeInfo: PayloadJsonSerializerContext.Default.ImmutableArrayInt32);

        return byteData.Select(x => (byte)x).ToArray();
    }

    public override void Write(Utf8JsonWriter writer, ReadOnlyMemory<byte> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var item in value.Span)
        {
            writer.WriteNumberValue(item);
        }

        writer.WriteEndArray();
    }
}
