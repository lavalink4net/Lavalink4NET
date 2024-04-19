namespace Lavalink4NET.Extensions;

using System;
using Lavalink4NET.Clients;
using Lavalink4NET.Integrations;
using Lavalink4NET.Players;
using Lavalink4NET.Rest;
using Lavalink4NET.Socket;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Internal;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLavalinkCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLogging();
        services.AddHttpClient();
        services.AddMemoryCache();

        services.TryAddSingleton<ISystemClock, SystemClock>();
        services.TryAddSingleton<ILavalinkSocketFactory, LavalinkSocketFactory>();
        services.TryAddSingleton<ILavalinkApiClientFactory, LavalinkApiClientFactory>();
        services.TryAddSingleton<IAudioService, AudioService>();
        services.TryAddSingleton<ITrackManager, TrackManager>();
        services.TryAddSingleton<IPlayerManager, PlayerManager>();
        services.TryAddSingleton<IIntegrationManager, IntegrationManager>();
        services.TryAddSingleton<ILavalinkApiClientProvider, LavalinkApiClientProvider>();
        services.TryAddSingleton<ILavalinkSessionProvider, LavalinkSessionProvider>();
        services.TryAddSingleton<IReconnectStrategy, ExponentialBackoffReconnectStrategy>();
        services.TryAddSingleton<ILavalinkVoiceServerInterceptor, LavalinkVoiceServerInterceptor>();

        services.AddHostedService<AudioServiceHost>();

        return services;
    }

    public static IServiceCollection AddLavalink<TClient>(this IServiceCollection services) where TClient : class, IDiscordClientWrapper
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLavalinkCore();

        services.AddOptions<AudioServiceOptions>();
        services.TryAddSingleton<IDiscordClientWrapper, TClient>();

        return services;
    }

    public static IServiceCollection ConfigureLavalink(this IServiceCollection services, Action<AudioServiceOptions> options)
    {
        return services.Configure(options);
    }

    public static IServiceCollection AddReconnectStrategy<TReconnectStrategy>(this IServiceCollection services)
        where TReconnectStrategy : class, IReconnectStrategy
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Singleton<IReconnectStrategy, TReconnectStrategy>());

        return services;
    }

    public static IServiceCollection AddReconnectStrategy<TReconnectStrategy>(this IServiceCollection services, Func<IServiceProvider, TReconnectStrategy> implementationFactory)
        where TReconnectStrategy : class, IReconnectStrategy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(implementationFactory);

        services.Replace(ServiceDescriptor.Singleton<IReconnectStrategy, TReconnectStrategy>(implementationFactory));

        return services;
    }

    public static IServiceCollection AddReconnectStrategy<TReconnectStrategy>(this IServiceCollection services, TReconnectStrategy reconnectStrategy)
        where TReconnectStrategy : class, IReconnectStrategy
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(ServiceDescriptor.Singleton<IReconnectStrategy>(reconnectStrategy));

        return services;
    }
}
