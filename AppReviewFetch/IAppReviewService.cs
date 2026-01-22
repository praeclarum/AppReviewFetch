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

    /// <summary>
    /// Responds to a customer review. Creates a new response or updates an existing one.
    /// </summary>
    /// <param name="reviewId">The unique identifier of the review to respond to.</param>
    /// <param name="responseText">The text of the response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created or updated review response.</returns>
    Task<ReviewResponse> RespondToReviewAsync(string reviewId, string responseText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a developer response to a review.
    /// </summary>
    /// <param name="responseId">The unique identifier of the response to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteReviewResponseAsync(string responseId, CancellationToken cancellationToken = default);
}
