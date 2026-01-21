using System.Text.Json.Serialization;

namespace AppReviewFetch.Models.AppStoreConnect;

/// <summary>
/// App Store Connect API response for customer reviews.
/// See: https://developer.apple.com/documentation/appstoreconnectapi/customer-reviews
/// </summary>
public class CustomerReviewsResponse
{
    [JsonPropertyName("data")]
    public List<CustomerReviewData> Data { get; set; } = new();

    [JsonPropertyName("included")]
    public List<IncludedResource>? Included { get; set; }

    [JsonPropertyName("links")]
    public PageLinks Links { get; set; } = new();

    [JsonPropertyName("meta")]
    public MetaData? Meta { get; set; }
}

public class CustomerReviewData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public CustomerReviewAttributes Attributes { get; set; } = new();

    [JsonPropertyName("relationships")]
    public CustomerReviewRelationships? Relationships { get; set; }
}

public class CustomerReviewAttributes
{
    [JsonPropertyName("rating")]
    public int Rating { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("reviewerNickname")]
    public string? ReviewerNickname { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTimeOffset CreatedDate { get; set; }

    [JsonPropertyName("territory")]
    public string? Territory { get; set; }
}

public class CustomerReviewRelationships
{
    [JsonPropertyName("response")]
    public RelationshipLink? Response { get; set; }
}

public class RelationshipLink
{
    [JsonPropertyName("data")]
    public RelationshipData? Data { get; set; }

    [JsonPropertyName("links")]
    public PageLinks? Links { get; set; }
}

public class RelationshipData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class IncludedResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public ReviewResponseAttributes? Attributes { get; set; }
}

public class ReviewResponseAttributes
{
    [JsonPropertyName("responseBody")]
    public string ResponseBody { get; set; } = string.Empty;

    [JsonPropertyName("lastModifiedDate")]
    public DateTimeOffset? LastModifiedDate { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }
}

public class PageLinks
{
    [JsonPropertyName("self")]
    public string? Self { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("prev")]
    public string? Previous { get; set; }
}

public class MetaData
{
    [JsonPropertyName("paging")]
    public PagingMetadata? Paging { get; set; }
}

public class PagingMetadata
{
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("limit")]
    public int? Limit { get; set; }
}

/// <summary>
/// Error response from App Store Connect API.
/// </summary>
public class AppStoreConnectErrorResponse
{
    [JsonPropertyName("errors")]
    public List<ApiError> Errors { get; set; } = new();
}

public class ApiError
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public ErrorSource? Source { get; set; }
}

public class ErrorSource
{
    [JsonPropertyName("pointer")]
    public string? Pointer { get; set; }

    [JsonPropertyName("parameter")]
    public string? Parameter { get; set; }
}
