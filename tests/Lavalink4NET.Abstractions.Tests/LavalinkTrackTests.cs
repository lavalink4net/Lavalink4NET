namespace Lavalink4NET.Abstractions.Tests;

using System.Text.Json;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Rest;
using Lavalink4NET.Tracks;

public sealed class LavalinkTrackTests
{
    [Theory]
    [InlineData("QAAAjAIAJFZhbmNlIEpveSAtICdSaXB0aWRlJyBPZmZpY2lhbCBWaWRlbwAObXVzaHJvb212aWRlb3MAAAAAAAMgyAALdUpfMUhNQUdiNGsAAQAraHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj11Sl8xSE1BR2I0awAHeW91dHViZQAAAAAAAAAA")]
    [InlineData("QAAAjQIAK1JpdGEgT3JhIC0gWW91ciBTb25nIChPZmZpY2lhbCBMeXJpYyBWaWRlbykACFJpdGEgT3JhAAAAAAACwwgAC2k5NU5sYjdraVBvAAEAK2h0dHBzOi8vd3d3LnlvdXR1YmUuY29tL3dhdGNoP3Y9aTk1TmxiN2tpUG8AB3lvdXR1YmUAAAAAAAAAAA==")]
    [InlineData("QAAAmAIAMkx1a2FzIEdyYWhhbSAtIExvdmUgU29tZW9uZSBbT0ZGSUNJQUwgTVVTSUMgVklERU9dAAxMdWthcyBHcmFoYW0AAAAAAAOhsAALZE40NHhwSGpOeEUAAQAraHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj1kTjQ0eHBIak54RQAHeW91dHViZQAAAAAAAAAA")]
    [InlineData("QAAAcQIADU1FVEFNT1JQSE9TSVMACklOVEVSV09STEQAAAAAAAIt7gAIOTU5OTA4ODcAAQA1aHR0cHM6Ly9tdXNpYy55YW5kZXgucnUvYWxidW0vMTk2MjI4MjYvdHJhY2svOTU5OTA4ODcAAAAAAAAAAAAA")]
    [InlineData("QAAAiwIAC0RlYXRoIG9mIE1lAApTQUlOVCBQSE5YAAAAAAAAdVgAKGh0dHBzOi8vd3d3Lm1ib3hkcml2ZS5jb20vc3RhcnRtdXNpYy5tcDMAAQAoaHR0cHM6Ly93d3cubWJveGRyaXZlLmNvbS9zdGFydG11c2ljLm1wMwAEaHR0cAADbXAzAAAAAAAAAAA=")]
    public void TestTrackDecodeEncodeRoundtripV2(string trackIdentifier)
    {
        // Arrange
        var track = LavalinkTrack.ParseLegacy(trackIdentifier, provider: null);
        track.TrackData = null; // avoid caching

        // Act
        var actualTrackIdentifier = track.ToStringLegacy(version: 2);

        // Assert
        Assert.Equal(trackIdentifier, actualTrackIdentifier);
    }

