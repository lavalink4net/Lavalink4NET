namespace Lavalink4NET.Players;

using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;

internal sealed class LavalinkVoiceServerInterceptor : ILavalinkVoiceServerInterceptor
{
    public ValueTask<VoiceServer> InterceptAsync(
        ulong guildId,
        VoiceServer voiceServer,
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<VoiceServer>(voiceServer);
    }
}