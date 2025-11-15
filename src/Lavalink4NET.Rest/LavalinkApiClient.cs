namespace Lavalink4NET.Rest;

using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Web;
using Lavalink4NET.Protocol;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Protocol.Models.RoutePlanners;
using Lavalink4NET.Protocol.Requests;
using Lavalink4NET.Protocol.Responses;
using Lavalink4NET.Rest.Entities.Server;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Rest.Entities.Usage;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class LavalinkApiClient : LavalinkApiClientBase, ILavalinkApiClient
{
    private readonly TimeSpan _trackSearchSuccessCacheDuration;
    private readonly TimeSpan _trackSearchFailureCacheDuration;
    private readonly IMemoryCache _memoryCache;

    public LavalinkApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<LavalinkApiClientOptions> options,
        IMemoryCache memoryCache,
        ILogger<LavalinkApiClient> logger)
        : base(httpClientFactory, options, logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        Endpoints = new LavalinkApiEndpoints(options.Value.BaseAddress);
        _memoryCache = memoryCache;

        _trackSearchSuccessCacheDuration = options.Value.TrackCacheOptions.SuccessCacheDuration.GetValueOrDefault(TimeSpan.FromHours(8));
        _trackSearchFailureCacheDuration = options.Value.TrackCacheOptions.FailureCacheDuration.GetValueOrDefault(TimeSpan.FromHours(0.5));
    }

    public LavalinkApiEndpoints Endpoints { get; }

    public async ValueTask<LavalinkTrack?> LoadTrackAsync(
        string identifier,
        TrackLoadOptions loadOptions = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(identifier);

        var trackLoadResult = await LoadTracksAsync(
            identifier: identifier,
            loadOptions: loadOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return trackLoadResult.IsSuccess ? trackLoadResult.Track : null;
    }

    public async ValueTask<TrackLoadResult> LoadTracksAsync(
        string identifier,
        TrackLoadOptions loadOptions = default,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(identifier);

        identifier = BuildIdentifier(identifier, loadOptions);

        var cacheAccessor = new CacheAccessor<LoadResultModel>(
            MemoryCache: _memoryCache,
            Key: $"track-{identifier}",
            Mode: loadOptions.CacheMode);

        if (!cacheAccessor.TryGet(out var loadResult))
        {
            loadResult = await LoadTracksInternalAsync(identifier, cancellationToken).ConfigureAwait(false);

            var success = loadResult is TrackLoadResultModel or PlaylistLoadResultModel or SearchLoadResultModel;

            var relativeExpiration = success
                ? _trackSearchSuccessCacheDuration
                : _trackSearchFailureCacheDuration;

            cacheAccessor.Set(loadResult, relativeExpiration);
        }

        static TrackLoadResult CreatePlaylist(PlaylistLoadResultData loadResult)
        {
            var (playlist, tracks) = LavalinkApiClient.CreatePlaylist(loadResult);
            return TrackLoadResult.CreatePlaylist(tracks, playlist);
        }

        return loadResult switch
        {
            TrackLoadResultModel result => TrackLoadResult.CreateTrack(CreateTrack(result.Data)),
            SearchLoadResultModel result => TrackLoadResult.CreateSearch(result.Data.Select(CreateTrack).ToImmutableArray()),
            ErrorLoadResultModel result => TrackLoadResult.CreateError(new TrackException(result.Data.Severity, result.Data.Message, result.Data.Cause)),
            PlaylistLoadResultModel result => CreatePlaylist(result.Data),

            _ => TrackLoadResult.CreateEmpty(),
        };
    }

    public async ValueTask<LavalinkServerInformation> RetrieveServerInformationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var httpClient = CreateHttpClient();

        var model = await httpClient
            .GetFromJsonAsync(Endpoints.Information, ProtocolSerializerContext.Default.LavalinkServerInformationModel, cancellationToken)
            .ConfigureAwait(false);

        return LavalinkServerInformation.FromModel(model!);
    }

    public async ValueTask<LavalinkServerStatistics> RetrieveStatisticsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var httpClient = CreateHttpClient();

        var model = await httpClient
            .GetFromJsonAsync(Endpoints.Statistics, ProtocolSerializerContext.Default.LavalinkServerStatisticsModel, cancellationToken)
            .ConfigureAwait(false);

        return LavalinkServerStatistics.FromModel(model!);
    }

    public async ValueTask<string> RetrieveVersionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var httpClient = CreateHttpClient();

        return await httpClient
            .GetStringAsync(Endpoints.Version, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<SessionModel> UpdateSessionAsync(string sessionId, SessionUpdateProperties properties, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        var requestUri = Endpoints.Session(sessionId);
        using var httpClient = CreateHttpClient();

        using var jsonContent = JsonContent.Create(
            inputValue: properties,
            jsonTypeInfo: ProtocolSerializerContext.Default.SessionUpdateProperties);

        using var responseMessage = await httpClient
            .PatchAsync(requestUri, jsonContent, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessStatusCodeAsync(responseMessage, cancellationToken).ConfigureAwait(false);

        var model = await responseMessage.Content
            .ReadFromJsonAsync(ProtocolSerializerContext.Default.SessionModel, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return model!;
    }

    public async ValueTask<PlayerInformationModel> UpdatePlayerAsync(string sessionId, ulong guildId, PlayerUpdateProperties properties, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        var requestUri = Endpoints.Player(sessionId, guildId);
        using var httpClient = CreateHttpClient();

        using var jsonContent = JsonContent.Create(
            inputValue: properties,
            jsonTypeInfo: ProtocolSerializerContext.Default.PlayerUpdateProperties);

        using var responseMessage = await httpClient
            .PatchAsync(requestUri, jsonContent, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessStatusCodeAsync(responseMessage, cancellationToken).ConfigureAwait(false);

        var model = await responseMessage.Content
            .ReadFromJsonAsync(ProtocolSerializerContext.Default.PlayerInformationModel, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return model!;
    }

    internal static string BuildIdentifier(string identifier, TrackLoadOptions loadOptions = default)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        return StrictSearchHelper.Process(loadOptions.SearchBehavior, identifier, loadOptions.SearchMode);
    }

    internal static (PlaylistInformation Playlist, ImmutableArray<LavalinkTrack> Tracks) CreatePlaylist(PlaylistLoadResultData loadResult)
    {
        ArgumentNullException.ThrowIfNull(loadResult);

        var tracks = loadResult.Tracks.Select(CreateTrack).ToImmutableArray();

        var selectedTrack = loadResult.PlaylistInformation.SelectedTrack is null
            ? null
            : tracks[loadResult.PlaylistInformation.SelectedTrack.Value];

        var playlistInformation = new PlaylistInformation(
            Name: loadResult.PlaylistInformation.Name,
            SelectedTrack: selectedTrack,
            AdditionalInformation: loadResult.AdditionalInformation);

        return (playlistInformation, tracks);
    }

    internal static LavalinkTrack CreateTrack(TrackModel track) => new()
    {
        Duration = track.Information.Duration,
        Identifier = track.Information.Identifier,
        IsLiveStream = track.Information.IsLiveStream,
        IsSeekable = track.Information.IsSeekable,
        SourceName = track.Information.SourceName,
        StartPosition = track.Information.Position,
        Title = track.Information.Title,
        Uri = track.Information.Uri,
        TrackData = track.Data,
        Author = track.Information.Author,
        ArtworkUri = track.Information.ArtworkUri,
        Isrc = track.Information.Isrc,
        AdditionalInformation = track.AdditionalInformation,

        ProbeInfo = track.AdditionalInformation.TryGetValue("probeInfo", out var probeInformationElement)
            ? probeInformationElement.GetString()
            : null,
    };

    private async ValueTask<LoadResultModel> LoadTracksInternalAsync(string identifier, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(identifier);

        var metricTag = KeyValuePair.Create<string, object?>("Identifier", identifier);
        Diagnostics.QueriedTracks.Add(1, metricTag);

        var queryParameters = HttpUtility.ParseQueryString(string.Empty);
        queryParameters["identifier"] = identifier;

        var requestUri = new UriBuilder(Endpoints.LoadTracks) { Query = queryParameters.ToString(), }.Uri;

        using var httpClient = CreateHttpClient();

        using var responseMessage = await httpClient
            .GetAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessStatusCodeAsync(responseMessage, cancellationToken).ConfigureAwait(false);

        var model = await responseMessage.Content
            .ReadFromJsonAsync(ProtocolSerializerContext.Default.LoadResultModel, cancellationToken)
            .ConfigureAwait(false);

        return model!;
    }

    public async ValueTask DestroyPlayerAsync(string sessionId, ulong guildId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sessionId);

        var requestUri = Endpoints.Player(sessionId, guildId);
        using var httpClient = CreateHttpClient();

        using var responseMessage = await httpClient
            .DeleteAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessStatusCodeAsync(responseMessage, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<PlayerInformationModel?> GetPlayerAsync(string sessionId, ulong guildId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sessionId);

        var requestUri = Endpoints.Player(sessionId, guildId);
        using var httpClient = CreateHttpClient();

        using var responseMessage = await httpClient
            .GetAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        if (responseMessage.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessStatusCodeAsync(responseMessage, cancellationToken).ConfigureAwait(false);

        var model = await responseMessage.Content
            .ReadFromJsonAsync(ProtocolSerializerContext.Default.PlayerInformationModel, cancellationToken)
            .ConfigureAwait(false);

        return model!;
    }

    public async ValueTask<ImmutableArray<PlayerInformationModel>> GetPlayersAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sessionId);

        var requestUri = Endpoints.Players(sessionId);
        using var httpClient = CreateHttpClient();

        var model = await httpClient
            .GetFromJsonAsync(requestUri, ProtocolSerializerContext.Default.ImmutableArrayPlayerInformationModel, cancellationToken)
            .ConfigureAwait(false);

        return model!;
    }

    public async ValueTask<RoutePlannerInformationModel?> GetRoutePlannerInformationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requestUri = Endpoints.RoutePlannerStatus;
        using var httpClient = CreateHttpClient();

        using var responseMessage = await httpClient
            .GetAsync(requestUri, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessStatusCodeAsync(responseMessage, cancellationToken).ConfigureAwait(false);

        if (responseMessage.StatusCode is HttpStatusCode.NoContent)
        {
            return null;
        }

        var model = await responseMessage.Content
            .ReadFromJsonAsync(ProtocolSerializerContext.Default.RoutePlannerInformationModel, cancellationToken)
            .ConfigureAwait(false);

        return model!;
    }

    public async ValueTask UnmarkFailedAddressAsync(AddressUnmarkProperties properties, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        var requestUri = Endpoints.RoutePlannerAddress;
        using var httpClient = CreateHttpClient();

        using var responseMessage = await httpClient
            .PostAsJsonAsync(requestUri, properties, ProtocolSerializerContext.Default.AddressUnmarkProperties, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessStatusCodeAsync(responseMessage, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask UnmarkAllFailedAddressesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requestUri = Endpoints.AllRoutePlannerAddresses;
        using var httpClient = CreateHttpClient();

        using var responseMessage = await httpClient
            .PostAsync(requestUri, content: null, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessStatusCodeAsync(responseMessage, cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask EnsureSuccessStatusCodeAsync(HttpResponseMessage responseMessage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(responseMessage);

        if (responseMessage.IsSuccessStatusCode)
        {
            return;
        }

        var responseContent = await responseMessage.Content
            .ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        var errorResponse = default(HttpErrorResponse?);
        try
        {
            errorResponse = JsonSerializer.Deserialize(responseContent, ProtocolSerializerContext.Default.HttpErrorResponse);
        }
        catch (JsonException)
        {
        }

        var innerException = errorResponse is null
            ? default(Exception?)
            : new RemoteLavalinkException(errorResponse.ErrorMessage, errorResponse.StackTrace);

        var httpRequestException = errorResponse is null
            ? new HttpRequestException($"Response status code does not indicate success: {responseMessage.StatusCode} ({responseMessage.ReasonPhrase}): '{responseContent}' at {responseMessage.RequestMessage?.RequestUri}: {responseContent}.", innerException)
            : new HttpRequestException($"Response status code does not indicate success: {errorResponse.StatusCode} ({errorResponse.ReasonPhrase}): '{errorResponse.ErrorMessage}' at {errorResponse.RequestPath}", innerException);

        httpRequestException.Data["StatusCode"] = responseMessage.StatusCode;
        httpRequestException.Data["ReasonPhrase"] = responseMessage.ReasonPhrase;
        httpRequestException.Data["RequestUri"] = responseMessage.RequestMessage?.RequestUri;
        httpRequestException.Data["ResponseContent"] = responseContent;
        httpRequestException.Data["ErrorResponse"] = errorResponse;
        httpRequestException.Data["RequestPath"] = errorResponse?.RequestPath;
        httpRequestException.Data["ErrorMessage"] = errorResponse?.ErrorMessage;
        httpRequestException.Data["StackTrace"] = errorResponse?.StackTrace;

        throw httpRequestException;
    }
}

internal sealed class RemoteLavalinkException : Exception
{
    private readonly string? _stackTrace;

    public RemoteLavalinkException(string? message, string? stackTrace)
        : base(message)
    {
        _stackTrace = stackTrace;
    }

    public override string? StackTrace => _stackTrace ?? base.StackTrace;
}

internal static class StrictSearchHelper
{
    public static string Process(StrictSearchBehavior searchBehavior, string identifier, TrackSearchMode searchMode) => searchBehavior switch
    {
        StrictSearchBehavior.Throw => ProcessThrow(identifier, searchMode),
        StrictSearchBehavior.Resolve => ProcessResolve(identifier, searchMode),
        StrictSearchBehavior.Implicit => ProcessImplicit(identifier, searchMode),
        StrictSearchBehavior.Explicit => ProcessExplicit(identifier, searchMode),
        StrictSearchBehavior.Passthrough => ProcessPassthrough(identifier, searchMode),
        _ => throw new NotImplementedException()
    };

    private static string ProcessThrow(string identifier, TrackSearchMode searchMode)
    {
        var separatorIndex = identifier.AsSpan().IndexOfAny(':', ' ', '\t');

        if (separatorIndex is -1 || identifier[separatorIndex] is not ':')
        {
            return PrefixIdentifier(identifier, searchMode);
        }

        var currentPrefix = identifier[..separatorIndex];

        if (identifier.AsSpan(separatorIndex + 1).StartsWith("//"))
        {
            if (currentPrefix.Equals("https", StringComparison.OrdinalIgnoreCase) ||
                currentPrefix.Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                return identifier;
            }
        }

        throw new InvalidOperationException($"The query '{identifier}' has an search mode specified while search mode is set explicitly and strict mode is enabled (throw).");
    }

    private static string ProcessResolve(string identifier, TrackSearchMode searchMode)
    {
        var separatorIndex = identifier.AsSpan().IndexOfAny(':', ' ', '\t');

        if (separatorIndex is -1 || identifier[separatorIndex] is not ':')
        {
            return PrefixIdentifier(identifier, searchMode);
        }

        var currentPrefix = identifier[..separatorIndex];

        if (identifier.AsSpan(separatorIndex + 1).StartsWith("//"))
        {
            if (currentPrefix.Equals("https", StringComparison.OrdinalIgnoreCase) ||
                currentPrefix.Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                return identifier;
            }
        }

        return PrefixIdentifier(identifier, searchMode);
    }

    private static string ProcessImplicit(string identifier, TrackSearchMode searchMode)
    {
        ArgumentNullException.ThrowIfNull(searchMode.Prefix);

        var separatorIndex = identifier.AsSpan().IndexOfAny(':', ' ', '\t');

        if (separatorIndex is -1 || identifier[separatorIndex] is not ':')
        {
            return PrefixIdentifier(identifier, searchMode);
        }

        throw new InvalidOperationException($"The query '{identifier}' has an search mode specified while search mode is set explicitly and strict mode is enabled (implicit).");
    }

    private static string ProcessExplicit(string identifier, TrackSearchMode searchMode)
    {
        return PrefixIdentifier(identifier, searchMode);
    }

    private static string ProcessPassthrough(string identifier, TrackSearchMode searchMode)
    {
        if (searchMode.Prefix is null)
        {
            return identifier;
        }

        var separatorIndex = identifier.AsSpan().IndexOfAny(':', ' ', '\t');

        if (separatorIndex is -1 || identifier[separatorIndex] is not ':')
        {
            return PrefixIdentifier(identifier, searchMode);
        }

        return identifier;
    }

    private static string PrefixIdentifier(string identifier, TrackSearchMode searchMode)
    {
        return searchMode.Prefix is null
            ? identifier
            : $"{searchMode.Prefix}:{identifier}";
    }
}

file static class Diagnostics
{
    public static Counter<long> QueriedTracks { get; }

    static Diagnostics()
    {
        var meter = new Meter("Lavalink4NET");

        QueriedTracks = meter.CreateCounter<long>(
            name: "queried-tracks",
            unit: "Tracks",
            description: "The number of tracks queried from the node (non-cached only).");
    }
}