    [Theory]
    [InlineData("QAAAjgMAJFZhbmNlIEpveSAtICdSaXB0aWRlJyBPZmZpY2lhbCBWaWRlbwAObXVzaHJvb212aWRlb3MAAAAAAAMgyAALdUpfMUhNQUdiNGsAAQAraHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj11Sl8xSE1BR2I0awAAAAd5b3V0dWJlAAAAAAAAAAA=")]
    [InlineData("QAAAjwMAK1JpdGEgT3JhIC0gWW91ciBTb25nIChPZmZpY2lhbCBMeXJpYyBWaWRlbykACFJpdGEgT3JhAAAAAAACwwgAC2k5NU5sYjdraVBvAAEAK2h0dHBzOi8vd3d3LnlvdXR1YmUuY29tL3dhdGNoP3Y9aTk1TmxiN2tpUG8AAAAHeW91dHViZQAAAAAAAAAA")]
    [InlineData("QAAAmgMAMkx1a2FzIEdyYWhhbSAtIExvdmUgU29tZW9uZSBbT0ZGSUNJQUwgTVVTSUMgVklERU9dAAxMdWthcyBHcmFoYW0AAAAAAAOhsAALZE40NHhwSGpOeEUAAQAraHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj1kTjQ0eHBIak54RQAAAAd5b3V0dWJlAAAAAAAAAAA=")]
    [InlineData("QAAAcwMADU1FVEFNT1JQSE9TSVMACklOVEVSV09STEQAAAAAAAIt7gAIOTU5OTA4ODcAAQA1aHR0cHM6Ly9tdXNpYy55YW5kZXgucnUvYWxidW0vMTk2MjI4MjYvdHJhY2svOTU5OTA4ODcAAAAAAAAAAAAAAAA=")]
    [InlineData("QAAAjQMAC0RlYXRoIG9mIE1lAApTQUlOVCBQSE5YAAAAAAAAdVgAKGh0dHBzOi8vd3d3Lm1ib3hkcml2ZS5jb20vc3RhcnRtdXNpYy5tcDMAAQAoaHR0cHM6Ly93d3cubWJveGRyaXZlLmNvbS9zdGFydG11c2ljLm1wMwAAAARodHRwAANtcDMAAAAAAAAAAA==")]
    [InlineData("QAACLgMAIFZvaXMgc3VyIHRvbiBjaGVtaW4gLSBUZWNobm8gTWl4AAdCRU5ORVRUAAAAAAACt+wAFjMxbmZkRW9vTEVxN2RuM1VNY0llQjUAAQA1aHR0cHM6Ly9vcGVuLnNwb3RpZnkuY29tL3RyYWNrLzMxbmZkRW9vTEVxN2RuM1VNY0llQjUBAEBodHRwczovL2kuc2Nkbi5jby9pbWFnZS9hYjY3NjE2ZDAwMDBiMjczMWI5NmU2NDUwMTZjNGQ0MzE4NDJhYTkzAQAMREVBNjIyMzAxODAxAAdzcG90aWZ5AQAgVm9pcyBzdXIgdG9uIGNoZW1pbiAoVGVjaG5vIE1peCkBADVodHRwczovL29wZW4uc3BvdGlmeS5jb20vYWxidW0vNzlDeWM4R1JXbkx5amRKU015SjBkQgEANmh0dHBzOi8vb3Blbi5zcG90aWZ5LmNvbS9hcnRpc3QvMXI0M3dXNzB0bkdVYXVRWXZZNXc0OAEAQGh0dHBzOi8vaS5zY2RuLmNvL2ltYWdlL2FiNjc2MTYxMDAwMGU1ZWI4MTFiYzk2N2VlZDNkNjFlMThmZGE0OWIBAGtodHRwczovL3Auc2Nkbi5jby9tcDMtcHJldmlldy8zMzU0YWZlNDI5NmQyYjg4OWQzNjk2NTgwNGQ0MTRjNWMyYjM0MTJiP2NpZD1kZmI5ZjcxMDNmNDI0M2Q2Yjg5NzcyYjBhZDhmMTkwNgAAAAAAAAAAAA==")] // Previous sample from failing decode
    public void TestTrackDecodeEncodeRoundtripV3(string trackIdentifier)
    {
        // Arrange
        var track = LavalinkTrack.ParseLegacy(trackIdentifier, provider: null);
        track.TrackData = null; // avoid caching

        // Act
        var actualTrackIdentifier = track.ToStringLegacy(version: 3);

        // Assert
        Assert.Equal(trackIdentifier, actualTrackIdentifier);
    }

