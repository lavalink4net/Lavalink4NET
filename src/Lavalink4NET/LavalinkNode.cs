namespace Lavalink4NET;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol;
using Lavalink4NET.Protocol.Payloads;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Protocol.Requests;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Rest.Entities.Usage;
using Lavalink4NET.Socket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class LavalinkNode : IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdownCancellationTokenSource;
    private readonly CancellationToken _shutdownCancellationToken;
    private readonly LavalinkNodeOptions _options;
    private readonly LavalinkNodeServiceContext _serviceContext;
    private readonly ILavalinkApiClient _apiClient;
    private readonly LavalinkApiEndpoints _apiEndpoints;
    private readonly ILogger<LavalinkNode> _logger;
    private readonly Stopwatch _readyStopwatch;
    private TaskCompletionSource<string> _readyTaskCompletionSource;
    private Task? _executeTask;
    private bool _disposed;

    internal event AsyncEventHandler<ConnectionClosedEventArgs>? SocketConnectionClosed;
    internal event AsyncEventHandler<ConnectionStartedEventArgs>? SocketConnectionStarted;
    internal event AsyncEventHandler<ConnectionReadyEventArgs>? SocketConnectionReady;

    public LavalinkNode(
        LavalinkNodeServiceContext serviceContext,
        ILavalinkApiClient apiClient,
        IOptions<LavalinkNodeOptions> options,
        LavalinkApiEndpoints apiEndpoints,
        ILogger<LavalinkNode> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceContext);
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _readyTaskCompletionSource = new TaskCompletionSource<string>(
            creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

        _serviceContext = serviceContext;
        _apiClient = apiClient;
        _apiEndpoints = apiEndpoints;
        _options = options.Value;
        _logger = logger;

        _readyStopwatch = new Stopwatch();

        Label = _options.Label ?? $"Lavalink-{CorrelationIdGenerator.GetNextId()}";

        _shutdownCancellationTokenSource = new CancellationTokenSource();
        _shutdownCancellationToken = _shutdownCancellationTokenSource.Token;
    }

    public string Label { get; }

    public bool IsReady => _readyTaskCompletionSource.Task.IsCompletedSuccessfully;

    public string? SessionId { get; private set; }

    public async ValueTask WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        await _readyTaskCompletionSource.Task
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string SerializePayload(IPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var jsonWriterOptions = new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var arrayBufferWriter = new ArrayBufferWriter<byte>();
        var utf8JsonWriter = new Utf8JsonWriter(arrayBufferWriter, jsonWriterOptions);

        JsonSerializer.Serialize(utf8JsonWriter, payload, ProtocolSerializerContext.Default.IPayload);

        return Encoding.UTF8.GetString(arrayBufferWriter.WrittenSpan);
    }

    private async ValueTask ProcessEventAsync(IEventPayload payload, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(payload);

        var player = await _serviceContext.PlayerManager
            .GetPlayerAsync(payload.GuildId, cancellationToken)
            .ConfigureAwait(false);

        if (player is null)
        {
            _logger.ReceivedEventPayloadForNonRegisteredPlayer(Label, payload.GuildId);
            return;
        }

        var task = payload switch
        {
            TrackEndEventPayload trackEvent => ProcessTrackEndEventAsync(player, trackEvent, cancellationToken),
            TrackStartEventPayload trackEvent => ProcessTrackStartEventAsync(player, trackEvent, cancellationToken),
            TrackStuckEventPayload trackEvent => ProcessTrackStuckEventAsync(player, trackEvent, cancellationToken),
            TrackExceptionEventPayload trackEvent => ProcessTrackExceptionEventAsync(player, trackEvent, cancellationToken),
            WebSocketClosedEventPayload closedEvent => ProcessWebSocketClosedEventAsync(player, closedEvent, cancellationToken),
            _ => ValueTask.CompletedTask,
        };

        await task.ConfigureAwait(false);
    }

    private async ValueTask ProcessPayloadAsync(ILavalinkSocket socket, IPayload payload, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(payload);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.ReceivedPayloadFromLavalinkNode(Label, SerializePayload(payload));
        }

        if (payload is ReadyPayload readyPayload)
        {
            if (!_readyTaskCompletionSource.TrySetResult(readyPayload.SessionId))
            {
                _logger.MultipleReadyPayloadsReceived(Label);
            }

            SessionId = readyPayload.SessionId;

            // Enable resuming, if wanted
            if (_options.ResumptionOptions.IsEnabled && !readyPayload.SessionResumed)
            {
                var sessionUpdateProperties = new SessionUpdateProperties
                {
                    IsSessionResumptionEnabled = true,
                    Timeout = _options.ResumptionOptions.Timeout.Value,
                };

                await _apiClient
                    .UpdateSessionAsync(readyPayload.SessionId, sessionUpdateProperties, cancellationToken)
                    .ConfigureAwait(false);
            }

            _logger.Ready(Label, SessionId);
            await InvokeConnectionReadyAsync(this, new ConnectionReadyEventArgs(socket, readyPayload.SessionId, readyPayload.SessionResumed));
        }

        if (SessionId is null)
        {
            _logger.PayloadReceivedBeforeReadyPayload(Label);
            return;
        }

        if (payload is IEventPayload eventPayload)
        {
            await ProcessEventAsync(eventPayload, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (payload is PlayerUpdatePayload playerUpdatePayload)
        {
            var player = await _serviceContext.PlayerManager
                .GetPlayerAsync(playerUpdatePayload.GuildId, cancellationToken)
                .ConfigureAwait(false);

            if (player is null)
            {
                _logger.ReceivedPlayerUpdatePayloadForNonRegisteredPlayer(Label, playerUpdatePayload.GuildId);
                return;
            }

            if (player is ILavalinkPlayerListener playerListener)
            {
                var state = playerUpdatePayload.State;

                await playerListener
                    .NotifyPlayerUpdateAsync(state.AbsoluteTimestamp, state.Position, state.IsConnected, state.Latency, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (payload is StatisticsPayload statisticsPayload)
        {
            await ProcessStatisticsPayloadAsync(statisticsPayload, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ProcessStatisticsPayloadAsync(StatisticsPayload statisticsPayload, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(statisticsPayload);

        var memoryUsage = new ServerMemoryUsageStatistics(
            FreeMemory: statisticsPayload.MemoryUsage.FreeMemory,
            UsedMemory: statisticsPayload.MemoryUsage.UsedMemory,
            AllocatedMemory: statisticsPayload.MemoryUsage.AllocatedMemory,
            ReservableMemory: statisticsPayload.MemoryUsage.ReservableMemory);

        var processorUsage = new ServerProcessorUsageStatistics(
            CoreCount: statisticsPayload.ProcessorUsage.CoreCount,
            SystemLoad: statisticsPayload.ProcessorUsage.SystemLoad,
            LavalinkLoad: statisticsPayload.ProcessorUsage.LavalinkLoad);

        var frameStatistics = statisticsPayload.FrameStatistics is null ? default(ServerFrameStatistics?) : new ServerFrameStatistics(
            SentFrames: statisticsPayload.FrameStatistics.SentFrames,
            NulledFrames: statisticsPayload.FrameStatistics.NulledFrames,
            DeficitFrames: statisticsPayload.FrameStatistics.DeficitFrames);

        var statistics = new LavalinkServerStatistics(
            ConnectedPlayers: statisticsPayload.ConnectedPlayers,
            PlayingPlayers: statisticsPayload.PlayingPlayers,
            Uptime: statisticsPayload.Uptime,
            MemoryUsage: memoryUsage,
            ProcessorUsage: processorUsage,
            FrameStatistics: frameStatistics);

        var tag = KeyValuePair.Create<string, object?>("label", Label);

        Diagnostics.ConnectedPlayers.Set(statistics.ConnectedPlayers, tag);
        Diagnostics.PlayingPlayers.Set(statistics.PlayingPlayers, tag);
        Diagnostics.FreeMemory.Set(statistics.MemoryUsage.FreeMemory, tag);
        Diagnostics.UsedMemory.Set(statistics.MemoryUsage.UsedMemory, tag);
        Diagnostics.AllocatedMemory.Set(statistics.MemoryUsage.AllocatedMemory, tag);
        Diagnostics.ReservableMemory.Set(statistics.MemoryUsage.ReservableMemory, tag);
        Diagnostics.CoreCount.Set(statistics.ProcessorUsage.CoreCount, tag);
        Diagnostics.SystemLoad.Set(statistics.ProcessorUsage.SystemLoad, tag);
        Diagnostics.LavalinkLoad.Set(statistics.ProcessorUsage.LavalinkLoad, tag);
        Diagnostics.SentFrames.Set(statistics.FrameStatistics?.SentFrames ?? 0, tag);
        Diagnostics.NulledFrames.Set(statistics.FrameStatistics?.NulledFrames ?? 0, tag);
        Diagnostics.DeficitFrames.Set(statistics.FrameStatistics?.DeficitFrames ?? 0, tag);

        var eventArgs = new StatisticsUpdatedEventArgs(statistics);

        await _serviceContext.NodeListener
            .OnStatisticsUpdatedAsync(eventArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ProcessTrackEndEventAsync(ILavalinkPlayer player, TrackEndEventPayload trackEndEvent, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(trackEndEvent);

        var track = LavalinkApiClient.CreateTrack(trackEndEvent.Track);

        if (player is ILavalinkPlayerListener playerListener)
        {
            await playerListener
                .NotifyTrackEndedAsync(track, trackEndEvent.Reason, cancellationToken)
                .ConfigureAwait(false);
        }

        var eventArgs = new TrackEndedEventArgs(
            player: player,
            track: track,
            reason: trackEndEvent.Reason);

        await _serviceContext.NodeListener
            .OnTrackEndedAsync(eventArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ProcessTrackExceptionEventAsync(ILavalinkPlayer player, TrackExceptionEventPayload trackExceptionEvent, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(trackExceptionEvent);

        var track = LavalinkApiClient.CreateTrack(trackExceptionEvent.Track);

        var exception = new TrackException(
            Severity: trackExceptionEvent.Exception.Severity,
            Message: trackExceptionEvent.Exception.Message,
            Cause: trackExceptionEvent.Exception.Cause);

        if (player is ILavalinkPlayerListener playerListener)
        {
            await playerListener
                .NotifyTrackExceptionAsync(track, exception, cancellationToken)
                .ConfigureAwait(false);
        }

        var eventArgs = new TrackExceptionEventArgs(
            player: player,
            track: track,
            exception: exception);

        await _serviceContext.NodeListener
            .OnTrackExceptionAsync(eventArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ProcessTrackStartEventAsync(ILavalinkPlayer player, TrackStartEventPayload trackStartEvent, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(trackStartEvent);

        var track = LavalinkApiClient.CreateTrack(trackStartEvent.Track);

        if (player is ILavalinkPlayerListener playerListener)
        {
            await playerListener
                .NotifyTrackStartedAsync(track, cancellationToken)
                .ConfigureAwait(false);
        }

        var eventArgs = new TrackStartedEventArgs(
            player: player,
            track: track);

        await _serviceContext.NodeListener
            .OnTrackStartedAsync(eventArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ProcessTrackStuckEventAsync(ILavalinkPlayer player, TrackStuckEventPayload trackStuckEvent, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(trackStuckEvent);

        var track = LavalinkApiClient.CreateTrack(trackStuckEvent.Track);

        if (player is ILavalinkPlayerListener playerListener)
        {
            await playerListener
                .NotifyTrackStuckAsync(track, trackStuckEvent.ExceededThreshold, cancellationToken)
                .ConfigureAwait(false);
        }

        var eventArgs = new TrackStuckEventArgs(
            player: player,
            track: track,
            threshold: trackStuckEvent.ExceededThreshold);

        await _serviceContext.NodeListener
            .OnTrackStuckAsync(eventArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ProcessWebSocketClosedEventAsync(ILavalinkPlayer player, WebSocketClosedEventPayload webSocketClosedEvent, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(webSocketClosedEvent);

        var closeCode = (WebSocketCloseStatus)webSocketClosedEvent.Code;

        if (player is ILavalinkPlayerListener playerListener)
        {
            await playerListener
                .NotifyWebSocketClosedAsync(closeCode, webSocketClosedEvent.Reason, webSocketClosedEvent.WasByRemote, cancellationToken)
                .ConfigureAwait(false);
        }

        var eventArgs = new WebSocketClosedEventArgs(
            player: player,
            closeCode: closeCode,
            reason: webSocketClosedEvent.Reason,
            byRemote: webSocketClosedEvent.WasByRemote);

        await _serviceContext.NodeListener
            .OnWebSocketClosedAsync(eventArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask RunAsync(ClientInformation clientInformation, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        cancellationToken.ThrowIfCancellationRequested();

        if (_executeTask is not null)
        {
            throw new InvalidOperationException("The node was already started.");
        }

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            token1: cancellationToken,
            token2: _shutdownCancellationToken);

        var linkedCancellationToken = cancellationTokenSource.Token;

        try
        {
            _executeTask = ReceiveInternalAsync(clientInformation, linkedCancellationToken);
            await _executeTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception exception)
        {
            _readyTaskCompletionSource.TrySetException(exception);
            _logger.LogError(exception, "An exception occurred while running the Lavalink node.");
            throw;
        }
        finally
        {
            _readyTaskCompletionSource.TrySetCanceled(CancellationToken.None);
        }
    }

    private async Task ReceiveInternalAsync(ClientInformation clientInformation, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var webSocketUri = _options.WebSocketUri ?? _apiEndpoints.WebSocket;

        _readyStopwatch.Restart();

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCancellationToken);
        using var __ = new CancellationTokenDisposable(cancellationTokenSource);

        while (!_shutdownCancellationToken.IsCancellationRequested)
        {
            if (_readyTaskCompletionSource.Task.IsCompleted)
            {
                // Initiate reconnect
                _readyTaskCompletionSource = new TaskCompletionSource<string>(
                    creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
            }

            var socketOptions = new LavalinkSocketOptions
            {
                Label = Label,
                HttpClientName = _options.HttpClientName,
                Uri = webSocketUri,
                ShardCount = clientInformation.ShardCount,
                UserId = clientInformation.CurrentUserId,
                Passphrase = _options.Passphrase,
                SessionId = SessionId,
                BufferSize = _options.BufferSize,
            };

            using var socket = _serviceContext.LavalinkSocketFactory.Create(Options.Create(socketOptions));
            using var socketCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
            using var ___ = new CancellationTokenDisposable(socketCancellationSource);

            if (socket is null)
            {
                break;
            }

            socket.ConnectionClosed += InvokeConnectionClosedAsync;
            socket.ConnectionStarted += InvokeConnectionStartedAsync;

            try
            {
                _ = socket.RunAsync(socketCancellationSource.Token).AsTask();

                await ReceiveInternalAsync(socket, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                socket.ConnectionClosed -= InvokeConnectionClosedAsync;
                socket.ConnectionStarted -= InvokeConnectionStartedAsync;
            }
        }
    }

    private async Task InvokeConnectionClosedAsync(object sender, ConnectionClosedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(eventArgs);

        await SocketConnectionClosed.InvokeAsync(this, eventArgs);
        await _serviceContext.NodeListener.OnConnectionClosedAsync(eventArgs).AsTask();
    }

    private async Task InvokeConnectionStartedAsync(object sender, ConnectionStartedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(eventArgs);

        await SocketConnectionStarted.InvokeAsync(this, eventArgs);
        await _serviceContext.NodeListener.OnConnectionStartedAsync(eventArgs).AsTask();
    }

    private async Task InvokeConnectionReadyAsync(object sender, ConnectionReadyEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(eventArgs);

        await SocketConnectionReady.InvokeAsync(this, eventArgs);
        await _serviceContext.NodeListener.OnConnectionReadyAsync(eventArgs).AsTask();
    }

    private async ValueTask ReceiveInternalAsync(ILavalinkSocket socket, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IPayload? payload;
            try
            {
                payload = await socket
                    .ReceiveAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (WebSocketException exception) when (exception.WebSocketErrorCode is WebSocketError.ConnectionClosedPrematurely)
            {
                _logger.ExceptionOccurredDuringCommunication(Label, exception);
                break;
            }
            catch (Exception exception)
            {
                _logger.ExceptionOccurredDuringCommunication(Label, exception);
                continue;
            }

            if (payload is null)
            {
                break;
            }

            try
            {
                await ProcessPayloadAsync(socket, payload, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.ExceptionOccurredWhileProcessingPayload(Label, payload, exception);
            }

            foreach (var (_, integration) in _serviceContext.IntegrationManager)
            {
                try
                {
                    await integration
                        .ProcessPayloadAsync(payload, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.ExceptionOccurredWhileExecutingIntegrationHandler(Label, exception);
                }
            }
        }
    }

    private void ThrowIfDisposed()
    {
#if NET7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_disposed, this);
#else
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioService));
        }
#endif
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _readyTaskCompletionSource.TrySetCanceled();
        _shutdownCancellationTokenSource.Cancel();
        _shutdownCancellationTokenSource.Dispose();

        if (_executeTask is not null)
        {
            try
            {
                await _executeTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // ignore
            }
        }
    }
}

file readonly record struct CancellationTokenDisposable(CancellationTokenSource CancellationTokenSource) : IDisposable
{
    public void Dispose() => CancellationTokenSource.Cancel();
}

internal static partial class Logging
{
    [LoggerMessage(1, LogLevel.Debug, "[{Label}] Received an event payload for a non-registered player: {GuildId}.", EventName = nameof(ReceivedEventPayloadForNonRegisteredPlayer))]
    public static partial void ReceivedEventPayloadForNonRegisteredPlayer(this ILogger<LavalinkNode> logger, string label, ulong guildId);

    [LoggerMessage(2, LogLevel.Trace, "[{Label}] Received payload from lavalink node: {Payload}", EventName = nameof(ReceivedPayloadFromLavalinkNode))]
    public static partial void ReceivedPayloadFromLavalinkNode(this ILogger<LavalinkNode> logger, string label, string payload);

    [LoggerMessage(3, LogLevel.Warning, "[{Label}] Multiple ready payloads were received.", EventName = nameof(MultipleReadyPayloadsReceived))]
    public static partial void MultipleReadyPayloadsReceived(this ILogger<LavalinkNode> logger, string label);

    [LoggerMessage(4, LogLevel.Information, "[{Label}] Node is ready (session identifier: {SessionId}).", EventName = nameof(Ready))]
    public static partial void Ready(this ILogger<LavalinkNode> logger, string label, string sessionId);

    [LoggerMessage(5, LogLevel.Warning, "[{Label}] A payload was received before the ready payload was received. The payload will be ignored.", EventName = nameof(PayloadReceivedBeforeReadyPayload))]
    public static partial void PayloadReceivedBeforeReadyPayload(this ILogger<LavalinkNode> logger, string label);

    [LoggerMessage(6, LogLevel.Debug, "[{Label}] Received a player update payload for a non-registered player: {GuildId}.", EventName = nameof(ReceivedPlayerUpdatePayloadForNonRegisteredPlayer))]
    public static partial void ReceivedPlayerUpdatePayloadForNonRegisteredPlayer(this ILogger<LavalinkNode> logger, string label, ulong guildId);

    [LoggerMessage(7, LogLevel.Error, "[{Label}] Exception occurred while executing integration handler.", EventName = nameof(ExceptionOccurredWhileExecutingIntegrationHandler))]
    public static partial void ExceptionOccurredWhileExecutingIntegrationHandler(this ILogger<LavalinkNode> logger, string label, Exception exception);

    [LoggerMessage(8, LogLevel.Error, "[{Label}] Exception occurred during communication.", EventName = nameof(ExceptionOccurredDuringCommunication))]
    public static partial void ExceptionOccurredDuringCommunication(this ILogger<LavalinkNode> logger, string label, Exception exception);

    [LoggerMessage(9, LogLevel.Error, "[{Label}] Exception occurred while processing a payload: {Payload}.", EventName = nameof(ExceptionOccurredWhileProcessingPayload))]
    public static partial void ExceptionOccurredWhileProcessingPayload(this ILogger<LavalinkNode> logger, string label, object payload, Exception exception);
}

file static class Diagnostics
{
    static Diagnostics()
    {
        var meter = new Meter("Lavalink4NET");

        ConnectedPlayers = new AbsoluteCounterInt32(meter.CreateUpDownCounter<int>(name: "server-connected-players", unit: "Players"));
        PlayingPlayers = new AbsoluteCounterInt32(meter.CreateUpDownCounter<int>(name: "server-playing-players", unit: "Players"));
        FreeMemory = new AbsoluteCounterInt64(meter.CreateUpDownCounter<long>(name: "server-free-memory", "Bytes"));
        UsedMemory = new AbsoluteCounterInt64(meter.CreateUpDownCounter<long>(name: "server-used-memory", "Bytes"));
        AllocatedMemory = new AbsoluteCounterInt64(meter.CreateUpDownCounter<long>(name: "server-allocated-memory", "Bytes"));
        ReservableMemory = new AbsoluteCounterInt64(meter.CreateUpDownCounter<long>(name: "server-reservable-memory", "Bytes"));
        CoreCount = new AbsoluteCounterInt32(meter.CreateUpDownCounter<int>(name: "server-core-count", "Cores"));
        SystemLoad = new AbsoluteCounterSingle(meter.CreateUpDownCounter<float>(name: "server-system-load", "Load%"));
        LavalinkLoad = new AbsoluteCounterSingle(meter.CreateUpDownCounter<float>(name: "server-lavalink-load", "Load%"));
        SentFrames = new AbsoluteCounterInt32(meter.CreateUpDownCounter<int>(name: "server-sent-frames", "Frames"));
        NulledFrames = new AbsoluteCounterInt32(meter.CreateUpDownCounter<int>(name: "server-nulled-frames", "Frames"));
        DeficitFrames = new AbsoluteCounterInt32(meter.CreateUpDownCounter<int>(name: "server-deficit-frames", "Frames"));
    }

    public static AbsoluteCounterInt32 ConnectedPlayers { get; }

    public static AbsoluteCounterInt32 PlayingPlayers { get; }

    public static AbsoluteCounterInt64 FreeMemory { get; }

    public static AbsoluteCounterInt64 UsedMemory { get; }

    public static AbsoluteCounterInt64 AllocatedMemory { get; }

    public static AbsoluteCounterInt64 ReservableMemory { get; }

    public static AbsoluteCounterInt32 CoreCount { get; }

    public static AbsoluteCounterSingle SystemLoad { get; }

    public static AbsoluteCounterSingle LavalinkLoad { get; }

    public static AbsoluteCounterInt32 SentFrames { get; }

    public static AbsoluteCounterInt32 NulledFrames { get; }

    public static AbsoluteCounterInt32 DeficitFrames { get; }
}

file abstract class AbsoluteCounterBase<T> where T : struct
{
    private readonly UpDownCounter<T> _counter;
    private T _value;

    protected AbsoluteCounterBase(UpDownCounter<T> counter)
    {
        ArgumentNullException.ThrowIfNull(counter);

        _counter = counter;
    }

    public void Set(T value, KeyValuePair<string, object?> tag)
    {
        _counter.Add(Mutate(value, _value), tag);
        _value = value;
    }

    protected abstract T Mutate(T operand1, T operand2);
}

file sealed class AbsoluteCounterInt32(UpDownCounter<int> counter) : AbsoluteCounterBase<int>(counter)
{
    protected override int Mutate(int operand1, int operand2) => operand1 - operand2;
}

file sealed class AbsoluteCounterInt64(UpDownCounter<long> counter) : AbsoluteCounterBase<long>(counter)
{
    protected override long Mutate(long operand1, long operand2) => operand1 - operand2;
}

file sealed class AbsoluteCounterSingle(UpDownCounter<float> counter) : AbsoluteCounterBase<float>(counter)
{
    protected override float Mutate(float operand1, float operand2) => operand1 - operand2;
}
