namespace Lavalink4NET.Experiments.Receive.Server;

using System.Threading;
using System.Threading.Tasks;

public interface ILavalinkVoiceServer
{
    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}