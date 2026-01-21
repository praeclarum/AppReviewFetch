using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppReviewFetch.Exceptions;
using AppReviewFetch.Models.AppStoreConnect;
using Microsoft.IdentityModel.Tokens;

namespace AppReviewFetch;

/// <summary>
/// Service for fetching customer reviews from the App Store Connect API.
/// Documentation: https://developer.apple.com/documentation/appstoreconnectapi/customer-reviews
/// </summary>
public class AppStoreConnectService : IAppReviewService
{
    private const string BaseUrl = "https://api.appstoreconnect.apple.com";
    private const string ApiVersion = "v1";
    private const int TokenExpirationMinutes = 20; // Apple recommends max 20 minutes

    private readonly HttpClient _httpClient;
    private readonly AppStoreConnectCredentials _credentials;
    private string? _cachedToken;
    private DateTime _tokenExpiration = DateTime.MinValue;

    public AppStoreConnectService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _credentials = LoadCredentials();
    }

    public AppStoreConnectService() : this(new HttpClient())
    {
    }

    /// <summary>
    /// Fetches a page of reviews for a specified application.
    /// </summary>
    public async Task<ReviewPageResponse> GetReviewsAsync(
        string appId,
        ReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentException("App ID cannot be null or empty", nameof(appId));
        }

        ArgumentNullException.ThrowIfNull(request);

        // Build the API URL with query parameters
        var url = BuildReviewsUrl(appId, request);

        // Get or refresh JWT token
        var token = GetOrRefreshToken();

        // Create HTTP request
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Add("Authorization", $"Bearer {token}");
        httpRequest.Headers.Add("Accept", "application/json");

        // Execute request
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        // Handle response
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiErrorAsync(response, content);
        }

        // Parse and transform response
        var apiResponse = JsonSerializer.Deserialize<CustomerReviewsResponse>(content);
        if (apiResponse == null)
        {
            throw new AppReviewFetchException("Failed to deserialize API response");
        }

        return TransformResponse(apiResponse);
    }

    /// <summary>
    /// Builds the URL for the reviews API endpoint with query parameters.
    /// </summary>
    private string BuildReviewsUrl(string appId, ReviewRequest request)
    {
        var queryParams = new List<string>();

        // Filter by app
        queryParams.Add($"filter[app]={Uri.EscapeDataString(appId)}");

        // Include developer responses
        queryParams.Add("include=response");

        // Sort order
        var sortParam = request.SortOrder switch
        {
            ReviewSortOrder.NewestFirst => "-createdDate",
            ReviewSortOrder.OldestFirst => "createdDate",
            ReviewSortOrder.HighestRatingFirst => "-rating",
            ReviewSortOrder.LowestRatingFirst => "rating",
            ReviewSortOrder.MostHelpful => "-rating,createdDate", // Approximation
            _ => "-createdDate"
        };
        queryParams.Add($"sort={sortParam}");

        // Platform filter (if applicable)
        if (!string.IsNullOrWhiteSpace(request.Platform))
        {
            // Note: App Store Connect API doesn't have a direct platform filter for reviews
            // Reviews are associated with apps, and apps are already platform-specific
        }

        // Territory/Country filter
        if (!string.IsNullOrWhiteSpace(request.Country))
        {
            queryParams.Add($"filter[territory]={Uri.EscapeDataString(request.Country)}");
        }

        // Pagination
        if (request.Limit > 0)
        {
            queryParams.Add($"limit={request.Limit}");
        }

        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            queryParams.Add($"cursor={Uri.EscapeDataString(request.Cursor)}");
        }

        var query = string.Join("&", queryParams);
        return $"{BaseUrl}/{ApiVersion}/customerReviews?{query}";
    }

    /// <summary>
    /// Generates or returns a cached JWT token for App Store Connect API authentication.
    /// </summary>
    private string GetOrRefreshToken()
    {
        // Check if we have a valid cached token
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiration)
        {
            return _cachedToken;
        }

        // Generate new token
        try
        {
            var now = DateTime.UtcNow;
            var expiration = now.AddMinutes(TokenExpirationMinutes);

            // Load private key
            using var ecdsa = LoadPrivateKey(_credentials.PrivateKey);

            var signingCredentials = new SigningCredentials(
                new ECDsaSecurityKey(ecdsa),
                SecurityAlgorithms.EcdsaSha256
            );

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _credentials.IssuerId,
                Audience = "appstoreconnect-v1",
                IssuedAt = now,
                Expires = expiration,
                SigningCredentials = signingCredentials
            };

            // Add Key ID to header
            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateJwtSecurityToken(tokenDescriptor);
            token.Header["kid"] = _credentials.KeyId;
            token.Header["typ"] = "JWT";

            _cachedToken = handler.WriteToken(token);
            _tokenExpiration = expiration.AddMinutes(-1); // Refresh 1 minute early

            return _cachedToken;
        }
        catch (Exception ex)
        {
            throw new AuthenticationException("Failed to generate JWT token", ex);
        }
    }

    /// <summary>
    /// Loads the ECDSA private key from the P8 key content.
    /// </summary>
    private ECDsa LoadPrivateKey(string privateKeyContent)
    {
        try
        {
            // Remove PEM headers/footers and whitespace
            var keyData = privateKeyContent
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();

            var keyBytes = Convert.FromBase64String(keyData);

            var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(keyBytes, out _);

            return ecdsa;
        }
        catch (Exception ex)
        {
            throw new AuthenticationException("Failed to load private key", ex);
        }
    }

    /// <summary>
    /// Loads credentials from the configuration file.
    /// </summary>
    private AppStoreConnectCredentials LoadCredentials()
    {
        try
        {
            // Use platform-appropriate config directory
            // Windows: %LOCALAPPDATA%\AppReviewFetch
            // macOS/Linux: ~/.config/AppReviewFetch
            var configDir = OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            
            var credentialsPath = Path.Combine(configDir, "AppReviewFetch", "Credentials.json");

            if (!File.Exists(credentialsPath))
            {
                throw new CredentialsException(
                    $"Credentials file not found at: {credentialsPath}. " +
                    "Please create the file with your App Store Connect API credentials.");
            }

            var json = File.ReadAllText(credentialsPath);
            var rootCredentials = JsonSerializer.Deserialize<Credentials>(json);

            if (rootCredentials?.AppStoreConnect == null)
            {
                throw new CredentialsException(
                    "App Store Connect credentials not found in credentials file. " +
                    "Please ensure the file contains an 'appStoreConnect' property.");
            }

            var credentials = rootCredentials.AppStoreConnect;

            // Validate required fields
            if (string.IsNullOrWhiteSpace(credentials.KeyId) ||
                string.IsNullOrWhiteSpace(credentials.IssuerId) ||
                string.IsNullOrWhiteSpace(credentials.PrivateKey))
            {
                throw new CredentialsException(
                    "Credentials file is missing required fields (keyId, issuerId, or privateKey)");
            }

            return credentials;
        }
        catch (CredentialsException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CredentialsException("Failed to load credentials", ex);
        }
    }

    /// <summary>
    /// Transforms the App Store Connect API response into our standard format.
    /// </summary>
    private ReviewPageResponse TransformResponse(CustomerReviewsResponse apiResponse)
    {
        var reviews = new List<AppReview>();

        // Build a dictionary of included responses for quick lookup
        var responsesById = new Dictionary<string, IncludedResource>();
        if (apiResponse.Included != null)
        {
            foreach (var included in apiResponse.Included)
            {
                if (included.Type == "customerReviewResponses")
                {
                    responsesById[included.Id] = included;
                }
            }
        }

        // Transform each review
        foreach (var reviewData in apiResponse.Data)
        {
            var review = new AppReview
            {
                Id = reviewData.Id,
                Rating = reviewData.Attributes.Rating,
                Title = reviewData.Attributes.Title,
                Body = reviewData.Attributes.Body,
                ReviewerNickname = reviewData.Attributes.ReviewerNickname,
                CreatedDate = reviewData.Attributes.CreatedDate,
                Territory = reviewData.Attributes.Territory
            };

            // Check if there's a developer response
            if (reviewData.Relationships?.Response?.Data != null)
            {
                var responseId = reviewData.Relationships.Response.Data.Id;
                if (responsesById.TryGetValue(responseId, out var responseResource) &&
                    responseResource.Attributes != null)
                {
                    review.DeveloperResponse = new ReviewResponse
                    {
                        Id = responseResource.Id,
                        Body = responseResource.Attributes.ResponseBody,
                        CreatedDate = responseResource.Attributes.LastModifiedDate ?? DateTimeOffset.UtcNow,
                        ModifiedDate = responseResource.Attributes.LastModifiedDate,
                        State = responseResource.Attributes.State
                    };
                }
            }

            reviews.Add(review);
        }

        // Extract pagination metadata
        var pagination = new PaginationMetadata
        {
            NextCursor = ExtractCursorFromUrl(apiResponse.Links.Next),
            PreviousCursor = ExtractCursorFromUrl(apiResponse.Links.Previous),
            HasMorePages = !string.IsNullOrEmpty(apiResponse.Links.Next),
            TotalCount = apiResponse.Meta?.Paging?.Total
        };

        return new ReviewPageResponse
        {
            Reviews = reviews,
            Pagination = pagination
        };
    }

    /// <summary>
    /// Extracts the cursor parameter from a URL.
    /// </summary>
    private string? ExtractCursorFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var uri = new Uri(url);
        var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return queryParams["cursor"];
    }

    /// <summary>
    /// Throws an appropriate exception based on the API error response.
    /// </summary>
    private async Task ThrowApiErrorAsync(HttpResponseMessage response, string content)
    {
        try
        {
            var errorResponse = JsonSerializer.Deserialize<AppStoreConnectErrorResponse>(content);
            if (errorResponse?.Errors != null && errorResponse.Errors.Count > 0)
            {
                var firstError = errorResponse.Errors[0];
                var statusCode = int.TryParse(firstError.Status, out var code) ? code : (int)response.StatusCode;

                throw new ApiErrorException(
                    statusCode,
                    firstError.Code,
                    firstError.Title,
                    firstError.Detail
                );
            }
        }
        catch (ApiErrorException)
        {
            throw;
        }
        catch
        {
            // If we can't parse the error, throw a generic error
        }

        throw new ApiErrorException(
            (int)response.StatusCode,
            $"Request failed with status {response.StatusCode}: {content}"
        );
    }

    /// <summary>
    /// Lists all apps accessible with the current credentials.
    /// </summary>
    public async Task<AppListResponse> GetAppsAsync(CancellationToken cancellationToken = default)
    {
        // Build the API URL with appStoreVersions included to get platform information
        var url = $"{BaseUrl}/{ApiVersion}/apps?limit=200&include=appStoreVersions";

        // Get or refresh JWT token
        var token = GetOrRefreshToken();

        // Create HTTP request
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Add("Authorization", $"Bearer {token}");
        httpRequest.Headers.Add("Accept", "application/json");

        // Execute request
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        // Handle response
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiErrorAsync(response, content);
        }

        // Parse and transform response
        var apiResponse = JsonSerializer.Deserialize<AppsResponse>(content);
        if (apiResponse == null)
        {
            throw new AppReviewFetchException("Failed to deserialize API response");
        }

        return TransformAppsResponse(apiResponse);
    }

    /// <summary>
    /// Transforms the App Store Connect apps response into our standard format.
    /// </summary>
    private AppListResponse TransformAppsResponse(AppsResponse apiResponse)
    {
        var apps = new List<AppInfo>();

        // Build a dictionary of app store versions by app ID for quick lookup
        var versionsByAppId = new Dictionary<string, List<string>>();
        
        if (apiResponse.Included != null)
        {
            foreach (var included in apiResponse.Included)
            {
                if (included.Type == "appStoreVersions" && included.Attributes?.Platform != null)
                {
                    // We need to find which app this version belongs to by checking relationships
                    // For now, we'll collect all versions and match them later
                }
            }
        }

        foreach (var appData in apiResponse.Data)
        {
            var platforms = new List<string>();
            
            // Extract platform information from relationships if available
            if (appData.Relationships?.AppStoreVersions?.Data != null)
            {
                foreach (var versionRef in appData.Relationships.AppStoreVersions.Data)
                {
                    // Find the matching included version
                    var version = apiResponse.Included?.FirstOrDefault(i => 
                        i.Id == versionRef.Id && i.Type == "appStoreVersions");
                    
                    if (version?.Attributes?.Platform != null)
                    {
                        var platform = ConvertPlatformName(version.Attributes.Platform);
                        if (!platforms.Contains(platform))
                        {
                            platforms.Add(platform);
                        }
                    }
                }
            }

            var app = new AppInfo
            {
                Id = appData.Id,
                Name = appData.Attributes.Name,
                BundleId = appData.Attributes.BundleId,
                Sku = appData.Attributes.Sku,
                Store = "App Store",
                PrimaryLocale = appData.Attributes.PrimaryLocale,
                IsAvailable = appData.Attributes.AvailableInNewTerritories,
                Platforms = platforms
            };

            apps.Add(app);
        }

        return new AppListResponse
        {
            Apps = apps
        };
    }

    /// <summary>
    /// Converts Apple API platform names to user-friendly names.
    /// </summary>
    private static string ConvertPlatformName(string apiPlatform)
    {
        return apiPlatform switch
        {
            "IOS" => "iOS",
            "MAC_OS" => "macOS",
            "TV_OS" => "tvOS",
            "VISION_OS" => "visionOS",
            _ => apiPlatform
        };
    }
}
