using System.Text.Json.Serialization;

namespace AppReviewFetch;

/// <summary>
/// Credentials for authenticating with App Store Connect API.
/// </summary>
public class AppStoreConnectCredentials
{
    /// <summary>
    /// The Key ID from App Store Connect.
    /// </summary>
    [JsonPropertyName("keyId")]
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// The Issuer ID (Team ID) from App Store Connect.
    /// </summary>
    [JsonPropertyName("issuerId")]
    public string IssuerId { get; set; } = string.Empty;

    /// <summary>
    /// The private key content (P8 file content).
    /// </summary>
    [JsonPropertyName("privateKey")]
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Default App ID to use if not specified in requests.
    /// </summary>
    [JsonPropertyName("appId")]
    public string? AppId { get; set; }
}
