namespace Lavalink4NET.Experiments.Receive.Connections.Discovery;

using System.Net;
using System.Net.Sockets;

internal interface IIpDiscoveryService
{
    ValueTask<IPEndPoint?> DiscoverExternalAddressAsync(Socket socket, uint ssrc, CancellationToken cancellationToken = default);
}
