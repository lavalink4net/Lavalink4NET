namespace Lavalink4NET.Experiments.Receive.Connections;

using System.Globalization;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Lavalink4NET.Clients;
using Lavalink4NET.Experiments.Receive.Connections.Features;
using Lavalink4NET.Experiments.Receive.Connections.Payloads;
using Lavalink4NET.Experiments.Receive.Sessions;
using Microsoft.AspNetCore.Connections.Features;

internal sealed class VoiceConnectionHandler : IVoiceConnectionHandler
{
    private readonly IVoiceServerSessionManager _sessionManager;
    private readonly IVoiceProtocolHandler _protocolHandler;
    private readonly IHttpMessageHandlerFactory _httpMessageHandlerFactory;
    private readonly ILogger<VoiceConnectionHandler> _logger;

    public VoiceConnectionHandler(
        IVoiceServerSessionManager sessionManager,
        IVoiceProtocolHandler protocolHandler,
        IHttpMessageHandlerFactory httpMessageHandlerFactory,
        ILogger<VoiceConnectionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(sessionManager);
        ArgumentNullException.ThrowIfNull(protocolHandler);
        ArgumentNullException.ThrowIfNull(httpMessageHandlerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _sessionManager = sessionManager;
        _protocolHandler = protocolHandler;
        _httpMessageHandlerFactory = httpMessageHandlerFactory;
        _logger = logger;
    }

    public async ValueTask ProcessAsync(
        VoiceConnectionContext connectionContext,
        IVoiceConnectionHandle connectionHandle,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(connectionContext);

        var payloadResult = await _protocolHandler
            .ReadAsync(connectionContext, cancellationToken)
            .ConfigureAwait(false);

        if (!payloadResult.IsSuccess)
        {
            throw new WebSocketException($"Failed to read initial payload: {payloadResult.CloseStatus} {payloadResult.CloseStatusDescription}");
        }

        if (payloadResult.Payload is not IdentifyPayload identifyPayload)
        {
            throw new WebSocketException("Expected identify payload.");
        }

        if (!Guid.TryParseExact(identifyPayload.Token, "N", result: out var token) ||
            !_sessionManager.TryResolve(token, out var guildId, out var voiceServer))
        {
            throw new WebSocketException("Invalid session id.");
        }

        string sourceConnectionId;
        if (connectionContext.Features.Get<IConnectionIdFeature>() is { } connectionIdFeature)
        {
            sourceConnectionId = connectionIdFeature.ConnectionId;
        }
        else
        {
            sourceConnectionId = CorrelationIdGenerator.GetNextId();
            connectionContext.Features.Set<IConnectionIdFeature>(new ConnectionIdFeature { ConnectionId = sourceConnectionId });
        }

        var remoteConnectionId = CorrelationIdGenerator.GetNextId();
        var sourceLabel = $"Local/{sourceConnectionId}@{guildId}";
        var remoteLabel = $"Remote/{sourceConnectionId}@{guildId}";

        connectionContext.Features.Set<IGuildIdFeature>(new GuildIdFeature(guildId));
        connectionContext.Features.Set<IConnectionLabelFeature>(new ConnectionLabelFeature(sourceLabel));

        using var gatewaySocket = new ClientWebSocket();
        using var httpMessageHandler = _httpMessageHandlerFactory.CreateHandler();
        using var httpMessageInvoker = new HttpMessageInvoker(httpMessageHandler);
        var version = connectionContext.Features.Get<IVoiceGatewayVersionFeature>()?.Version;

        var uri = BuildUri(voiceServer, version);

        await gatewaySocket
            .ConnectAsync(uri, httpMessageInvoker, cancellationToken)
            .ConfigureAwait(false);

        var remoteConnectionContext = new VoiceConnectionContext(gatewaySocket);

        remoteConnectionContext.Features.Set<IConnectionIdFeature>(new ConnectionIdFeature { ConnectionId = remoteConnectionId, });
        remoteConnectionContext.Features.Set<IConnectionLabelFeature>(new ConnectionLabelFeature(remoteLabel));
        remoteConnectionContext.Features.Set<IVoiceGatewayVersionFeature>(new VoiceGatewayVersionFeature(version ?? 4));
        remoteConnectionContext.Features.Set<IGuildIdFeature>(new GuildIdFeature(guildId));

        var remoteIdentifyPayload = new IdentifyPayload
        {
            GuildId = guildId,
            Token = voiceServer.Token,
            SessionId = identifyPayload.SessionId,
            UserId = identifyPayload.UserId,
        };

        await _protocolHandler
            .WriteAsync(remoteConnectionContext, remoteIdentifyPayload, cancellationToken)
            .ConfigureAwait(false);

        var task1 = ProxyAsync(connectionContext, remoteConnectionContext, connectionHandle, isRemote: false, cancellationToken).AsTask();
        var task2 = ProxyAsync(remoteConnectionContext, connectionContext, connectionHandle, isRemote: true, cancellationToken).AsTask();

        await Task
            .WhenAny(task1, task2)
            .ConfigureAwait(false);
    }

    private async ValueTask ProxyAsync(
        VoiceConnectionContext sourceConnectionContext,
        VoiceConnectionContext destinationConnectionContext,
        IVoiceConnectionHandle connectionHandle,
        bool isRemote = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sourceConnectionContext);
        ArgumentNullException.ThrowIfNull(destinationConnectionContext);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _protocolHandler
                .ReadAsync(sourceConnectionContext, cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to read payload: {CloseStatus} {CloseStatusDescription}", result.CloseStatus, result.CloseStatusDescription);
                break;
            }

            var receivedPayload = result.Payload;

            switch (receivedPayload)
            {
                case ReadyPayload payload when isRemote:
                    var localEndPoint = await connectionHandle
                        .SetReadyAsync(payload, cancellationToken)
                        .ConfigureAwait(false);

                    receivedPayload = new ReadyPayload
                    {
                        Ssrc = payload.Ssrc,
                        Ip = localEndPoint.Address.ToString(),
                        Port = localEndPoint.Port,
                        Modes = payload.Modes,
                    };

                    break;

                case SessionDescriptionPayload payload when isRemote:
                    await connectionHandle
                        .SetSessionDescriptionAsync(payload, cancellationToken)
                        .ConfigureAwait(false);

                    break;

                case SelectProtocolPayload payload when !isRemote:
                    var remoteEndPoint = await connectionHandle
                        .SelectProtocolAsync(payload, cancellationToken)
                        .ConfigureAwait(false);

                    receivedPayload = new SelectProtocolPayload
                    {
                        Data = new SelectProtocolData
                        {
                            Address = remoteEndPoint.Address.ToString(),
                            Port = remoteEndPoint.Port,
                            Mode = payload.Data.Mode,
                        },
                        Protocol = payload.Protocol,
                    };

                    break;
            }

            await _protocolHandler
                .WriteAsync(destinationConnectionContext, receivedPayload, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static Uri BuildUri(VoiceServer voiceServer, int? version = null)
    {
        var host = voiceServer.Endpoint;
        var endPointSeparatorIndex = host.LastIndexOf(':');
        var port = default(int?); // WSS default port

        if (endPointSeparatorIndex is not -1)
        {
            host = host[..endPointSeparatorIndex];
            port = int.Parse(voiceServer.Endpoint[(endPointSeparatorIndex + 1)..]);
        }

        var uriBuilder = new UriBuilder
        {
            Scheme = Uri.UriSchemeWss,
            Host = host,
            Port = port ?? 443,
            Path = "/",
        };

        if (version.HasValue)
        {
            var queryParameters = HttpUtility.ParseQueryString(string.Empty);
            queryParameters["v"] = version.Value.ToString(CultureInfo.InvariantCulture);
            uriBuilder.Query = queryParameters.ToString();
        }

        return uriBuilder.Uri;
    }
}
