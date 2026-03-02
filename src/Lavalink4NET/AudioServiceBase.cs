namespace Lavalink4NET;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Integrations;
using Lavalink4NET.Players;
using Lavalink4NET.Rest;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;

public abstract class AudioServiceBase : IAudioService, ILavalinkNodeListener
{
    private readonly CancellationTokenSource _shutdownCancellationTokenSource;
    private readonly TaskCompletionSource _readyTaskCompletionSource;
    private readonly TimeSpan _readyTimeout;
    private readonly ILogger<AudioServiceBase> _logger;
    private Task? _executeTask;
    private int _disposeState;

    protected AudioServiceBase(
        IDiscordClientWrapper discordClient,
        ILavalinkApiClientProvider apiClientProvider,
        IIntegrationManager integrations,
        IPlayerManager players,
        ITrackManager tracks,
        TimeSpan readyTimeout,
        ILogger<AudioServiceBase> logger)
    {
        ArgumentNullException.ThrowIfNull(apiClientProvider);
        ArgumentNullException.ThrowIfNull(integrations);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(tracks);
        ArgumentNullException.ThrowIfNull(logger);

        _shutdownCancellationTokenSource = new CancellationTokenSource();
        ShutdownCancellationToken = _shutdownCancellationTokenSource.Token;

        _readyTaskCompletionSource = new TaskCompletionSource(
            creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

        DiscordClient = discordClient;
        ApiClientProvider = apiClientProvider;
        Integrations = integrations;
        Players = players;
        Tracks = tracks;
        _readyTimeout = readyTimeout;
        _logger = logger;
    }

    protected internal CancellationToken ShutdownCancellationToken { get; }

    public IDiscordClientWrapper DiscordClient { get; }

    public ILavalinkApiClientProvider ApiClientProvider { get; }

    public IIntegrationManager Integrations { get; }

    public IPlayerManager Players { get; }

    public ITrackManager Tracks { get; }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (_executeTask is not null)
        {
            return ValueTask.CompletedTask;
        }

        _executeTask = RunAsync(ShutdownCancellationToken).AsTask();

        return _executeTask.IsCompleted ? new ValueTask(_executeTask) : ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (_executeTask is null)
        {
            return;
        }

        try
        {
            _shutdownCancellationTokenSource.Cancel();
        }
        finally
        {
            try
            {
                await _executeTask
                    .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                _executeTask = null; // Abandon task
                _logger.LogError(exception, "Reached timeout while waiting for the audio service being stopped.");
            }
        }
    }

    protected void NotifyReady()
    {
        _readyTaskCompletionSource.TrySetResult();
    }

