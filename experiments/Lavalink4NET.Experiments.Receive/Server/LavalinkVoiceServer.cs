namespace Lavalink4NET.Experiments.Receive.Server;

using System;
using System.Diagnostics.Metrics;
using System.Globalization;
using Lavalink4NET.Experiments.Receive.Connections;
using Lavalink4NET.Experiments.Receive.Connections.Discovery;
using Lavalink4NET.Experiments.Receive.Connections.Features;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

internal sealed class LavalinkVoiceServer : IHttpApplication<HttpContext>, ILavalinkVoiceServer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IVoiceConnectionHandler _voiceConnectionHandler;
    private readonly IIpDiscoveryService _ipDiscoveryService;
    private readonly IServer _server;
    private readonly WebSocketMiddleware _webSocketMiddleware;

    public LavalinkVoiceServer(
        IVoiceConnectionHandler voiceConnectionHandler,
        IIpDiscoveryService ipDiscoveryService,
        ILoggerFactory loggerFactory,
        IOptions<LavalinkVoiceServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(voiceConnectionHandler);

        _voiceConnectionHandler = voiceConnectionHandler;
        _ipDiscoveryService = ipDiscoveryService;
        var services = new ServiceCollection();

        // HTTP Kestrel Host
        services.TryAddSingleton<IHostEnvironment, LavalinkWebHostEnvironment>();
        services.TryAddSingleton<IMeterFactory, LavalinkMeterFactory>();

        // Logging
        services.TryAddSingleton(loggerFactory);
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));

        var builder = new LavalinkKestrelWebHostBuilder(services);

        builder.UseKestrel((context, serverOptions) =>
        {
            serverOptions.ListenLocalhost(options.Value.Port, x => x.UseHttps());
        });

        var webSocketOptions = new WebSocketOptions { };

        _webSocketMiddleware = new WebSocketMiddleware(
            next: ProcessRequestInternalAsync,
            options: Options.Create(webSocketOptions),
            loggerFactory: loggerFactory);

        _serviceProvider = services.BuildServiceProvider();
        _server = _serviceProvider.GetRequiredService<IServer>();
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        await _server
            .StartAsync(this, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        await _server
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

        if (!httpContext.Request.Query.TryGetValue("v", out var versionValue))
        {
            httpContext.Response.StatusCode = StatusCodes.Status200OK;

            await httpContext.Response
                .WriteAsync("Lavalink4NET Voice Proxy Server")
                .ConfigureAwait(false);

            return;
        }

        var cancellationToken = httpContext.RequestAborted;
        cancellationToken.ThrowIfCancellationRequested();

        if (!httpContext.WebSockets.IsWebSocketRequest)
        {
            httpContext.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            httpContext.Response.Headers[HeaderNames.Upgrade] = "websocket";
            httpContext.Response.Headers[HeaderNames.Connection] = "Upgrade";

            return;
        }

        if (!int.TryParse(versionValue.ToString(), CultureInfo.InvariantCulture, out var version) || version is not 3 and not 4)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            await httpContext.Response
                .WriteAsync("Invalid version parameter.")
                .ConfigureAwait(false);

            return;
        }

        var webSocketAcceptContext = new WebSocketAcceptContext { };

        var webSocket = await httpContext.WebSockets
            .AcceptWebSocketAsync(webSocketAcceptContext)
            .ConfigureAwait(false);

        var connectionContext = new VoiceConnectionContext(webSocket);

        connectionContext.Features.Set(httpContext.Features.GetRequiredFeature<IConnectionIdFeature>());
        connectionContext.Features.Set<IVoiceGatewayVersionFeature>(new VoiceGatewayVersionFeature(version));

        await _voiceConnectionHandler
            .ProcessAsync(connectionContext, new VoiceConnectionHandle(_ipDiscoveryService), cancellationToken)
            .ConfigureAwait(false);
    }
}
