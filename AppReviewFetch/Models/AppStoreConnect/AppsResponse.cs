using System.Text.Json.Serialization;

namespace AppReviewFetch.Models.AppStoreConnect;

/// <summary>
/// App Store Connect API response for apps list.
/// See: https://developer.apple.com/documentation/appstoreconnectapi/get-v1-apps
/// </summary>
public class AppsResponse
{
    [JsonPropertyName("data")]
    public List<AppData> Data { get; set; } = new();

    [JsonPropertyName("links")]
    public PageLinks Links { get; set; } = new();

    [JsonPropertyName("meta")]
    public MetaData? Meta { get; set; }
}

public class AppData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public AppAttributes Attributes { get; set; } = new();
}

public class AppAttributes
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("bundleId")]
    public string? BundleId { get; set; }

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    [JsonPropertyName("primaryLocale")]
    public string? PrimaryLocale { get; set; }

    [JsonPropertyName("isOrEverWasMadeForKids")]
    public bool? IsOrEverWasMadeForKids { get; set; }

    [JsonPropertyName("subscriptionStatusUrl")]
    public string? SubscriptionStatusUrl { get; set; }

    [JsonPropertyName("subscriptionStatusUrlVersion")]
    public string? SubscriptionStatusUrlVersion { get; set; }

    [JsonPropertyName("subscriptionStatusUrlForSandbox")]
    public string? SubscriptionStatusUrlForSandbox { get; set; }

    [JsonPropertyName("availableInNewTerritories")]
    public bool? AvailableInNewTerritories { get; set; }

    [JsonPropertyName("contentRightsDeclaration")]
    public string? ContentRightsDeclaration { get; set; }
}
