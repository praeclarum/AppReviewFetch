using System.Text.Json.Serialization;

namespace AppReviewFetch;

/// <summary>
/// Root credentials object containing credentials for all supported app stores.
/// </summary>
public class Credentials
{
    /// <summary>
    /// Credentials for Apple App Store Connect API.
    /// </summary>
    [JsonPropertyName("appStoreConnect")]
    public AppStoreConnectCredentials? AppStoreConnect { get; set; }

    /// <summary>
    /// Credentials for Google Play Developer API.
    /// </summary>
    [JsonPropertyName("googlePlay")]
    public GooglePlayCredentials? GooglePlay { get; set; }
    
    // Future properties:
    // [JsonPropertyName("windowsStore")]
    // public WindowsStoreCredentials? WindowsStore { get; set; }
}
