namespace Lavalink4NET.Experiments.Receive.Connections;

internal interface IVoiceConnectionHandler
{
    ValueTask ProcessAsync(
        VoiceConnectionContext connectionContext,
        IVoiceConnectionHandle connectionHandle,
        CancellationToken cancellationToken = default);
}
