namespace Lavalink4NET.Experiments.Receive.Connections.Discovery;

using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

internal sealed class IpDiscoveryService : IIpDiscoveryService
{
    public async ValueTask<IPEndPoint?> DiscoverExternalAddressAsync(Socket socket, uint ssrc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(socket);

        using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            do
            {
                // discover external address
                var address = await DiscoverExternalAddressSingleAsync(
                    socket: socket,
                    ssrc: ssrc,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (address is not null)
                {
                    // got response!
                    return address;
                }
            }
            while (await periodicTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
        }

        // no attempts left, give up or cancellation requested
        return null;
    }

    private async ValueTask<IPEndPoint?> DiscoverExternalAddressSingleAsync(Socket socket, uint ssrc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(socket);

        // rent a buffer from the shared buffer array pool with a minimum size of 74 bytes (can
        // hold the request).
        var pooledBuffer = ArrayPool<byte>.Shared.Rent(74);
        var buffer = pooledBuffer.AsMemory(0, 74);

        try
        {
            // encode payload data
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Span[0..], 0x01); // Request Payload Type
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Span[2..], 70); // encoded payload size (always 70)
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Span[4..], ssrc); // encode the client's SSRC (big-endian)

            // send payload
            await socket
                .SendAsync(buffer, SocketFlags.None, cancellationToken)
                .ConfigureAwait(false);

            var startTime = DateTimeOffset.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                var receiveResult = await socket
                    .ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (receiveResult.ReceivedBytes is not 74) // Total Length
                {
                    continue;
                }

                var payloadType = BinaryPrimitives.ReadUInt16BigEndian(buffer.Span[0..]);
                var encodedSize = BinaryPrimitives.ReadUInt16BigEndian(buffer.Span[2..]);
                var ssrcValue = BinaryPrimitives.ReadUInt32BigEndian(buffer.Span[4..]);

                // validate header
                if (payloadType is 0x02 && encodedSize is 70 && ssrcValue == ssrc)
                {
                    var addressSpan = buffer[8..64];
                    var addressTerminatorOffset = addressSpan.Span.IndexOf((byte)0);
                    var addressLength = addressTerminatorOffset is -1 ? 64 : addressTerminatorOffset;
                    var address = Encoding.ASCII.GetString(addressSpan.Span[..addressLength]);
                    var port = BinaryPrimitives.ReadUInt16BigEndian(buffer.Span[72..]);

                    return new IPEndPoint(IPAddress.Parse(address), port);
                }
            }
        }
        finally
        {
            // return buffer to pool
            ArrayPool<byte>.Shared.Return(pooledBuffer);
        }

        // timeout exceeded
        return null;
    }
}
