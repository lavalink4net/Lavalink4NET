namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

using System.Text.Json.Serialization;

internal sealed record class SelectProtocolPayload : IVoicePayload
{
    [JsonRequired]
    [JsonPropertyName("protocol")]
    public required string Protocol { get; set; }

    [JsonRequired]
    [JsonPropertyName("data")]
    public required SelectProtocolData Data { get; set; }
}
