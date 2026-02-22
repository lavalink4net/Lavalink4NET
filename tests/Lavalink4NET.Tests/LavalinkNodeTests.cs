namespace Lavalink4NET.Tests;

using System;
using System.Collections.Immutable;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Integrations;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Protocol.Models.Usage;
using Lavalink4NET.Protocol.Payloads;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest;
using Lavalink4NET.Socket;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public sealed class LavalinkNodeTests
{
    [Fact]
    public async Task TestIsReadyIsInitiallyFalseAsync()
    {
        // Arrange
        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: Mock.Of<ILavalinkSocketFactory>(),
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(),
            NodeListener: Mock.Of<ILavalinkNodeListener>());

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        await using var _ = node.ConfigureAwait(false);

        // Act
        var isReady = node.IsReady;

        // Assert
        Assert.False(isReady);
    }

    [Fact]
    public async Task TestIsReadyIsTrueAfterReadyEventAsync()
    {
        // Arrange
        using var socketFactory = new LavalinkSocketFactory();

        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: socketFactory,
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(),
            NodeListener: Mock.Of<ILavalinkNodeListener>());

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        _ = node.RunAsync(new ClientInformation("Client", 0, 1)).AsTask();
        await using var __ = node.ConfigureAwait(false);

        socketFactory.Socket.Send(new ReadyPayload(false, "abc"));
        await Task.Delay(100).ConfigureAwait(true);

        // Act
        var isReady = node.IsReady;

        // Assert
        Assert.True(isReady);

        // Clean Up
        socketFactory.Socket.Complete();
    }

    [Fact]
    public async Task TestPayloadBeforeReadyIsNotProcessedAsync()
    {
        // Arrange
        using var socketFactory = new LavalinkSocketFactory();
        var nodeListener = new Mock<ILavalinkNodeListener>();

        nodeListener
            .Setup(listener => listener.OnTrackEndedAsync(It.IsAny<TrackEndedEventArgs>(), It.IsAny<CancellationToken>()))
            .Verifiable();

        var player = new Mock<IPlayer>();

        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: socketFactory,
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(x
                => x.GetPlayerAsync(123UL, It.IsAny<CancellationToken>())
                == ValueTask.FromResult<ILavalinkPlayer?>(player.Object)),
            NodeListener: Mock.Of<ILavalinkNodeListener>());

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        _ = node.RunAsync(new ClientInformation("Client", 0, 1)).AsTask();
        await using var __ = node.ConfigureAwait(false);

        // Act
        socketFactory.Socket.Send(new PlayerUpdatePayload(123UL, new PlayerStateModel(
            AbsoluteTimestamp: DateTimeOffset.UtcNow,
            Position: default,
            IsConnected: true,
            Latency: null)));

        socketFactory.Socket.Send(new ReadyPayload(false, "abc"));
        socketFactory.Socket.Complete();

        // Assert
        player.Verify(
            expression: listener => listener.NotifyPlayerUpdateAsync(It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            times: Times.Never());
    }

    [Fact]
    public async Task TestPayloadAfterReadyIsProcessedAsync()
    {
        // Arrange
        using var socketFactory = new LavalinkSocketFactory();

        var player = new Mock<IPlayer>();

        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: socketFactory,
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(x
                => x.GetPlayerAsync(123UL, It.IsAny<CancellationToken>())
                == ValueTask.FromResult<ILavalinkPlayer?>(player.Object)),
            NodeListener: Mock.Of<ILavalinkNodeListener>());

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        _ = node.RunAsync(new ClientInformation("Client", 0, 1)).AsTask();
        await using var __ = node.ConfigureAwait(false);

        // Act
        socketFactory.Socket.Send(new ReadyPayload(false, "abc"));

        socketFactory.Socket.Send(new PlayerUpdatePayload(123UL, new PlayerStateModel(
            AbsoluteTimestamp: DateTimeOffset.UtcNow,
            Position: default,
            IsConnected: true,
            Latency: null)));

        socketFactory.Socket.Complete();

        // Assert
        player.Verify(
            expression: listener => listener.NotifyPlayerUpdateAsync(It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
            times: Times.Once());
    }

    [Fact]
    public async Task TestStatisticsPayloadDispatchesStatisticsUpdatedEventAsync()
    {
        // Arrange
        using var socketFactory = new LavalinkSocketFactory();

        var nodeListener = new Mock<ILavalinkNodeListener>();

        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: socketFactory,
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(x
                => x.GetPlayerAsync(123UL, It.IsAny<CancellationToken>())
                == ValueTask.FromResult<ILavalinkPlayer?>(Mock.Of<IPlayer>())),
            NodeListener: nodeListener.Object);

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        _ = node.RunAsync(new ClientInformation("Client", 0, 1)).AsTask();
        await using var __ = node.ConfigureAwait(false);

        // Act
        socketFactory.Socket.Send(new ReadyPayload(false, "abc"));

        socketFactory.Socket.Send(new StatisticsPayload(
            ConnectedPlayers: 1,
            PlayingPlayers: 1,
            Uptime: TimeSpan.FromHours(4),
            MemoryUsage: new ServerMemoryUsageStatisticsModel(
                FreeMemory: 1000,
                UsedMemory: 2000,
                AllocatedMemory: 3000,
                ReservableMemory: 4000),
            ProcessorUsage: new ServerProcessorUsageStatisticsModel(
                CoreCount: 32,
                SystemLoad: 0.5F,
                LavalinkLoad: 0.25F),
            FrameStatistics: new ServerFrameStatisticsModel(
                SentFrames: 100,
                NulledFrames: 0,
                DeficitFrames: 0)));

        socketFactory.Socket.Complete();

        // Assert
        nodeListener.Verify(
            expression: listener => listener.OnStatisticsUpdatedAsync(It.IsAny<StatisticsUpdatedEventArgs>(), It.IsAny<CancellationToken>()),
            times: Times.Once());
    }

    [Fact]
    public async Task TestTrackEndedPayloadDispatchesTrackEndedEventAsync()
    {
        // Arrange
        using var socketFactory = new LavalinkSocketFactory();

        var nodeListener = new Mock<ILavalinkNodeListener>();

        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: socketFactory,
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(x
                => x.GetPlayerAsync(123UL, It.IsAny<CancellationToken>())
                == ValueTask.FromResult<ILavalinkPlayer?>(Mock.Of<IPlayer>())),
            NodeListener: nodeListener.Object);

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        _ = node.RunAsync(new ClientInformation("Client", 0, 1)).AsTask();
        await using var __ = node.ConfigureAwait(false);

        // Act
        socketFactory.Socket.Send(new ReadyPayload(false, "abc"));

        socketFactory.Socket.Send(new TrackEndEventPayload(
            GuildId: 123UL,
            Track: CreateDummyTrack(),
            Reason: TrackEndReason.Finished));

        socketFactory.Socket.Complete();

        // Assert
        nodeListener.Verify(
            expression: listener => listener.OnTrackEndedAsync(It.IsAny<TrackEndedEventArgs>(), It.IsAny<CancellationToken>()),
            times: Times.Once());
    }

    [Fact]
    public async Task TestTrackStartedPayloadDispatchesTrackStartedEventAsync()
    {
        // Arrange
        using var socketFactory = new LavalinkSocketFactory();

        var nodeListener = new Mock<ILavalinkNodeListener>();

        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: socketFactory,
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(x
                => x.GetPlayerAsync(123UL, It.IsAny<CancellationToken>())
                == ValueTask.FromResult<ILavalinkPlayer?>(Mock.Of<IPlayer>())),
            NodeListener: nodeListener.Object);

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        _ = node.RunAsync(new ClientInformation("Client", 0, 1)).AsTask();
        await using var __ = node.ConfigureAwait(false);

        // Act
        socketFactory.Socket.Send(new ReadyPayload(false, "abc"));

        socketFactory.Socket.Send(new TrackStartEventPayload(
            GuildId: 123UL,
            Track: CreateDummyTrack()));

        socketFactory.Socket.Complete();

        // Assert
        nodeListener.Verify(
            expression: listener => listener.OnTrackStartedAsync(It.IsAny<TrackStartedEventArgs>(), It.IsAny<CancellationToken>()),
            times: Times.Once());
    }

    [Fact]
    public async Task TestReadyPayloadDispatchesConnectionReadyEventAsync()
    {
        // Arrange
        using var socketFactory = new LavalinkSocketFactory();

        var nodeListener = new Mock<ILavalinkNodeListener>();

        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: socketFactory,
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(),
            NodeListener: nodeListener.Object);

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        _ = node.RunAsync(new ClientInformation("Client", 0, 1)).AsTask();
        await using var __ = node.ConfigureAwait(false);

        // Act
        socketFactory.Socket.Send(new ReadyPayload(false, "abc"));
        socketFactory.Socket.Complete();

        await Task.Delay(100);

        // Assert
        nodeListener.Verify(
            expression: listener => listener.OnConnectionReadyAsync(It.IsAny<CancellationToken>()),
            times: Times.Once());
    }

    [Fact]
    public async Task TestReadyPayloadDispatchesConnectionReadyOnReconnectAsync()
    {
        // Arrange
        using var socketFactory = new LavalinkSocketFactory();

        var nodeListener = new Mock<ILavalinkNodeListener>();

        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: socketFactory,
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(),
            NodeListener: nodeListener.Object);

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        _ = node.RunAsync(new ClientInformation("Client", 0, 1)).AsTask();
        await using var __ = node.ConfigureAwait(false);

        // Act - Send two ReadyPayloads to simulate initial connection + reconnection
        socketFactory.Socket.Send(new ReadyPayload(false, "session-1"));
        await Task.Delay(100);

        socketFactory.Socket.Send(new ReadyPayload(false, "session-2"));
        await Task.Delay(100);

        socketFactory.Socket.Complete();

        // Assert - Should be called twice (initial + reconnect)
        nodeListener.Verify(
            expression: listener => listener.OnConnectionReadyAsync(It.IsAny<CancellationToken>()),
            times: Times.Exactly(2));
    }

    [Fact]
    public async Task TestTrackExceptionPayloadDispatchesTrackExceptionEventAsync()
    {
        // Arrange
        using var socketFactory = new LavalinkSocketFactory();

        var nodeListener = new Mock<ILavalinkNodeListener>();

        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: socketFactory,
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(x
                => x.GetPlayerAsync(123UL, It.IsAny<CancellationToken>())
                == ValueTask.FromResult<ILavalinkPlayer?>(Mock.Of<IPlayer>())),
            NodeListener: nodeListener.Object);

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        _ = node.RunAsync(new ClientInformation("Client", 0, 1)).AsTask();
        await using var __ = node.ConfigureAwait(false);

        // Act
        socketFactory.Socket.Send(new ReadyPayload(false, "abc"));

        socketFactory.Socket.Send(new TrackExceptionEventPayload(
            GuildId: 123UL,
            Track: CreateDummyTrack(),
            Exception: new TrackExceptionModel(
                Message: "abc",
                Severity: ExceptionSeverity.Common,
                Cause: null)));

        socketFactory.Socket.Complete();

        // Assert
        nodeListener.Verify(
            expression: listener => listener.OnTrackExceptionAsync(It.IsAny<TrackExceptionEventArgs>(), It.IsAny<CancellationToken>()),
            times: Times.Once());
    }

    [Fact]
    public async Task TestTrackExceptionPayloadDispatchesTrackStuckEventAsync()
    {
        // Arrange
        using var socketFactory = new LavalinkSocketFactory();

        var nodeListener = new Mock<ILavalinkNodeListener>();

        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: socketFactory,
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(x
                => x.GetPlayerAsync(123UL, It.IsAny<CancellationToken>())
                == ValueTask.FromResult<ILavalinkPlayer?>(Mock.Of<IPlayer>())),
            NodeListener: nodeListener.Object);

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        _ = node.RunAsync(new ClientInformation("Client", 0, 1)).AsTask();
        await using var __ = node.ConfigureAwait(false);

        // Act
        socketFactory.Socket.Send(new ReadyPayload(false, "abc"));

        socketFactory.Socket.Send(new TrackStuckEventPayload(
            GuildId: 123UL,
            Track: CreateDummyTrack(),
            ExceededThreshold: TimeSpan.FromSeconds(30)));

        socketFactory.Socket.Complete();

        // Assert
        nodeListener.Verify(
            expression: listener => listener.OnTrackStuckAsync(It.IsAny<TrackStuckEventArgs>(), It.IsAny<CancellationToken>()),
            times: Times.Once());
    }

    [Fact]
    public async Task TestWebSocketClosedPayloadDispatchesWebSocketClosedEventAsync()
    {
        // Arrange
        using var socketFactory = new LavalinkSocketFactory();

        var nodeListener = new Mock<ILavalinkNodeListener>();

        var serviceContext = new LavalinkNodeServiceContext(
            ClientWrapper: Mock.Of<IDiscordClientWrapper>(),
            LavalinkSocketFactory: socketFactory,
            IntegrationManager: new IntegrationManager(),
            PlayerManager: Mock.Of<IPlayerManager>(x
                => x.GetPlayerAsync(123UL, It.IsAny<CancellationToken>())
                == ValueTask.FromResult<ILavalinkPlayer?>(Mock.Of<IPlayer>())),
            NodeListener: nodeListener.Object);

        var node = new LavalinkNode(
            serviceContext,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            options: Options.Create(new NodeOptions { }),
            apiEndpoints: new LavalinkApiEndpoints(new Uri("http://localhost/")),
            logger: NullLogger<LavalinkNode>.Instance);

        _ = node.RunAsync(new ClientInformation("Client", 0, 1)).AsTask();
        await using var __ = node.ConfigureAwait(false);

        // Act
        socketFactory.Socket.Send(new ReadyPayload(false, "abc"));

        socketFactory.Socket.Send(new WebSocketClosedEventPayload(
            GuildId: 123UL,
            Code: 1000,
            Reason: "abc",
            WasByRemote: true));

        socketFactory.Socket.Complete();

        // Assert
        nodeListener.Verify(
            expression: listener => listener.OnWebSocketClosedAsync(It.IsAny<WebSocketClosedEventArgs>(), It.IsAny<CancellationToken>()),
            times: Times.Once());
    }

    private static TrackModel CreateDummyTrack()
    {
        return new TrackModel(
            Data: "abc",
            Information: new TrackInformationModel(
                Identifier: "video",
                IsSeekable: true,
                Author: "author",
                Duration: TimeSpan.FromSeconds(120),
                IsLiveStream: false,
                Position: TimeSpan.FromSeconds(10),
                Title: "video",
                Uri: null,
                ArtworkUri: null,
                Isrc: null,
                SourceName: "manual"),
            AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty);
    }
}

