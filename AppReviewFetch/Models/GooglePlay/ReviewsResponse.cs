using System.Text.Json.Serialization;

namespace AppReviewFetch.Models.GooglePlay;

/// <summary>
/// Response from the Google Play Developer API reviews list endpoint.
/// Documentation: https://developers.google.com/android-publisher/api-ref/rest/v3/reviews/list
/// </summary>
public class ReviewsResponse
{
    [JsonPropertyName("reviews")]
    public List<Review> Reviews { get; set; } = new();

    [JsonPropertyName("tokenPagination")]
    public TokenPagination? TokenPagination { get; set; }
}

public class TokenPagination
{
    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }

    [JsonPropertyName("previousPageToken")]
    public string? PreviousPageToken { get; set; }
}

public class Review
{
    [JsonPropertyName("reviewId")]
    public string ReviewId { get; set; } = string.Empty;

    [JsonPropertyName("authorName")]
    public string? AuthorName { get; set; }

    [JsonPropertyName("comments")]
    public List<Comment> Comments { get; set; } = new();
}

public class Comment
{
    [JsonPropertyName("userComment")]
    public UserComment? UserComment { get; set; }

    [JsonPropertyName("developerComment")]
    public DeveloperComment? DeveloperComment { get; set; }
}

public class UserComment
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("lastModified")]
    public Timestamp? LastModified { get; set; }

    [JsonPropertyName("starRating")]
    public int StarRating { get; set; }

    [JsonPropertyName("reviewerLanguage")]
    public string? ReviewerLanguage { get; set; }

    [JsonPropertyName("device")]
    public string? Device { get; set; }

    [JsonPropertyName("androidOsVersion")]
    public int? AndroidOsVersion { get; set; }

    [JsonPropertyName("appVersionCode")]
    public int? AppVersionCode { get; set; }

    [JsonPropertyName("appVersionName")]
    public string? AppVersionName { get; set; }

    [JsonPropertyName("thumbsUpCount")]
    public int? ThumbsUpCount { get; set; }

    [JsonPropertyName("thumbsDownCount")]
    public int? ThumbsDownCount { get; set; }
}

public class DeveloperComment
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("lastModified")]
    public Timestamp? LastModified { get; set; }
}

public class Timestamp
{
    [JsonPropertyName("seconds")]
    public long Seconds { get; set; }

    [JsonPropertyName("nanos")]
    public int Nanos { get; set; }

    public DateTimeOffset ToDateTimeOffset()
    {
        return DateTimeOffset.FromUnixTimeSeconds(Seconds).AddTicks(Nanos / 100);
    }
}

/// <summary>
/// Response from the Google Play Developer API list applications endpoint.
/// Note: There's no direct "list apps" endpoint in Google Play API.
/// Apps must be accessed via package name. This model is for potential future use.
/// </summary>
public class ApplicationsResponse
{
    [JsonPropertyName("applications")]
    public List<Application> Applications { get; set; } = new();
}

public class Application
{
    [JsonPropertyName("packageName")]
    public string PackageName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
