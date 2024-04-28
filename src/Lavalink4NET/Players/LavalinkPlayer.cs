namespace Lavalink4NET.Players;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Protocol.Models.Filters;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Protocol.Requests;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;

public class LavalinkPlayer : ILavalinkPlayer, ILavalinkPlayerListener
{
    private readonly ILogger<LavalinkPlayer> _logger;
    private readonly ISystemClock _systemClock;
    private readonly bool _disconnectOnStop;
    private readonly IPlayerLifecycle _playerLifecycle;
    private int _disposed;
    private DateTimeOffset _syncedAt;
    private TimeSpan _unstretchedRelativePosition;
    private bool _disconnectOnDestroy;
    private bool _connectedOnce;
    private ulong _trackVersion;
    private volatile ITrackQueueItem? _stoppedItem;
    private volatile ITrackQueueItem? _currentItem;
    private volatile ITrackQueueItem? _replacedItem;
    private volatile ITrackQueueItem? _nextItem;
    private UpDownCounter<int>? _previousStateCounter;
    private string? _previousVoiceServer;

    public LavalinkPlayer(IPlayerProperties<LavalinkPlayer, LavalinkPlayerOptions> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        SessionId = properties.SessionId;
        ApiClient = properties.ApiClient;
        DiscordClient = properties.DiscordClient;
        GuildId = properties.InitialState.GuildId;
        VoiceChannelId = properties.VoiceChannelId;

        Label = properties.Label;
        _systemClock = properties.SystemClock;
        _logger = properties.Logger;
        _syncedAt = properties.SystemClock.UtcNow;
        _playerLifecycle = properties.Lifecycle;

        _unstretchedRelativePosition = default;
        _connectedOnce = false;

        _disconnectOnDestroy = properties.Options.Value.DisconnectOnDestroy;
        _disconnectOnStop = properties.Options.Value.DisconnectOnStop;

        VoiceServer = new VoiceServer(properties.InitialState.VoiceState.Token, properties.InitialState.VoiceState.Endpoint);
        VoiceState = new VoiceState(properties.VoiceChannelId, properties.InitialState.VoiceState.SessionId);

        Filters = new PlayerFilterMap(this);

        if (properties.InitialState.IsPaused)
        {
            State = PlayerState.Paused;
        }
        else if (properties.InitialState.CurrentTrack is null)
        {
            State = PlayerState.NotPlaying;
        }
        else
        {
            State = PlayerState.Playing;
        }

        _previousStateCounter = State switch
        {
            PlayerState.Paused => Diagnostics.PausedPlayers,
            PlayerState.NotPlaying => Diagnostics.NotPlayingPlayers,
            PlayerState.Playing => Diagnostics.PlayingPlayers,
            _ => null,
        };

        _previousStateCounter?.Add(1, KeyValuePair.Create<string, object?>("label", Label));

        _currentItem = properties.InitialState.CurrentTrack is not null
            ? properties.Options.Value.InitialTrack ?? new TrackQueueItem(new TrackReference(LavalinkApiClient.CreateTrack(properties.InitialState.CurrentTrack)))
            : null;

        Refresh(properties.InitialState);
    }

    public ulong GuildId { get; }

    public bool IsPaused { get; private set; }

    public VoiceServer? VoiceServer { get; private set; }

    public VoiceState VoiceState { get; private set; }

    public TrackPosition? Position
    {
        get
        {
            if (CurrentTrack is null)
            {
                return null;
            }

            return new TrackPosition(
                SystemClock: _systemClock,
                SyncedAt: _syncedAt,
                UnstretchedRelativePosition: _unstretchedRelativePosition,
                TimeStretchFactor: 1F); // TODO: time stretch
        }
    }

    public PlayerState State { get; private set; }

    public ulong VoiceChannelId { get; private set; }

    public float Volume { get; private set; }

    public ILavalinkApiClient ApiClient { get; }

