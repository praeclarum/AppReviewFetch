using System.Text.Json;
using AppReviewFetch.Exceptions;

namespace AppReviewFetch;

/// <summary>
/// Omni service that aggregates results from multiple app store review services.
/// Supports Apple App Store Connect and Google Play Developer APIs.
/// Uses concurrent requests to improve performance when accessing multiple stores.
/// Integrates with AppDatabase to cache apps and store metadata.
/// </summary>
public class OmniService : IAppReviewService
{
    private readonly AppStoreConnectService? _appStoreService;
    private readonly GooglePlayService? _googlePlayService;
    private readonly bool _hasAppStore;
    private readonly bool _hasGooglePlay;
    private readonly AppDatabase _appDatabase;

    public OmniService(HttpClient httpClient, AppDatabase? appDatabase = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _appDatabase = appDatabase ?? new AppDatabase(AppDatabase.GetDefaultDatabasePath());

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

    public OmniService() : this(new HttpClient(), null)
    {
    }

    /// <summary>
    /// Fetches reviews for a specified application.
    /// The appQuery can be:
    /// - An App Store Connect app ID (numeric)
    /// - A Google Play package name (e.g., com.example.app)
    /// - A bundle ID from the database
    /// - An app name from the database
    /// The service will first try to resolve the query using the database,
    /// then fall back to format-based detection.
    /// </summary>
    public async Task<ReviewPageResponse> GetReviewsAsync(
        string appQuery,
        ReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appQuery))
        {
            throw new ArgumentException("App query cannot be null or empty", nameof(appQuery));
        }

        ArgumentNullException.ThrowIfNull(request);

        // Load database and try to resolve the query
        await _appDatabase.LoadAsync(cancellationToken);
        var resolved = _appDatabase.ResolveAppForApi(appQuery, includeHidden: true);

        if (resolved.HasValue)
        {
            // Found in database - use the correct API identifier and route to the right service
            var (apiIdentifier, store, app) = resolved.Value;
            
            if (store.Equals("App Store", StringComparison.OrdinalIgnoreCase) && _hasAppStore)
            {
                return await _appStoreService!.GetReviewsAsync(apiIdentifier, request, cancellationToken);
            }
            else if (store.Equals("Google Play", StringComparison.OrdinalIgnoreCase) && _hasGooglePlay)
            {
                return await _googlePlayService!.GetReviewsAsync(apiIdentifier, request, cancellationToken);
            }
            else
            {
                throw new AppReviewFetchException(
                    $"App '{app.Name}' is from {store}, but credentials are not configured for that store.");
            }
        }

        // Not in database - fall back to format-based detection
        var isNumeric = appQuery.All(char.IsDigit);
        var isPackageName = appQuery.Contains('.');

        if (isNumeric && _hasAppStore)
        {
            return await _appStoreService!.GetReviewsAsync(appQuery, request, cancellationToken);
        }
        else if (isPackageName && _hasGooglePlay)
        {
            return await _googlePlayService!.GetReviewsAsync(appQuery, request, cancellationToken);
        }
        else if (_hasAppStore)
        {
            // Fallback to App Store if available
            return await _appStoreService!.GetReviewsAsync(appQuery, request, cancellationToken);
        }
        else if (_hasGooglePlay)
        {
            // Fallback to Google Play if available
            return await _googlePlayService!.GetReviewsAsync(appQuery, request, cancellationToken);
        }

        throw new AppReviewFetchException(
            $"Unable to determine which service to use for app query: {appQuery}. " +
            $"Try running 'list' first to populate the database, or use 'add-app' to add it manually.");
    }

    /// <summary>
    /// Lists all apps accessible across all configured services.
    /// Executes requests concurrently for improved performance.
    /// Apps from different stores are distinguished by their Store property.
    /// Results are merged with the local database and saved.
    /// </summary>
    public async Task<AppListResponse> GetAppsAsync(CancellationToken cancellationToken = default)
    {
        // Load database first
        await _appDatabase.LoadAsync(cancellationToken);

        var tasks = new List<Task<AppListResponse>>();
        var storeNames = new List<string>();

        // Start concurrent requests for each available service
        if (_hasAppStore)
        {
            tasks.Add(GetAppsWithFallbackAsync(_appStoreService!, "App Store", cancellationToken));
            storeNames.Add("App Store");
        }

        if (_hasGooglePlay)
        {
            tasks.Add(GetAppsWithFallbackAsync(_googlePlayService!, "Google Play", cancellationToken));
            storeNames.Add("Google Play");
        }

        // Wait for all tasks to complete
        var results = await Task.WhenAll(tasks);

        // Merge API results with database
        var allWarnings = new List<string>();
        for (int i = 0; i < results.Length; i++)
        {
            var result = results[i];
            var storeName = storeNames[i];
            
            if (result.Apps.Count > 0)
            {
                _appDatabase.MergeApps(result.Apps, storeName);
            }
            
            allWarnings.AddRange(result.Warnings);
        }

        // Save updated database
        try
        {
            await _appDatabase.SaveAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            allWarnings.Add($"Warning: Failed to save app database: {ex.Message}");
        }

        // Return all apps from database (this includes manually added apps)
        var allApps = _appDatabase.GetAllApps(includeHidden: true);

        return new AppListResponse
        {
            Apps = allApps,
            Warnings = allWarnings
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
            // Return error as warning - don't let one service failure break everything
            return new AppListResponse 
            { 
                Apps = new List<AppInfo>(),
                Warnings = new List<string> { $"{storeName}: Error - {ex.Message}" }
            };
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

    /// <summary>
    /// Gets the app database for direct access (e.g., for CLI commands).
    /// </summary>
    public AppDatabase GetAppDatabase() => _appDatabase;
}
