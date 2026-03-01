namespace Lavalink4NET.Protocol.Requests;

using System.Text.Json.Serialization;

public sealed record class VoiceStateProperties(
    [property: JsonRequired]
    [property: JsonPropertyName("token")]
    string Token,

    [property: JsonRequired]
    [property: JsonPropertyName("endpoint")]
    string Endpoint,

    [property: JsonRequired]
    [property: JsonPropertyName("sessionId")]
    string SessionId,

    [property: JsonPropertyName("channelId")]
    string? ChannelId);