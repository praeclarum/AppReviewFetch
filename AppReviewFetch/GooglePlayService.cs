using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppReviewFetch.Exceptions;
using AppReviewFetch.Models.GooglePlay;
using Microsoft.IdentityModel.Tokens;

namespace AppReviewFetch;

/// <summary>
/// Service for fetching app reviews from the Google Play Developer API.
/// Documentation: https://developers.google.com/android-publisher/api-ref/rest/v3/reviews
/// </summary>
public class GooglePlayService : IAppReviewService
{
    private const string BaseUrl = "https://androidpublisher.googleapis.com/androidpublisher/v3";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const int TokenExpirationMinutes = 60; // Google tokens are valid for 1 hour

    private readonly HttpClient _httpClient;
    private readonly GooglePlayCredentials _credentials;
    private readonly ServiceAccountInfo _serviceAccountInfo;
    private string? _cachedToken;
    private DateTime _tokenExpiration = DateTime.MinValue;

    public GooglePlayService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _credentials = LoadCredentials();
        _serviceAccountInfo = ParseServiceAccountJson(_credentials.ServiceAccountJson);
    }

    public GooglePlayService() : this(new HttpClient())
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
            throw new ArgumentException("Package name cannot be null or empty", nameof(appId));
        }

        ArgumentNullException.ThrowIfNull(request);

        // Build the API URL with query parameters
        var url = BuildReviewsUrl(appId, request);

        // Get or refresh access token
        var token = await GetOrRefreshTokenAsync(cancellationToken);

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
        var apiResponse = JsonSerializer.Deserialize<ReviewsResponse>(content);
        if (apiResponse == null)
        {
            throw new AppReviewFetchException("Failed to deserialize API response");
        }

        return TransformResponse(apiResponse);
    }

    /// <summary>
    /// Lists all apps accessible to this service.
    /// Note: Google Play API doesn't have a direct "list apps" endpoint.
    /// This method attempts to discover apps by trying to access them.
    /// For now, it returns an empty list with a note in the documentation.
    /// Users should use package names directly.
    /// </summary>
    public async Task<AppListResponse> GetAppsAsync(CancellationToken cancellationToken = default)
    {
        // Google Play API doesn't provide a list apps endpoint
        // The API requires knowing the package name upfront
        // We could potentially use the Google Play Console API or other methods,
        // but for now we return an empty list
        // 
        // Alternative: We could maintain a configuration file with known package names
        
        return await Task.FromResult(new AppListResponse
        {
            Apps = new List<AppInfo>()
        });
    }

    /// <summary>
    /// Builds the URL for the reviews API endpoint with query parameters.
    /// </summary>
    private string BuildReviewsUrl(string packageName, ReviewRequest request)
    {
        var queryParams = new List<string>();

        // Maximum results per page (Google Play max is 100, but we'll default to 50)
        var maxResults = request.Limit > 0 ? Math.Min(request.Limit, 100) : 50;
        queryParams.Add($"maxResults={maxResults}");

        // Pagination token
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            queryParams.Add($"token={Uri.EscapeDataString(request.Cursor)}");
        }

        // Translate sort order - Google Play doesn't support all sort orders
        // We'll handle this client-side if needed
        // Google Play API doesn't have built-in sorting beyond the default

        var query = string.Join("&", queryParams);
        return $"{BaseUrl}/applications/{Uri.EscapeDataString(packageName)}/reviews?{query}";
    }

    /// <summary>
    /// Generates or returns a cached OAuth 2.0 access token for Google Play API authentication.
    /// </summary>
    private async Task<string> GetOrRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        // Check if we have a valid cached token
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiration)
        {
            return _cachedToken;
        }

        // Generate new token using JWT bearer assertion
        try
        {
            var now = DateTime.UtcNow;
            var expiration = now.AddMinutes(TokenExpirationMinutes);

            // Create JWT assertion
            var assertion = CreateJwtAssertion(now, expiration);

            // Exchange JWT for access token
            var formData = new Dictionary<string, string>
            {
                { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                { "assertion", assertion }
            };

            using var content = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new AuthenticationException(
                    $"Failed to obtain access token: {response.StatusCode} - {responseContent}");
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);
            if (tokenResponse?.AccessToken == null)
            {
                throw new AuthenticationException("Invalid token response");
            }

            _cachedToken = tokenResponse.AccessToken;
            // Refresh 5 minutes early to be safe
            _tokenExpiration = now.AddSeconds(tokenResponse.ExpiresIn - 300);

            return _cachedToken;
        }
        catch (AuthenticationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AuthenticationException("Failed to generate access token", ex);
        }
    }

    /// <summary>
    /// Creates a JWT assertion for Google OAuth 2.0 service account authentication.
    /// </summary>
    private string CreateJwtAssertion(DateTime now, DateTime expiration)
    {
        try
        {
            // Load private key
            var rsa = LoadPrivateKey(_serviceAccountInfo.PrivateKey);

            var signingCredentials = new SigningCredentials(
                new RsaSecurityKey(rsa),
                SecurityAlgorithms.RsaSha256
            );

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Iss, _serviceAccountInfo.ClientEmail),
                new Claim(JwtRegisteredClaimNames.Sub, _serviceAccountInfo.ClientEmail),
                new Claim(JwtRegisteredClaimNames.Aud, TokenUrl),
                new Claim("scope", "https://www.googleapis.com/auth/androidpublisher")
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                IssuedAt = now,
                Expires = expiration,
                SigningCredentials = signingCredentials
            };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateJwtSecurityToken(tokenDescriptor);

            return handler.WriteToken(token);
        }
        catch (Exception ex)
        {
            throw new AuthenticationException("Failed to create JWT assertion", ex);
        }
    }

    /// <summary>
    /// Loads the RSA private key from the service account JSON.
    /// </summary>
    private RSA LoadPrivateKey(string privateKeyPem)
    {
        try
        {
            // Remove PEM headers/footers and whitespace
            var keyData = privateKeyPem
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Trim();

            var keyBytes = Convert.FromBase64String(keyData);

            var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(keyBytes, out _);

            return rsa;
        }
        catch (Exception ex)
        {
            throw new AuthenticationException("Failed to load private key", ex);
        }
    }

    /// <summary>
    /// Loads credentials from the configuration file.
    /// </summary>
    private GooglePlayCredentials LoadCredentials()
    {
        try
        {
            // Use platform-appropriate config directory
            var configDir = OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            
            var credentialsPath = Path.Combine(configDir, "AppReviewFetch", "Credentials.json");

            if (!File.Exists(credentialsPath))
            {
                throw new CredentialsException(
                    $"Credentials file not found at: {credentialsPath}. " +
                    "Please create the file with your Google Play API credentials.");
            }

            var json = File.ReadAllText(credentialsPath);
            var rootCredentials = JsonSerializer.Deserialize<Credentials>(json);

            if (rootCredentials?.GooglePlay == null)
            {
                throw new CredentialsException(
                    "Google Play credentials not found in credentials file. " +
                    "Please ensure the file contains a 'googlePlay' property.");
            }

            var credentials = rootCredentials.GooglePlay;

            // Validate required fields
            if (string.IsNullOrWhiteSpace(credentials.ServiceAccountJson))
            {
                throw new CredentialsException(
                    "Credentials file is missing the serviceAccountJson field");
            }

            return credentials;
        }
        catch (CredentialsException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CredentialsException("Failed to load Google Play credentials", ex);
        }
    }

    /// <summary>
    /// Parses the service account JSON to extract required fields.
    /// </summary>
    private ServiceAccountInfo ParseServiceAccountJson(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new ServiceAccountInfo
            {
                ClientEmail = root.GetProperty("client_email").GetString() ?? 
                    throw new CredentialsException("client_email not found in service account JSON"),
                PrivateKey = root.GetProperty("private_key").GetString() ?? 
                    throw new CredentialsException("private_key not found in service account JSON"),
                ProjectId = root.GetProperty("project_id").GetString()
            };
        }
        catch (CredentialsException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CredentialsException("Failed to parse service account JSON", ex);
        }
    }

    /// <summary>
    /// Transforms the Google Play API response into our standard format.
    /// </summary>
    private ReviewPageResponse TransformResponse(ReviewsResponse apiResponse)
    {
        var reviews = new List<AppReview>();

        foreach (var reviewData in apiResponse.Reviews)
        {
            // Get the most recent user comment
            var userComment = reviewData.Comments
                .Where(c => c.UserComment != null)
                .OrderByDescending(c => c.UserComment?.LastModified?.Seconds ?? 0)
                .Select(c => c.UserComment)
                .FirstOrDefault();

            if (userComment == null) continue;

            // Get the most recent developer comment (if any)
            var developerComment = reviewData.Comments
                .Where(c => c.DeveloperComment != null)
                .OrderByDescending(c => c.DeveloperComment?.LastModified?.Seconds ?? 0)
                .Select(c => c.DeveloperComment)
                .FirstOrDefault();

            var review = new AppReview
            {
                Id = reviewData.ReviewId,
                Rating = userComment.StarRating,
                Title = null, // Google Play reviews don't have titles
                Body = userComment.Text,
                ReviewerNickname = reviewData.AuthorName,
                CreatedDate = userComment.LastModified?.ToDateTimeOffset() ?? DateTimeOffset.UtcNow,
                Territory = userComment.ReviewerLanguage // Using language as territory proxy
            };

            // Add developer response if present
            if (developerComment != null)
            {
                review.DeveloperResponse = new ReviewResponse
                {
                    Id = $"{reviewData.ReviewId}_response",
                    Body = developerComment.Text,
                    CreatedDate = developerComment.LastModified?.ToDateTimeOffset() ?? DateTimeOffset.UtcNow,
                    ModifiedDate = developerComment.LastModified?.ToDateTimeOffset()
                };
            }

            reviews.Add(review);
        }

        // Extract pagination metadata
        var pagination = new PaginationMetadata
        {
            NextCursor = apiResponse.TokenPagination?.NextPageToken,
            PreviousCursor = apiResponse.TokenPagination?.PreviousPageToken,
            HasMorePages = !string.IsNullOrEmpty(apiResponse.TokenPagination?.NextPageToken)
        };

        return new ReviewPageResponse
        {
            Reviews = reviews,
            Pagination = pagination
        };
    }

    /// <summary>
    /// Throws an appropriate exception based on the API error response.
    /// </summary>
    private async Task ThrowApiErrorAsync(HttpResponseMessage response, string content)
    {
        try
        {
            var errorDoc = JsonDocument.Parse(content);
            if (errorDoc.RootElement.TryGetProperty("error", out var errorElement))
            {
                var message = errorElement.TryGetProperty("message", out var msgElement)
                    ? msgElement.GetString()
                    : "Unknown error";

                var code = errorElement.TryGetProperty("code", out var codeElement)
                    ? codeElement.GetInt32()
                    : (int)response.StatusCode;

                throw new ApiErrorException(code, message ?? "Unknown error");
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
    /// Internal class to hold parsed service account information.
    /// </summary>
    private class ServiceAccountInfo
    {
        public string ClientEmail { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
    }

    /// <summary>
    /// Token response from Google OAuth 2.0.
    /// </summary>
    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }
}
