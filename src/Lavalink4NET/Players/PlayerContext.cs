namespace Lavalink4NET.Players;

using System;
using Lavalink4NET.Clients;
using Microsoft.Extensions.Internal;

internal sealed record class PlayerContext(
    IServiceProvider? ServiceProvider,
    ILavalinkSessionProvider SessionProvider,
    IDiscordClientWrapper DiscordClient,
    ISystemClock SystemClock,
    IPlayerLifecycleNotifier? LifecycleNotifier,
    ILavalinkVoiceServerInterceptor VoiceServerInterceptor);
