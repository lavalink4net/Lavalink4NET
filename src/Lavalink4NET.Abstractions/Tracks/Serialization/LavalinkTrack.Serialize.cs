namespace Lavalink4NET.Tracks
{
    public partial record class LavalinkTrack
    {
        public byte[] Serialize()
        {
            using MemoryStream memStream = new();
            using BinaryWriter writer = new(memStream);

            // Update this value as this method is updated.
            const byte Version = 4;

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

            writer.Write(Version);
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
    }
}
