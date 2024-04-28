namespace Lavalink4NET.Experiments.Receive.Connections;

using System.Net.WebSockets;
using Microsoft.AspNetCore.Http.Features;

public sealed class VoiceConnectionContext
{
    public VoiceConnectionContext(WebSocket webSocket)
    {
        ArgumentNullException.ThrowIfNull(webSocket);

        WebSocket = webSocket;
        Features = new FeatureCollection();
    }

    public WebSocket WebSocket { get; }

    public IFeatureCollection Features { get; }
}
