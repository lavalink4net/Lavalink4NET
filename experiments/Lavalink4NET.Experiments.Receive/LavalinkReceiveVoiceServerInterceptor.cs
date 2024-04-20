namespace Lavalink4NET.Experiments.Receive;

using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Players;
using Microsoft.Extensions.Options;

internal sealed class LavalinkReceiveVoiceServerInterceptor : ILavalinkVoiceServerInterceptor
{
    private readonly IVoiceServerSessionManager _sessionManager;
    private readonly LavalinkVoiceServerOptions _options;
    private readonly ILogger<LavalinkReceiveVoiceServerInterceptor> _logger;

    public LavalinkReceiveVoiceServerInterceptor(
        IVoiceServerSessionManager sessionManager,
        IOptions<LavalinkVoiceServerOptions> options,
        ILogger<LavalinkReceiveVoiceServerInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _sessionManager = sessionManager;
        _options = options.Value;
        _logger = logger;
    }

    public ValueTask<VoiceServer> InterceptAsync(
        ulong guildId,
        VoiceServer voiceServer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sessionToken = _sessionManager.Allocate(guildId, voiceServer);
        var proxiedVoiceServer = new VoiceServer(sessionToken.ToString("N"), $"{_options.BindAddress}:{_options.Port}");

        _logger.LogInformation(
            "Mapping voice server '{OriginalEndpoint}' ({OriginalToken}) to '{ProxiedEndpoint}' ({ProxiedToken})",
            voiceServer.Endpoint, voiceServer.Token, proxiedVoiceServer.Endpoint, proxiedVoiceServer.Token);

        return new ValueTask<VoiceServer>(proxiedVoiceServer);
    }
}
