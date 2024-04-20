namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

using System.Collections.Immutable;
using System.Text.Json.Serialization;

internal sealed record class ReadyPayload : IVoicePayload
{
    [JsonRequired]
    [JsonPropertyName("ssrc")]
    public required uint Ssrc { get; set; }

    [JsonRequired]
    [JsonPropertyName("ip")]
    public required string Ip { get; set; }

    [JsonRequired]
    [JsonPropertyName("port")]
    public required int Port { get; set; }

    [JsonRequired]
    [JsonPropertyName("modes")]
    public required ImmutableArray<string> Modes { get; set; }
}
