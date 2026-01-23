using System.Text.Json.Serialization;

namespace AppReviewFetch;

/// <summary>
/// Response containing a page of reviews and pagination metadata.
/// </summary>
public class ReviewPageResponse
{
    /// <summary>
    /// The list of reviews in this page.
    /// </summary>
    public List<AppReview> Reviews { get; set; } = new();

    /// <summary>
    /// Pagination information.
    /// </summary>
    public PaginationMetadata Pagination { get; set; } = new();

    /// <summary>
    /// Warnings or informational messages about the data.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Pagination metadata for navigating through review pages.
/// </summary>
public class PaginationMetadata
{
    /// <summary>
    /// Total count of reviews (if available from the API).
    /// </summary>
    public int? TotalCount { get; set; }

    /// <summary>
    /// Cursor for the next page of results.
    /// </summary>
    public string? NextCursor { get; set; }

    /// <summary>
    /// Cursor for the previous page of results.
    /// </summary>
    public string? PreviousCursor { get; set; }

    /// <summary>
    /// Whether there are more pages available.
    /// </summary>
    public bool HasMorePages { get; set; }
}

/// <summary>
/// Represents a single app review.
/// </summary>
public class AppReview
{
    /// <summary>
    /// Unique identifier for the review.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Rating value (typically 1-5).
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Review title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Review body/text content.
    /// </summary>
    public string? Body { get; set; }

    /// <summary>
    /// Reviewer's nickname or username.
    /// </summary>
    public string? ReviewerNickname { get; set; }

    /// <summary>
    /// Date when the review was created.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Territory/country code where the review was submitted.
    /// </summary>
    public string? Territory { get; set; }

    /// <summary>
    /// Developer response to the review (if any).
    /// </summary>
    public ReviewResponse? DeveloperResponse { get; set; }
}

/// <summary>
/// Represents a developer's response to a review.
/// </summary>
public class ReviewResponse
{
    /// <summary>
    /// Unique identifier for the response.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Response body/text content.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Date when the response was created.
    /// </summary>
    public DateTimeOffset CreatedDate { get; set; }

    /// <summary>
    /// Date when the response was last modified.
    /// </summary>
    public DateTimeOffset? ModifiedDate { get; set; }

    /// <summary>
    /// Current state of the response.
    /// </summary>
    public string? State { get; set; }
}
