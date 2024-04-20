namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

using System.Text.Json.Serialization;

[JsonSerializable(typeof(IVoicePayload))]
[JsonSerializable(typeof(IdentifyPayload))]
[JsonSerializable(typeof(ReadyPayload))]
[JsonSerializable(typeof(HelloPayload))]
[JsonSerializable(typeof(SelectProtocolPayload))]
internal sealed partial class PayloadJsonSerializerContext : JsonSerializerContext
{
}
