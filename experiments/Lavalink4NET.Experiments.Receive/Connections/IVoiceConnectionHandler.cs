namespace Lavalink4NET.Experiments.Receive.Connections;

public interface IVoiceConnectionHandler
{
    ValueTask ProcessAsync(
        VoiceConnectionContext connectionContext,
        CancellationToken cancellationToken = default);
}
