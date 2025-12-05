namespace Lavalink4NET.Rest.Entities.Tracks;

public readonly record struct TrackSearchMode(string? Prefix)
{
    public static readonly TrackSearchMode YouTube = new("ytsearch");
    public static readonly TrackSearchMode YouTubeMusic = new("ytmsearch");
    public static readonly TrackSearchMode SoundCloud = new("scsearch");
    public static readonly TrackSearchMode Bandcamp = new("bcsearch");
    public static readonly TrackSearchMode None = new(null);

    // Only available when using the Lavasearch integration
    public static readonly TrackSearchMode Spotify = new("spsearch");
    public static readonly TrackSearchMode AppleMusic = new("amsearch");
    public static readonly TrackSearchMode Deezer = new("dzsearch");
    public static readonly TrackSearchMode YandexMusic = new("ymsearch");
}
