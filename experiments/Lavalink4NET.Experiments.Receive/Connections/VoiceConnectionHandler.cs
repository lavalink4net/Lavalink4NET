namespace Lavalink4NET.Experiments.Receive.Connections;

using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Experiments.Receive.Connections.Payloads;
using Lavalink4NET.Experiments.Receive.Sessions;

internal sealed class VoiceConnectionHandler : IVoiceConnectionHandler
{
    private readonly IVoiceServerSessionManager _sessionManager;
    private readonly IVoiceProtocolHandler _protocolHandler;

    public VoiceConnectionHandler(
        IVoiceServerSessionManager sessionManager,
        IVoiceProtocolHandler protocolHandler)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(protocolHandler);

        _sessionManager = sessionManager;
        _protocolHandler = protocolHandler;
    }

    public async ValueTask ProcessAsync(WebSocket webSocket, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(webSocket);

        var payload = await _protocolHandler
            .ReadAsync(webSocket, cancellationToken)
            .ConfigureAwait(false);

        if (payload is not IdentifyPayload identifyPayload)
        {
            throw new WebSocketException("Expected identify payload.");
        }

        if (!Guid.TryParseExact(identifyPayload.Token, "N", out var token) ||
            !_sessionManager.TryResolve(token, out var guildId, out var voiceServer))
        {
            throw new WebSocketException("Invalid session id.");
        }

        ;
    }
}