    public string SessionId { get; }

    public PlayerConnectionState ConnectionState { get; private set; }

    public IDiscordClientWrapper DiscordClient { get; }

    public IPlayerFilters Filters { get; }

    public string Label { get; }

    public LavalinkTrack? CurrentTrack => CurrentItem?.Track;

    public ITrackQueueItem? CurrentItem
    {
        get => _currentItem;
        protected internal set => _currentItem = value;
    }

    private async ValueTask NotifyChannelUpdateCoreAsync(ulong? voiceChannelId, CancellationToken cancellationToken)
    {
        if (_disposed is 1)
        {
            return;
        }

        if (voiceChannelId is null)
        {
            _logger.PlayerDisconnected(Label);
            await using var _ = this.ConfigureAwait(false);
            return;
        }

        if (!_connectedOnce)
        {
            _connectedOnce = true;
            _logger.PlayerConnected(Label, voiceChannelId);
        }
        else
        {
            _logger.PlayerMoved(Label, voiceChannelId);
        }

        await NotifyChannelUpdateAsync(voiceChannelId, cancellationToken).ConfigureAwait(false);
    }

    async ValueTask ILavalinkPlayerListener.NotifyTrackEndedAsync(LavalinkTrack track, TrackEndReason endReason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(track);

        var currentTrackVersion = _trackVersion;

        var previousItem = endReason switch
        {
            TrackEndReason.Replaced => Interlocked.Exchange(ref _replacedItem, null) ?? ResolveTrackQueueItem(track),
            TrackEndReason.Stopped => Interlocked.Exchange(ref _stoppedItem, null) ?? ResolveTrackQueueItem(track),
            _ => ResolveTrackQueueItem(track),
        };

        try
        {
            await NotifyTrackEndedAsync(previousItem, endReason, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (Volatile.Read(ref _trackVersion) == currentTrackVersion && endReason is not TrackEndReason.Replaced)
            {
                CurrentItem = null;
                await UpdateStateAsync(PlayerState.NotPlaying, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    ValueTask ILavalinkPlayerListener.NotifyTrackExceptionAsync(LavalinkTrack track, TrackException exception, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(track);
        return NotifyTrackExceptionAsync(ResolveTrackQueueItem(track), exception, cancellationToken);
    }

    ValueTask ILavalinkPlayerListener.NotifyTrackStartedAsync(LavalinkTrack track, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(track);

        var nextTrack = Interlocked.Exchange(ref _nextItem, null) ?? CurrentItem;
        Debug.Assert(track.Identifier == nextTrack?.Identifier);

        CurrentItem = track.Identifier == nextTrack?.Identifier
            ? nextTrack
            : new TrackQueueItem(new TrackReference(track));

        Interlocked.Increment(ref _trackVersion);

        return NotifyTrackStartedAsync(ResolveTrackQueueItem(track), cancellationToken);
    }

    ValueTask ILavalinkPlayerListener.NotifyTrackStuckAsync(LavalinkTrack track, TimeSpan threshold, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(track);
        return NotifyTrackStuckAsync(ResolveTrackQueueItem(track), threshold, cancellationToken);
    }

    public virtual async ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        var properties = new PlayerUpdateProperties { IsPaused = true, };
        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);

        _logger.PlayerPaused(Label);
    }

    public virtual async ValueTask PlayAsync(ITrackQueueItem trackQueueItem, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        _replacedItem = CurrentItem;
        CurrentItem = _nextItem = trackQueueItem;

        Interlocked.Increment(ref _trackVersion);

        var updateProperties = new PlayerUpdateProperties();

        if (trackQueueItem.Reference.IsPresent)
        {
            var playableTrack = await trackQueueItem.Reference.Track
                .GetPlayableTrackAsync(cancellationToken)
                .ConfigureAwait(false);

            updateProperties.TrackData = playableTrack.ToString();
        }
        else
        {
            updateProperties.Identifier = trackQueueItem.Reference.Identifier;
        }

        if (properties.StartPosition is not null)
        {
            updateProperties.Position = properties.StartPosition.Value;
        }

        if (properties.EndTime is not null)
        {
            updateProperties.EndTime = properties.EndTime.Value;
        }

        await PerformUpdateAsync(updateProperties, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask PlayAsync(Uri uri, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        return PlayAsync(new TrackQueueItem(new TrackReference(uri.ToString())), properties, cancellationToken);
    }

    public ValueTask PlayFileAsync(FileInfo fileInfo, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        return PlayAsync(new TrackQueueItem(new TrackReference(fileInfo.FullName)), properties, cancellationToken);
    }

    public ValueTask PlayAsync(LavalinkTrack track, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        return PlayAsync(new TrackQueueItem(new TrackReference(track)), properties, cancellationToken);
    }

    public ValueTask PlayAsync(string identifier, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        return PlayAsync(new TrackQueueItem(new TrackReference(identifier)), properties, cancellationToken);
    }

    public ValueTask PlayAsync(TrackReference trackReference, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        return PlayAsync(new TrackQueueItem(trackReference), properties, cancellationToken);
    }

    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();

        var model = await ApiClient
            .GetPlayerAsync(SessionId, GuildId, cancellationToken)
            .ConfigureAwait(false);

        Refresh(model!);
    }

    public virtual async ValueTask ResumeAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();

        var properties = new PlayerUpdateProperties { IsPaused = false, };
        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);

        _logger.PlayerResumed(Label);
    }

    public virtual async ValueTask SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();

        var properties = new PlayerUpdateProperties { Position = position, };
        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask SeekAsync(TimeSpan position, SeekOrigin seekOrigin, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        var targetPosition = seekOrigin switch
        {
            SeekOrigin.Begin => position,
            SeekOrigin.Current => Position!.Value.Position + position, // TODO: check how this works with time stretch
            SeekOrigin.End => CurrentTrack!.Duration + position,

            _ => throw new ArgumentOutOfRangeException(
                nameof(seekOrigin),
                seekOrigin,
                "Invalid seek origin."),
        };

        return SeekAsync(targetPosition, cancellationToken);
    }

    public virtual async ValueTask SetVolumeAsync(float volume, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var properties = new PlayerUpdateProperties { Volume = volume, };
        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);

        _logger.PlayerVolumeChanged(Label, volume);
    }

    public virtual async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        // Store stopped track to restore state information in TrackEnd dispatch
        _stoppedItem = Interlocked.Exchange(ref _currentItem, null);

        var properties = new PlayerUpdateProperties
        {
            TrackData = new Optional<string?>(null),
        };

        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);

