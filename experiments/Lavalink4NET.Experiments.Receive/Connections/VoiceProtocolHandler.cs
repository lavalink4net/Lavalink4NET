namespace Lavalink4NET.Experiments.Receive.Connections;

using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Experiments.Receive.Connections.Payloads;

internal sealed class VoiceProtocolHandler : IVoiceProtocolHandler
{
    public async ValueTask<IVoicePayload> ReadAsync(WebSocket webSocket, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(webSocket);

        var pooledBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);

        try
        {
            var buffer = new Memory<byte>(pooledBuffer);

            var result = await webSocket
                .ReceiveAsync(buffer, cancellationToken)
                .ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException("WebSocket connection is closing.");
            }

            var payloadBuffer = buffer[..result.Count];

            return JsonSerializer.Deserialize(payloadBuffer.Span, PayloadJsonSerializerContext.Default.IVoicePayload)!;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pooledBuffer);
        }
    }
}
