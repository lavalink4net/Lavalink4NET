using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Extensions;
using ExampleBot;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = new HostApplicationBuilder(args);

// Your host service
builder.Services.AddHostedService<ApplicationHost>();


// DSharpPlus
builder.Services.AddDiscordClient("Your token here", DiscordIntents.AllUnprivileged);
builder.Services.AddCommandsExtension((IServiceProvider sp, CommandsExtension c) => c.AddCommands(typeof(MusicCommands).Assembly));


// Lavalink4NET
builder.Services.AddLavalink();


// Logging
builder.Services.AddLogging(s => s.AddConsole().SetMinimumLevel(LogLevel.Debug));


// Start the host
builder.Build().Run();


file sealed class ApplicationHost : BackgroundService
{
    private readonly DiscordClient _discordClient;

    public ApplicationHost(DiscordClient discordClient)
    {
        ArgumentNullException.ThrowIfNull(discordClient);
        _discordClient = discordClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Connect to discord gateway and initialize node connection
        await _discordClient
            .ConnectAsync()
            .ConfigureAwait(false);

        await Task
            .Delay(-1, stoppingToken)
            .ConfigureAwait(false);
    }
}