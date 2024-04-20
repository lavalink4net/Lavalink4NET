namespace Lavalink4NET.Experiments.Receive.Connections.Payloads;

[Flags]
internal enum SpeakingFlags : byte
{
    None = 0,
    Microphone = 1 << 0,
    Soundshare = 1 << 1,
    Priority = 1 << 2
}