        _logger.PlayerStopped(Label);

        if (_disconnectOnStop)
        {
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    async ValueTask ILavalinkPlayerListener.NotifyWebSocketClosedAsync(WebSocketCloseStatus closeStatus, string reason, bool byRemote, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await NotifyWebSocketClosedAsync(closeStatus, reason, byRemote, cancellationToken).ConfigureAwait(false);
    }

    protected void EnsureNotDestroyed()
    {
#if NET7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_disposed is not 0, this);
#else
        if (_disposed is not 0)
        {
            throw new ObjectDisposedException(nameof(LavalinkPlayer));
        }
#endif
    }

    protected virtual ValueTask NotifyWebSocketClosedAsync(WebSocketCloseStatus closeStatus, string reason, bool byRemote = false, CancellationToken cancellationToken = default) => default;

    protected virtual ValueTask NotifyTrackEndedAsync(ITrackQueueItem track, TrackEndReason endReason, CancellationToken cancellationToken = default) => default;

    protected virtual ValueTask NotifyChannelUpdateAsync(ulong? voiceChannelId, CancellationToken cancellationToken = default) => default;

    protected virtual ValueTask NotifyTrackExceptionAsync(ITrackQueueItem track, TrackException exception, CancellationToken cancellationToken = default) => default;

    protected virtual ValueTask NotifyTrackStartedAsync(ITrackQueueItem track, CancellationToken cancellationToken = default) => default;

    protected virtual ValueTask NotifyTrackStuckAsync(ITrackQueueItem track, TimeSpan threshold, CancellationToken cancellationToken = default) => default;

    protected virtual ValueTask NotifyFiltersUpdatedAsync(IPlayerFilters filters, CancellationToken cancellationToken = default) => default;

    private async ValueTask PerformUpdateAsync(PlayerUpdateProperties properties, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        var model = await ApiClient
            .UpdatePlayerAsync(SessionId, GuildId, properties, cancellationToken)
            .ConfigureAwait(false);

        Refresh(model!);

        var state = this switch
        {
            { IsPaused: true, } => PlayerState.Paused,
            { CurrentTrack: null, } => PlayerState.NotPlaying,
            _ => PlayerState.Playing,
        };

        await UpdateStateAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private void Refresh(PlayerInformationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Debug.Assert(model.GuildId == GuildId);

        IsPaused = model.IsPaused;

        var currentTrack = CurrentTrack;

        if (currentTrack is null && model.CurrentTrack is null)
        {
            CurrentItem = null;
        }
        else if (model.CurrentTrack?.Information.Identifier != currentTrack?.Identifier)
        {
            // This indicates that the track had been restored from API information
            Debug.Assert(_nextItem is not null || model.CurrentTrack?.Information.Identifier == currentTrack?.Identifier);

            var track = model.CurrentTrack is null
                ? _nextItem
                : new TrackQueueItem(new TrackReference(LavalinkApiClient.CreateTrack(model.CurrentTrack)));

            CurrentItem = track;

            Interlocked.Increment(ref _trackVersion);
        }

        Volume = model.Volume;

        // TODO: restore filters
    }

    internal async ValueTask UpdateFiltersAsync(PlayerFilterMapModel filterMap, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var properties = new PlayerUpdateProperties
        {
            Filters = filterMap,
        };

        _logger.PlayerFiltersChanged(Label);

        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);
        await NotifyFiltersUpdatedAsync(Filters, cancellationToken).ConfigureAwait(false);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) is not 0)
        {
            return;
        }

