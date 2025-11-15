namespace Lavalink4NET.Tracks;

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Text;

public partial record class LavalinkTrack : ISpanFormattable
{
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    public override string ToString() => ToString(version: null);

    public string ToString(int? version)
    {
        if (TrackData is null || version is not null)
            TrackData = Utf8ToUtf16(Serialize(version));

        return TrackData;
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        try
        {
            string data = ToString();
            data.CopyTo(destination);
            charsWritten = data.Length;
        }
        catch
        {
            charsWritten = 0;
            return false;
        }

        return true;
    }

    internal static string Utf8ToUtf16(byte[] data)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Base64.GetMaxEncodedToUtf8Length(data.Length));

        try
        {
            var operationStatus = Base64.EncodeToUtf8(
                data,
                buffer,
                out _,
                out int bytesWritten
            );

            if (operationStatus is not OperationStatus.Done)
                throw new InvalidOperationException("Error while encoding to Base64.");

            // Gets Utf16 representation
            return Encoding.UTF8.GetString(new ArraySegment<byte>(buffer, 0, bytesWritten));
        }

        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