    [Theory]
    [InlineData("QAAAjAIAJFZhbmNlIEpveSAtICdSaXB0aWRlJyBPZmZpY2lhbCBWaWRlbwAObXVzaHJvb212aWRlb3MAAAAAAAMgyAALdUpfMUhNQUdiNGsAAQAraHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj11Sl8xSE1BR2I0awAHeW91dHViZQAAAAAAAAAA")]
    [InlineData("QAAAjQIAK1JpdGEgT3JhIC0gWW91ciBTb25nIChPZmZpY2lhbCBMeXJpYyBWaWRlbykACFJpdGEgT3JhAAAAAAACwwgAC2k5NU5sYjdraVBvAAEAK2h0dHBzOi8vd3d3LnlvdXR1YmUuY29tL3dhdGNoP3Y9aTk1TmxiN2tpUG8AB3lvdXR1YmUAAAAAAAAAAA==")]
    [InlineData("QAAAmAIAMkx1a2FzIEdyYWhhbSAtIExvdmUgU29tZW9uZSBbT0ZGSUNJQUwgTVVTSUMgVklERU9dAAxMdWthcyBHcmFoYW0AAAAAAAOhsAALZE40NHhwSGpOeEUAAQAraHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj1kTjQ0eHBIak54RQAHeW91dHViZQAAAAAAAAAA")]
    [InlineData("QAAAiwIAC0RlYXRoIG9mIE1lAApTQUlOVCBQSE5YAAAAAAAAdVgAKGh0dHBzOi8vd3d3Lm1ib3hkcml2ZS5jb20vc3RhcnRtdXNpYy5tcDMAAQAoaHR0cHM6Ly93d3cubWJveGRyaXZlLmNvbS9zdGFydG11c2ljLm1wMwAEaHR0cAADbXAzAAAAAAAAAAA=")]
    [InlineData("QAAA2QMAPFJpY2sgQXN0bGV5IC0gTmV2ZXIgR29ubmEgR2l2ZSBZb3UgVXAgKE9mZmljaWFsIE11c2ljIFZpZGVvKQALUmljayBBc3RsZXkAAAAAAANACAALZFF3NHc5V2dYY1EAAQAraHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj1kUXc0dzlXZ1hjUQEANGh0dHBzOi8vaS55dGltZy5jb20vdmkvZFF3NHc5V2dYY1EvbWF4cmVzZGVmYXVsdC5qcGcAAAd5b3V0dWJlAAAAAAAAAAA=")] // v3
    public void TestTrackDecoding(string base64)
    {
        // verify the header of the base64 encoded track
        var result = LavalinkTrack.TryParseLegacy(base64, provider: null, out _);
        Assert.True(result);
    }

