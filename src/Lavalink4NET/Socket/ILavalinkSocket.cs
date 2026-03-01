namespace Lavalink4NET.Socket;

using System;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Events;
using Lavalink4NET.Protocol.Payloads;

public interface ILavalinkSocket : IDisposable
{
    event AsyncEventHandler<ConnectionClosedEventArgs>? ConnectionClosed;

    event AsyncEventHandler<ConnectionStartedEventArgs>? ConnectionStarted;

    string Label { get; }

    ValueTask<IPayload?> ReceiveAsync(CancellationToken cancellationToken = default);

    ValueTask RunAsync(CancellationToken cancellationToken = default);
}
