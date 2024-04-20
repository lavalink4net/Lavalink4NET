namespace Lavalink4NET.Experiments.Receive;

using System;
using System.Diagnostics.Metrics;
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
    private readonly IVoiceServerSessionManager _serverSessionManager;
    private readonly IServer _server;
    private readonly WebSocketMiddleware _webSocketMiddleware;

    public LavalinkVoiceServer(
        IVoiceServerSessionManager serverSessionManager,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(serverSessionManager);

        _serverSessionManager = serverSessionManager;

        var services = new ServiceCollection();

        // HTTP Kestrel Host
        services.TryAddSingleton<IHostEnvironment, LavalinkWebHostEnvironment>();
        services.TryAddSingleton<IMeterFactory, LavalinkMeterFactory>();

        // Logging
        services.TryAddSingleton(loggerFactory);
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));

        var builder = new LavalinkKestrelWebHostBuilder(services);

        builder.UseKestrel((context, options) =>
        {
            options.ListenLocalhost(16389, x => x.UseHttps());
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

        if (httpContext.Request.Path.Equals("/"))
        {
            httpContext.Response.StatusCode = StatusCodes.Status200OK;

            await httpContext.Response
                .WriteAsync("Lavalink4NET Voice Proxy Server")
                .ConfigureAwait(false);

            return;
        }

        if (!httpContext.Request.Path.Equals("/voice"))
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

            await httpContext.Response
                .WriteAsync("Not Found")
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

        var webSocketAcceptContext = new WebSocketAcceptContext { };

        var webSocket = await httpContext.WebSockets
            .AcceptWebSocketAsync(webSocketAcceptContext)
            .ConfigureAwait(false);
    }
}
