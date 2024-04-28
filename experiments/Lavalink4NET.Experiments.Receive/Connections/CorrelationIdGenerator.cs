namespace Lavalink4NET.Experiments.Receive.Connections;

// Based on https://source.dot.net/#Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets/src/Servers/Kestrel/shared/CorrelationIdGenerator.cs,f22660215e7e9131

internal static class CorrelationIdGenerator
{
    private static ReadOnlySpan<char> Encode32Chars => "0123456789ABCDEFGHIJKLMNOPQRSTUV";
    private static long _lastId = DateTime.UtcNow.Ticks;

    public static string GetNextId() => GenerateId(Interlocked.Increment(ref _lastId));

    private static string GenerateId(long id) => string.Create(13, id, (buffer, value) =>
    {
        buffer[12] = Encode32Chars[(int)(value & 31)];
        buffer[11] = Encode32Chars[(int)((value >> 5) & 31)];
        buffer[10] = Encode32Chars[(int)((value >> 10) & 31)];
        buffer[9] = Encode32Chars[(int)((value >> 15) & 31)];
        buffer[8] = Encode32Chars[(int)((value >> 20) & 31)];
        buffer[7] = Encode32Chars[(int)((value >> 25) & 31)];
        buffer[6] = Encode32Chars[(int)((value >> 30) & 31)];
        buffer[5] = Encode32Chars[(int)((value >> 35) & 31)];
        buffer[4] = Encode32Chars[(int)((value >> 40) & 31)];
        buffer[3] = Encode32Chars[(int)((value >> 45) & 31)];
        buffer[2] = Encode32Chars[(int)((value >> 50) & 31)];
        buffer[1] = Encode32Chars[(int)((value >> 55) & 31)];
        buffer[0] = Encode32Chars[(int)((value >> 60) & 31)];
    });
}
