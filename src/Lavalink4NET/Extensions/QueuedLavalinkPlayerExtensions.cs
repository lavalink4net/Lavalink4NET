namespace Lavalink4NET.Extensions;

using System;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;

public static class QueuedLavalinkPlayerExtensions
{
    /// <summary>
    /// Adds a <see cref="TrackLoadResult"/> to the player's queue.<br/>
    /// Returns the index of the track in the queue (or the last track added to the queue if <paramref name="loadResult"/> is a playlist)
    /// </summary>
    /// <param name="player">The player to enqueue the load result to.</param>
    /// <param name="loadResult">The load result you want to enqueue.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The index of the track in the queue (or the last track added to the queue if <paramref name="loadResult"/> is a playlist)</returns>
    /// <exception cref="InvalidOperationException">When the <paramref name="loadResult"/> contains no tracks.</exception>
    public static async ValueTask<int> PlayAsync(this IQueuedLavalinkPlayer player, TrackLoadResult loadResult, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int? queueOffset = default;

        if (loadResult.Playlist is null)
        {
            // Single track
            queueOffset = await player
              .PlayAsync(loadResult.Track!, cancellationToken: cancellationToken)
              .ConfigureAwait(false);
        }
        else
        {
            // Playlist
            foreach (var track in loadResult.Tracks)
            {
                queueOffset = await player
                  .PlayAsync(track, cancellationToken: cancellationToken)
                  .ConfigureAwait(false);
            }
        }

        return queueOffset ?? throw new InvalidOperationException("No track is present.");
    }
}
