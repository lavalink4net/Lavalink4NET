namespace Lavalink4NET.Rest;

public sealed class RemoteLavalinkException : Exception
{
    private readonly string? _stackTrace;

    public RemoteLavalinkException(string? message, string? stackTrace)
        : base(message)
    {
        _stackTrace = stackTrace;
    }

    public override string? StackTrace => _stackTrace ?? base.StackTrace;
}
