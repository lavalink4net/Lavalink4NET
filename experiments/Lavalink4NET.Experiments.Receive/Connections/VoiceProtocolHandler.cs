namespace Lavalink4NET.Experiments.Receive.Connections;

using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Experiments.Receive.Connections.Features;
using Lavalink4NET.Experiments.Receive.Connections.Payloads;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;

internal sealed class VoiceProtocolHandler : IVoiceProtocolHandler
{
    private readonly ILogger<VoiceProtocolHandler> _logger;

    public VoiceProtocolHandler(ILogger<VoiceProtocolHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    public async ValueTask<PayloadReadResult> ReadAsync(
        VoiceConnectionContext connectionContext,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(connectionContext);

        var label = connectionContext.Features.Get<IConnectionLabelFeature>()?.Label
            ?? connectionContext.Features.GetRequiredFeature<IConnectionIdFeature>().ConnectionId;

        var pooledBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);

        try
        {
            var buffer = new Memory<byte>(pooledBuffer);

            var result = await connectionContext.WebSocket
                .ReceiveAsync(buffer, cancellationToken)
                .ConfigureAwait(false);

            if (result.MessageType is WebSocketMessageType.Close)
            {
                var closeStatus = connectionContext.WebSocket.CloseStatus ?? WebSocketCloseStatus.NormalClosure;

                _logger.LogInformation(
                    "[{Label}] Lost connection to voice gateway: {CloseStatus} {CloseStatusDescription}.",
                    label, closeStatus, connectionContext.WebSocket.CloseStatusDescription);

                return new PayloadReadResult(closeStatus, connectionContext.WebSocket.CloseStatusDescription, byRemote: true);
            }

            if (result.MessageType is not WebSocketMessageType.Text)
            {
                _logger.LogWarning("[{Label}] Received a non-text message over the voice gateway connection.", label);

                await connectionContext.WebSocket
                    .CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Expected text message.", cancellationToken)
                    .ConfigureAwait(false);

                return new PayloadReadResult(WebSocketCloseStatus.InvalidMessageType, "Expected text message.", byRemote: false);
            }

            if (!result.EndOfMessage)
            {
                _logger.LogWarning("[{Label}] Received a partial payload from voice gateway.", label);

                await connectionContext.WebSocket
                    .CloseAsync(WebSocketCloseStatus.MessageTooBig, "Payload is too large.", cancellationToken)
                    .ConfigureAwait(false);

                return new PayloadReadResult(WebSocketCloseStatus.MessageTooBig, "Payload is too large.", byRemote: false);
            }

            var payloadBuffer = buffer[..result.Count];

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("[{Label}] Received payload: {Payload}", label, Encoding.UTF8.GetString(payloadBuffer.Span));
            }

            var payload = JsonSerializer.Deserialize(
                utf8Json: payloadBuffer.Span,
                jsonTypeInfo: PayloadJsonSerializerContext.Default.IVoicePayload)!;

            return new PayloadReadResult(payload);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pooledBuffer);
        }
    }

    public async ValueTask WriteAsync(
        VoiceConnectionContext connectionContext,
        IVoicePayload payload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(connectionContext);
        ArgumentNullException.ThrowIfNull(payload);

        var label = connectionContext.Features.Get<IConnectionLabelFeature>()?.Label
            ?? connectionContext.Features.GetRequiredFeature<IConnectionIdFeature>().ConnectionId;

        using var bufferWriter = new PooledBufferWriter<byte>();

        using (var utf8JsonWriter = new Utf8JsonWriter(bufferWriter))
        {
            JsonSerializer.Serialize(utf8JsonWriter, payload, PayloadJsonSerializerContext.Default.IVoicePayload);
        }

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[{Label}] Sending payload: {Payload}", label, Encoding.UTF8.GetString(bufferWriter.WrittenSpan));
        }

        await connectionContext.WebSocket
            .SendAsync(bufferWriter.WrittenMemory, WebSocketMessageType.Text, true, cancellationToken)
            .ConfigureAwait(false);
    }
}
