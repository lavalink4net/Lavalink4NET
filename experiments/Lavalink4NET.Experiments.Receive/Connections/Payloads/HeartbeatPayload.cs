namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

public sealed record class HeartbeatPayload : IVoicePayload
{
    public ulong SequenceNumber { get; set; }
}
