using System;
using DSharpPlus.Extensions;
using Lavalink4NET.DSharpPlus;
using Microsoft.Extensions.DependencyInjection;

namespace Lavalink4NET.Extensions;

/// <summary>
/// A collection of extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Lavalink4NET DSharpPlus extension to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the extension to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLavalink(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLavalink<DiscordClientWrapper>();

        services.Configure<DiscordClientWrapper>(client =>
            services.ConfigureEventHandlers(events =>
            {
                events.HandleGuildDownloadCompleted(client.OnGuildDownloadCompleted);
                events.HandleVoiceServerUpdated(client.OnVoiceServerUpdated);
                events.HandleVoiceStateUpdated(client.OnVoiceStateUpdated);
            })
        );

        return services;
    }
}
