namespace Lavalink4NET.Protocol.Models;

using System;
using System.Text.Json.Serialization;
using Lavalink4NET.Protocol.Converters;

public sealed record class SessionModel(
    [property: JsonRequired]
    [property: JsonPropertyName("resuming")]
    bool IsSessionResumptionEnabled,

    [property: JsonRequired]
    [property: JsonPropertyName("timeout")]
    [property: JsonConverter(typeof(NullableSecondsDurationJsonConverter))]
    TimeSpan? SessionTimeout);