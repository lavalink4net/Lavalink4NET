namespace Lavalink4NET.Experiments.Receive.Server;

using System.Threading;
using System.Threading.Tasks;

internal sealed class LavalinkVoiceServerHost : IHostedService
{
    private readonly ILavalinkVoiceServer _lavalinkVoiceServer;

    public LavalinkVoiceServerHost(ILavalinkVoiceServer lavalinkVoiceServer)
    {
        ArgumentNullException.ThrowIfNull(lavalinkVoiceServer);

        _lavalinkVoiceServer = lavalinkVoiceServer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return _lavalinkVoiceServer.StartAsync(cancellationToken).AsTask();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return _lavalinkVoiceServer.StopAsync(cancellationToken).AsTask();
    }
}
