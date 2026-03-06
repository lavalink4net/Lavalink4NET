namespace Lavalink4NET.Protocol.Requests;

using System.Text.Json.Serialization;
using Lavalink4NET.Protocol.Converters;

public sealed record class SessionUpdateProperties
{
    [JsonPropertyName("resuming")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonConverter(typeof(OptionalJsonConverter<bool>))]
    public Optional<bool> IsSessionResumptionEnabled { get; set; }

    [JsonPropertyName("timeout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonConverter(typeof(OptionalJsonConverter<TimeSpan?, NullableSecondsDurationJsonConverter>))]
    public Optional<TimeSpan?> Timeout { get; set; }
}