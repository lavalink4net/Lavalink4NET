namespace Lavalink4NET.Experiments.Receive.Server;

using System;
using Microsoft.Extensions.DependencyInjection;

internal sealed class LavalinkKestrelWebHostBuilder : IWebHostBuilder
{
    private readonly IServiceCollection _serviceCollection;

    public LavalinkKestrelWebHostBuilder(IServiceCollection serviceCollection)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        _serviceCollection = serviceCollection;
    }

    IWebHostBuilder IWebHostBuilder.ConfigureServices(Action<IServiceCollection> configureServices)
    {
        configureServices(_serviceCollection);
        return this;
    }


    IWebHost IWebHostBuilder.Build() => throw new NotImplementedException();

    IWebHostBuilder IWebHostBuilder.ConfigureAppConfiguration(Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate)
    {
        configureDelegate(new WebHostBuilderContext(), new ConfigurationBuilder());
        return this;
    }

    IWebHostBuilder IWebHostBuilder.ConfigureServices(Action<WebHostBuilderContext, IServiceCollection> configureServices)
    {
        configureServices(new WebHostBuilderContext(), _serviceCollection);
        return this;
    }

    string IWebHostBuilder.GetSetting(string key) => throw new NotImplementedException();

    IWebHostBuilder IWebHostBuilder.UseSetting(string key, string? value) => throw new NotImplementedException();
}
