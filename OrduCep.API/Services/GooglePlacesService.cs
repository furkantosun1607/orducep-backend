using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OrduCep.Domain.Entities;

namespace OrduCep.API.Services;

public interface IGooglePlacesService
{
    bool IsConfigured { get; }
    Task<GooglePlaceMatchResult?> FindPlaceIdAsync(Orduevi orduevi, CancellationToken cancellationToken);
    Task<GooglePlaceDetailsDto?> GetPlaceDetailsAsync(string placeId, CancellationToken cancellationToken);
}

public sealed record GooglePlaceMatchResult(
    string PlaceId,
    string ResourceName,
    string Query);

public sealed record GooglePlaceDetailsDto(
    string PlaceId,
    string DisplayName,
    string FormattedAddress,
    string GoogleMapsUri,
    double? Rating,
    int? UserRatingCount,
    string? NationalPhoneNumber,
    string? InternationalPhoneNumber,
    GoogleMapsLinksDto? GoogleMapsLinks,
    IReadOnlyList<GooglePlacePhotoDto> Photos,
    IReadOnlyList<GooglePlaceReviewDto> Reviews,
    IReadOnlyList<GoogleAttributionDto> Attributions,
    DateTime FetchedAtUtc);

public sealed record GooglePlacePhotoDto(
    string Name,
    string? PhotoUri,
    int? WidthPx,
    int? HeightPx,
    IReadOnlyList<GoogleAttributionDto> AuthorAttributions);

public sealed record GooglePlaceReviewDto(
    string? AuthorName,
    string? AuthorUri,
    string? ProfilePhotoUri,
    double? Rating,
    string? Text,
    string? RelativePublishTimeDescription,
    DateTime? PublishTime);

public sealed record GoogleAttributionDto(
    string? DisplayName,
    string? Uri,
    string? PhotoUri);

public sealed record GoogleMapsLinksDto(
    string? DirectionsUri,
    string? PlaceUri,
    string? WriteAReviewUri,
    string? ReviewsUri,
    string? PhotosUri);

public sealed class GooglePlacesApiException : Exception
{
    public GooglePlacesApiException(HttpStatusCode statusCode, string operation, string responseBody)
        : base(BuildMessage(statusCode, operation))
    {
        StatusCode = statusCode;
        Operation = operation;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string Operation { get; }

    public string ResponseBody { get; }

    private static string BuildMessage(HttpStatusCode statusCode, string operation) =>
        $"Google Places {operation} isteği başarısız oldu ({(int)statusCode}). Places API (New), faturalandırma ve API anahtarı kısıtlarını kontrol edin.";
}

public sealed class GooglePlacesService : IGooglePlacesService
{
    private const string PlacesBaseUrl = "https://places.googleapis.com/v1";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GooglePlacesService> _logger;

    public GooglePlacesService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GooglePlacesService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    private string ApiKey => FirstNonBlank(
        _configuration["GoogleMaps:ApiKey"],
        _configuration["GOOGLE_MAPS_API_KEY"],
        Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY"),
        Environment.GetEnvironmentVariable("GoogleMaps__ApiKey"),
        Environment.GetEnvironmentVariable("GoogleMaps:ApiKey")) ?? string.Empty;

    private string LanguageCode => _configuration["GoogleMaps:LanguageCode"] ?? "tr";

    private string RegionCode => _configuration["GoogleMaps:RegionCode"] ?? "TR";

    private int MaxPhotos => ReadBoundedInt("GoogleMaps:MaxPhotos", 6, 0, 10);

    private int MaxReviews => ReadBoundedInt("GoogleMaps:MaxReviews", 5, 0, 5);

    private int PhotoMaxWidthPx => ReadBoundedInt("GoogleMaps:PhotoMaxWidthPx", 1200, 120, 4800);

    public async Task<GooglePlaceMatchResult?> FindPlaceIdAsync(Orduevi orduevi, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var query = BuildSearchQuery(orduevi);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{PlacesBaseUrl}/places:searchText")
        {
            Content = JsonContent.Create(new
            {
                textQuery = query,
                languageCode = LanguageCode,
                regionCode = RegionCode,
                maxResultCount = 1
            })
        };

        request.Headers.Add("X-Goog-Api-Key", ApiKey);
        request.Headers.Add("X-Goog-FieldMask", "places.id,places.name");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await LogGoogleErrorAsync(response, "Text Search", cancellationToken);
            throw new GooglePlacesApiException(response.StatusCode, "Text Search", body);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("places", out var places) || places.GetArrayLength() == 0)
            return null;

        var first = places[0];
        var placeId = ReadString(first, "id");
        var resourceName = ReadString(first, "name");

        return string.IsNullOrWhiteSpace(placeId)
            ? null
            : new GooglePlaceMatchResult(placeId, resourceName, query);
    }

    public async Task<GooglePlaceDetailsDto?> GetPlaceDetailsAsync(string placeId, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{PlacesBaseUrl}/places/{Uri.EscapeDataString(placeId)}?languageCode={LanguageCode}&regionCode={RegionCode}");
        request.Headers.Add("X-Goog-Api-Key", ApiKey);
        request.Headers.Add(
            "X-Goog-FieldMask",
            "id,displayName,formattedAddress,googleMapsUri,googleMapsLinks,rating,userRatingCount,nationalPhoneNumber,internationalPhoneNumber,photos,reviews,attributions");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var body = await LogGoogleErrorAsync(response, "Place Details", cancellationToken);
            throw new GooglePlacesApiException(response.StatusCode, "Place Details", body);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var photos = await ReadPhotosAsync(root, cancellationToken);

        return new GooglePlaceDetailsDto(
            PlaceId: ReadString(root, "id"),
            DisplayName: ReadLocalizedText(root, "displayName"),
            FormattedAddress: ReadString(root, "formattedAddress"),
            GoogleMapsUri: ReadString(root, "googleMapsUri"),
            Rating: ReadNullableDouble(root, "rating"),
            UserRatingCount: ReadNullableInt(root, "userRatingCount"),
            NationalPhoneNumber: ReadOptionalString(root, "nationalPhoneNumber"),
            InternationalPhoneNumber: ReadOptionalString(root, "internationalPhoneNumber"),
            GoogleMapsLinks: ReadGoogleMapsLinks(root),
            Photos: photos,
            Reviews: ReadReviews(root),
            Attributions: ReadAttributions(root, "attributions"),
            FetchedAtUtc: DateTime.UtcNow);
    }

