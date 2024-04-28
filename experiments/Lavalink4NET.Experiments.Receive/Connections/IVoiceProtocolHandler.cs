namespace Lavalink4NET.Experiments.Receive.Connections;

using Lavalink4NET.Experiments.Receive.Connections.Payloads;

internal interface IVoiceProtocolHandler
{
    ValueTask<PayloadReadResult> ReadAsync(
        VoiceConnectionContext connectionContext,
        CancellationToken cancellationToken = default);

    ValueTask WriteAsync(
        VoiceConnectionContext connectionContext,
        IVoicePayload payload,
        CancellationToken cancellationToken = default);
}
