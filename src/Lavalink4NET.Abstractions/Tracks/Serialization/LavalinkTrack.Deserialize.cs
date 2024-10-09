using System.Buffers.Text;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Unicode;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Buffers.Binary;

namespace Lavalink4NET.Tracks
{
    public partial record class LavalinkTrack
    {
        public static LavalinkTrack Deserialize(byte[] data)
        {
            using MemoryStream memStream = new(data);
            using BinaryReader reader = new(memStream);
            Dictionary<string, JsonElement> additionalInformationBuilder = new();

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

                KeyValuePair<string, JsonNode?> isPreview = new("isPreview", reader.ReadBoolean());
                json.Add(isPreview);
                additionalInformationBuilder.Add(isPreview.Key, new());

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
                TrackData = EncodeDataToUtf16(data)
            };
        }

        // These LEGACY regions are a temporary measure in place while I work on re-writing the serialization system.
        // The plan is to automatically detect legacy encoding formats and implement these methods within the new decoding system.
        // ... but I need to actually write the new decoding system first.
        //
        // - Nycro, Oct. 7, 2024

        #region LEGACY PARSING

        public static LavalinkTrack ParseLegacy(string s, IFormatProvider? provider)
        {
            return ParseLegacy(s.AsSpan(), provider);
        }

        public static LavalinkTrack ParseLegacy(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            if (!TryParseLegacy(s, provider, out var result))
            {
                throw new ArgumentException("Invalid track.", nameof(s));
            }

            return result;
        }

        public static bool TryParseLegacy([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out LavalinkTrack result)
        {
            return TryParseLegacy(s is null ? default : s.AsSpan(), provider, out result);
        }

        public static bool TryParseLegacy(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out LavalinkTrack result)
        {
            var pool = ArrayPool<byte>.Shared.Rent(s.Length);

            try
            {
                var operationStatus = Utf8.FromUtf16(
                    source: s,
                    destination: pool,
                    charsRead: out _,
                    bytesWritten: out var utf8BytesWritten);

                if (operationStatus is not OperationStatus.Done)
                {
                    Debug.Assert(operationStatus is not OperationStatus.DestinationTooSmall);

                    result = null;
                    return false;
                }

                operationStatus = Base64.DecodeFromUtf8InPlace(
                    buffer: pool.AsSpan(0, utf8BytesWritten),
                    bytesWritten: out var decodedBytesWritten);

                if (operationStatus is not OperationStatus.Done)
                {
                    Debug.Assert(operationStatus is not OperationStatus.DestinationTooSmall);

                    result = null;
                    return false;
                }

                return TryParseLegacy(s, pool.AsSpan(0, decodedBytesWritten), out result);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pool);
            }
        }

        internal static bool TryParseLegacy(ReadOnlySpan<char> originalTrackData, ReadOnlySpan<byte> buffer, [MaybeNullWhen(false)] out LavalinkTrack result)
        {
            var trackDecoder = new LavalinkTrackDecoder(buffer);
            return TryParseLegacy(originalTrackData, ref trackDecoder, out result);
        }

