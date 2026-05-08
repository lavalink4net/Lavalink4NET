using System;

namespace Lavalink4NET.Players;

using Lavalink4NET.Rest.Entities.Tracks;

public record class LavalinkPlayerOptions
{
    public bool DisconnectOnStop { get; set; }

    public bool DisconnectOnDestroy { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether Lavalink4NET should attempt to recover voice connectivity
    ///     by re-sending a voice state update when Discord closes the voice websocket (e.g. 4014/4015).
    /// </summary>
    public bool EnableVoiceAutoReconnect { get; set; } = true;

    /// <summary>
    ///     Gets or sets the minimum time between automatic voice reconnect attempts.
    /// </summary>
    public TimeSpan VoiceReconnectCooldown { get; set; } = TimeSpan.FromSeconds(10);

    public string? Label { get; set; }

    public ITrackQueueItem? InitialTrack { get; set; }

    public TimeSpan? InitialPosition { get; set; }

    public TrackLoadOptions InitialLoadOptions { get; set; }

    public float? InitialVolume { get; set; }

    public bool SelfDeaf { get; set; }

    public bool SelfMute { get; set; }
}
