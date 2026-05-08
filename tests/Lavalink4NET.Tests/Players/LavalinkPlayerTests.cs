namespace Lavalink4NET.Tests.Players;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Filters;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Protocol.Models.Filters;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Protocol.Requests;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

public sealed class LavalinkPlayerTests
{
    [Fact]
    public void TestPlayerIsNotPlayingInitially()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: null,
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var playerProperties = CreateProperties(playerModel);
        var player = new LavalinkPlayer(playerProperties);

        // Act
        var status = player.State;

        // Assert
        Assert.Equal(PlayerState.NotPlaying, status);
    }

    [Fact]
    public void TestPlayerIsPausedWhenSpecifiedInModelAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var playerProperties = CreateProperties(playerModel);
        var player = new LavalinkPlayer(playerProperties);

        // Act
        var status = player.State;

        // Assert
        Assert.Equal(PlayerState.Paused, status);
    }

    [Fact]
    public void TestPlayerIsPlayingIfCurrentTrackIsSpecifiedAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var playerProperties = CreateProperties(playerModel);
        var player = new LavalinkPlayer(playerProperties);

        // Act
        var status = player.State;

        // Assert
        Assert.Equal(PlayerState.Playing, status);
    }

    [Fact]
    public async Task TestPlayerIsDisposedIfDisconnectOnDestroyIsTrueOnChannelDisconnectAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: null,
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var playerProperties = CreateProperties(playerModel);

        var player = new LavalinkPlayer(playerProperties);
        var playerListener = player as ILavalinkPlayerListener;

        // Act
        await playerListener.NotifyVoiceStateUpdatedAsync(new VoiceState(null, null));

        // Assert
        Assert.Equal(PlayerState.Destroyed, player.State);
    }

    [Fact]
    public async Task TestVoiceAutoReconnectAttemptsRejoinOn4014Async()
    {
        // Arrange
        var discordClientMock = new Mock<IDiscordClientWrapper>(MockBehavior.Strict);

        discordClientMock
            .Setup(x => x.SendVoiceUpdateAsync(
                guildId: 0UL,
                voiceChannelId: 42UL,
                selfDeaf: true,
                selfMute: true,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: null,
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var options = new LavalinkPlayerOptions
        {
            EnableVoiceAutoReconnect = true,
            VoiceReconnectCooldown = TimeSpan.FromSeconds(10),
            SelfDeaf = true,
            SelfMute = true,
        };

        var playerProperties = CreateProperties(playerModel: playerModel, options: options, discordClientMock: discordClientMock);
        var player = new LavalinkPlayer(playerProperties);

        // Ensure we have a voice channel to re-assert.
        var listener = (ILavalinkPlayerListener)player;
        await listener.NotifyVoiceStateUpdatedAsync(new VoiceState(VoiceChannelId: 42UL, SessionId: "abc"));

        // Act
        await listener.NotifyWebSocketClosedAsync((WebSocketCloseStatus)4014, "Disconnected.", byRemote: true);

        // Assert
        discordClientMock.VerifyAll();
    }

    [Fact]
    public async Task TestVoiceAutoReconnectAttemptsRejoinOn4015Async()
    {
        // Arrange
        var discordClientMock = new Mock<IDiscordClientWrapper>(MockBehavior.Strict);

        discordClientMock
            .Setup(x => x.SendVoiceUpdateAsync(
                guildId: 0UL,
                voiceChannelId: 42UL,
                selfDeaf: false,
                selfMute: false,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: null,
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var options = new LavalinkPlayerOptions
        {
            EnableVoiceAutoReconnect = true,
            VoiceReconnectCooldown = TimeSpan.FromSeconds(10),
            SelfDeaf = false,
            SelfMute = false,
        };

        var playerProperties = CreateProperties(playerModel: playerModel, options: options, discordClientMock: discordClientMock);
        var player = new LavalinkPlayer(playerProperties);

        // Ensure we have a voice channel to re-assert.
        var listener = (ILavalinkPlayerListener)player;
        await listener.NotifyVoiceStateUpdatedAsync(new VoiceState(VoiceChannelId: 42UL, SessionId: "abc"));

        // Act
        await listener.NotifyWebSocketClosedAsync((WebSocketCloseStatus)4015, "Voice server crashed.", byRemote: true);

        // Assert
        discordClientMock.VerifyAll();
    }

    [Fact]
    public async Task TestVoiceAutoReconnectIsSuppressedWhenDisabledAsync()
    {
        // Arrange
        var discordClientMock = new Mock<IDiscordClientWrapper>(MockBehavior.Strict);
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: null,
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var options = new LavalinkPlayerOptions
        {
            EnableVoiceAutoReconnect = false,
            VoiceReconnectCooldown = TimeSpan.FromSeconds(10),
            SelfDeaf = false,
            SelfMute = false,
        };

        var playerProperties = CreateProperties(playerModel: playerModel, options: options, discordClientMock: discordClientMock);
        var player = new LavalinkPlayer(playerProperties);
        var listener = (ILavalinkPlayerListener)player;
        await listener.NotifyVoiceStateUpdatedAsync(new VoiceState(VoiceChannelId: 42UL, SessionId: "abc"));

        // Act
        await listener.NotifyWebSocketClosedAsync((WebSocketCloseStatus)4014, "Disconnected.", byRemote: true);

        // Assert
        discordClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TestVoiceAutoReconnectIsRateLimitedByCooldownAsync()
    {
        // Arrange
        var discordClientMock = new Mock<IDiscordClientWrapper>(MockBehavior.Strict);

        discordClientMock
            .Setup(x => x.SendVoiceUpdateAsync(
                guildId: 0UL,
                voiceChannelId: 42UL,
                selfDeaf: false,
                selfMute: false,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: null,
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var options = new LavalinkPlayerOptions
        {
            EnableVoiceAutoReconnect = true,
            VoiceReconnectCooldown = TimeSpan.FromMinutes(1),
            SelfDeaf = false,
            SelfMute = false,
        };

        var playerProperties = CreateProperties(playerModel: playerModel, options: options, discordClientMock: discordClientMock);
        var player = new LavalinkPlayer(playerProperties);
        var listener = (ILavalinkPlayerListener)player;
        await listener.NotifyVoiceStateUpdatedAsync(new VoiceState(VoiceChannelId: 42UL, SessionId: "abc"));

        // Act
        await listener.NotifyWebSocketClosedAsync((WebSocketCloseStatus)4014, "Disconnected.", byRemote: true);
        await listener.NotifyWebSocketClosedAsync((WebSocketCloseStatus)4014, "Disconnected.", byRemote: true);

        // Assert
        discordClientMock.Verify(x => x.SendVoiceUpdateAsync(0UL, 42UL, false, false, It.IsAny<CancellationToken>()), Times.Once);
        discordClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TestVoiceAutoReconnectCooldownIsThreadSafeAsync()
    {
        // Arrange
        var discordClientMock = new Mock<IDiscordClientWrapper>(MockBehavior.Strict);

        discordClientMock
            .Setup(x => x.SendVoiceUpdateAsync(
                guildId: 0UL,
                voiceChannelId: 42UL,
                selfDeaf: false,
                selfMute: false,
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: null,
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var options = new LavalinkPlayerOptions
        {
            EnableVoiceAutoReconnect = true,
            VoiceReconnectCooldown = TimeSpan.FromMinutes(5),
            SelfDeaf = false,
            SelfMute = false,
        };

        var playerProperties = CreateProperties(playerModel: playerModel, options: options, discordClientMock: discordClientMock);
        var player = new LavalinkPlayer(playerProperties);
        var listener = (ILavalinkPlayerListener)player;
        await listener.NotifyVoiceStateUpdatedAsync(new VoiceState(VoiceChannelId: 42UL, SessionId: "abc"));

        // Act
        await Task.WhenAll(
            listener.NotifyWebSocketClosedAsync((WebSocketCloseStatus)4014, "Disconnected.", byRemote: true).AsTask(),
            listener.NotifyWebSocketClosedAsync((WebSocketCloseStatus)4014, "Disconnected.", byRemote: true).AsTask());

        // Assert
        discordClientMock.Verify(x => x.SendVoiceUpdateAsync(0UL, 42UL, false, false, It.IsAny<CancellationToken>()), Times.Once);
        discordClientMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TestVoiceChannelIdIsUpdatedAfterPlayerMoveAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: null,
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            options: new LavalinkPlayerOptions { DisconnectOnDestroy = false, });

        var player = new LavalinkPlayer(playerProperties);
        var playerListener = player as ILavalinkPlayerListener;

        // Act
        await playerListener.NotifyVoiceStateUpdatedAsync(new VoiceState(VoiceChannelId: 128UL, null)); // initial update
        await playerListener.NotifyVoiceStateUpdatedAsync(new VoiceState(VoiceChannelId: 123UL, null)); // move

        // Assert
        Assert.Equal(123UL, player.VoiceChannelId);
    }

    [Fact]
    public void TestPositionIsNullIfPlayerNotPlaying()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: null,
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var playerProperties = CreateProperties(playerModel);
        var player = new LavalinkPlayer(playerProperties);

        // Act
        var position = player.Position;

        // Assert
        Assert.Null(position);
    }

    [Fact]
    public async Task TestPositionIsNotNullIfPlayerReceivedTrackPosition()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var playerProperties = CreateProperties(playerModel);
        var player = new LavalinkPlayer(playerProperties);
        var listener = player as ILavalinkPlayerListener;

        await listener
            .NotifyPlayerUpdateAsync(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30), true, TimeSpan.FromMilliseconds(130))
            .ConfigureAwait(true);

        // Act
        var position = player.Position;

        // Assert
        Assert.NotNull(position);
    }

    [Fact]
    public async Task TestUpdateSetPausedToTrueIfPlayerIsPausedAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var called = false;

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            updateAction: properties =>
            {
                Assert.True(properties.IsPaused.IsPresent);
                Assert.True(properties.IsPaused.Value);

                called = true;
            });

        var player = new LavalinkPlayer(playerProperties);

        // Act
        await player
            .PauseAsync()
            .ConfigureAwait(true);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task TestUpdateSetPausedToFalseIfPlayerIsResumedAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var called = false;

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            updateAction: properties =>
            {
                Assert.True(properties.IsPaused.IsPresent);
                Assert.False(properties.IsPaused.Value);

                called = true;
            });

        var player = new LavalinkPlayer(playerProperties);

        // Act
        await player
            .ResumeAsync()
            .ConfigureAwait(true);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task TestUpdateSetsVolumeIfPlayerIsResumedAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var called = false;

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            updateAction: properties =>
            {
                Assert.True(properties.Volume.IsPresent);
                Assert.Equal(0.1F, properties.Volume.Value);

                called = true;
            });

        var player = new LavalinkPlayer(playerProperties);

        // Act
        await player
            .SetVolumeAsync(0.1F)
            .ConfigureAwait(true);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task TestStopStopsTrackAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var called = false;

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            mutatedPlayerModel: playerModel with { CurrentTrack = null, },
            updateAction: properties =>
            {
                Assert.True(properties.TrackData.IsPresent);
                Assert.Null(properties.TrackData.Value);

                called = true;
            });

        var player = new LavalinkPlayer(playerProperties);

        // Act
        await player
            .StopAsync()
            .ConfigureAwait(true);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task TestStopSetsCurrentTrackToNullAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            mutatedPlayerModel: playerModel with { CurrentTrack = null, });

        var player = new LavalinkPlayer(playerProperties);

        // Act
        await player
            .StopAsync()
            .ConfigureAwait(true);

        // Assert
        Assert.Null(player.CurrentTrack);
    }

    [Fact]
    public async Task TestStopDestroysPlayerIfSpecifiedInOptionsAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            mutatedPlayerModel: playerModel with { CurrentTrack = null, },
            options: new LavalinkPlayerOptions { DisconnectOnStop = true, });

        var player = new LavalinkPlayer(playerProperties);

        // Act
        await player
            .StopAsync()
            .ConfigureAwait(true);

        // Assert
        Assert.Equal(PlayerState.Destroyed, player.State);
    }

    [Fact]
    public async Task TestSeekAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var called = false;

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            updateAction: properties =>
            {
                Assert.True(properties.Position.IsPresent);
                Assert.Equal(30, properties.Position.Value.TotalSeconds);

                called = true;
            });

        var player = new LavalinkPlayer(playerProperties);

        // Act
        await player
            .SeekAsync(TimeSpan.FromSeconds(30))
            .ConfigureAwait(true);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task TestSeekWithOriginBeginAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var called = false;

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            updateAction: properties =>
            {
                Assert.True(properties.Position.IsPresent);
                Assert.Equal(30, properties.Position.Value.TotalSeconds);

                called = true;
            });

        var player = new LavalinkPlayer(playerProperties);
        var listener = player as ILavalinkPlayerListener;

        await listener
            .NotifyPlayerUpdateAsync(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(20), connected: true, latency: null)
            .ConfigureAwait(true);

        // Act
        await player
            .SeekAsync(TimeSpan.FromSeconds(30), SeekOrigin.Begin)
            .ConfigureAwait(true);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task TestSeekRelativePositiveAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var called = false;

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            updateAction: properties =>
            {
                Assert.True(properties.Position.IsPresent);
                Assert.Equal(50, properties.Position.Value.TotalSeconds, tolerance: 2);

                called = true;
            });

        var player = new LavalinkPlayer(playerProperties);
        var listener = player as ILavalinkPlayerListener;

        await listener
            .NotifyPlayerUpdateAsync(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(20), connected: true, latency: null)
            .ConfigureAwait(true);

        // Act
        await player
            .SeekAsync(TimeSpan.FromSeconds(30), SeekOrigin.Current)
            .ConfigureAwait(true);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task TestSeekRelativeNegativeAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var called = false;

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            updateAction: properties =>
            {
                Assert.True(properties.Position.IsPresent);
                Assert.Equal(10, properties.Position.Value.TotalSeconds, tolerance: 2);

                called = true;
            });

        var player = new LavalinkPlayer(playerProperties);
        var listener = player as ILavalinkPlayerListener;

        await listener
            .NotifyPlayerUpdateAsync(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(20), connected: true, latency: null)
            .ConfigureAwait(true);

        // Act
        await player
            .SeekAsync(TimeSpan.FromSeconds(-10), SeekOrigin.Current)
            .ConfigureAwait(true);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task TestSeekEndAsync()
    {
        // Arrange
        var playerModel = new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(), // 120 seconds long
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel());

        var called = false;

        var playerProperties = CreateProperties(
            playerModel: playerModel,
            updateAction: properties =>
            {
                Assert.True(properties.Position.IsPresent);
                Assert.Equal(120 - 10, properties.Position.Value.TotalSeconds, tolerance: 2);

                called = true;
            });

        var player = new LavalinkPlayer(playerProperties);
        var listener = player as ILavalinkPlayerListener;

        await listener
            .NotifyPlayerUpdateAsync(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(20), connected: true, latency: null)
            .ConfigureAwait(true);

        // Act
        await player
            .SeekAsync(TimeSpan.FromSeconds(-10), SeekOrigin.End)
            .ConfigureAwait(true);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task TestSeekWithUnknownSeekOriginThrowsArgumentOufOfRangeExceptionAsync()
    {
        // Arrange
        var playerProperties = CreateProperties(
            playerModel: new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel()));

        var player = new LavalinkPlayer(playerProperties);

        // Act
        var exception = await Record
            .ExceptionAsync(async () => await player
                .SeekAsync(TimeSpan.FromSeconds(30), (SeekOrigin)42)
                .ConfigureAwait(false))
            .ConfigureAwait(true);

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public async Task TestTrackEndedEventIsTriggeredWhenReceivedAsync()
    {
        // Arrange
        var playerProperties = CreateProperties(
            playerModel: new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: false,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel()));

        var player = new CustomTracingLavalinkPlayer(playerProperties);
        var listener = player as ILavalinkPlayerListener;

        // Act
        await listener
            .NotifyTrackEndedAsync(player.CurrentTrack!, TrackEndReason.Finished)
            .ConfigureAwait(true);

        // Assert
        Assert.Contains("NotifyTrackEndedAsync", player.TriggeredEvents);
    }

    [Fact]
    public async Task TestChannelUpdateEventIsTriggeredWhenReceivedAsync()
    {
        // Arrange
        var playerProperties = CreateProperties(
            playerModel: new PlayerInformationModel(
                GuildId: 0UL,
                CurrentTrack: new TrackModel(
                    Data: "abc",
                    Information: CreateDummyTrack(),
                    AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
                Volume: 1F,
                IsPaused: true,
                VoiceState: CreateVoiceState(),
                Filters: new PlayerFilterMapModel()));

        var player = new CustomTracingLavalinkPlayer(playerProperties);
        var listener = player as ILavalinkPlayerListener;

        // Act
        await listener
            .NotifyVoiceStateUpdatedAsync(new VoiceState(42, null))
            .ConfigureAwait(true);

        // Assert
        Assert.Contains("NotifyChannelUpdateAsync", player.TriggeredEvents);
    }

    [Fact]
    public async Task TestTrackExceptionEventIsTriggeredWhenReceivedAsync()
    {
        // Arrange
        var playerProperties = CreateProperties(
            playerModel: new PlayerInformationModel(
            GuildId: 0UL,
            CurrentTrack: new TrackModel(
                Data: "abc",
                Information: CreateDummyTrack(),
                AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
            Volume: 1F,
            IsPaused: true,
            VoiceState: CreateVoiceState(),
            Filters: new PlayerFilterMapModel()));

        var player = new CustomTracingLavalinkPlayer(playerProperties);
        var listener = player as ILavalinkPlayerListener;
        var exception = new TrackException(ExceptionSeverity.Common, "abc", "abc");

        // Act
        await listener
            .NotifyTrackExceptionAsync(player.CurrentTrack!, exception)
            .ConfigureAwait(true);

        // Assert
        Assert.Contains("NotifyTrackExceptionAsync", player.TriggeredEvents);
    }

    [Fact]
    public async Task TestTrackStartedEventIsTriggeredWhenReceivedAsync()
    {
        // Arrange
        var playerProperties = CreateProperties(
            playerModel: new PlayerInformationModel(
                GuildId: 0UL,
                CurrentTrack: new TrackModel(
                    Data: "abc",
                    Information: CreateDummyTrack(),
                    AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
                Volume: 1F,
                IsPaused: true,
                VoiceState: CreateVoiceState(),
                Filters: new PlayerFilterMapModel()));

        var player = new CustomTracingLavalinkPlayer(playerProperties);
        var listener = player as ILavalinkPlayerListener;

        // Act
        await listener
            .NotifyTrackStartedAsync(player.CurrentTrack!)
            .ConfigureAwait(true);

        // Assert
        Assert.Contains("NotifyTrackStartedAsync", player.TriggeredEvents);
    }

    [Fact]
    public async Task TestTrackStuckEventIsTriggeredWhenReceivedAsync()
    {
        // Arrange
        var playerProperties = CreateProperties(
            playerModel: new PlayerInformationModel(
                GuildId: 0UL,
                CurrentTrack: new TrackModel(
                    Data: "abc",
                    Information: CreateDummyTrack(),
                    AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
                Volume: 1F,
                IsPaused: true,
                VoiceState: CreateVoiceState(),
                Filters: new PlayerFilterMapModel()));

        var player = new CustomTracingLavalinkPlayer(playerProperties);
        var listener = player as ILavalinkPlayerListener;

        // Act
        await listener
            .NotifyTrackStuckAsync(player.CurrentTrack!, TimeSpan.FromSeconds(1))
            .ConfigureAwait(true);

        // Assert
        Assert.Contains("NotifyTrackStuckAsync", player.TriggeredEvents);
    }

    [Fact]
    public async Task TestFiltersUpdatedEventIsTriggeredAfterFiltersCommitAsync()
    {
        // Arrange
        var playerProperties = CreateProperties(
            playerModel: new PlayerInformationModel(
                GuildId: 0UL,
                CurrentTrack: new TrackModel(
                    Data: "abc",
                    Information: CreateDummyTrack(),
                    AdditionalInformation: ImmutableDictionary<string, JsonElement>.Empty),
                Volume: 1F,
                IsPaused: true,
                VoiceState: CreateVoiceState(),
                Filters: new PlayerFilterMapModel()));

        var player = new CustomTracingLavalinkPlayer(playerProperties);
        player.Filters.Equalizer = new EqualizerFilterOptions(new Equalizer());

        // Act
        await player.Filters
            .CommitAsync()
            .ConfigureAwait(true);

        // Assert
        Assert.Contains("NotifyFiltersUpdatedAsync", player.TriggeredEvents);
    }

    private static PlayerProperties<CustomTracingLavalinkPlayer, LavalinkPlayerOptions> CreateProperties(
        PlayerInformationModel playerModel,
        PlayerInformationModel? mutatedPlayerModel = null,
        LavalinkPlayerOptions? options = null,
        Action<PlayerUpdateProperties>? updateAction = null,
        Mock<IDiscordClientWrapper>? discordClientMock = null)
    {
        var sessionId = "abc";
        var apiClientMock = new Mock<ILavalinkApiClient>(MockBehavior.Strict);

        apiClientMock
            .Setup(x => x.UpdatePlayerAsync(
                sessionId,
                playerModel.GuildId,
                It.IsAny<PlayerUpdateProperties>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ulong, PlayerUpdateProperties, CancellationToken>((s, g, p, c) => updateAction?.Invoke(p))
            .ReturnsAsync(mutatedPlayerModel ?? playerModel);

        apiClientMock
            .Setup(x => x.DestroyPlayerAsync("abc", 0, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        discordClientMock ??= new Mock<IDiscordClientWrapper>();

        var sessionProvider = Mock.Of<ILavalinkSessionProvider>(x
            => x.GetSessionAsync(playerModel.GuildId, It.IsAny<CancellationToken>())
            == ValueTask.FromResult(new LavalinkPlayerSession(apiClientMock.Object, "abc", "abc")));

        return new PlayerProperties<CustomTracingLavalinkPlayer, LavalinkPlayerOptions>(
            Context: new PlayerContext(
                ServiceProvider: null,
                SessionProvider: sessionProvider,
                DiscordClient: discordClientMock.Object,
                SystemClock: new SystemClock(),
                LifecycleNotifier: null),
            Lifecycle: Mock.Of<IPlayerLifecycle>(),
            ApiClient: apiClientMock.Object,
            InitialState: playerModel,
            InitialTrack: playerModel.CurrentTrack is null
                ? null
                : new TrackQueueItem(new TrackReference(LavalinkApiClient.CreateTrack(playerModel.CurrentTrack))),
            Label: "Player",
            VoiceChannelId: 0,
            SessionId: sessionId,
            Options: Options.Create(options ?? new LavalinkPlayerOptions()),
            Logger: NullLogger<CustomTracingLavalinkPlayer>.Instance);
    }

    private static TrackInformationModel CreateDummyTrack()
    {
        return new TrackInformationModel(
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
            SourceName: "manual");
    }

    private static VoiceStateModel CreateVoiceState()
    {
        return new VoiceStateModel(
            Token: "abc",
            Endpoint: "server.discord.gg",
            SessionId: "abc");
    }
}

internal sealed class CustomTracingLavalinkPlayer : LavalinkPlayer
{
    public CustomTracingLavalinkPlayer(IPlayerProperties<CustomTracingLavalinkPlayer, LavalinkPlayerOptions> properties)
        : base(properties)
    {
    }

    public List<string> TriggeredEvents { get; } = new();

    protected override ValueTask NotifyChannelUpdateAsync(ulong? voiceChannelId, CancellationToken cancellationToken = default)
    {
        TriggeredEvents.Add(nameof(NotifyChannelUpdateAsync));
        return base.NotifyChannelUpdateAsync(voiceChannelId, cancellationToken);
    }

    protected override ValueTask NotifyFiltersUpdatedAsync(IPlayerFilters filters, CancellationToken cancellationToken = default)
    {
        TriggeredEvents.Add(nameof(NotifyFiltersUpdatedAsync));
        return base.NotifyFiltersUpdatedAsync(filters, cancellationToken);
    }

    protected override ValueTask NotifyTrackEndedAsync(ITrackQueueItem track, TrackEndReason endReason, CancellationToken cancellationToken = default)
    {
        TriggeredEvents.Add(nameof(NotifyTrackEndedAsync));
        return base.NotifyTrackEndedAsync(track, endReason, cancellationToken);
    }

    protected override ValueTask NotifyTrackExceptionAsync(ITrackQueueItem track, TrackException exception, CancellationToken cancellationToken = default)
    {
        TriggeredEvents.Add(nameof(NotifyTrackExceptionAsync));
        return base.NotifyTrackExceptionAsync(track, exception, cancellationToken);
    }

    protected override ValueTask NotifyTrackStartedAsync(ITrackQueueItem track, CancellationToken cancellationToken = default)
    {
        TriggeredEvents.Add(nameof(NotifyTrackStartedAsync));
        return base.NotifyTrackStartedAsync(track, cancellationToken);
    }

    protected override ValueTask NotifyTrackStuckAsync(ITrackQueueItem track, TimeSpan threshold, CancellationToken cancellationToken = default)
    {
        TriggeredEvents.Add(nameof(NotifyTrackStuckAsync));
        return base.NotifyTrackStuckAsync(track, threshold, cancellationToken);
    }
}
