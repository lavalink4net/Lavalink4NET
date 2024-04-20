namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

using System.Text.Json.Serialization;
using Lavalink4NET.Experiments.Receive.Connections.Payloads.Converters;

internal sealed record class SessionDescriptionPayload : IVoicePayload
{
    [JsonRequired]
    [JsonPropertyName("mode")]
    public required string Mode { get; set; }

    [JsonRequired]
    [JsonPropertyName("secret_key")]
    [JsonConverter(typeof(SecretKeyJsonConverter))]
    public required ReadOnlyMemory<byte> SecretKey { get; set; }
}
