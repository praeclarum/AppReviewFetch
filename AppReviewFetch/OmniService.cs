using System.Text.Json;
using AppReviewFetch.Exceptions;

namespace AppReviewFetch;

/// <summary>
/// Omni service that aggregates results from multiple app store review services.
/// Supports Apple App Store Connect and Google Play Developer APIs.
/// Uses concurrent requests to improve performance when accessing multiple stores.
/// </summary>
public class OmniService : IAppReviewService
{
    private readonly AppStoreConnectService? _appStoreService;
    private readonly GooglePlayService? _googlePlayService;
    private readonly bool _hasAppStore;
    private readonly bool _hasGooglePlay;

    public OmniService(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        // Try to initialize each service - they will throw if credentials are missing
        try
        {
            _appStoreService = new AppStoreConnectService(httpClient);
            _hasAppStore = true;
        }
        catch (CredentialsException)
        {
            _hasAppStore = false;
        }

        try
        {
            _googlePlayService = new GooglePlayService(httpClient);
            _hasGooglePlay = true;
        }
        catch (CredentialsException)
        {
            _hasGooglePlay = false;
        }

        // Require at least one service to be available
        if (!_hasAppStore && !_hasGooglePlay)
        {
            throw new CredentialsException(
                "No valid credentials found for any supported app store. " +
                "Please configure credentials for App Store Connect or Google Play.");
        }
    }

    public OmniService() : this(new HttpClient())
    {
    }

    /// <summary>
    /// Fetches reviews for a specified application.
    /// The appId can be:
    /// - An App Store Connect app ID (numeric)
    /// - A Google Play package name (e.g., com.example.app)
    /// The service will attempt to determine which store to query based on the format.
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

        // Determine which service to use based on appId format
        // If it's numeric, assume App Store; if it contains dots, assume Google Play
        var isNumeric = appId.All(char.IsDigit);
        var isPackageName = appId.Contains('.');

        if (isNumeric && _hasAppStore)
        {
            return await _appStoreService!.GetReviewsAsync(appId, request, cancellationToken);
        }
        else if (isPackageName && _hasGooglePlay)
        {
            return await _googlePlayService!.GetReviewsAsync(appId, request, cancellationToken);
        }
        else if (_hasAppStore)
        {
            // Fallback to App Store if available
            return await _appStoreService!.GetReviewsAsync(appId, request, cancellationToken);
        }
        else if (_hasGooglePlay)
        {
            // Fallback to Google Play if available
            return await _googlePlayService!.GetReviewsAsync(appId, request, cancellationToken);
        }

        throw new AppReviewFetchException(
            $"Unable to determine which service to use for app ID: {appId}");
    }

    /// <summary>
    /// Lists all apps accessible across all configured services.
    /// Executes requests concurrently for improved performance.
    /// Apps from different stores are distinguished by their Store property.
    /// </summary>
    public async Task<AppListResponse> GetAppsAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<AppListResponse>>();

        // Start concurrent requests for each available service
        if (_hasAppStore)
        {
            tasks.Add(GetAppsWithFallbackAsync(_appStoreService!, "App Store", cancellationToken));
        }

        if (_hasGooglePlay)
        {
            tasks.Add(GetAppsWithFallbackAsync(_googlePlayService!, "Google Play", cancellationToken));
        }

        // Wait for all tasks to complete
        var results = await Task.WhenAll(tasks);

        // Combine results from all services
        var allApps = new List<AppInfo>();
        foreach (var result in results)
        {
            allApps.AddRange(result.Apps);
        }

        return new AppListResponse
        {
            Apps = allApps
        };
    }

    /// <summary>
    /// Gets apps from a service with error handling.
    /// Returns empty list if the service fails to avoid breaking the entire operation.
    /// </summary>
    private async Task<AppListResponse> GetAppsWithFallbackAsync(
        IAppReviewService service,
        string storeName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await service.GetAppsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log the error but continue - don't let one service failure break everything
            Console.Error.WriteLine($"Warning: Failed to fetch apps from {storeName}: {ex.Message}");
            return new AppListResponse { Apps = new List<AppInfo>() };
        }
    }

    /// <summary>
    /// Checks if credentials are configured for a specific store.
    /// </summary>
    public bool HasCredentials(string store)
    {
        return store.ToLowerInvariant() switch
        {
            "appstore" or "app store" or "apple" => _hasAppStore,
            "googleplay" or "google play" or "android" => _hasGooglePlay,
            _ => false
        };
    }

    /// <summary>
    /// Gets a list of stores that have valid credentials configured.
    /// </summary>
    public List<string> GetAvailableStores()
    {
        var stores = new List<string>();
        
        if (_hasAppStore)
        {
            stores.Add("App Store");
        }
        
        if (_hasGooglePlay)
        {
            stores.Add("Google Play");
        }
        
        return stores;
    }
}
