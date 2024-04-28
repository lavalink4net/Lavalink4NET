namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

using System.Text.Json.Serialization;

internal sealed record class SpeakingPayload : IVoicePayload
{
    [JsonRequired]
    [JsonPropertyName("speaking")]
    public required SpeakingFlags Flags { get; set; }

    [JsonPropertyName("delay")]
    public int? Delay { get; set; }

    [JsonRequired]
    [JsonPropertyName("ssrc")]
    public required int Ssrc { get; set; }
}
