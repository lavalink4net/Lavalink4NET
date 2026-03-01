namespace Lavalink4NET.Players;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Protocol.Requests;
using Lavalink4NET.Rest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class LavalinkPlayerHandle<TPlayer, TOptions> : ILavalinkPlayerHandle, IAsyncDisposable
    where TPlayer : ILavalinkPlayer
    where TOptions : LavalinkPlayerOptions
{
    private readonly ulong _guildId;
    private readonly ILogger<TPlayer> _logger;
    private readonly IOptions<TOptions> _options;
    private readonly PlayerContext _playerContext;
    private readonly PlayerFactory<TPlayer, TOptions> _playerFactory;
    private object _value;
    private VoiceServer? _voiceServer;
    private VoiceState? _voiceState;
    private int _disposeState;

    public LavalinkPlayerHandle(
        ulong guildId,
        PlayerContext playerContext,
        PlayerFactory<TPlayer, TOptions> playerFactory,
        IOptions<TOptions> options,
        ILogger<TPlayer> logger)
    {
        ArgumentNullException.ThrowIfNull(playerContext);
        ArgumentNullException.ThrowIfNull(playerFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _value = new TaskCompletionSource<ILavalinkPlayer>(TaskCreationOptions.RunContinuationsAsynchronously);

        _guildId = guildId;
        _playerContext = playerContext;
        _playerFactory = playerFactory;
        _options = options;
        _logger = logger;

        Interlocked.Increment(ref Diagnostics.PlayerHandles);
        Interlocked.Increment(ref Diagnostics.PendingHandles);
    }

    public ILavalinkPlayer? Player => _value as ILavalinkPlayer;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) is not 0)
        {
            return;
        }

        if (_value is ILavalinkPlayer player)
        {
            await using var _ = player.ConfigureAwait(false);

            Interlocked.Decrement(ref Diagnostics.ActivePlayers);
        }
        else
        {
            Interlocked.Decrement(ref Diagnostics.PendingHandles);
        }

        Interlocked.Decrement(ref Diagnostics.PlayerHandles);
    }

    public ValueTask<ILavalinkPlayer> GetPlayerAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (_value is TaskCompletionSource<ILavalinkPlayer> taskCompletionSource)
        {
            return new ValueTask<ILavalinkPlayer>(task: taskCompletionSource.Task.WaitAsync(cancellationToken));
        }

        return ValueTask.FromResult<ILavalinkPlayer>(Unsafe.As<object, TPlayer>(ref Unsafe.AsRef(in _value)));
    }

    public async ValueTask UpdateVoiceServerAsync(VoiceServer voiceServer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_disposeState is 1)
        {
            return;
        }

        _voiceServer = voiceServer;

        if (_voiceState is not null)
        {
            _ = CompleteAsync(isVoiceServerUpdated: true, cancellationToken).Preserve();
        }
    }

    public async ValueTask UpdateVoiceStateAsync(VoiceState voiceState, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_disposeState is 1)
        {
            return;
        }

        _voiceState = voiceState;

        if (_voiceServer is not null)
        {
            _ = CompleteAsync(isVoiceServerUpdated: false, cancellationToken).Preserve();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposeState is not 0)
        {
            throw new ObjectDisposedException(objectName: nameof(LavalinkPlayerHandle<TPlayer, TOptions>));
        }
    }

    private async ValueTask CompleteAsync(bool isVoiceServerUpdated, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        Debug.Assert(_voiceServer is not null);
        Debug.Assert(_voiceState is not null);

        if (_value is TaskCompletionSource<ILavalinkPlayer> taskCompletionSource)
        {
            // _value is automatically set by CreatePlayerAsync
            try
            {
                // CreatePlayerAsync can throw if request to lavalink fails
                // We should handle this to avoid never completed lavalink player handle
                var player = await CreatePlayerAsync(cancellationToken).ConfigureAwait(false);

                taskCompletionSource.TrySetResult(player);

                Interlocked.Decrement(ref Diagnostics.PendingHandles);
                Interlocked.Increment(ref Diagnostics.ActivePlayers);
            }
            catch (Exception e)
            {
                // Here we're passing CancellationToken.None to ensure what the player disposing event will properly
                // clean up this handle from cache
                await _playerContext.LifecycleNotifier!.NotifyDisposeAsync(_guildId, CancellationToken.None);
                taskCompletionSource.TrySetException(e);
                await DisposeAsync();
            }
        }
        else
        {
            // Player already created which indicates that the completion indicates a voice server or voice state update
            if (_value is not ILavalinkPlayerListener playerListener)
            {
                return;
            }

            if (isVoiceServerUpdated)
            {
                await playerListener
                    .NotifyVoiceServerUpdatedAsync(_voiceServer.Value, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await playerListener
                    .NotifyVoiceStateUpdatedAsync(_voiceState.Value, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<TPlayer> CreatePlayerAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        Debug.Assert(_voiceServer is not null);
        Debug.Assert(_voiceState is not null);

        var playerSession = await _playerContext.SessionProvider
            .GetSessionAsync(_guildId, cancellationToken)
            .ConfigureAwait(false);

        var playerProperties = new PlayerUpdateProperties
        {
            VoiceState = new VoiceStateProperties(
                Token: _voiceServer.Value.Token,
                Endpoint: _voiceServer.Value.Endpoint,
                SessionId: _voiceState.Value.SessionId!,
                ChannelId: _voiceState.Value.VoiceChannelId?.ToString()),
        };

        if (_options.Value.InitialTrack is not null)
        {
            var initialTrack = _options.Value.InitialTrack;
            var loadOptions = _options.Value.InitialLoadOptions;

            if (initialTrack.Reference.IsPresent)
            {
                var playableTrack = await initialTrack.Reference.Track.GetPlayableTrackAsync(cancellationToken);
                playerProperties = playerProperties with { TrackData = playableTrack.ToString() };
            }
            else
            {
                var identifier = LavalinkApiClient.BuildIdentifier(
                    identifier: initialTrack.Identifier!,
                    loadOptions: loadOptions);

                playerProperties = playerProperties with { Identifier = identifier, };
            }
        }

        if (_options.Value.InitialPosition is not null)
        {
            playerProperties = playerProperties with { Position = _options.Value.InitialPosition.Value };
        }

        if (_options.Value.InitialVolume is not null)
        {
            playerProperties = playerProperties with { Volume = _options.Value.InitialVolume.Value, };
        }

        if (playerSession.SessionId is null)
        {
            throw new InvalidOperationException(
                "The session identifier for the preferred node is not available.\n" +
                "Please ensure that the connection to the node has been established and is ready.\n" +
                "If you are not using Lavalink4NET in combination with Microsoft.Extensions.Hosting, ensure that you have explicitly called `.StartAsync()` on the required services.");
        }

        var initialState = await playerSession.ApiClient
            .UpdatePlayerAsync(playerSession.SessionId, _guildId, playerProperties, cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var label = _options.Value.Label ?? $"{typeof(TPlayer).Name}@{_guildId} on {playerSession.Label}";

        var lifecycle = _playerContext.LifecycleNotifier is null
            ? new EmptyPlayerLifecycle(this) as IPlayerLifecycle
            : new PlayerLifecycle(this, _guildId, _playerContext.LifecycleNotifier);

        var properties = new PlayerProperties<TPlayer, TOptions>(
            Context: _playerContext,
            VoiceChannelId: _voiceState.Value.VoiceChannelId!.Value,
            InitialTrack: _options.Value.InitialTrack,
            InitialState: initialState,
            Label: label,
            SessionId: playerSession.SessionId,
            Lifecycle: lifecycle,
            ApiClient: playerSession.ApiClient,
            Options: _options,
            Logger: _logger);

        var player = await _playerFactory(properties, cancellationToken).ConfigureAwait(false);

        _value = player; // Set value early to make it available for lifecycle notifications

        await lifecycle
            .NotifyPlayerCreatedAsync(player, cancellationToken)
            .ConfigureAwait(false);

        if (player is ILavalinkPlayerListener playerListener)
        {
            await playerListener
                .NotifyVoiceServerUpdatedAsync(_voiceServer.Value, cancellationToken)
                .ConfigureAwait(false);

            await playerListener
                .NotifyVoiceStateUpdatedAsync(_voiceState.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        return player;
    }
}

file sealed class EmptyPlayerLifecycle(ILavalinkPlayerHandle handle) : IPlayerLifecycle
{
    public ValueTask DisposeAsync() => handle.DisposeAsync();

    public ValueTask NotifyPlayerCreatedAsync(ILavalinkPlayer player, CancellationToken cancellationToken = default) => default;

    public ValueTask NotifyStateChangedAsync(ILavalinkPlayer player, PlayerState playerState, CancellationToken cancellationToken = default) => default;
}

file sealed class PlayerLifecycle : IPlayerLifecycle
{
    private readonly ILavalinkPlayerHandle _handle;
    private readonly ulong _guildId;
    private readonly IPlayerLifecycleNotifier _lifecycleNotifier;

    public PlayerLifecycle(ILavalinkPlayerHandle handle, ulong guildId, IPlayerLifecycleNotifier lifecycleNotifier)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(lifecycleNotifier);

        _handle = handle;
        _guildId = guildId;
        _lifecycleNotifier = lifecycleNotifier;
    }

    public async ValueTask DisposeAsync()
    {
        await using var _ = _handle.ConfigureAwait(false);

        await _lifecycleNotifier
            .NotifyDisposeAsync(_guildId)
            .ConfigureAwait(false);
    }

    public ValueTask NotifyPlayerCreatedAsync(ILavalinkPlayer player, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Debug.Assert(player.GuildId == _guildId);
        return _lifecycleNotifier.NotifyPlayerCreatedAsync(player, cancellationToken);
    }

    public ValueTask NotifyStateChangedAsync(ILavalinkPlayer player, PlayerState playerState, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Debug.Assert(player.GuildId == _guildId);
        return _lifecycleNotifier.NotifyStateChangedAsync(player, playerState, cancellationToken);
    }
}

file static class Diagnostics
{
    private static long _playerHandles;
    private static long _pendingHandles;
    private static long _activePlayers;

    public static ref long PlayerHandles => ref _playerHandles;

    public static ref long PendingHandles => ref _pendingHandles;

    public static ref long ActivePlayers => ref _activePlayers;

    static Diagnostics()
    {
        var meter = new Meter("Lavalink4NET");

        meter.CreateObservableGauge(
            name: "active-players",
            observeValue: static () => Volatile.Read(ref _activePlayers),
            unit: "Players",
            description: "The number of active players managed over all audio services.");

        meter.CreateObservableGauge(
            name: "player-handles",
            observeValue: static () => Volatile.Read(ref _playerHandles),
            unit: "Handles",
            description: "The number of player handles managed over all audio services.");

        meter.CreateObservableGauge(
            name: "pending-handles",
            observeValue: static () => Volatile.Read(ref _pendingHandles),
            unit: "Handles",
            description: "The number of pending player handles managed over all audio services.");
    }
}
