namespace Lavalink4NET.Events;

using System;
using Lavalink4NET.Socket;

public sealed class ConnectionStartedEventArgs : EventArgs
{
    public ConnectionStartedEventArgs(ILavalinkSocket lavalinkSocket, Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(lavalinkSocket);
        ArgumentNullException.ThrowIfNull(endpoint);

        LavalinkSocket = lavalinkSocket;
        Endpoint = endpoint;
    }

    public ILavalinkSocket LavalinkSocket { get; }

    public Uri Endpoint { get; }
}
