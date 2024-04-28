namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

using System.Text.Json.Nodes;

public sealed record class DynamicVoicePayload : IVoicePayload
{
    public int OperationCode { get; set; }

    public JsonObject? Data { get; set; }
}
