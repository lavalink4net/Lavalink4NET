namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

using System.Collections.Immutable;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(IVoicePayload))]
[JsonSerializable(typeof(IdentifyPayload))]
[JsonSerializable(typeof(ReadyPayload))]
[JsonSerializable(typeof(HelloPayload))]
[JsonSerializable(typeof(SelectProtocolPayload))]
[JsonSerializable(typeof(SessionDescriptionPayload))]
[JsonSerializable(typeof(ImmutableArray<int>))]
[JsonSerializable(typeof(SpeakingPayload))]
internal sealed partial class PayloadJsonSerializerContext : JsonSerializerContext
{
}
