namespace Lavalink4NET.Tracks;

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;

public partial record class LavalinkTrack
#if NET7_0_OR_GREATER
    : ISpanParsable<LavalinkTrack>
#endif
{
    public static LavalinkTrack Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        
    }

    public static LavalinkTrack Parse(string s, IFormatProvider? provider)
    {
        
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out LavalinkTrack result)
    {
        
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out LavalinkTrack result)
    {
        
    }
}