    [Theory]
    [InlineData("QAAAjAIAJFZhbmNlIEpveSAtICdSaXB0aWRlJyBPZmZpY2lhbCBWaWRlbwAObXVzaHJvb212aWRlb3MAAAAAAAMgyAALdUpfMUhNQUdiNGsAAQAraHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj11Sl8xSE1BR2I0awAHeW91dHViZQAAAAAAAAAA")]
    [InlineData("QAAAjQIAK1JpdGEgT3JhIC0gWW91ciBTb25nIChPZmZpY2lhbCBMeXJpYyBWaWRlbykACFJpdGEgT3JhAAAAAAACwwgAC2k5NU5sYjdraVBvAAEAK2h0dHBzOi8vd3d3LnlvdXR1YmUuY29tL3dhdGNoP3Y9aTk1TmxiN2tpUG8AB3lvdXR1YmUAAAAAAAAAAA==")]
    [InlineData("QAAAmAIAMkx1a2FzIEdyYWhhbSAtIExvdmUgU29tZW9uZSBbT0ZGSUNJQUwgTVVTSUMgVklERU9dAAxMdWthcyBHcmFoYW0AAAAAAAOhsAALZE40NHhwSGpOeEUAAQAraHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj1kTjQ0eHBIak54RQAHeW91dHViZQAAAAAAAAAA")]
    [InlineData("QAAAiwIAC0RlYXRoIG9mIE1lAApTQUlOVCBQSE5YAAAAAAAAdVgAKGh0dHBzOi8vd3d3Lm1ib3hkcml2ZS5jb20vc3RhcnRtdXNpYy5tcDMAAQAoaHR0cHM6Ly93d3cubWJveGRyaXZlLmNvbS9zdGFydG11c2ljLm1wMwAEaHR0cAADbXAzAAAAAAAAAAA=")]
    [InlineData("QAAA2QMAPFJpY2sgQXN0bGV5IC0gTmV2ZXIgR29ubmEgR2l2ZSBZb3UgVXAgKE9mZmljaWFsIE11c2ljIFZpZGVvKQALUmljayBBc3RsZXkAAAAAAANACAALZFF3NHc5V2dYY1EAAQAraHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj1kUXc0dzlXZ1hjUQEANGh0dHBzOi8vaS55dGltZy5jb20vdmkvZFF3NHc5V2dYY1EvbWF4cmVzZGVmYXVsdC5qcGcAAAd5b3V0dWJlAAAAAAAAAAA=")] // v3
    public void TrackDoesNotThrowOnMissingData(string base64)
    {
        var data = Convert.FromBase64String(base64);

        var exception = Record.Exception(() =>
        {
            for (var size = 0; size < data.Length - 1; size++)
            {
                var result = LavalinkTrack.TryParseLegacy(base64, data.AsSpan(0, size), out _);
                Assert.False(result);
            }
        });

        Assert.Null(exception);
    }

    [Fact]
    public void TestTrackDecodeEncodeRoundTripValidate()
    {
        var trackInfo = new LavalinkTrack
        {
            Author = "Test Author",
            Duration = TimeSpan.FromSeconds(10),
            IsLiveStream = true,
            IsSeekable = false,
            StartPosition = default,
            Uri = new Uri("https://example.com/"),
            SourceName = "mp3",
            Title = "Test Title",
            Identifier = "abcd",
        };

        var track = trackInfo.ToString();

        // decode back
        var actualTrackInfo = LavalinkTrack.ParseLegacy(track, provider: null);

        Assert.Equal(trackInfo.Author, actualTrackInfo.Author);
        Assert.Equal(trackInfo.Duration, actualTrackInfo.Duration);
        Assert.Equal(trackInfo.IsLiveStream, actualTrackInfo.IsLiveStream);
        Assert.Equal(trackInfo.IsSeekable, actualTrackInfo.IsSeekable);
        Assert.Equal(trackInfo.StartPosition, actualTrackInfo.StartPosition);
        Assert.Equal(trackInfo.SourceName, actualTrackInfo.SourceName);
        Assert.Equal(trackInfo.Title, actualTrackInfo.Title);
        Assert.Equal(trackInfo.Identifier, actualTrackInfo.Identifier);
    }

