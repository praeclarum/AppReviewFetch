namespace AppReviewFetch;

/// <summary>
/// Response containing a list of apps.
/// </summary>
public class AppListResponse
{
    /// <summary>
    /// The list of apps.
    /// </summary>
    public List<AppInfo> Apps { get; set; } = new();
}

/// <summary>
/// Information about an application.
/// </summary>
public class AppInfo
{
    /// <summary>
    /// Unique identifier for the app (as used by this library/API).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The app's display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The bundle identifier (iOS/macOS) or package name (Android).
    /// </summary>
    public string? BundleId { get; set; }

    /// <summary>
    /// The SKU or product identifier.
    /// </summary>
    public string? Sku { get; set; }

    /// <summary>
    /// The platform(s) this app is available on.
    /// </summary>
    public List<string> Platforms { get; set; } = new();

    /// <summary>
    /// The store this app is from (e.g., "App Store", "Google Play", "Microsoft Store").
    /// </summary>
    public string Store { get; set; } = string.Empty;

    /// <summary>
    /// Primary locale/language code.
    /// </summary>
    public string? PrimaryLocale { get; set; }

    /// <summary>
    /// Whether the app is currently available in the store.
    /// </summary>
    public bool? IsAvailable { get; set; }

    /// <summary>
    /// The app's current version in the store.
    /// </summary>
    public string? CurrentVersion { get; set; }
}
