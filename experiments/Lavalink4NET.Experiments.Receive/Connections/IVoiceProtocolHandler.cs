namespace Lavalink4NET.Experiments.Receive.Connections;

using System.Net.WebSockets;
using Lavalink4NET.Experiments.Receive.Connections.Payloads;

internal interface IVoiceProtocolHandler
{
    ValueTask<IVoicePayload> ReadAsync(
        WebSocket webSocket,
        CancellationToken cancellationToken = default);
}
