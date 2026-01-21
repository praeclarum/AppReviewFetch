namespace AppReviewFetch;

/// <summary>
/// Interface for fetching app reviews from various app stores.
/// </summary>
public interface IAppReviewService
{
    /// <summary>
    /// Fetches a page of reviews for a specified application.
    /// </summary>
    /// <param name="appId">The application identifier.</param>
    /// <param name="request">The request parameters including pagination, filtering, and sorting options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A page of reviews with pagination metadata.</returns>
    Task<ReviewPageResponse> GetReviewsAsync(string appId, ReviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all apps accessible to this service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of apps with their metadata.</returns>
    Task<AppListResponse> GetAppsAsync(CancellationToken cancellationToken = default);
}
