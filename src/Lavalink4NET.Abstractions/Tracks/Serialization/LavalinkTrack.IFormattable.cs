namespace Lavalink4NET.Tracks;

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Text.Unicode;

public partial record class LavalinkTrack : ISpanFormattable
{
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        
    }

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        
    }
}
