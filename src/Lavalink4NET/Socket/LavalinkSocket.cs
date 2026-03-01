namespace Lavalink4NET.Socket;

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Lavalink4NET.Events;
using Lavalink4NET.Protocol;
using Lavalink4NET.Protocol.Payloads;
using Lavalink4NET.Rest;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class LavalinkSocket : ILavalinkSocket
{
    private readonly Channel<IPayload> _channel;
    private readonly IHttpMessageHandlerFactory _httpMessageHandlerFactory;
    private readonly IReconnectStrategy _reconnectStrategy;
    private readonly ISystemClock _systemClock;
    private readonly ILogger<LavalinkSocket> _logger;
    private readonly LavalinkSocketOptions _options;
    private bool _disposed;

    public event AsyncEventHandler<ConnectionClosedEventArgs>? ConnectionClosed;

    public event AsyncEventHandler<ConnectionStartedEventArgs>? ConnectionStarted;

    public LavalinkSocket(
        IHttpMessageHandlerFactory httpMessageHandlerFactory,
        IReconnectStrategy reconnectStrategy,
        ISystemClock systemClock,
        ILogger<LavalinkSocket> logger,
        IOptions<LavalinkSocketOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpMessageHandlerFactory);
        ArgumentNullException.ThrowIfNull(reconnectStrategy);
        ArgumentNullException.ThrowIfNull(systemClock);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        Label = options.Value.Label ?? $"Lavalink-{CorrelationIdGenerator.GetNextId()}";
        _httpMessageHandlerFactory = httpMessageHandlerFactory;
        _reconnectStrategy = reconnectStrategy;
        _systemClock = systemClock;
        _logger = logger;
        _options = options.Value;
        _channel = Channel.CreateUnbounded<IPayload>();
    }

    public string Label { get; }

    public async ValueTask<IPayload?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await _channel.Reader
                .ReadAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ChannelClosedException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var buffer = GC.AllocateUninitializedArray<byte>(_options.BufferSize);

        async ValueTask<WebSocket> ConnectWithRetryAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var attempt = 0;
            var interruptedSince = default(DateTimeOffset?);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.AttemptingToConnect(Label);

                var webSocket = new ClientWebSocket();

                var assemblyVersion = typeof(LavalinkSocket).Assembly.GetName().Version;
                webSocket.Options.SetRequestHeader("Authorization", _options.Passphrase);
                webSocket.Options.SetRequestHeader("User-Id", _options.UserId.ToString());
                webSocket.Options.SetRequestHeader("Client-Name", $"Lavalink4NET/{assemblyVersion}");

                if (_options.SessionId is not null)
                {
                    webSocket.Options.SetRequestHeader("Session-Id", _options.SessionId);
                }

                try
                {
#if NET7_0_OR_GREATER
                    var httpMessageHandler = _httpMessageHandlerFactory.CreateHandler(_options.HttpClientName);
                    var httpMessageInvoker = new HttpMessageInvoker(httpMessageHandler, disposeHandler: true);

                    await webSocket
                        .ConnectAsync(_options.Uri, httpMessageInvoker, cancellationToken)
                        .ConfigureAwait(false);
#else
                    await webSocket
                        .ConnectAsync(_options.Uri, cancellationToken)
                        .ConfigureAwait(false);
#endif

                    interruptedSince = null;
                    attempt = 0;

                    _logger.ConnectionEstablished(Label);
                    await InvokeConnectionStartedAsync(new ConnectionStartedEventArgs(this, _options.Uri));

                    return webSocket;
                }
                catch (Exception exception)
                {
                    if (attempt == 0)
                    {
                        _logger.FailedToConnectFirstTime(Label, exception);
                    }
                    else
                    {
                        _logger.FailedToConnect(Label, exception);
                    }

                    var connectionClosedEventArgs = new ConnectionClosedEventArgs(
                        lavalinkSocket: this,
                        closeStatus: webSocket.CloseStatus,
                        closeStatusDescription: webSocket.CloseStatusDescription,
                        exception: exception);

                    await InvokeConnectionClosedAsync(connectionClosedEventArgs).ConfigureAwait(false);

                    if (!connectionClosedEventArgs.AllowReconnect)
                    {
                        throw;
                    }

                    interruptedSince ??= _systemClock.UtcNow;

                    var reconnectDelay = await _reconnectStrategy
                        .GetNextDelayAsync(interruptedSince.Value, ++attempt, cancellationToken)
                        .ConfigureAwait(false);

                    if (reconnectDelay is null)
                    {
                        throw;
                    }

                    if (reconnectDelay.Value > TimeSpan.Zero)
                    {
                        _logger.WaitingBeforeReconnect(Label, reconnectDelay.Value.TotalSeconds);

                        await Task
                            .Delay(reconnectDelay.Value, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            using var webSocket = await ConnectWithRetryAsync(
                cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await ReceiveInternalAsync(webSocket, buffer, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReceiveInternalAsync(WebSocket webSocket, Memory<byte> receiveBuffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(webSocket);
        Debug.Assert(webSocket.State is WebSocketState.Open);

        using var _ = webSocket;

        try
        {
            while (true)
            {
                var receiveResult = await webSocket
                    .ReceiveAsync(receiveBuffer, cancellationToken)
                    .ConfigureAwait(false);

                if (receiveResult.MessageType is not WebSocketMessageType.Text)
                {
                    if (receiveResult.MessageType is WebSocketMessageType.Close)
                    {
                        return;
                    }

                    ThrowIfInvalidMessageType();
                }

                if (!receiveResult.EndOfMessage)
                {
                    ThrowIfNotEndOfMessage(Encoding.UTF8.GetString(receiveBuffer.Span[..receiveResult.Count]));
                }

                var buffer = receiveBuffer[..receiveResult.Count];
                await ProcessAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception exception)
        {
            _logger.CommunicationError(Label, exception);

            var connectionClosedEventArgs = new ConnectionClosedEventArgs(
                lavalinkSocket: this,
                closeStatus: webSocket.CloseStatus,
                closeStatusDescription: webSocket.CloseStatusDescription,
                exception: exception);

            await InvokeConnectionClosedAsync(connectionClosedEventArgs).ConfigureAwait(false);

            _channel.Writer.TryComplete(exception);
        }
        finally
        {
            _channel.Writer.TryComplete();
        }

        [DoesNotReturn]
        static void ThrowIfNotEndOfMessage(string content) => throw new InvalidDataException($"Received a partial payload: {content}.");

        [DoesNotReturn]
        static void ThrowIfInvalidMessageType() => throw new InvalidDataException("Received bad frame type.");
    }

    private ValueTask ProcessAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Deserialize(
            utf8Json: buffer.Span,
            jsonTypeInfo: ProtocolSerializerContext.Default.IPayload);

        var result = _channel.Writer.TryWrite(payload!);
        Debug.Assert(result);

        return default;
    }

    private async Task InvokeConnectionClosedAsync(ConnectionClosedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        try
        {
            await ConnectionClosed
                .InvokeAsync(this, eventArgs)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.ErrorConnectionClosedEvent(Label, exception);
        }
    }

    private async Task InvokeConnectionStartedAsync(ConnectionStartedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        try
        {
            await ConnectionStarted
                .InvokeAsync(this, eventArgs)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.ErrorConnectionStartedEvent(Label, exception);
        }
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _channel.Writer.TryComplete();
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

internal static partial class Logging
{
    [LoggerMessage(1, LogLevel.Debug, "[{Label}] Attempting to connect to Lavalink node...", EventName = nameof(AttemptingToConnect))]
    public static partial void AttemptingToConnect(this ILogger<LavalinkSocket> logger, string label);

    [LoggerMessage(2, LogLevel.Information, "[{Label}] Connection to Lavalink node established.", EventName = nameof(ConnectionEstablished))]
    public static partial void ConnectionEstablished(this ILogger<LavalinkSocket> logger, string label);

    [LoggerMessage(3, LogLevel.Debug, "[{Label}] Failed to connect to the Lavalink node.", EventName = nameof(FailedToConnect))]
    public static partial void FailedToConnect(this ILogger<LavalinkSocket> logger, string label, Exception exception);

    [LoggerMessage(4, LogLevel.Debug, "[{Label}] Waiting {Duration} second(s) before reconnecting to the node.", EventName = nameof(WaitingBeforeReconnect))]
    public static partial void WaitingBeforeReconnect(this ILogger<LavalinkSocket> logger, string label, double duration);

    [LoggerMessage(5, LogLevel.Error, "[{Label}] An error occurred during communication with the Lavalink server.", EventName = nameof(CommunicationError))]
    public static partial void CommunicationError(this ILogger<LavalinkSocket> logger, string label, Exception exception);

    [LoggerMessage(6, LogLevel.Warning, "[{Label}] An error occurred while dispatching the ConnectionClosed event.", EventName = nameof(ErrorConnectionClosedEvent))]
    public static partial void ErrorConnectionClosedEvent(this ILogger<LavalinkSocket> logger, string label, Exception exception);

    [LoggerMessage(7, LogLevel.Warning, "[{Label}] Failed to connect to the Lavalink node.", EventName = nameof(FailedToConnectFirstTime))]
    public static partial void FailedToConnectFirstTime(this ILogger<LavalinkSocket> logger, string label, Exception exception);

    [LoggerMessage(8, LogLevel.Warning, "[{Label}] An error occurred while dispatching the ConnectionStarted event.", EventName = nameof(ErrorConnectionStartedEvent))]
    public static partial void ErrorConnectionStartedEvent(this ILogger<LavalinkSocket> logger, string label, Exception exception);
}
