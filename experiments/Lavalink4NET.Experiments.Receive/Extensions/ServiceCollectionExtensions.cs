namespace Lavalink4NET.Experiments.Receive.Extensions;

using Lavalink4NET.Experiments.Receive.Connections;
using Lavalink4NET.Experiments.Receive.Server;
using Lavalink4NET.Experiments.Receive.Sessions;
using Lavalink4NET.Players;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLavalinkReceive(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IVoiceServerSessionManager, VoiceServerSessionManager>();
        services.TryAddSingleton<IVoiceProtocolHandler, VoiceProtocolHandler>();
        services.TryAddSingleton<IVoiceConnectionHandler, VoiceConnectionHandler>();

        services.TryAddSingleton<ILavalinkVoiceServer, LavalinkVoiceServer>();

        services.Configure<LavalinkVoiceServerOptions>(static _ => { });

        services.AddHostedService<LavalinkVoiceServerHost>();

        services.Replace(ServiceDescriptor.Singleton<ILavalinkVoiceServerInterceptor, LavalinkReceiveVoiceServerInterceptor>());

        return services;
    }
}
