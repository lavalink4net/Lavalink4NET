using System.Buffers.Binary;

namespace Lavalink4NET.Tracks
{
    public partial record class LavalinkTrack
    {
        public byte[] Serialize(int? version = null)
        {
            using MemoryStream memStream = new();
            using BinaryWriter writer = new(memStream);

            // Update this value as this method is updated.
            version ??= 4;

            // Do NOT update this value as this method is updated.
            // This indicates legacy tracks.
            if (version < 4)
                return SerializeLegacy(version.Value);

            // The serialization structure that follows is largely the same as legacy tracks.
            //
            // However, among a few other slight improvements (like removing the need to repeatedly
            // re-allocate buffers to ensure a container is large enough), the C# BinaryReader/BinaryWriter
            // use little-endian format, whereas the original structure uses big-endian - so they are incompatible.
            //
            // While there were 0 problems with the original system, and it worked perfectly fine, the positive benefits that come
            // with switching to C# binary encoding well outweigh any that would come from staying.
            // It is easier to expand, standardized, requires less extensions, natively integrated, optimized, and most of all, much cleaner.
            // It additionally allows for dynamic buffer sizing, as it is capable of writing directly to a MemoryStream.

            if (SourceName is null)
            {
                throw new InvalidOperationException("Unknown source.");
            }

            bool isProbingAudioTrack = IsProbingTrack(SourceName);

            if (isProbingAudioTrack && ProbeInfo is null)
            {
                throw new InvalidOperationException("For the HTTP and local source audio manager, a probe info must be given.");
            }

            string rawUri = Uri?.ToString() ?? string.Empty;
            string rawArtworkUri = ArtworkUri?.ToString() ?? string.Empty;
            string isrc = Isrc ?? string.Empty;
            string probeInfo = ProbeInfo ?? string.Empty;

            long startPosition = (long)Math.Round(StartPosition?.TotalMilliseconds ?? 0);
            long duration = Duration == TimeSpan.MaxValue
                ? long.MaxValue
                : (long)Math.Round(Duration.TotalMilliseconds);

            writer.Write(version.Value);
            writer.Write(Title);
            writer.Write(Author);
            writer.Write(duration);
            writer.Write(Identifier);
            writer.Write(IsLiveStream);
            writer.Write(rawUri);
            writer.Write(rawArtworkUri);
            writer.Write(isrc);
            writer.Write(SourceName);
            writer.Write(probeInfo);

            if (IsExtendedTrack(SourceName))
            {
                void WriteJson(string propertyName)
                {
                    string json = AdditionalInformation.TryGetValue(propertyName, out var jsonElement)
                        ? jsonElement.GetString()!
                        : string.Empty;

                    writer.Write(json);
                }

                bool isPreview = AdditionalInformation.TryGetValue("isPreview", out var isPreviewElement) && isPreviewElement.GetBoolean();

                WriteJson("albumName");
                WriteJson("albumUrl");
                WriteJson("artistUrl");
                WriteJson("artistArtworkUrl");
                WriteJson("previewUrl");
                writer.Write(isPreview);
            }

            writer.Write(startPosition);

            return memStream.ToArray();
        }

        private byte[] SerializeLegacy(int version)
        {
            Span<byte> buffer = stackalloc byte[256];

            int bytesWritten;
            while (!TryEncodeLegacy(buffer, version, out bytesWritten))
            {
                buffer = GC.AllocateUninitializedArray<byte>(buffer.Length * 2);
            }

            return buffer[..bytesWritten].ToArray();
        }

        #region LEGACY ENCODING

        internal bool TryEncodeLegacy(Span<byte> buffer, int version, out int bytesWritten)
        {
            if (version is not 2 and not 3)
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

            if (version >= 3)
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
            EncodeHeaderLegacy(headerBuffer, payloadLength, (byte)version);

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