        if (_previousVoiceServer is not null)
        {
            Diagnostics.VoiceServer.Add(-1, KeyValuePair.Create<string, object?>("server", _previousVoiceServer));
            _previousVoiceServer = null;
        }

        await UpdateStateAsync(PlayerState.Destroyed).ConfigureAwait(false);

        // Dispose the lifecycle to notify the player is being destroyed
        await using var _ = _playerLifecycle.ConfigureAwait(false);

        _logger.PlayerDestroyed(Label);

        await ApiClient
            .DestroyPlayerAsync(SessionId, GuildId)
            .ConfigureAwait(false);

        if (_disconnectOnDestroy)
        {
            await DiscordClient
                .SendVoiceUpdateAsync(GuildId, null, false, false)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public ValueTask NotifyPlayerUpdateAsync(
        DateTimeOffset timestamp,
        TimeSpan position,
        bool connected,
        TimeSpan? latency,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _unstretchedRelativePosition = position;
        _syncedAt = timestamp;

        ConnectionState = new PlayerConnectionState(
            IsConnected: connected,
            Latency: latency);

        _logger.PlayerUpdateProcessed(Label, timestamp, position, connected, latency);

        return default;
    }

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var _ = this.ConfigureAwait(false);

        await DiscordClient
            .SendVoiceUpdateAsync(GuildId, null, false, false, cancellationToken)
            .ConfigureAwait(false);

        _disconnectOnDestroy = false;
    }

