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

    [JsonPropertyName("included")]
    public List<IncludedItem>? Included { get; set; }
}

public class AppData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public AppAttributes Attributes { get; set; } = new();

    [JsonPropertyName("relationships")]
    public AppRelationships? Relationships { get; set; }
}

public class AppRelationships
{
    [JsonPropertyName("appStoreVersions")]
    public AppStoreVersionsRelationship? AppStoreVersions { get; set; }
}

public class AppStoreVersionsRelationship
{
    [JsonPropertyName("data")]
    public List<RelationshipData>? Data { get; set; }
}

public class IncludedItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public AppStoreVersionAttributes? Attributes { get; set; }
}

public class AppStoreVersionAttributes
{
    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("versionString")]
    public string? VersionString { get; set; }
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
