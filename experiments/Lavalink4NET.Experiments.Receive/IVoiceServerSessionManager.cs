namespace Lavalink4NET.Experiments.Receive;

using Lavalink4NET.Clients;

interface IVoiceServerSessionManager
{
    Guid Allocate(ulong guildId, VoiceServer voiceServer);

    bool TryResolve(Guid sessionId, out ulong guildId, out VoiceServer voiceServer);
}