    public async ValueTask WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _readyTaskCompletionSource.Task
                .WaitAsync(_readyTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(
                message: "Reached timeout while waiting for the audio service being ready.",
                innerException: exception);
        }
    }

    public event AsyncEventHandler<TrackEndedEventArgs>? TrackEnded;

    public event AsyncEventHandler<TrackExceptionEventArgs>? TrackException;

    public event AsyncEventHandler<TrackStartedEventArgs>? TrackStarted;

    public event AsyncEventHandler<TrackStuckEventArgs>? TrackStuck;

    public event AsyncEventHandler<StatisticsUpdatedEventArgs>? StatisticsUpdated;

    public event AsyncEventHandler<WebSocketClosedEventArgs>? WebSocketClosed;

    public event AsyncEventHandler<ConnectionClosedEventArgs>? ConnectionClosed;

    public event AsyncEventHandler<ConnectionReadyEventArgs>? ConnectionReady;

    protected virtual ValueTask OnTrackEndedAsync(TrackEndedEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(eventArgs);
        return TrackEnded.InvokeAsync(this, eventArgs);
    }

    protected virtual ValueTask OnConnectionClosedAsync(ConnectionClosedEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(eventArgs);
        return ConnectionClosed.InvokeAsync(this, eventArgs);
    }

    protected virtual ValueTask OnTrackExceptionAsync(TrackExceptionEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(eventArgs);
        return TrackException.InvokeAsync(this, eventArgs);
    }

    protected virtual ValueTask OnTrackStartedAsync(TrackStartedEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(eventArgs);
        return TrackStarted.InvokeAsync(this, eventArgs);
    }

    protected virtual ValueTask OnTrackStuckAsync(TrackStuckEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(eventArgs);
        return TrackStuck.InvokeAsync(this, eventArgs);
    }

    protected virtual ValueTask OnStatisticsUpdatedAsync(StatisticsUpdatedEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(eventArgs);
        return StatisticsUpdated.InvokeAsync(this, eventArgs);
    }

    protected virtual ValueTask OnWebSocketClosedAsync(WebSocketClosedEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(eventArgs);
        return WebSocketClosed.InvokeAsync(this, eventArgs);
    }

    protected virtual ValueTask OnConnectionReadyAsync(ConnectionReadyEventArgs eventArgs, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(eventArgs);
        return ConnectionReady.InvokeAsync(this, eventArgs);
    }

    ValueTask ILavalinkNodeListener.OnTrackEndedAsync(TrackEndedEventArgs eventArgs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnTrackEndedAsync(eventArgs, cancellationToken);
    }

    ValueTask ILavalinkNodeListener.OnTrackExceptionAsync(TrackExceptionEventArgs eventArgs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnTrackExceptionAsync(eventArgs, cancellationToken);
    }

    ValueTask ILavalinkNodeListener.OnTrackStartedAsync(TrackStartedEventArgs eventArgs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnTrackStartedAsync(eventArgs, cancellationToken);
    }

    ValueTask ILavalinkNodeListener.OnTrackStuckAsync(TrackStuckEventArgs eventArgs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnTrackStuckAsync(eventArgs, cancellationToken);
    }

    ValueTask ILavalinkNodeListener.OnStatisticsUpdatedAsync(StatisticsUpdatedEventArgs eventArgs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnStatisticsUpdatedAsync(eventArgs, cancellationToken);
    }

    ValueTask ILavalinkNodeListener.OnWebSocketClosedAsync(WebSocketClosedEventArgs eventArgs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnWebSocketClosedAsync(eventArgs, cancellationToken);
    }

    private async ValueTask<ClientInformation> WaitForClientReadyAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var originalCancellationToken = cancellationToken;
        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellationTokenSource.CancelAfter(timeout);
        cancellationToken = cancellationTokenSource.Token;

        _logger.WaitingForClientBeingReady();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var clientInformation = await DiscordClient
                .WaitForReadyAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.DiscordClientIsReady(clientInformation.Label, (int)stopwatch.ElapsedMilliseconds);

            return clientInformation;
        }
        catch (OperationCanceledException exception) when (!originalCancellationToken.IsCancellationRequested)
        {
            _logger.TimedOutWhileWaitingForDiscordClientBeingReady(exception);
            throw new TimeoutException("Timed out while waiting for discord client being ready.", exception);
        }
    }

    private async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.StartingAudioService();

        try
        {
            var clientTask = WaitForClientReadyAsync(
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken);

            var clientInformation = await clientTask.ConfigureAwait(false);
            await RunInternalAsync(clientInformation, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.AudioServiceError(exception);
            throw;
        }
        finally
        {
            _logger.AudioServiceStopped();
        }
    }

    private void ThrowIfDisposed()
    {
#if NET7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_disposeState is not 0, this);
#else
        if (_disposeState is not 0)
        {
            throw new ObjectDisposedException(nameof(AudioService));
        }
#endif
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) is 0)
        {
            return;
        }

        _shutdownCancellationTokenSource.Cancel();
        _shutdownCancellationTokenSource.Dispose();

        if (_executeTask is not null)
        {
            try
            {
                await _executeTask.ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected abstract ValueTask RunInternalAsync(ClientInformation clientInformation, CancellationToken cancellationToken = default);

    ValueTask ILavalinkNodeListener.OnConnectionClosedAsync(ConnectionClosedEventArgs eventArgs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(eventArgs);

        return OnConnectionClosedAsync(eventArgs, cancellationToken);
    }

    ValueTask ILavalinkNodeListener.OnConnectionReadyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OnConnectionReadyAsync(new ConnectionReadyEventArgs(), cancellationToken);
    }
}

internal static partial class Logging
{
    [LoggerMessage(1, LogLevel.Debug, "Waiting for client being ready...", EventName = nameof(WaitingForClientBeingReady))]
    public static partial void WaitingForClientBeingReady(this ILogger<AudioServiceBase> logger);

    [LoggerMessage(2, LogLevel.Error, "Timed out while waiting for discord client being ready.", EventName = nameof(TimedOutWhileWaitingForDiscordClientBeingReady))]
    public static partial void TimedOutWhileWaitingForDiscordClientBeingReady(this ILogger<AudioServiceBase> logger, Exception exception);

    [LoggerMessage(3, LogLevel.Information, "Discord client ({ClientLabel}) is ready ({Duration}ms).", EventName = nameof(DiscordClientIsReady))]
    public static partial void DiscordClientIsReady(this ILogger<AudioServiceBase> logger, string clientLabel, int duration);

    [LoggerMessage(4, LogLevel.Information, "Starting audio service...", EventName = nameof(StartingAudioService))]
    public static partial void StartingAudioService(this ILogger<AudioServiceBase> logger);

    [LoggerMessage(5, LogLevel.Information, "Audio service stopped.", EventName = nameof(AudioServiceStopped))]
    public static partial void AudioServiceStopped(this ILogger<AudioServiceBase> logger);

    [LoggerMessage(6, LogLevel.Information, "Audio Service is ready ({Duration}ms).", EventName = nameof(AudioServiceIsReady))]
    public static partial void AudioServiceIsReady(this ILogger<AudioServiceBase> logger, long duration);

    [LoggerMessage(7, LogLevel.Error, "The audio Service entered a failure state.", EventName = nameof(AudioServiceError))]
    public static partial void AudioServiceError(this ILogger<AudioServiceBase> logger, Exception exception);
}