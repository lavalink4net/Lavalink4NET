namespace Lavalink4NET.Experiments.Receive;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

internal sealed class LavalinkVoiceServer : IHttpApplication<HttpContext>
{
    private readonly IVoiceServerSessionManager _serverSessionManager;
    private readonly KestrelServer _kestrelServer;
    private readonly WebSocketMiddleware _webSocketMiddleware;

    public LavalinkVoiceServer(
        IVoiceServerSessionManager serverSessionManager,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(serverSessionManager);

        _serverSessionManager = serverSessionManager;

        var kestrelServerOptions = new KestrelServerOptions { AddServerHeader = false, };
        kestrelServerOptions.ListenLocalhost(16389);

        var socketTransportOptions = new SocketTransportOptions { Backlog = 4, };
        var socketTransportFactory = new SocketTransportFactory(Options.Create(socketTransportOptions), loggerFactory);

        _kestrelServer = new KestrelServer(
            options: Options.Create(kestrelServerOptions),
            transportFactory: socketTransportFactory,
            loggerFactory: loggerFactory);

        var webSocketOptions = new WebSocketOptions { };

        _webSocketMiddleware = new WebSocketMiddleware(
            next: ProcessRequestInternalAsync,
            options: Options.Create(webSocketOptions),
            loggerFactory: loggerFactory);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        await _kestrelServer
            .StartAsync(this, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await _kestrelServer
            .StopAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    HttpContext IHttpApplication<HttpContext>.CreateContext(IFeatureCollection contextFeatures)
    {
        ArgumentNullException.ThrowIfNull(contextFeatures);

        return new DefaultHttpContext(contextFeatures);
    }

    void IHttpApplication<HttpContext>.DisposeContext(HttpContext context, Exception? exception)
    {
    }

    Task IHttpApplication<HttpContext>.ProcessRequestAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _webSocketMiddleware.Invoke(context);
    }

    private async Task ProcessRequestInternalAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var cancellationToken = httpContext.RequestAborted;
        cancellationToken.ThrowIfCancellationRequested();

        if (!httpContext.WebSockets.IsWebSocketRequest)
        {
            httpContext.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            httpContext.Response.Headers[HeaderNames.Upgrade] = "websocket";
            httpContext.Response.Headers[HeaderNames.Connection] = "Upgrade";

            return;
        }

        var webSocketAcceptContext = new WebSocketAcceptContext { };

        var webSocket = await httpContext.WebSockets
            .AcceptWebSocketAsync(webSocketAcceptContext)
            .ConfigureAwait(false);
    }
}