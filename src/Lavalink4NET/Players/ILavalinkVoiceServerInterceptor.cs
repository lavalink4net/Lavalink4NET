namespace Lavalink4NET.Players;

using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;

public interface ILavalinkVoiceServerInterceptor
{
    ValueTask<VoiceServer> InterceptAsync(
        ulong guildId,
        VoiceServer voiceServer,
        CancellationToken cancellationToken = default);
}
