namespace Lavalink4NET.Experiments.Receive.Connections;

using System.Net.WebSockets;

public interface IVoiceConnectionHandler
{
    ValueTask ProcessAsync(
        WebSocket webSocket,
        CancellationToken cancellationToken = default);
}
