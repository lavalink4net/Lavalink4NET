namespace Lavalink4NET.Experiments.Receive;

using System.Collections.Concurrent;
using Lavalink4NET.Clients;

internal sealed class VoiceServerSessionManager : IVoiceServerSessionManager
{
    private readonly ConcurrentDictionary<Guid, (ulong GuildId, VoiceServer VoiceServer)> _voiceServers;

    public VoiceServerSessionManager()
    {
        _voiceServers = new ConcurrentDictionary<Guid, (ulong GuildId, VoiceServer VoiceServer)>();
    }

    public Guid Allocate(ulong guildId, VoiceServer voiceServer)
    {
        var sessionId = Guid.NewGuid();
        _voiceServers.TryAdd(sessionId, (guildId, voiceServer));

        return sessionId;
    }

    public bool TryResolve(Guid sessionId, out ulong guildId, out VoiceServer voiceServer)
    {
        if (_voiceServers.TryGetValue(sessionId, out var pair))
        {
            guildId = pair.GuildId;
            voiceServer = pair.VoiceServer;

            return true;
        }

        guildId = default;
        voiceServer = default;

        return false;
    }
}