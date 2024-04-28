namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

using System.Text.Json.Serialization;

internal sealed record class SelectProtocolData
{
    [JsonRequired]
    [JsonPropertyName("address")]
    public required string Address { get; set; }

    [JsonRequired]
    [JsonPropertyName("port")]
    public required int Port { get; set; }

    [JsonRequired]
    [JsonPropertyName("mode")]
    public required string Mode { get; set; }
}