file sealed record class NodeOptions : LavalinkNodeOptions
{
}

file sealed class LavalinkSocket : ILavalinkSocket
{
    private readonly Channel<IPayload> _payloadChannel;
    private readonly CancellationTokenSource _shutdownCancellationTokenSource;
    private readonly SemaphoreSlim _flushSemaphoreSlim;

    public event AsyncEventHandler<ConnectionClosedEventArgs>? ConnectionClosed;

    public LavalinkSocket()
    {
        _payloadChannel = Channel.CreateUnbounded<IPayload>();
        _shutdownCancellationTokenSource = new CancellationTokenSource();
        _flushSemaphoreSlim = new SemaphoreSlim(0, 1);
    }

    public string Label => string.Empty;

    public void Dispose()
    {
        _shutdownCancellationTokenSource.Cancel();
        Complete();
    }

    public void Complete()
    {
        if (_payloadChannel.Writer.TryComplete())
        {
            _flushSemaphoreSlim.Wait();
            ConnectionClosed?.Invoke(this, new ConnectionClosedEventArgs(this, WebSocketCloseStatus.NormalClosure, null));
        }
    }

    public async ValueTask<IPayload?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await _payloadChannel.Reader
                .ReadAsync(cancellationToken)
                .AsTask()
                .WaitAsync(_shutdownCancellationTokenSource.Token)
                .ConfigureAwait(false);
        }
        catch
        {
            if (_flushSemaphoreSlim.CurrentCount is 0)
            {
                _flushSemaphoreSlim.Release();
            }

            return null;
        }
    }

    public void Send(IPayload payload)
    {
        _payloadChannel.Writer.TryWrite(payload);
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await Task
                .Delay(-1, _shutdownCancellationTokenSource.Token)
                .ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }
    }
}

file sealed class LavalinkSocketFactory : ILavalinkSocketFactory, IDisposable
{
    private LavalinkSocket? _socket;

    public LavalinkSocketFactory()
    {
        _socket = Socket = new LavalinkSocket();
    }

    public ILavalinkSocket? Create(IOptions<LavalinkSocketOptions> options)
        => Interlocked.Exchange(ref _socket, null);

    public void Dispose() => Socket.Dispose();

    public LavalinkSocket Socket { get; }
}

internal interface IPlayer : ILavalinkPlayer, ILavalinkPlayerListener
{
}