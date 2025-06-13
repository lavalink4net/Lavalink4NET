namespace ExampleBot;

using System;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.Commands;
using Lavalink4NET;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using DSharpPlus.Commands.ContextChecks;

public class MusicCommands
{
    private readonly IAudioService _audioService;

    public MusicCommands(IAudioService audioService)
    {
        ArgumentNullException.ThrowIfNull(audioService);
        _audioService = audioService;
    }

    [Command("play")]
    [Description("Plays music")]
    [DirectMessageUsage(DirectMessageUsage.DenyDMs)]
    public async Task Play(CommandContext context,

        [Parameter("query")]
        [Description("Track to play")]
        string query)
    {
        // This operation could take a while - deferring the interaction lets Discord know we've
        // received it and lets us update it later. Users see a "thinking..." state.
        await context.DeferResponseAsync().ConfigureAwait(false);

        // Attempt to get the player
        var player = await GetPlayerAsync(context, connectToVoiceChannel: true).ConfigureAwait(false);

        // If something went wrong getting the player, don't attempt to play any tracks
        if (player is null)
            return;

        // Fetch the tracks
        var track = await _audioService.Tracks
            .LoadTrackAsync(query, TrackSearchMode.YouTube)
            .ConfigureAwait(false);

        // If no results were found
        if (track is null)
        {
            var errorResponse = new DiscordFollowupMessageBuilder()
                .WithContent("😖 No results.")
                .AsEphemeral();

            await context
                .EditResponseAsync(errorResponse)
                .ConfigureAwait(false);

            return;
        }

        // Play the track
        var position = await player
            .PlayAsync(track)
            .ConfigureAwait(false);

        // If it was added to the queue
        if (position is 0)
        {
            await context
                .FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"🔈 Playing: {track.Uri}"))
                .ConfigureAwait(false);
        }

        // If it was played directly
        else
        {
            await context
                .FollowupAsync(new DiscordFollowupMessageBuilder().WithContent($"🔈 Added to queue: {track.Uri}"))
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<QueuedLavalinkPlayer?> GetPlayerAsync(CommandContext commandContext, bool connectToVoiceChannel = true)
    {
        ArgumentNullException.ThrowIfNull(commandContext);

        var retrieveOptions = new PlayerRetrieveOptions(
            ChannelBehavior: connectToVoiceChannel ? PlayerChannelBehavior.Join : PlayerChannelBehavior.None);

        var playerOptions = new QueuedLavalinkPlayerOptions { HistoryCapacity = 10000 };

        var result = await _audioService.Players
            .RetrieveAsync(commandContext.Guild!.Id, commandContext.Member?.VoiceState.ChannelId, playerFactory: PlayerFactory.Queued, Options.Create(playerOptions), retrieveOptions)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
                PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
                _ => "Unknown error.",
            };

            var errorResponse = new DiscordFollowupMessageBuilder()
                .WithContent(errorMessage)
                .AsEphemeral();

            await commandContext
                .FollowupAsync(errorResponse)
                .ConfigureAwait(false);

            return null;
        }

        return result.Player;
    }
}