namespace Lavalink4NET.Experiments.Receive.Connections;

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Lavalink4NET.Experiments.Receive.Connections.Discovery;
using Lavalink4NET.Experiments.Receive.Connections.Payloads;

internal sealed class VoiceConnectionHandle : IVoiceConnectionHandle
{
    private readonly IIpDiscoveryService _ipDiscoveryService;
    private SelectProtocolPayload? _selectProtocolPayload;
    private ReadyPayload? _readyPayload;
    private SessionDescriptionPayload? _sessionDescriptionPayload;
    private Socket? _localSocket;
    private Socket? _remoteSocket;

    public VoiceConnectionHandle(IIpDiscoveryService ipDiscoveryService)
    {
        ArgumentNullException.ThrowIfNull(ipDiscoveryService);

        _ipDiscoveryService = ipDiscoveryService;
    }

    public async ValueTask<IPEndPoint> SelectProtocolAsync(SelectProtocolPayload selectProtocolPayload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(selectProtocolPayload);

        _selectProtocolPayload = selectProtocolPayload;

        using var discoveryCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var externalRemoteAddress = await _ipDiscoveryService
            .DiscoverExternalAddressAsync(_remoteSocket!, _readyPayload!.Ssrc, discoveryCancellationTokenSource.Token)
            .ConfigureAwait(false);

        await CompleteAsync(cancellationToken).ConfigureAwait(false);

        return externalRemoteAddress;
    }

    public async ValueTask<IPEndPoint> SetReadyAsync(ReadyPayload readyPayload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(readyPayload);

        if (_readyPayload is not null)
        {
            throw new InvalidOperationException("Ready payload already received.");
        }

        _readyPayload = readyPayload;

        _localSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _localSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0));

        _remoteSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _remoteSocket.Bind(new IPEndPoint(IPAddress.Any, _readyPayload.Port));
        _remoteSocket.Connect(new IPEndPoint(IPAddress.Parse(_readyPayload.Ip), _readyPayload.Port));

        _ = ProxyAsync(_localSocket!, _remoteSocket!, cancellationToken).AsTask();

        await CompleteAsync(cancellationToken).ConfigureAwait(false);

        return (IPEndPoint)_localSocket.LocalEndPoint!;
    }

    public async ValueTask SetSessionDescriptionAsync(SessionDescriptionPayload sessionDescriptionPayload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sessionDescriptionPayload);

        _sessionDescriptionPayload = sessionDescriptionPayload;

        await CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<IPEndPoint> CompleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_selectProtocolPayload is null || _readyPayload is null || _sessionDescriptionPayload is null)
        {
            return default;
        }

        _ = ProxyAsync(_remoteSocket!, _localSocket!, cancellationToken).AsTask();

        return default;
    }

    private async ValueTask HandleIpDiscoveryAsync(Socket sourceSocket, IPEndPoint endPoint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sourceSocket);

        using var bufferWriter = new PooledBufferWriter<byte>();

        var header = bufferWriter.GetMemory(8);

        BinaryPrimitives.WriteUInt16BigEndian(header.Span[0..2], 0x02); // Mark as response
        BinaryPrimitives.WriteUInt16BigEndian(header.Span[2..4], 70); // Encode (constant) length
        BinaryPrimitives.WriteUInt32BigEndian(header.Span[4..8], _readyPayload!.Ssrc); // Encode SSRC

        bufferWriter.Advance(8);

        // Encode IP
        var ipContent = bufferWriter.GetMemory(64);
        var encodedByteCount = Encoding.UTF8.GetBytes(endPoint.Address.ToString(), ipContent.Span);
        ipContent.Span[encodedByteCount] = 0;
        bufferWriter.Advance(64);

        // Encode port
        var portContent = bufferWriter.GetMemory(2);
        BinaryPrimitives.WriteUInt16BigEndian(portContent.Span, (ushort)endPoint.Port);
        bufferWriter.Advance(2);

        if (!sourceSocket.Connected)
        {
            sourceSocket!.Connect(endPoint);
        }

        await sourceSocket
            .SendAsync(bufferWriter.WrittenMemory, SocketFlags.None, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ProxyAsync(Socket sourceSocket, Socket destinationSocket, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await sourceSocket
                    .ReceiveMessageFromAsync(buffer, SocketFlags.None, sourceSocket.LocalEndPoint!, cancellationToken)
                    .ConfigureAwait(false);

                if (result.ReceivedBytes is 0)
                {
                    break;
                }

                var data = new ReadOnlyMemory<byte>(buffer, 0, result.ReceivedBytes);

                if (sourceSocket == _localSocket && data.Length is 74 && data.Span[0..2].SequenceEqual(new byte[] { 0x00, 0x01, }))
                {
                    await HandleIpDiscoveryAsync(sourceSocket, (IPEndPoint)result.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await destinationSocket
                    .SendAsync(data, SocketFlags.None, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
