namespace Lavalink4NET.Extensions;

using System;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;

public static class QueuedLavalinkPlayerExtensions
{
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
