namespace Lavalink4NET.Events;

using System;
using Lavalink4NET.Socket;

public sealed class ConnectionReadyEventArgs : EventArgs
{
    public ConnectionReadyEventArgs(ILavalinkSocket lavalinkSocket, string sessionId, bool wasResumed)
    {
        ArgumentNullException.ThrowIfNull(lavalinkSocket);
        ArgumentNullException.ThrowIfNull(sessionId);

        LavalinkSocket = lavalinkSocket;
        SessionId = sessionId;
        WasResumed = wasResumed;
    }

    public ILavalinkSocket LavalinkSocket { get; }

    public string SessionId { get; }

    public bool WasResumed { get; }
}
