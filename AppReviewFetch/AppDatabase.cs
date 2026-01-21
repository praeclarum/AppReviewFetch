using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppReviewFetch;

/// <summary>
/// Manages a local database of apps, caching API results and storing additional metadata.
/// </summary>
public class AppDatabase
{
    private readonly string _databasePath;
    private Dictionary<string, AppInfo> _apps = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AppDatabase(string databasePath)
    {
        _databasePath = databasePath;
    }

    /// <summary>
    /// Gets the path to the default database file in the config directory.
    /// </summary>
    public static string GetDefaultDatabasePath()
    {
        var configDir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        
        return Path.Combine(configDir, "AppReviewFetch", "Apps.json");
    }

    /// <summary>
    /// Loads the database from disk. Creates a new empty database if the file doesn't exist.
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_databasePath))
        {
            _apps = new Dictionary<string, AppInfo>();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_databasePath, cancellationToken);
            var appList = JsonSerializer.Deserialize<List<AppInfo>>(json, _jsonOptions);
            _apps = appList?.ToDictionary(a => GetAppKey(a.Store, a.BundleId ?? a.Id), StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, AppInfo>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load app database from {_databasePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves the database to disk safely using atomic write (temp file + move).
    /// </summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to temp file first
        var tempPath = _databasePath + ".tmp";
        try
        {
            var appList = _apps.Values.OrderBy(a => a.Store).ThenBy(a => a.Name).ToList();
            var json = JsonSerializer.Serialize(appList, _jsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);

            // Atomic move (on most platforms, this replaces the target file)
            File.Move(tempPath, _databasePath, overwrite: true);
        }
        catch
        {
            // Clean up temp file if it exists
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
            }
            throw;
        }
    }

    /// <summary>
    /// Merges apps from an API response with the database.
    /// Updates API-provided fields but preserves metadata fields.
    /// </summary>
    public void MergeApps(IEnumerable<AppInfo> apiApps, string storeName)
    {
        foreach (var apiApp in apiApps)
        {
            var key = GetAppKey(storeName, apiApp.BundleId ?? apiApp.Id);
            
            if (_apps.TryGetValue(key, out var existingApp))
            {
                // Preserve metadata
                var projectUrl = existingApp.ProjectUrl;
                var isHidden = existingApp.IsHidden;
                var notes = existingApp.Notes;

                // Update with API data
                existingApp.Id = apiApp.Id;
                existingApp.Name = apiApp.Name;
                existingApp.BundleId = apiApp.BundleId;
                existingApp.Sku = apiApp.Sku;
                existingApp.Platforms = apiApp.Platforms;
                existingApp.Store = storeName;
                existingApp.PrimaryLocale = apiApp.PrimaryLocale;
                existingApp.IsAvailable = apiApp.IsAvailable;
                existingApp.CurrentVersion = apiApp.CurrentVersion;

                // Restore metadata
                existingApp.ProjectUrl = projectUrl;
                existingApp.IsHidden = isHidden;
                existingApp.Notes = notes;
            }
            else
            {
                // New app from API
                apiApp.Store = storeName;
                _apps[key] = apiApp;
            }
        }
    }

    /// <summary>
    /// Adds or updates an app in the database.
    /// </summary>
    public void AddOrUpdateApp(AppInfo app)
    {
        var key = GetAppKey(app.Store, app.BundleId ?? app.Id);
        _apps[key] = app;
    }

    /// <summary>
    /// Deletes an app from the database by bundle ID and store.
    /// </summary>
    public bool DeleteApp(string store, string bundleIdOrAppId)
    {
        var key = GetAppKey(store, bundleIdOrAppId);
        return _apps.Remove(key);
    }

    /// <summary>
    /// Gets all apps from the database.
    /// </summary>
    public List<AppInfo> GetAllApps(bool includeHidden = true)
    {
        var apps = _apps.Values.AsEnumerable();
        if (!includeHidden)
        {
            apps = apps.Where(a => !a.IsHidden);
        }
        return apps.OrderBy(a => a.Store).ThenBy(a => a.Name).ToList();
    }

    /// <summary>
    /// Gets an app by store and bundle ID.
    /// </summary>
    public AppInfo? GetApp(string store, string bundleIdOrAppId)
    {
        var key = GetAppKey(store, bundleIdOrAppId);
        _apps.TryGetValue(key, out var app);
        return app;
    }

    /// <summary>
    /// Finds apps by name (case-insensitive partial match).
    /// </summary>
    public List<AppInfo> FindAppsByName(string nameQuery, bool includeHidden = true)
    {
        var apps = _apps.Values
            .Where(a => a.Name.Contains(nameQuery, StringComparison.OrdinalIgnoreCase));
        
        if (!includeHidden)
        {
            apps = apps.Where(a => !a.IsHidden);
        }

        return apps.OrderBy(a => a.Store).ThenBy(a => a.Name).ToList();
    }

    /// <summary>
    /// Resolves an app query (ID, bundle ID, or name) to a specific app.
    /// Searches in order: exact ID match, exact bundle ID match, case-insensitive name match.
    /// </summary>
    /// <param name="query">The search query (app ID, bundle ID, or app name).</param>
    /// <param name="includeHidden">Whether to include hidden apps in the search.</param>
    /// <returns>The matched app, or null if no match found. If multiple apps match by name, returns the first one.</returns>
    public AppInfo? ResolveApp(string query, bool includeHidden = true)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var searchApps = includeHidden ? _apps.Values : _apps.Values.Where(a => !a.IsHidden);

        // 1. Try exact app ID match (case-insensitive)
        var byId = searchApps.FirstOrDefault(a => 
            a.Id.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (byId != null) return byId;

        // 2. Try exact bundle ID match (case-insensitive)
        var byBundleId = searchApps.FirstOrDefault(a => 
            a.BundleId != null && a.BundleId.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (byBundleId != null) return byBundleId;

        // 3. Try exact name match (case-insensitive)
        var byExactName = searchApps.FirstOrDefault(a => 
            a.Name.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (byExactName != null) return byExactName;

        // 4. Try partial name match (case-insensitive)
        var byPartialName = searchApps.FirstOrDefault(a => 
            a.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        
        return byPartialName;
    }

    /// <summary>
    /// Resolves an app query and returns the correct identifier to use for API calls.
    /// For App Store, returns the numeric app ID.
    /// For Google Play, returns the package name (bundle ID).
    /// </summary>
    /// <param name="query">The search query (app ID, bundle ID, or app name).</param>
    /// <param name="includeHidden">Whether to include hidden apps in the search.</param>
    /// <returns>A tuple of (identifier to use for API, store name, app info), or null if no match found.</returns>
    public (string ApiIdentifier, string Store, AppInfo App)? ResolveAppForApi(string query, bool includeHidden = true)
    {
        var app = ResolveApp(query, includeHidden);
        if (app == null) return null;

        // Determine the correct identifier for the API
        string apiIdentifier;
        if (app.Store.Equals("App Store", StringComparison.OrdinalIgnoreCase))
        {
            // App Store uses numeric app ID
            apiIdentifier = app.Id;
        }
        else if (app.Store.Equals("Google Play", StringComparison.OrdinalIgnoreCase))
        {
            // Google Play uses package name (bundle ID)
            apiIdentifier = app.BundleId ?? app.Id;
        }
        else
        {
            // Default: use app ID
            apiIdentifier = app.Id;
        }

        return (apiIdentifier, app.Store, app);
    }

    /// <summary>
    /// Creates a composite key for lookups using store and bundle ID.
    /// </summary>
    private static string GetAppKey(string store, string bundleIdOrAppId)
    {
        return $"{store}:{bundleIdOrAppId}";
    }
}
