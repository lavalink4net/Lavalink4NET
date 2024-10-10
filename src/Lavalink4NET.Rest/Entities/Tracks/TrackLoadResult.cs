namespace Lavalink4NET.Rest.Entities.Tracks;

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Lavalink4NET.Tracks;

public readonly record struct TrackLoadResult
{
    private readonly object? _value; // either LavalinkTrack[] (immutable!), LavalinkTrack, or ExceptionData, null (no matches)
    private readonly PlaylistInformation? _playlist;

    public TrackLoadResult(object? value, PlaylistInformation? playlist)
    {
        _value = value;
        _playlist = playlist;
    }

    public PlaylistInformation? Playlist => _playlist?.Name is not null ? _playlist : null;

    [MemberNotNullWhen(true, nameof(Playlist))]
    public bool IsPlaylist => _playlist?.Name is not null;

    [MemberNotNullWhen(true, nameof(Track))]
    public bool IsSuccess => _value is LavalinkTrack[] or LavalinkTrack;

    [MemberNotNullWhen(false, nameof(Track))]
    public bool IsFailed => !IsSuccess;

    public bool HasMatches => _value is LavalinkTrack[] or LavalinkTrack;

    public TrackException? Exception => (_value as ExceptionData)?.Exception;

    public LavalinkTrack? Track => _value switch
    {
        LavalinkTrack track => track,
        LavalinkTrack[] tracks when tracks.Length > 0 => tracks[0],
        _ => null,
    };

    public ImmutableArray<LavalinkTrack> Tracks => _value switch
    {
        LavalinkTrack track => ImmutableArray.Create(track),
        LavalinkTrack[] tracks => Unsafe.As<LavalinkTrack[], ImmutableArray<LavalinkTrack>>(ref Unsafe.AsRef(in tracks)),
        _ => ImmutableArray<LavalinkTrack>.Empty,
    };

    public int Count => _value switch
    {
        LavalinkTrack => 1,
        LavalinkTrack[] tracks => tracks.Length,
        _ => 0,
    };

    public static TrackLoadResult CreatePlaylist(ImmutableArray<LavalinkTrack> tracks, PlaylistInformation playlist)
    {
        ArgumentNullException.ThrowIfNull(playlist.Name);

        var tracksArray = Unsafe.As<ImmutableArray<LavalinkTrack>, LavalinkTrack[]>(ref tracks);
        return new TrackLoadResult(tracksArray, playlist);
    }

    public static TrackLoadResult CreateTrack(LavalinkTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return new TrackLoadResult(track, default);
    }

    public static TrackLoadResult CreateSearch(ImmutableArray<LavalinkTrack> tracks)
    {
        var tracksArray = Unsafe.As<ImmutableArray<LavalinkTrack>, LavalinkTrack[]>(ref tracks);
        return new TrackLoadResult(tracksArray, default);
    }

    public static TrackLoadResult CreateEmpty()
    {
        return new TrackLoadResult(null, default);
    }

    public static TrackLoadResult CreateError(TrackException exception)
    {
        return new TrackLoadResult(new ExceptionData(exception), default);
    }
}

file sealed record class ExceptionData(TrackException Exception);