        internal static bool TryParseLegacy(ReadOnlySpan<char> originalTrackData, ref LavalinkTrackDecoder trackDecoder, [MaybeNullWhen(false)] out LavalinkTrack result)
        {
            result = null;

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

        internal ref struct LavalinkTrackDecoder(ReadOnlySpan<byte> buffer)
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

        #endregion

        #region LEGACY FORMATTING

        public string ToStringLegacy()
        {
            return ToStringLegacy(version: null, format: null, formatProvider: null);
        }

        public string ToStringLegacy(string? format, IFormatProvider? formatProvider)
        {
            return ToStringLegacy(version: null, format: format, formatProvider: formatProvider);
        }

        public string ToStringLegacy(int? version)
        {
            return ToStringLegacy(version: version, format: null, formatProvider: null);
        }

        public string ToStringLegacy(int? version, string? format, IFormatProvider? formatProvider)
        {
            // The ToString method is culture-neutral and format-neutral
            if (TrackData is not null && version is null)
            {
                return TrackData;
            }

            Span<char> buffer = stackalloc char[256];

            int charsWritten;
            while (!TryFormatLegacy(buffer, out charsWritten, version, format ?? default, formatProvider))
            {
                buffer = GC.AllocateUninitializedArray<char>(buffer.Length * 2);
            }

            var trackData = new string(buffer[..charsWritten]);

            if (version is null)
            {
                TrackData = trackData;
            }

            return trackData;
        }

        public bool TryFormatLegacy(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            return TryFormatLegacy(destination, out charsWritten, version: null, format, provider);
        }

#pragma warning disable IDE0060
        public bool TryFormatLegacy(Span<char> destination, out int charsWritten, int? version, ReadOnlySpan<char> format, IFormatProvider? provider)
#pragma warning restore IDE0060
        {
            var buffer = ArrayPool<byte>.Shared.Rent(destination.Length);

            try
            {
                var result = TryEncodeLegacy(buffer, version, out var bytesWritten);

                if (!result)
                {
                    charsWritten = default;
                    return false;
                }

                var operationStatus = Base64.EncodeToUtf8InPlace(
                    buffer: buffer,
                    dataLength: bytesWritten,
                    bytesWritten: out var base64BytesWritten);

                if (operationStatus is not OperationStatus.Done)
                {
                    if (operationStatus is OperationStatus.DestinationTooSmall)
                    {
                        charsWritten = default;
                        return false;
                    }

                    throw new InvalidOperationException("Error while encoding to Base64.");
                }

                operationStatus = Utf8.ToUtf16(
                    source: buffer.AsSpan(0, base64BytesWritten),
                    destination: destination,
                    bytesRead: out _,
                    charsWritten: out charsWritten);

                if (operationStatus is not OperationStatus.Done)
                {
                    if (operationStatus is OperationStatus.DestinationTooSmall)
                    {
                        charsWritten = default;
                        return false;
                    }

                    throw new InvalidOperationException("Error while encoding to UTF-8.");
                }

                return true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        internal bool TryEncodeLegacy(Span<byte> buffer, int? version, out int bytesWritten)
        {
            var versionValue = version ?? 3;

            if (versionValue is not 2 and not 3)
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            if (SourceName is null)
            {
                throw new InvalidOperationException("Unknown source.");
            }

            var isProbingAudioTrack = IsProbingTrack(SourceName);

            if (isProbingAudioTrack && ProbeInfo is null)
            {
                throw new InvalidOperationException("For the HTTP and local source audio manager, a probe info must be given.");
            }

            if (buffer.Length < 5)
            {
                bytesWritten = 0;
                return false;
            }

            // Reserve 5 bytes for the header
            var headerBuffer = buffer[..5];
            buffer = buffer[5..];
            bytesWritten = 5;

            // Write title and author
            if (!TryEncodeStringLegacy(ref buffer, Title, ref bytesWritten) ||
                !TryEncodeStringLegacy(ref buffer, Author, ref bytesWritten))
            {
                return false;
            }

            // Write track duration
            if (buffer.Length < 8)
            {
                return false;
            }

            var duration = Duration == TimeSpan.MaxValue
                ? long.MaxValue
                : (long)Math.Round(Duration.TotalMilliseconds);

            BinaryPrimitives.WriteInt64BigEndian(
                destination: buffer[..8],
                value: duration);

            buffer = buffer[8..];
            bytesWritten += 8;

            // Write track identifier
            if (!TryEncodeStringLegacy(ref buffer, Identifier, ref bytesWritten))
            {
                return false;
            }

            // Write stream flag
            if (buffer.Length < 1)
            {
                return false;
            }

            buffer[0] = (byte)(IsLiveStream ? 1 : 0);

            bytesWritten++;
            buffer = buffer[1..];

            var rawUri = Uri is null ? string.Empty : Uri.ToString();

            if (!TryEncodeOptionalStringLegacy(ref buffer, rawUri, ref bytesWritten))
            {
                return false;
            }

            if (versionValue >= 3)
            {
                var rawArtworkUri = ArtworkUri is null ? string.Empty : ArtworkUri.ToString();

                if (!TryEncodeOptionalStringLegacy(ref buffer, rawArtworkUri, ref bytesWritten) ||
                    !TryEncodeOptionalStringLegacy(ref buffer, Isrc, ref bytesWritten))
                {
                    return false;
                }
            }

            // Write source name
            if (!TryEncodeStringLegacy(ref buffer, SourceName, ref bytesWritten))
            {
                return false;
            }

            // Write probe information
            if (isProbingAudioTrack && !TryEncodeStringLegacy(ref buffer, ProbeInfo, ref bytesWritten))
            {
                return false;
            }

            if (IsExtendedTrack(SourceName))
            {
                bool TryEncodeOptionalJsonString(ref Span<byte> buffer, string propertyName, ref int bytesWritten)
                {
                    var value = AdditionalInformation.TryGetValue(propertyName, out var jsonElement)
                        ? jsonElement.GetString()!
                        : string.Empty;

                    return TryEncodeOptionalStringLegacy(ref buffer, value, ref bytesWritten);
                }

                if (!TryEncodeOptionalJsonString(ref buffer, "albumName", ref bytesWritten) ||
                    !TryEncodeOptionalJsonString(ref buffer, "albumUrl", ref bytesWritten) ||
                    !TryEncodeOptionalJsonString(ref buffer, "artistUrl", ref bytesWritten) ||
                    !TryEncodeOptionalJsonString(ref buffer, "artistArtworkUrl", ref bytesWritten) ||
                    !TryEncodeOptionalJsonString(ref buffer, "previewUrl", ref bytesWritten))
                {
                    return false;
                }

                var isPreview = AdditionalInformation.TryGetValue("isPreview", out var isPreviewElement) && isPreviewElement.GetBoolean();

                if (buffer.Length < 1)
                {
                    return false;
                }

                buffer[0] = (byte)(isPreview ? 1 : 0);
                bytesWritten++;
                buffer = buffer[1..];
            }

            // Write track start position
            if (buffer.Length < 8)
            {
                return false;
            }

            BinaryPrimitives.WriteInt64BigEndian(
                destination: buffer[..8],
                value: (long)Math.Round(StartPosition?.TotalMilliseconds ?? 0));

            // buffer = buffer[8..];
            bytesWritten += 8;

            var payloadLength = bytesWritten - 4;
            EncodeHeaderLegacy(headerBuffer, payloadLength, (byte)versionValue);

            return true;
        }

        private static void EncodeHeaderLegacy(Span<byte> headerBuffer, int payloadLength, byte version)
        {
            // Set "has version" in header
            var header = 0b01000000000000000000000000000000 | payloadLength;
            BinaryPrimitives.WriteInt32BigEndian(headerBuffer, header);

            // version
            headerBuffer[4] = version;
        }

        private static bool TryEncodeStringLegacy(ref Span<byte> span, ReadOnlySpan<char> value, ref int bytesWritten)
        {
            if (span.Length < 2)
            {
                return false;
            }

            var lengthBuffer = span[..2];
            span = span[2..];

            var previousBytesWritten = bytesWritten;

            if (!TryWriteModifiedUtf8Legacy(ref span, value, ref bytesWritten))
            {
                return false;
            }

            var utf8BytesWritten = bytesWritten - previousBytesWritten;

            BinaryPrimitives.WriteUInt16BigEndian(lengthBuffer, (ushort)utf8BytesWritten);

            bytesWritten += 2;

            return true;
        }

        private static bool TryEncodeOptionalStringLegacy(ref Span<byte> span, ReadOnlySpan<char> value, ref int bytesWritten)
        {
            if (span.Length < 1)
            {
                return false;
            }

            var present = !value.IsWhiteSpace();

            span[0] = (byte)(present ? 1 : 0);
            span = span[1..];
            bytesWritten++;

            if (!present)
            {
                return true;
            }

            if (!TryEncodeStringLegacy(ref span, value, ref bytesWritten))
            {
                return false;
            }

            return true;
        }

        private static bool TryWriteModifiedUtf8Legacy(ref Span<byte> span, ReadOnlySpan<char> value, ref int bytesWritten)
        {
            // Ported from https://android.googlesource.com/platform/prebuilts/fullsdk/sources/android-29/+/refs/heads/androidx-wear-release/java/io/DataOutputStream.java

            int index;
            for (index = 0; index < value.Length; index++)
            {
                var character = value[index];

                if (character is not (>= (char)0x0001 and <= (char)0x007F))
                {
                    break;
                }

                if (span.IsEmpty)
                {
                    return false;
                }

                span[0] = (byte)character;
                bytesWritten++;
                span = span[1..];
            }

            for (; index < value.Length; index++)
            {
                var character = value[index];

                if (character is >= (char)0x0001 and <= (char)0x007F)
                {
                    if (span.IsEmpty)
                    {
                        return false;
                    }

                    span[0] = (byte)character;
                    bytesWritten++;
                    span = span[1..];
                }
                else if (character > 0x07FF)
                {
                    if (span.Length < 3)
                    {
                        return false;
                    }

                    span[0] = (byte)(0xE0 | ((character >> 12) & 0x0F));
                    span[1] = (byte)(0x80 | ((character >> 6) & 0x3F));
                    span[2] = (byte)(0x80 | ((character >> 0) & 0x3F));
                    bytesWritten += 3;
                    span = span[3..];
                }
                else
                {
                    if (span.Length < 2)
                    {
                        return false;
                    }

                    span[0] = (byte)(0xC0 | ((character >> 6) & 0x1F));
                    span[1] = (byte)(0x80 | ((character >> 0) & 0x3F));
                    bytesWritten += 2;
                    span = span[2..];
                }
            }

            return true;
        }

        #endregion
    }
}
