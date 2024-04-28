namespace Lavalink4NET.Experiments.Receive.Connections;

using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using Lavalink4NET.Experiments.Receive.Connections.Payloads;

internal readonly record struct PayloadReadResult
{
    private readonly object? _value;

    public PayloadReadResult(IVoicePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        _value = payload;
    }

    public PayloadReadResult(WebSocketCloseStatus closeStatus, string? closeStatusDescription, bool byRemote = false)
    {
        _value = new PayloadReadCloseResult(closeStatus, closeStatusDescription, byRemote);
    }

    public IVoicePayload? Payload => _value as IVoicePayload;

    public WebSocketCloseStatus CloseStatus => _value is PayloadReadCloseResult closeResult
        ? closeResult.CloseStatus
        : WebSocketCloseStatus.Empty;

    public string? CloseStatusDescription => _value is PayloadReadCloseResult closeResult
        ? closeResult.CloseStatusDescription
        : null;

    public bool ByRemote => _value is PayloadReadCloseResult closeResult && closeResult.ByRemote;

    [MemberNotNullWhen(true, nameof(Payload))]
    public bool IsSuccess => _value is IVoicePayload;
}
