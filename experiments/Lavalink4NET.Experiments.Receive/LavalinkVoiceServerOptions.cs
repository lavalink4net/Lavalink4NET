namespace Lavalink4NET.Experiments.Receive;

using System.Net;
using System.Net.Sockets;

public sealed record class LavalinkVoiceServerOptions
{
    public int Port { get; set; } = 16389;

    public IPAddress BindAddress { get; set; } = Socket.OSSupportsIPv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback;
}
