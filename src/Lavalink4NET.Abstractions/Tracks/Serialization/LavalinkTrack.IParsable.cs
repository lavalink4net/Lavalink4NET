namespace Lavalink4NET.Tracks;

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;

public partial record class LavalinkTrack
#if NET7_0_OR_GREATER
    : ISpanParsable<LavalinkTrack>
#endif
{
    public static LavalinkTrack Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s.ToString(), provider);

    public static LavalinkTrack Parse(ReadOnlySpan<char> s) => Parse(s, null);

    public static LavalinkTrack Parse(string s, IFormatProvider? provider) => Deserialize(Utf16ToUtf8(s), s);

    public static LavalinkTrack Parse(string s) => Parse(s, null);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out LavalinkTrack result)
    {
        try
        {
            string data = s.ToString();
            return TryParse(data, provider, out result);
        }
        catch
        {
            result = null;
            return false;
        }
    }

    public static bool TryParse(string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out LavalinkTrack result)
    {
        try
        {
            // Although this can be null, the try catch makes that irrelevant.
            // Indicating it can't be null silences the code analytics.
            result = Parse(s!, provider);
            return true;
        }

        catch
        {
            result = null;
            return false;
        }
    }

    internal static byte[] Utf16ToUtf8(string data)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(data);
        byte[] result = ArrayPool<byte>.Shared.Rent(Base64.GetMaxDecodedFromUtf8Length(buffer.Length));

        try
        {
            var operationStatus = Base64.DecodeFromUtf8(
                buffer,
                result,
                out _,
                out int bytesWritten
            );

            if (operationStatus is not OperationStatus.Done)
                throw new InvalidOperationException("Error while decoding from Base64.");

            return new ArraySegment<byte>(result, 0, bytesWritten).ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(result);
        }
    }
}
