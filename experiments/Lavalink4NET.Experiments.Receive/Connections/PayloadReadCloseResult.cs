namespace Lavalink4NET.Experiments.Receive.Connections;

using System.Net.WebSockets;

internal sealed record class PayloadReadCloseResult(
    WebSocketCloseStatus CloseStatus,
    string? CloseStatusDescription,
    bool ByRemote = false);