    private async ValueTask UpdateStateAsync(PlayerState playerState, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

#if DEBUG
        var computedState = this switch
        {
            { _disposed: not 0, } => PlayerState.Destroyed,
            { IsPaused: true, } => PlayerState.Paused,
            { CurrentTrack: null, } => PlayerState.NotPlaying,
            _ => PlayerState.Playing,
        };

        Debug.Assert(playerState == computedState, $"playerState ({playerState}) == computedState ({computedState})");
#endif

        if (playerState == State)
        {
            return;
        }

        State = playerState;

        await _playerLifecycle
            .NotifyStateChangedAsync(this, playerState, cancellationToken)
            .ConfigureAwait(false);

        _previousStateCounter?.Add(-1, KeyValuePair.Create<string, object?>("label", Label));

        _previousStateCounter = playerState switch
        {
            PlayerState.Paused => Diagnostics.PausedPlayers,
            PlayerState.NotPlaying => Diagnostics.NotPlayingPlayers,
            PlayerState.Playing => Diagnostics.PlayingPlayers,
            _ => null,
        };

        _previousStateCounter?.Add(1, KeyValuePair.Create<string, object?>("label", Label));
    }

    protected virtual async ValueTask NotifyVoiceStateUpdatedAsync(VoiceState voiceState, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_disposed is 1)
        {
            return;
        }

        if (VoiceState.VoiceChannelId != VoiceChannelId)
        {
            VoiceServer = null;
        }

        VoiceState = voiceState;
        VoiceChannelId = voiceState.VoiceChannelId ?? VoiceChannelId;

        await NotifyChannelUpdateCoreAsync(voiceState.VoiceChannelId, cancellationToken).ConfigureAwait(false);

        if (voiceState.VoiceChannelId is not null)
        {
            await UpdateVoiceCredentialsAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    protected virtual async ValueTask NotifyVoiceServerUpdatedAsync(VoiceServer voiceServer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_disposed is 1)
        {
            return;
        }

        VoiceServer = voiceServer;
        await UpdateVoiceCredentialsAsync(cancellationToken).ConfigureAwait(false);
    }

    ValueTask ILavalinkPlayerListener.NotifyVoiceStateUpdatedAsync(VoiceState voiceState, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return NotifyVoiceStateUpdatedAsync(voiceState, cancellationToken);
    }

    ValueTask ILavalinkPlayerListener.NotifyVoiceServerUpdatedAsync(VoiceServer voiceServer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return NotifyVoiceServerUpdatedAsync(voiceServer, cancellationToken);
    }

    private ValueTask UpdateVoiceCredentialsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_disposed is 1 || VoiceServer is null || VoiceServer is { Endpoint: null, } or { Token: null, } || VoiceState.SessionId is null)
        {
            return ValueTask.CompletedTask;
        }

        var properties = new PlayerUpdateProperties
        {
            VoiceState = new VoiceStateProperties(
                Token: VoiceServer.Value.Token,
                Endpoint: VoiceServer.Value.Endpoint,
                SessionId: VoiceState.SessionId),
        };

        var voiceServerName = GetVoiceServerName(VoiceServer.Value);

        if (!StringComparer.Ordinal.Equals(voiceServerName, _previousVoiceServer))
        {
            if (_previousVoiceServer is not null)
            {
                Diagnostics.VoiceServer.Add(-1, KeyValuePair.Create<string, object?>("server", _previousVoiceServer));
            }

            if (voiceServerName is not null)
            {
                Diagnostics.VoiceServer.Add(1, KeyValuePair.Create<string, object?>("server", voiceServerName));
            }

            _previousVoiceServer = voiceServerName;
        }

        return PerformUpdateAsync(properties, cancellationToken);
    }

    [return: NotNullIfNotNull(nameof(track))]
    private ITrackQueueItem? ResolveTrackQueueItem(LavalinkTrack? track)
    {
        if (track is null)
        {
            Debug.Assert(CurrentItem is null);
            return null;
        }

        Debug.Assert(track.Identifier == CurrentItem?.Track?.Identifier);

        if (track.Identifier == CurrentItem?.Track?.Identifier)
        {
            return CurrentItem;
        }

        return new TrackQueueItem(new TrackReference(track));
    }

    private static string? GetVoiceServerName(VoiceServer voiceServer)
    {
        return voiceServer.Endpoint.Split('.', StringSplitOptions.TrimEntries).FirstOrDefault();
    }
}

