namespace ExampleBot;

﻿using NetCord.Rest;
using Lavalink4NET;
using Lavalink4NET.NetCord;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using NetCord.Services.ApplicationCommands;

public class MusicModule(IAudioService audioService) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("play", "Plays a track!")]
    public async Task PlayAsync([SlashCommandParameter(Description = "The query to search for")] string query)
    {
        await RespondAsync(InteractionCallback.DeferredMessage());

        var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: PlayerChannelBehavior.Join);

        var result = await audioService.Players
            .RetrieveAsync(Context, playerFactory: PlayerFactory.Queued, retrieveOptions);

        if (!result.IsSuccess)
        {
            var errorMessage = GetErrorMessage(result.Status);
            await FollowupAsync(errorMessage);
            return;
        }

        var player = result.Player;

        var track = await audioService.Tracks
            .LoadTrackAsync(query, TrackSearchMode.SoundCloud);

        if (track is null)
        {
            await FollowupAsync("No tracks found.");
            return;
        }

        await player.PlayAsync(track);

        await FollowupAsync($"Now playing: {track.Title}");
    }

    private static string GetErrorMessage(PlayerRetrieveStatus retrieveStatus) => retrieveStatus switch
    {
        PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
        PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
        _ => "Unknown error.",
    };
}