    private async Task<IReadOnlyList<GooglePlacePhotoDto>> ReadPhotosAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("photos", out var photosElement) || photosElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<GooglePlacePhotoDto>();

        var photos = new List<GooglePlacePhotoDto>();

        foreach (var photo in photosElement.EnumerateArray().Take(MaxPhotos))
        {
            var name = ReadString(photo, "name");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            photos.Add(new GooglePlacePhotoDto(
                Name: name,
                PhotoUri: await GetPhotoUriAsync(name, cancellationToken),
                WidthPx: ReadNullableInt(photo, "widthPx"),
                HeightPx: ReadNullableInt(photo, "heightPx"),
                AuthorAttributions: ReadAttributions(photo, "authorAttributions")));
        }

        return photos;
    }

    private async Task<string?> GetPhotoUriAsync(string photoName, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{PlacesBaseUrl}/{photoName}/media?maxWidthPx={PhotoMaxWidthPx}&skipHttpRedirect=true");

        request.Headers.Add("X-Goog-Api-Key", ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await LogGoogleErrorAsync(response, "Place Photo", cancellationToken);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ReadOptionalString(document.RootElement, "photoUri");
    }

    private IReadOnlyList<GooglePlaceReviewDto> ReadReviews(JsonElement root)
    {
        if (!root.TryGetProperty("reviews", out var reviewsElement) || reviewsElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<GooglePlaceReviewDto>();

        return reviewsElement
            .EnumerateArray()
            .Take(MaxReviews)
            .Select(review =>
            {
                var author = review.TryGetProperty("authorAttribution", out var authorElement)
                    ? authorElement
                    : default;

                return new GooglePlaceReviewDto(
                    AuthorName: author.ValueKind == JsonValueKind.Object ? ReadOptionalString(author, "displayName") : null,
                    AuthorUri: author.ValueKind == JsonValueKind.Object ? ReadOptionalString(author, "uri") : null,
                    ProfilePhotoUri: author.ValueKind == JsonValueKind.Object ? ReadOptionalString(author, "photoUri") : null,
                    Rating: ReadNullableDouble(review, "rating"),
                    Text: ReadLocalizedText(review, "text"),
                    RelativePublishTimeDescription: ReadOptionalString(review, "relativePublishTimeDescription"),
                    PublishTime: ReadNullableDateTime(review, "publishTime"));
            })
            .ToList();
    }

    private IReadOnlyList<GoogleAttributionDto> ReadAttributions(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var attributionsElement) || attributionsElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<GoogleAttributionDto>();

        return attributionsElement
            .EnumerateArray()
            .Select(attribution => new GoogleAttributionDto(
                DisplayName: ReadOptionalString(attribution, "displayName"),
                Uri: ReadOptionalString(attribution, "uri"),
                PhotoUri: ReadOptionalString(attribution, "photoUri")))
            .ToList();
    }

    private static GoogleMapsLinksDto? ReadGoogleMapsLinks(JsonElement root)
    {
        if (!root.TryGetProperty("googleMapsLinks", out var links) || links.ValueKind != JsonValueKind.Object)
            return null;

        return new GoogleMapsLinksDto(
            DirectionsUri: ReadOptionalString(links, "directionsUri"),
            PlaceUri: ReadOptionalString(links, "placeUri"),
            WriteAReviewUri: ReadOptionalString(links, "writeAReviewUri"),
            ReviewsUri: ReadOptionalString(links, "reviewsUri"),
            PhotosUri: ReadOptionalString(links, "photosUri"));
    }

    private string BuildSearchQuery(Orduevi orduevi)
    {
        var parts = new[] { orduevi.Name, orduevi.Address, "Türkiye" }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim());

        return string.Join(' ', parts);
    }

    private int ReadBoundedInt(string key, int fallback, int min, int max)
    {
        var value = int.TryParse(_configuration[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

        return Math.Clamp(value, min, max);
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Google Maps API anahtarı tanımlı değil. GOOGLE_MAPS_API_KEY veya GoogleMaps:ApiKey kullanın.");
    }

    private async Task<string> LogGoogleErrorAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "Google Places {Operation} failed with {StatusCode}: {Body}",
            operation,
            (int)response.StatusCode,
            body);
        return body;
    }

    private static string ReadLocalizedText(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
            return string.Empty;

        return element.ValueKind switch
        {
            JsonValueKind.Object when element.TryGetProperty("text", out var text) => text.GetString() ?? string.Empty,
            JsonValueKind.String => element.GetString() ?? string.Empty,
            _ => string.Empty
        };
    }

    private static string ReadString(JsonElement root, string propertyName) =>
        ReadOptionalString(root, propertyName) ?? string.Empty;

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    private static int? ReadNullableInt(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var element) && element.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static double? ReadNullableDouble(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var element) && element.TryGetDouble(out var value)
            ? value
            : null;
    }

    private static DateTime? ReadNullableDateTime(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var element)
               && element.ValueKind == JsonValueKind.String
               && DateTime.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var value)
            ? value
            : null;
    }
}
