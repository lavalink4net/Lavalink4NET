namespace Lavalink4NET;

using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;

internal interface ILavalinkNodeListener
{
    ValueTask OnTrackEndedAsync(TrackEndedEventArgs eventArgs, CancellationToken cancellationToken = default);

    ValueTask OnTrackExceptionAsync(TrackExceptionEventArgs eventArgs, CancellationToken cancellationToken = default);

    ValueTask OnTrackStartedAsync(TrackStartedEventArgs eventArgs, CancellationToken cancellationToken = default);

    ValueTask OnTrackStuckAsync(TrackStuckEventArgs eventArgs, CancellationToken cancellationToken = default);

    ValueTask OnStatisticsUpdatedAsync(StatisticsUpdatedEventArgs eventArgs, CancellationToken cancellationToken = default);

    ValueTask OnWebSocketClosedAsync(WebSocketClosedEventArgs eventArgs, CancellationToken cancellationToken = default);

    ValueTask OnConnectionClosedAsync(ConnectionClosedEventArgs eventArgs, CancellationToken cancellationToken = default);

    ValueTask OnConnectionStartedAsync(ConnectionStartedEventArgs eventArgs, CancellationToken cancellationToken = default);

    ValueTask OnConnectionReadyAsync(ConnectionReadyEventArgs eventArgs, CancellationToken cancellationToken = default);
}
