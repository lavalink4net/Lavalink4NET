namespace Lavalink4NET.Experiments.Receive.Extensions;

using Lavalink4NET.Players;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLavalinkReceive(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IVoiceServerSessionManager, VoiceServerSessionManager>();
        services.TryAddSingleton<ILavalinkVoiceServer, LavalinkVoiceServer>();
        services.Configure<LavalinkVoiceServerOptions>(static _ => { });

        services.AddHostedService<LavalinkVoiceServerHost>();

        services.Replace(ServiceDescriptor.Singleton<ILavalinkVoiceServerInterceptor, LavalinkReceiveVoiceServerInterceptor>());

        return services;
    }
}
