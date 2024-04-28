namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

public sealed record class HeartbeatAckPayload : IVoicePayload
{
    public ulong SequenceNumber { get; set; }
}
