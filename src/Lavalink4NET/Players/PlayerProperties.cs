namespace Lavalink4NET.Players;

using System;
using Lavalink4NET.Clients;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Rest;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed record class PlayerProperties<TPlayer, TOptions>(
    PlayerContext Context,
    ITrackQueueItem? InitialTrack,
    PlayerInformationModel InitialState,
    string Label,
    ulong VoiceChannelId,
    string SessionId,
    IPlayerLifecycle Lifecycle,
    ILavalinkApiClient ApiClient,
    IOptions<TOptions> Options,
    ILogger<TPlayer> Logger)
    : IPlayerProperties<TPlayer, TOptions>
    where TOptions : LavalinkPlayerOptions
    where TPlayer : ILavalinkPlayer
{
    public IDiscordClientWrapper DiscordClient => Context.DiscordClient;

    public IServiceProvider? ServiceProvider => Context.ServiceProvider;

    public ISystemClock SystemClock => Context.SystemClock;

    public ILavalinkVoiceServerInterceptor VoiceServerInterceptor => Context.VoiceServerInterceptor;
}
