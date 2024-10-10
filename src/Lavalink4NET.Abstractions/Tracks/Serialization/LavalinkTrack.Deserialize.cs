using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Lavalink4NET.Tracks
{
    public partial record class LavalinkTrack
    {
        public static bool TryDeserialize(byte[] data, [MaybeNullWhen(false)] out LavalinkTrack track) => TryDeserialize(data, null, out track);

        public static bool TryDeserialize(byte[] data, string? originalTrackData, [MaybeNullWhen(false)] out LavalinkTrack track)
        {
            try
            {
                track = Deserialize(data, originalTrackData);
                return true;
            }
            catch
            {
                track = null;
                return false;
            }
        }

        public static LavalinkTrack Deserialize(byte[] data, string? originalTrackData = null)
        {
            originalTrackData ??= Utf8ToUtf16(data);
            uint header = BinaryPrimitives.ReadUInt32BigEndian(data);
            uint size = header & 0x3FFFFFFF;

            // Legacy encoded track!!
            if (size == data.Length - 4 && DeserializeLegacy(originalTrackData, data, out var result))
                return result;

            using MemoryStream memStream = new(data);
            using BinaryReader reader = new(memStream);
            Dictionary<string, JsonElement> additionalInformationBuilder = new();

            // The following fields are encoded in order using
            // a BinaryWriter for writing and BinaryReader for  reading.

            byte version = reader.ReadByte();
            string title = reader.ReadString();
            string author = reader.ReadString();
            long durationMs = reader.ReadInt64();
            string identifier = reader.ReadString();
            bool isLiveStream = reader.ReadBoolean();
            string rawUri = reader.ReadString();
            string rawArtworkUri = reader.ReadString();
            string isrc = reader.ReadString();
            string sourceName = reader.ReadString();
            string probeInfo = reader.ReadString();

            if (IsExtendedTrack(sourceName))
            {
                using MemoryStream jsonStream = new();
                using Utf8JsonWriter jsonWriter = new(jsonStream);
                JsonObject json = new();

                void ReadJson(string propertyName)
                {
                    string? propertyValue = reader.ReadString();

                    if (propertyValue == string.Empty)
                        propertyValue = null;

                    // The additional information builder is filled with empty properties,
                    // which are then assigned after the entire Json object has finished writing.
                    json.Add(propertyName, propertyValue);
                    additionalInformationBuilder.Add(propertyName, new());
                }

                ReadJson("albumName");
                ReadJson("albumUrl");
                ReadJson("artistUrl");
                ReadJson("artistArtworkUrl");
                ReadJson("previewUrl");

                // "isPreview" is special, as it is encoded as a single byte,
                // not as a string.
                KeyValuePair<string, JsonNode?> isPreview = new("isPreview", reader.ReadBoolean());
                json.Add(isPreview);
                additionalInformationBuilder.Add(isPreview.Key, new());

                // Writing the json object and then reading it
                // allows for the transition to JsonElement based properties.
                json.WriteTo(jsonWriter);
                var jsonReader = new Utf8JsonReader(jsonStream.ToArray());
                var jsonDocument = JsonElement.ParseValue(ref jsonReader);

                foreach (string property in additionalInformationBuilder.Keys)
                {
                    additionalInformationBuilder[property] = jsonDocument.GetProperty(property);
                }
            }

            long startPositionMs = reader.ReadInt64();

            TimeSpan duration = durationMs >= TimeSpan.MaxValue.TotalMilliseconds
                ? TimeSpan.MaxValue
                : TimeSpan.FromMilliseconds(durationMs);

            TimeSpan? startPosition = startPositionMs is 0
                ? default(TimeSpan?)
                : TimeSpan.FromMilliseconds(startPositionMs);

            Uri.TryCreate(rawUri, UriKind.Absolute, out var uri);
            Uri.TryCreate(rawArtworkUri, UriKind.Absolute, out var artworkUri);

            return new LavalinkTrack()
            {
                Author = author,
                Identifier = identifier,
                Title = title,
                Duration = duration,
                IsLiveStream = isLiveStream,
                IsSeekable = !isLiveStream,
                ProbeInfo = probeInfo,
                SourceName = sourceName,
                StartPosition = startPosition,
                Uri = uri,
                ArtworkUri = artworkUri,
                Isrc = isrc,
                AdditionalInformation = additionalInformationBuilder.ToImmutableDictionary(),
                TrackData = originalTrackData
            };
        }

        private static bool DeserializeLegacy(ReadOnlySpan<char> originalTrackData, ReadOnlySpan<byte> buffer, [MaybeNullWhen(false)] out LavalinkTrack result)
        {
            result = null;
            var trackDecoder = new LegacyLavalinkTrackDecoder(buffer);

            if (!trackDecoder.TryReadHeader(out var version) ||
                !trackDecoder.TryReadString(out var title) ||
                !trackDecoder.TryReadString(out var author) ||
                !trackDecoder.TryReadInt64(out var durationValue) ||
                !trackDecoder.TryReadString(out var identifier) ||
                !trackDecoder.TryReadBoolean(out var isStream) ||
                !trackDecoder.TryReadOptionalString(out var rawUri))
            {
                return false;
            }

            var rawArtworkUri = default(string?);
            var isrc = default(string?);

            if (version >= 3 && (!trackDecoder.TryReadOptionalString(out rawArtworkUri) || !trackDecoder.TryReadOptionalString(out isrc)))
            {
                return false;
            }

            var uri = default(Uri?);
            if (rawUri is not null && !Uri.TryCreate(rawUri, UriKind.Absolute, out uri))
            {
                return false;
            }

            var artworkUri = default(Uri?);
            if (rawArtworkUri is not null && !Uri.TryCreate(rawArtworkUri, UriKind.Absolute, out artworkUri))
            {
                return false;
            }

            if (!trackDecoder.TryReadString(out var sourceName))
            {
                return false;
            }

            var containerProbeInformation = default(string?);

            if (IsProbingTrack(sourceName) && !trackDecoder.TryReadString(out containerProbeInformation))
            {
                return false;
            }

            var additionalInformationBuilder = ImmutableDictionary.CreateBuilder<string, JsonElement>();

            if (IsExtendedTrack(sourceName))
            {
                if (!trackDecoder.TryReadOptionalString(out var albumName) ||
                    !trackDecoder.TryReadOptionalString(out var rawAlbumUri) ||
                    !trackDecoder.TryReadOptionalString(out var rawArtistUri) ||
                    !trackDecoder.TryReadOptionalString(out var rawArtistArtworkUri) ||
                    !trackDecoder.TryReadOptionalString(out var rawPreviewUri) ||
                    !trackDecoder.TryReadBoolean(out var isPreview))
                {
                    return false;
                }

                var data = new JsonObject
                {
                    {"albumName", albumName },
                    {"albumUrl", rawAlbumUri },
                    {"artistUrl", rawArtistUri },
                    {"artistArtworkUrl", rawArtistArtworkUri },
                    {"previewUrl", rawPreviewUri },
                    {"isPreview", isPreview },
                };

                var bufferWriter = new ArrayBufferWriter<byte>();
                using var utf8JsonWriter = new Utf8JsonWriter(bufferWriter);
                data.WriteTo(utf8JsonWriter);
                utf8JsonWriter.Dispose();

                var utf8JsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);
                var jsonDocument = JsonElement.ParseValue(ref utf8JsonReader);

                additionalInformationBuilder.Add("albumName", jsonDocument.GetProperty("albumName"));
                additionalInformationBuilder.Add("albumUrl", jsonDocument.GetProperty("albumUrl"));
                additionalInformationBuilder.Add("artistUrl", jsonDocument.GetProperty("artistUrl"));
                additionalInformationBuilder.Add("artistArtworkUrl", jsonDocument.GetProperty("artistArtworkUrl"));
                additionalInformationBuilder.Add("previewUrl", jsonDocument.GetProperty("previewUrl"));
                additionalInformationBuilder.Add("isPreview", jsonDocument.GetProperty("isPreview"));
            }

            if (!trackDecoder.TryReadInt64(out var startPositionValue))
            {
                return false;
            }

            var startPosition = startPositionValue is 0
                ? default(TimeSpan?)
                : TimeSpan.FromMilliseconds(startPositionValue);

            var duration = durationValue >= TimeSpan.MaxValue.TotalMilliseconds
                ? TimeSpan.MaxValue
                : TimeSpan.FromMilliseconds(durationValue);

            result = new LavalinkTrack
            {
                Author = author,
                Identifier = identifier,
                Title = title,
                Duration = duration,
                IsLiveStream = isStream,
                IsSeekable = !isStream,
                ProbeInfo = containerProbeInformation,
                SourceName = sourceName,
                StartPosition = startPosition,
                Uri = uri,
                ArtworkUri = artworkUri,
                Isrc = isrc,
                TrackData = originalTrackData.ToString(),
                AdditionalInformation = additionalInformationBuilder.ToImmutable(),
            };

            return true;
        }

        internal ref struct LegacyLavalinkTrackDecoder(ReadOnlySpan<byte> buffer)
        {
            public ReadOnlySpan<byte> Buffer { get; set; } = buffer;

            public bool TryReadHeader(out int version)
            {
                version = 1;

                if (Buffer.Length is < 4)
                {
                    return false;
                }

                // the header is four bytes long, subtract
                var header = BinaryPrimitives.ReadUInt32BigEndian(Buffer);
                Buffer = Buffer[4..];

                var flags = (int)((header & 0xC0000000L) >> 30);
                var hasVersion = (flags & 1) is not 0;

                // verify size
                var size = header & 0x3FFFFFFF;

                if (size != Buffer.Length)
                {
                    // Invalid following payload length
                    return false;
                }

                if (hasVersion)
                {
                    if (Buffer.IsEmpty)
                    {
                        // Missing version
                        return false;
                    }

                    version = Buffer[0];
                    Buffer = Buffer[1..];
                }

                // verify version
                if (version is not 2 and not 3)
                {
                    // unsupported version
                    return false;
                }

                return true;
            }

            public bool TryReadBoolean(out bool value)
            {
                if (Buffer.IsEmpty)
                {
                    value = default;
                    return false;
                }

                value = Buffer[0] is not 0;
                Buffer = Buffer[1..];
                return true;
            }

            public bool TryReadInt64(out long value)
            {
                if (Buffer.Length < 8)
                {
                    value = default;
                    return false;
                }

                value = BinaryPrimitives.ReadInt64BigEndian(Buffer);
                Buffer = Buffer[8..];
                return true;
            }

            public bool TryReadOptionalString(out string? value)
            {
                if (!TryReadBoolean(out var isPresent))
                {
                    value = default;
                    return false;
                }

                if (!isPresent)
                {
                    value = null;
                    return true;
                }

                return TryReadString(out value);
            }

            public bool TryReadString([MaybeNullWhen(false)] out string value)
            {
                if (Buffer.Length < 2)
                {
                    value = default;
                    return false;
                }

                var length = BinaryPrimitives.ReadUInt16BigEndian(Buffer);
                Buffer = Buffer[2..];

                if (Buffer.Length < length)
                {
                    value = default;
                    return false;
                }

                var stringBuffer = Buffer[..length];
                Buffer = Buffer[length..];

                value = ReadModifiedUtf8(stringBuffer);
                return true;
            }

            private static string ReadModifiedUtf8(ReadOnlySpan<byte> value)
            {
                // Ported from https://android.googlesource.com/platform/prebuilts/fullsdk/sources/android-29/+/refs/heads/androidx-wear-release/java/io/DataInputStream.java

                Span<char> buffer = value.Length < 256
                    ? stackalloc char[256]
                    : GC.AllocateUninitializedArray<char>(value.Length * 2);

                var length = value.Length;
                var count = 0;
                var charactersWritten = 0;

                // Fast-read all ASCII characters
                while (!value.IsEmpty)
                {
                    var character = value[0];

                    if (character > 127)
                    {
                        break;
                    }

                    count++;
                    value = value[1..];
                    buffer[charactersWritten++] = (char)character;
                }

                while (!value.IsEmpty)
                {
                    var character = value[0];

                    switch (character >> 4)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                            // 0xxxxxxx
                            count++;
                            buffer[charactersWritten++] = (char)character;
                            value = value[1..];
                            break;

                        case 12:
                        case 13:
                            // 110x xxxx   10xx xxxx
                            count += 2;

                            if (count > length)
                            {
                                throw new InvalidDataException("Found partial character at end.");
                            }

                            var additionalCharacter = value[1];

                            if ((additionalCharacter & 0xC0) != 0x80)
                            {
                                throw new InvalidDataException($"malformed input around byte {count}");
                            }

                            buffer[charactersWritten++] = (char)(((character & 0x1F) << 6) | (additionalCharacter & 0x3F));
                            value = value[2..];

                            break;

                        case 14:
                            // 1110 xxxx  10xx xxxx  10xx xxxx
                            count += 3;

                            if (count > length)
                            {
                                throw new InvalidDataException("Found malformed input due to partial character at end");
                            }

                            var secondCharacter = (int)value[1];
                            var thirdCharacter = (int)value[2];

                            if (((secondCharacter & 0xC0) != 0x80) || ((thirdCharacter & 0xC0) != 0x80))
                            {
                                throw new InvalidDataException($"Found malformed input around byte {count - 1}");
                            }

                            buffer[charactersWritten++] = (char)(((character & 0x0F) << 12) | ((secondCharacter & 0x3F) << 6) | ((thirdCharacter & 0x3F) << 0));
                            value = value[3..];

                            break;

                        default:
                            // 10xx xxxx,  1111 xxxx
                            throw new InvalidDataException($"Found malformed input around byte {count}");
                    }
                }

                return buffer[..charactersWritten].ToString();
            }
        }
    }
}