internal static partial class Logging
{
    [LoggerMessage(1, LogLevel.Trace, "[{Label}] Processed player update (absolute timestamp: {AbsoluteTimestamp}, relative track position: {Position}, connected: {IsConnected}, latency: {Latency}).", EventName = nameof(PlayerUpdateProcessed))]
    public static partial void PlayerUpdateProcessed(this ILogger<LavalinkPlayer> logger, string label, DateTimeOffset absoluteTimestamp, TimeSpan position, bool isConnected, TimeSpan? latency);

    [LoggerMessage(2, LogLevel.Information, "[{Label}] Player moved to channel {ChannelId}.", EventName = nameof(PlayerMoved))]
    public static partial void PlayerMoved(this ILogger<LavalinkPlayer> logger, string label, ulong? channelId);

    [LoggerMessage(3, LogLevel.Information, "[{Label}] Player connected to channel {ChannelId}.", EventName = nameof(PlayerConnected))]
    public static partial void PlayerConnected(this ILogger<LavalinkPlayer> logger, string label, ulong? channelId);

    [LoggerMessage(4, LogLevel.Information, "[{Label}] Player disconnected from channel.", EventName = nameof(PlayerDisconnected))]
    public static partial void PlayerDisconnected(this ILogger<LavalinkPlayer> logger, string label);

    [LoggerMessage(5, LogLevel.Information, "[{Label}] Player paused.", EventName = nameof(PlayerPaused))]
    public static partial void PlayerPaused(this ILogger<LavalinkPlayer> logger, string label);

    [LoggerMessage(6, LogLevel.Information, "[{Label}] Player resumed.", EventName = nameof(PlayerResumed))]
    public static partial void PlayerResumed(this ILogger<LavalinkPlayer> logger, string label);

    [LoggerMessage(7, LogLevel.Information, "[{Label}] Player stopped.", EventName = nameof(PlayerStopped))]
    public static partial void PlayerStopped(this ILogger<LavalinkPlayer> logger, string label);

    [LoggerMessage(8, LogLevel.Information, "[{Label}] Player volume changed to {Volume}.", EventName = nameof(PlayerVolumeChanged))]
    public static partial void PlayerVolumeChanged(this ILogger<LavalinkPlayer> logger, string label, float volume);

    [LoggerMessage(9, LogLevel.Information, "[{Label}] Player filters changed.", EventName = nameof(PlayerFiltersChanged))]
    public static partial void PlayerFiltersChanged(this ILogger<LavalinkPlayer> logger, string label);

    [LoggerMessage(10, LogLevel.Information, "[{Label}] Player destroyed.", EventName = nameof(PlayerDestroyed))]
    public static partial void PlayerDestroyed(this ILogger<LavalinkPlayer> logger, string label);
}

file static class Diagnostics
{
    static Diagnostics()
    {
        var meter = new Meter("Lavalink4NET");

        PausedPlayers = meter.CreateUpDownCounter<int>("paused-players", "Players");
        NotPlayingPlayers = meter.CreateUpDownCounter<int>("not-playing-players", "Players");
        PlayingPlayers = meter.CreateUpDownCounter<int>("playing-players", "Players");
        VoiceServer = meter.CreateUpDownCounter<int>("voice-server", "Uses");
    }

    public static UpDownCounter<int> PausedPlayers { get; }

    public static UpDownCounter<int> NotPlayingPlayers { get; }

    public static UpDownCounter<int> PlayingPlayers { get; }

    public static UpDownCounter<int> VoiceServer { get; }
}