    [Fact]
    public void TestEncodeTrackV3WithIncreasingSize()
    {
        // Arrange
        var trackInfo = new LavalinkTrack
        {
            Author = "Test Author",
            Duration = TimeSpan.FromSeconds(10),
            IsLiveStream = true,
            IsSeekable = false,
            StartPosition = default,
            Uri = new Uri("https://example.com/"),
            SourceName = "mp3",
            Title = "Test Title",
            Identifier = "abcd",
        };

        var buffer = GC.AllocateUninitializedArray<byte>(4096);

        // Act
        var exception = Record.Exception(() =>
        {
            for (var size = 0; size < buffer.Length; size++)
            {
                var result = trackInfo.TryEncodeLegacy(
                    buffer: buffer.AsSpan()[..size],
                    version: 3,
                    bytesWritten: out _);

                if (result)
                {
                    break;
                }
            }
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void TestEncodeTrackV2WithIncreasingSize()
    {
        // Arrange
        var trackInfo = new LavalinkTrack
        {
            Author = "Test Author",
            Duration = TimeSpan.FromSeconds(10),
            IsLiveStream = true,
            IsSeekable = false,
            StartPosition = default,
            Uri = new Uri("https://example.com/"),
            SourceName = "mp3",
            Title = "Test Title",
            Identifier = "abcd",
        };

        var buffer = GC.AllocateUninitializedArray<byte>(4096);

        // Act
        var exception = Record.Exception(() =>
        {
            for (var size = 0; size < buffer.Length; size++)
            {
                var result = trackInfo.TryEncodeLegacy(
                    buffer: buffer.AsSpan()[..size],
                    version: 2,
                    bytesWritten: out _);

                if (result)
                {
                    break;
                }
            }
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void TestEncodeWithCachedTrackData()
    {
        // Arrange
        var trackInfo = new LavalinkTrack
        {
            Author = "Test Author",
            Duration = TimeSpan.FromSeconds(10),
            IsLiveStream = true,
            IsSeekable = false,
            StartPosition = default,
            Uri = new Uri("https://example.com/"),
            SourceName = "mp3",
            Title = "Test Title",
            Identifier = "abcd",
            TrackData = "cached-id"
        };

        // Act
        var result = trackInfo.ToString();

        // Assert
        Assert.Equal("cached-id", result);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void TestEncodeWithCachedTrackDataNotCachedWithExplicitVersion(int version)
    {
        // Arrange
        var trackInfo = new LavalinkTrack
        {
            Author = "Test Author",
            Duration = TimeSpan.FromSeconds(10),
            IsLiveStream = true,
            IsSeekable = false,
            StartPosition = default,
            Uri = new Uri("https://example.com/"),
            SourceName = "mp3",
            Title = "Test Title",
            Identifier = "abcd",
            TrackData = "cached-id"
        };

        // Act
        var result = trackInfo.ToStringLegacy(version);

        // Assert
        Assert.NotEqual("cached-id", result);
    }

    [Fact]
    public void TestEncodeHugeTrack()
    {
        // The track encoder uses a temporary stack local buffer
        // to encode the track. This test ensures that the encoder
        // can handle huge tracks without overflowing the stack.
        // The encoder should fall back to the heap if the stack
        // buffer is too small.

        // Arrange
        var trackInfo = new LavalinkTrack
        {
            Author = new string('@', 4096),
            Duration = TimeSpan.FromSeconds(10),
            IsLiveStream = true,
            IsSeekable = false,
            StartPosition = default,
            Uri = new Uri("https://example.com/"),
            SourceName = "mp3",
            Title = "Test Title",
            Identifier = "abcd",
        };

        // Act
        var result = trackInfo.ToStringLegacy(format: null, formatProvider: null);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void TestDecodeMutateAndEncode()
    {
        // This test should test if the track does not inherit the cached track data

        // Arrange
        // Parsing from a base64 string will cache it internally
        var originalTrackInfo = LavalinkTrack.ParseLegacy(
            s: "QAAAjAIAJFZhbmNlIEpveSAtICdSaXB0aWRlJyBPZmZpY2lhbCBWaWRlbwAObXVzaHJvb212aWRlb3MAAAAAAAMgyAALdUpfMUhNQUdiNGsAAQAraHR0cHM6Ly93d3cueW91dHViZS5jb20vd2F0Y2g/dj11Sl8xSE1BR2I0awAHeW91dHViZQAAAAAAAAAA",
            provider: null);

        // Mutate track
        var mutatedTrack = originalTrackInfo with { StartPosition = TimeSpan.FromSeconds(30), };

        // Act
        var result = mutatedTrack.ToString();

        // Assert
        Assert.NotEqual(result, originalTrackInfo.ToString());
    }

    [Fact]
    public void TestEncodeKnownTrackWithSpecialCharacters()
    {
        // Arrange
        var model = JsonSerializer.Deserialize<TrackModel>("""
            {
            	"encoded": "QAAA2gMAN0NvenkgYW5pbWFsIGNyb3NzaW5nIG11c2ljIHRoYXQgY3VyZSBteSBoZWFkYWNoZXPtoLztvL8AC1RlbmRvIEZhcm1zAAAAAAA/26gACzhUYkx1Qk9DbFNnAAEAK2h0dHBzOi8vd3d3LnlvdXR1YmUuY29tL3dhdGNoP3Y9OFRiTHVCT0NsU2cBADpodHRwczovL2kueXRpbWcuY29tL3ZpX3dlYnAvOFRiTHVCT0NsU2cvbWF4cmVzZGVmYXVsdC53ZWJwAAAHeW91dHViZQAAAAAAAAAA",
            	"info": {
            		"identifier": "8TbLuBOClSg",
            		"isSeekable": true,
            		"author": "Tendo Farms",
            		"length": 4185000,
            		"isStream": false,
            		"position": 0,
            		"title": "Cozy animal crossing music that cure my headaches🌿",
            		"uri": "https://www.youtube.com/watch?v=8TbLuBOClSg",
            		"sourceName": "youtube",
            		"artworkUrl": "https://i.ytimg.com/vi_webp/8TbLuBOClSg/maxresdefault.webp",
            		"isrc": null
            	},
            	"pluginInfo": {},
            	"userData": {}
            }
            """)!;

        var track = LavalinkApiClient.CreateTrack(model!);
        track.TrackData = null; // avoid caching

        // Act
        var actualIdentifier = track.ToStringLegacy(version: null);

        // Assert
        Assert.Equal(model.Data, actualIdentifier);
    }

    [Fact]
    public void TestDecodeKnownTrackWithSpecialCharacters()
    {
        // Arrange
        var model = JsonSerializer.Deserialize<TrackModel>("""
            {
            	"encoded": "QAAA2gMAN0NvenkgYW5pbWFsIGNyb3NzaW5nIG11c2ljIHRoYXQgY3VyZSBteSBoZWFkYWNoZXPtoLztvL8AC1RlbmRvIEZhcm1zAAAAAAA/26gACzhUYkx1Qk9DbFNnAAEAK2h0dHBzOi8vd3d3LnlvdXR1YmUuY29tL3dhdGNoP3Y9OFRiTHVCT0NsU2cBADpodHRwczovL2kueXRpbWcuY29tL3ZpX3dlYnAvOFRiTHVCT0NsU2cvbWF4cmVzZGVmYXVsdC53ZWJwAAAHeW91dHViZQAAAAAAAAAA",
            	"info": {
            		"identifier": "8TbLuBOClSg",
            		"isSeekable": true,
            		"author": "Tendo Farms",
            		"length": 4185000,
            		"isStream": false,
            		"position": 0,
            		"title": "Cozy animal crossing music that cure my headaches🌿",
            		"uri": "https://www.youtube.com/watch?v=8TbLuBOClSg",
            		"sourceName": "youtube",
            		"artworkUrl": "https://i.ytimg.com/vi_webp/8TbLuBOClSg/maxresdefault.webp",
            		"isrc": null
            	},
            	"pluginInfo": {},
            	"userData": {}
            }
            """)!;

        // Act
        var parsedTrack = LavalinkTrack.ParseLegacy(model.Data, provider: null);

        // Assert
        Assert.Equal(model.Information.Identifier, parsedTrack.Identifier);
        Assert.Equal(model.Information.IsSeekable, parsedTrack.IsSeekable);
        Assert.Equal(model.Information.Author, parsedTrack.Author);
        Assert.Equal(model.Information.Duration, parsedTrack.Duration);
        Assert.Equal(model.Information.IsLiveStream, parsedTrack.IsLiveStream);
        Assert.Equal(model.Information.Title, parsedTrack.Title);
        Assert.Equal(model.Information.Uri, parsedTrack.Uri);
        Assert.Equal(model.Information.SourceName, parsedTrack.SourceName);
        Assert.Equal(model.Information.ArtworkUri, parsedTrack.ArtworkUri);
        Assert.Equal(model.Information.Isrc, parsedTrack.Isrc);
    }

    [Fact]
    public void TestDecodedTrackEqualsParsedTrack()
    {
        // Arrange
        var model = JsonSerializer.Deserialize<TrackModel>("""
            {
            	"encoded": "QAAA2gMAN0NvenkgYW5pbWFsIGNyb3NzaW5nIG11c2ljIHRoYXQgY3VyZSBteSBoZWFkYWNoZXPtoLztvL8AC1RlbmRvIEZhcm1zAAAAAAA/26gACzhUYkx1Qk9DbFNnAAEAK2h0dHBzOi8vd3d3LnlvdXR1YmUuY29tL3dhdGNoP3Y9OFRiTHVCT0NsU2cBADpodHRwczovL2kueXRpbWcuY29tL3ZpX3dlYnAvOFRiTHVCT0NsU2cvbWF4cmVzZGVmYXVsdC53ZWJwAAAHeW91dHViZQAAAAAAAAAA",
            	"info": {
            		"identifier": "8TbLuBOClSg",
            		"isSeekable": true,
            		"author": "Tendo Farms",
            		"length": 4185000,
            		"isStream": false,
            		"position": 0,
            		"title": "Cozy animal crossing music that cure my headaches🌿",
            		"uri": "https://www.youtube.com/watch?v=8TbLuBOClSg",
            		"sourceName": "youtube",
            		"artworkUrl": "https://i.ytimg.com/vi_webp/8TbLuBOClSg/maxresdefault.webp",
            		"isrc": null
            	},
            	"pluginInfo": {},
            	"userData": {}
            }
            """)!;

        // Act
        var parsedTrack = LavalinkTrack.ParseLegacy(model.Data, provider: null);
        var decodedTrack = LavalinkApiClient.CreateTrack(model);

        // Assert
        Assert.Equal(decodedTrack, parsedTrack);
    }

    [Fact]
    public void TestDecodedTrackHasSameHashCode()
    {
        // Arrange
        var model = JsonSerializer.Deserialize<TrackModel>("""
            {
            	"encoded": "QAAA2gMAN0NvenkgYW5pbWFsIGNyb3NzaW5nIG11c2ljIHRoYXQgY3VyZSBteSBoZWFkYWNoZXPtoLztvL8AC1RlbmRvIEZhcm1zAAAAAAA/26gACzhUYkx1Qk9DbFNnAAEAK2h0dHBzOi8vd3d3LnlvdXR1YmUuY29tL3dhdGNoP3Y9OFRiTHVCT0NsU2cBADpodHRwczovL2kueXRpbWcuY29tL3ZpX3dlYnAvOFRiTHVCT0NsU2cvbWF4cmVzZGVmYXVsdC53ZWJwAAAHeW91dHViZQAAAAAAAAAA",
            	"info": {
            		"identifier": "8TbLuBOClSg",
            		"isSeekable": true,
            		"author": "Tendo Farms",
            		"length": 4185000,
            		"isStream": false,
            		"position": 0,
            		"title": "Cozy animal crossing music that cure my headaches🌿",
            		"uri": "https://www.youtube.com/watch?v=8TbLuBOClSg",
            		"sourceName": "youtube",
            		"artworkUrl": "https://i.ytimg.com/vi_webp/8TbLuBOClSg/maxresdefault.webp",
            		"isrc": null
            	},
            	"pluginInfo": {},
            	"userData": {}
            }
            """)!;

        // Act
        var parsedTrack = LavalinkTrack.ParseLegacy(model.Data, provider: null).GetHashCode();
        var decodedTrack = LavalinkApiClient.CreateTrack(model).GetHashCode();

        // Assert
        Assert.Equal(decodedTrack, parsedTrack);
    }
}