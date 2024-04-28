namespace Lavalink4NET.Experiments.Receive.Connections.Features;

using Microsoft.AspNetCore.Connections.Features;

internal sealed record class ConnectionIdFeature : IConnectionIdFeature
{
    public required string ConnectionId { get; set; }
}