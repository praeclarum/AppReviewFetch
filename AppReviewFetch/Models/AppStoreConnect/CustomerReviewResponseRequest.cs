using System.Text.Json.Serialization;

namespace AppReviewFetch.Models.AppStoreConnect;

/// <summary>
/// Request for creating or updating a customer review response.
/// See: https://developer.apple.com/documentation/appstoreconnectapi/post-v1-customerreviewresponses
/// </summary>
public class CustomerReviewResponseCreateRequest
{
    [JsonPropertyName("data")]
    public CustomerReviewResponseData Data { get; set; } = new();
}

public class CustomerReviewResponseData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "customerReviewResponses";

    [JsonPropertyName("attributes")]
    public CustomerReviewResponseRequestAttributes Attributes { get; set; } = new();

    [JsonPropertyName("relationships")]
    public CustomerReviewResponseRelationships Relationships { get; set; } = new();
}

public class CustomerReviewResponseRequestAttributes
{
    [JsonPropertyName("responseBody")]
    public string ResponseBody { get; set; } = string.Empty;
}

public class CustomerReviewResponseRelationships
{
    [JsonPropertyName("review")]
    public ReviewRelationship Review { get; set; } = new();
}

public class ReviewRelationship
{
    [JsonPropertyName("data")]
    public ReviewRelationshipData Data { get; set; } = new();
}

public class ReviewRelationshipData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "customerReviews";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Response from creating or updating a customer review response.
/// </summary>
public class CustomerReviewResponseCreateResponse
{
    [JsonPropertyName("data")]
    public CustomerReviewResponseDataItem Data { get; set; } = new();
}

public class CustomerReviewResponseDataItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public ReviewResponseAttributes Attributes { get; set; } = new();
}
