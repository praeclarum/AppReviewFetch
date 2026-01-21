using System.Text.Json.Serialization;

namespace AppReviewFetch;

/// <summary>
/// Credentials for Google Play Developer API.
/// </summary>
public class GooglePlayCredentials
{
    /// <summary>
    /// The complete service account JSON key file content as a string.
    /// This contains client_email, private_key, and other authentication details.
    /// Obtain this from Google Cloud Console > IAM & Admin > Service Accounts.
    /// </summary>
    [JsonPropertyName("serviceAccountJson")]
    public string ServiceAccountJson { get; set; } = string.Empty;
}
