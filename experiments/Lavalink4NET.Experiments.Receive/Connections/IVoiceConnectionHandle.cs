namespace Lavalink4NET.Experiments.Receive.Connections;

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Experiments.Receive.Connections.Payloads;

internal interface IVoiceConnectionHandle
{
    ValueTask<IPEndPoint> SelectProtocolAsync(SelectProtocolPayload selectProtocolPayload, CancellationToken cancellationToken = default);

    ValueTask SetSessionDescriptionAsync(SessionDescriptionPayload sessionDescriptionPayload, CancellationToken cancellationToken = default);

    ValueTask<IPEndPoint> SetReadyAsync(ReadyPayload readyPayload, CancellationToken cancellationToken = default);
}
