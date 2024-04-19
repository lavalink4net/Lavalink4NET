namespace Lavalink4NET.Players;

using System;
using Lavalink4NET.Clients;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Rest;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public interface IPlayerProperties<out TPlayer, out TOptions>
    where TOptions : LavalinkPlayerOptions
    where TPlayer : ILavalinkPlayer
{
    ILavalinkApiClient ApiClient { get; }

    IDiscordClientWrapper DiscordClient { get; }

    PlayerInformationModel InitialState { get; }

    ITrackQueueItem? InitialTrack { get; }

    string Label { get; }

    ILogger<TPlayer> Logger { get; }

    ISystemClock SystemClock { get; }

    IOptions<TOptions> Options { get; }

    IServiceProvider? ServiceProvider { get; }

    ILavalinkVoiceServerInterceptor VoiceServerInterceptor { get; }

    ulong VoiceChannelId { get; }

    string SessionId { get; }

    IPlayerLifecycle Lifecycle { get; }
}
