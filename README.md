<!-- Banner -->
<a href="https://github.com/angelobreuer/Lavalink4NET/">
	<img src="https://i.imgur.com/e1jv23h.png"/>
</a>

<!-- Center badges -->
<p align="center"><b>High performance Lavalink wrapper for .NET</b></p>

> [!IMPORTANT]
> We now have a FAQ channel for quick questions on the Lavalink4NET Support Server

[Lavalink4NET](https://github.com/angelobreuer/Lavalink4NET) is a [Lavalink](https://github.com/freyacodes/Lavalink) wrapper with node clustering, caching and custom players for .NET with support for [Discord.Net](https://github.com/RogueException/Discord.Net), [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus/), [Remora](https://github.com/Remora/Remora.Discord), and [NetCord](https://github.com/KubaZ2/NetCord).

[![Lavalink4NET Support Server Banner](https://discordapp.com/api/guilds/894533462428635146/embed.png?style=banner3)](https://discord.gg/cD4qTmnqRg)

### Features

- ⚖️ **Node Clustering / Load Balancing**<br>Distribute load across nodes for efficient and reliable audio playback (for large scale bots).

- ✳️ **Extensible**<br>Customize and enhance features using plugins to match your bot's needs.

- 🎤 **Lyrics**<br>Display song lyrics alongside audio playback to enrich the user experience.

- 🗳️ **Queueing / Voting-System**<br>Let users queue tracks and vote on the next songs, enhancing collaborative playlists.

- 🎵 **Track Decoding and Encoding**<br>Lavalink4NET supports high efficient track decoding and encoding of lavaplayer track identifiers.

- 🔄 **Auto-Reconnect and Resuming**<br>Maintain uninterrupted audio playback during connection disruptions.

- 🔌 **Fully Asynchronous Interface**<br>Effortlessly communicate with the Lavalink audio server without causing delays in your bot. All actions that can be offloaded are asynchronous and can be canceled at any time if needed.

- 📝 **Logging** *(optional)*<br>Enable insights for troubleshooting and debugging.

- ⚡ **Request Caching** *(optional)*<br>Improve performance by reducing redundant requests.

- ⏱️ **Inactivity Tracking** *(optional)*<br>Monitor inactive players and disconnect them to save resources.

- 🖋️ **Supports Lavalink plugins**<br>Expand capabilities by integrating with Lavalink plugins.

- 🎶 **Custom players**<br>Manage audio playback instances tailored to your bot's requirements.

- 🖼️ **Artwork resolution**<br>Lavalink4NET allows the user to resolve artwork images for the tracks to display an appealing image to the user.

- 🎚️ **Audio filter support**<br>Lavalink4NET supports all audio filters provided by lavaplayer and even more when installing the ExtraFilters plugin.

- 📊 **Statistics tracking support**<br>Lavalink4NET supports tracking and evaluation of node statistics. In clustering, node statistics can be used to evaluate the best node for efficient resource usage.

- ➕ **Compatible with [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus), [Discord.Net](https://github.com/discord-net/Discord.Net), [Remora](https://github.com/Remora/Remora.Discord), and [NetCord](https://github.com/KubaZ2/NetCord).**<br>Lavalink4NET has an adaptive client API, meaning it can support any discord client. Currently, DSharpPlus, Discord.Net, Remora and NetCord are supported out-of-the-box.

### Documentation

> [!IMPORTANT]
> You can find the documentation for Lavalink4NET v4 [here](https://lavalink4net.com/docs/introduction/intro).

### Components

Lavalink4NET offers high flexibility and extensibility by providing an isolated interface. You can extend Lavalink4NET by adding additional packages which add integrations with other services, support for additional lavalink/lavaplayer plugins, or additional client support.

#### _Client Support_

- [**Lavalink4NET.Discord.Net**](https://www.nuget.org/packages/Lavalink4NET.Discord.Net/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Discord.Net.svg?style=flat-square)<br>Enhance your Discord bots with advanced audio playback using this integration for Lavalink4NET. Designed for end users building Discord.Net-based applications.

- [**Lavalink4NET.DSharpPlus**](https://www.nuget.org/packages/Lavalink4NET.DSharpPlus/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.DSharpPlus.svg?style=flat-square)<br>Add powerful audio playback to your DSharpPlus-based applications with this integration for Lavalink4NET. Suitable for end users developing with DSharpPlus.

- [**Lavalink4NET.Remora.Discord**](https://www.nuget.org/packages/Lavalink4NET.Remora.Discord/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Remora.Discord.svg?style=flat-square)<br>Add powerful audio playback to your Remora-based discord bots with this integration for Lavalink4NET. Suitable for end users developing with Remora.

- [**Lavalink4NET.NetCord**](https://www.nuget.org/packages/Lavalink4NET.NetCord/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.NetCord.svg?style=flat-square)<br>Add powerful audio playback to your NetCord-based discord bots with this integration for Lavalink4NET. Suitable for end users developing with NetCord.

#### _Clustering_

- [**Lavalink4NET.Cluster**](https://www.nuget.org/packages/Lavalink4NET.Cluster/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Cluster.svg?style=flat-square)<br>Scale and improve performance by using multiple Lavalink nodes with this cluster support module. Ideal for handling high-demand music streaming applications.

#### _Integrations_

- [**Lavalink4NET.Integrations.ExtraFilters**](https://www.nuget.org/packages/Lavalink4NET.Integrations.ExtraFilters/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Integrations.ExtraFilters.svg?style=flat-square)<br>Enhance your audio playback experience with extra filters in Lavalink4NET. Apply additional audio effects and modifications to customize the sound output. Requires the installation of the corresponding plugin on the Lavalink node.

- [**Lavalink4NET.Integrations.SponsorBlock**](https://www.nuget.org/packages/Lavalink4NET.Integrations.SponsorBlock/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Integrations.SponsorBlock.svg?style=flat-square)<br>Integrate SponsorBlock functionality into Lavalink4NET. Automatically skip sponsored segments in videos for a seamless and uninterrupted playback experience. Requires the installation of the corresponding plugin on the Lavalink node.

- [**Lavalink4NET.Integrations.TextToSpeech**](https://www.nuget.org/packages/Lavalink4NET.Integrations.TextToSpeech/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Integrations.TextToSpeech.svg?style=flat-square)<br>Enable text-to-speech functionality in Lavalink4NET. Convert written text into spoken words, allowing your application to generate and play audio from text inputs. Requires the installation of the corresponding plugin on the Lavalink node.

- [**Lavalink4NET.Integrations.LyricsJava**](https://www.nuget.org/packages/Lavalink4NET.Integrations.LyricsJava/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Integrations.LyricsJava.svg?style=flat-square)<br>Fetch timed lyrics from youtube or non-timed lyrics from genius. Automatically fetches lyrics for the current track. Requires the installation of the corresponding plugin on the Lavalink node.

#### _Services_

- [**Lavalink4NET.Lyrics**](https://www.nuget.org/packages/Lavalink4NET.Lyrics/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Lyrics.svg?style=flat-square)<br>Fetch and display song lyrics from lyrics.ovh with this lyrics service integrated with Lavalink4NET. Enhance the music experience for your users.

- [**Lavalink4NET.Artwork**](https://www.nuget.org/packages/Lavalink4NET.Artwork/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Artwork.svg?style=flat-square)<br>Artwork resolution service for the Lavalink4NET client library.

- [**Lavalink4NET.InactivityTracking**](https://www.nuget.org/packages/Lavalink4NET.InactivityTracking/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.InactivityTracking.svg?style=flat-square)<br>Optimize resource usage by tracking and disconnecting inactive players. Ensure efficient audio playback in your application.

#### _Core Components_

- [**Lavalink4NET**](https://www.nuget.org/packages/Lavalink4NET/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.svg?style=flat-square)<br>This core library is used to implement client wrappers. It is not intended for end users. Please use Lavalink4NET.Discord.Net, Lavalink4NET.DSharpPlus, Lavalink4NET.Remora.Discord or Lavalink4NET.NetCord instead.

- [**Lavalink4NET.Abstractions**](https://www.nuget.org/packages/Lavalink4NET.Abstractions/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Abstractions.svg?style=flat-square)<br>General abstractions and common primitives for the Lavalink4NET client library.

- [**Lavalink4NET.Protocol**](https://www.nuget.org/packages/Lavalink4NET.Protocol/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Protocol.svg?style=flat-square)<br>Protocol implementation for the Lavalink4NET client library used to interact with the Lavalink REST API.

- [**Lavalink4NET.Rest**](https://www.nuget.org/packages/Lavalink4NET.Rest/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Rest.svg?style=flat-square)<br>Easily interact with the Lavalink REST API using this REST API client primitives library. Build custom functionalities or integrate Lavalink4NET with other services.

### Prerequisites

- At least one lavalink node
- At least .NET 6

### Getting Started

Lavalink4NET works by using dependency injection to make management of services very easy. You just have to add `services.AddLavalink();` to your startup code. We prefer using the host application builder to manage services required by Lavalink4NET.

```csharp
var builder = new HostApplicationBuilder(args);

builder.Services.AddLavalink(); // Contained in the client support packages

var app = builder.Build();

var audioService = app.Services.GetRequiredService<IAudioService>();

// [...]
```

> (ℹ️) Since Lavalink4NET v4, boilerplate code has been drastically reduced. It is also no longer required to initialize the node.

> (ℹ️) If you only use Dependency Injection without `Microsoft.Extensions.Hosting`, you may need to call `.StartAsync()` on various Lavalink4NET services, like `IAudioService`, `IInactivityTrackingService`, ...

```csharp
// Play a track
var playerOptions = new LavalinkPlayerOptions
{
    InitialTrack = new TrackQueueItem("https://www.youtube.com/watch?v=dQw4w9WgXcQ"),
};

await audioService.Players
    .JoinAsync(<guild id>, <voice channel id>, playerOptions, stoppingToken)
    .ConfigureAwait(false);
```

You can take a look at the [example bots](https://github.com/angelobreuer/Lavalink4NET/tree/dev/samples).
