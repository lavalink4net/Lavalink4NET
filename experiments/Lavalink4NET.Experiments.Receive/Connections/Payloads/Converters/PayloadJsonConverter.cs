namespace Lavalink4NET.Experiments.Receive.Connections.Payloads.Converters;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class PayloadJsonConverter : JsonConverter<IVoicePayload>
{
    public override IVoicePayload? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected the start of an object.");
        }

        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException("Expected a property name.");
        }

        var propertyName = reader.GetString();

        if (propertyName != "op")
        {
            throw new JsonException("Expected the 'op' property.");
        }

        if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
        {
            throw new JsonException("Expected a number.");
        }

        var op = reader.GetInt32();

        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException("Expected a property name.");
        }

        propertyName = reader.GetString();

        if (propertyName != "d")
        {
            throw new JsonException("Expected the 'd' property.");
        }

        if (!reader.Read())
        {
            throw new JsonException("Expected a value.");
        }

        var payload = op switch
        {
            0 => JsonSerializer.Deserialize<IdentifyPayload>(ref reader, options),
            _ => throw new JsonException($"Unknown operation code: {op}.")
        };

        if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("Expected the end of an object.");
        }

        return payload;
    }

    public override void Write(Utf8JsonWriter writer, IVoicePayload value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        var opCode = value switch
        {
            IdentifyPayload _ => 0,
            _ => throw new JsonException("Unknown payload type.")
        };

        writer.WriteNumber("op", opCode);
        writer.WritePropertyName("d");

        JsonSerializer.Serialize(writer, value, value.GetType(), options);

        writer.WriteEndObject();
    }
}
