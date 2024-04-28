namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

using System.Text.Json.Serialization;
using Lavalink4NET.Experiments.Receive.Connections.Payloads.Converters;

internal sealed record class IdentifyPayload : IVoicePayload
{
    [JsonRequired]
    [JsonPropertyName("server_id")]
    [JsonConverter(typeof(SnowflakeJsonConverter))]
    public required ulong GuildId { get; set; }

    [JsonRequired]
    [JsonPropertyName("user_id")]
    [JsonConverter(typeof(SnowflakeJsonConverter))]
    public required ulong UserId { get; set; }

    [JsonRequired]
    [JsonPropertyName("session_id")]
    public required string SessionId { get; set; }

    [JsonRequired]
    [JsonPropertyName("token")]
    public required string Token { get; set; }
}