namespace AppReviewFetch;

/// <summary>
/// Request parameters for fetching reviews.
/// </summary>
public class ReviewRequest
{
    /// <summary>
    /// The sort order for reviews. Default is newest first.
    /// </summary>
    public ReviewSortOrder SortOrder { get; set; } = ReviewSortOrder.NewestFirst;

    /// <summary>
    /// The platform filter (e.g., iOS, macOS, Android, Windows).
    /// </summary>
    public string? Platform { get; set; }

    /// <summary>
    /// The country/territory code (ISO 3166-1 alpha-2, e.g., "US", "GB").
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Cursor for pagination (from previous response).
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Number of items per page. Default is 100.
    /// </summary>
    public int Limit { get; set; } = 100;
}

/// <summary>
/// Sort order options for reviews.
/// </summary>
public enum ReviewSortOrder
{
    /// <summary>
    /// Sort by creation date, newest first (most recent reviews first).
    /// </summary>
    NewestFirst,

    /// <summary>
    /// Sort by creation date, oldest first.
    /// </summary>
    OldestFirst,

    /// <summary>
    /// Sort by rating, highest first.
    /// </summary>
    HighestRatingFirst,

    /// <summary>
    /// Sort by rating, lowest first.
    /// </summary>
    LowestRatingFirst,

    /// <summary>
    /// Sort by most helpful (based on helpfulness votes).
    /// </summary>
    MostHelpful
}
