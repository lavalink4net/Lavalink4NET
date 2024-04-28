namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

using System.Text.Json.Serialization;
using Lavalink4NET.Experiments.Receive.Connections.Payloads.Converters;

internal sealed record class HelloPayload : IVoicePayload
{
    [JsonRequired]
    [JsonConverter(typeof(HeartbeatIntervalJsonConverter))]
    [JsonPropertyName("heartbeat_interval")]
    public required TimeSpan HeartbeatInterval { get; set; }
}
