using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using AppReviewFetch;

namespace AppReviewFetchMcp;

/// <summary>
/// MCP tools for managing and querying app reviews from App Store Connect.
/// Provides AI assistants with access to app review data for analysis and monitoring.
/// </summary>
[McpServerToolType]
public static class AppReviewTools
{
    /// <summary>
    /// Lists all apps accessible through App Store Connect and Google Play.
    /// Returns comprehensive app information including IDs, names, bundle IDs, and SKUs.
    /// Use this tool to discover available apps before fetching their reviews.
    /// By default, hidden apps are excluded from the list.
    /// </summary>
    /// <param name="reviewService">The app review service instance (injected).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON array containing app details with id, name, bundleId, store, and sku for each app.</returns>
    [McpServerTool, Description("List all accessible apps from App Store Connect and Google Play. Returns app ID, name, bundle ID, store, and SKU for each app. Use this to find the app ID needed for fetching reviews.")]
    public static async Task<string> ListApps(
        IAppReviewService reviewService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var appsResponse = await reviewService.GetAppsAsync(cancellationToken);
            
            // Filter out hidden apps
            var visibleApps = appsResponse.Apps.Where(a => !a.IsHidden).ToList();
            
            var result = new
            {
                totalApps = visibleApps.Count,
                hiddenApps = appsResponse.Apps.Count - visibleApps.Count,
                apps = visibleApps.Select(app => new
                {
                    id = app.Id,
                    name = app.Name,
                    bundleId = app.BundleId,
                    sku = app.Sku,
                    store = app.Store,
                    projectUrl = app.ProjectUrl
                }).ToList(),
                warnings = appsResponse.Warnings
            };
            
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = true,
                message = ex.Message,
                type = ex.GetType().Name
            });
        }
    }

    /// <summary>
    /// Fetches reviews for a specific app with pagination, filtering, and sorting support.
    /// Returns detailed review information including ratings, text, developer responses, and metadata.
    /// Pagination is handled via cursor-based navigation for efficient data retrieval.
    /// The app identifier can be an app ID, bundle/package ID, or app name from the database.
    /// </summary>
    /// <param name="reviewService">The app review service instance (injected).</param>
    /// <param name="appId">The app identifier - can be app ID, bundle/package ID (e.g., com.example.app), or app name. The service will resolve it automatically.</param>
    /// <param name="sortOrder">Sort order: "NewestFirst" (default), "OldestFirst", "HighestRatingFirst", "LowestRatingFirst", or "MostHelpful".</param>
    /// <param name="country">Optional ISO 3166-1 alpha-2 country/territory code (e.g., "US", "GB", "JP") to filter reviews by region.</param>
    /// <param name="limit">Number of reviews per page (default: 50, max: 200 for App Store, 100 for Google Play). Consider using smaller values for initial exploration.</param>
    /// <param name="cursor">Pagination cursor from a previous response's nextCursor field. Leave empty for first page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON object with reviews array, pagination info (nextCursor, hasMorePages), and summary statistics (totalReviews, averageRating).</returns>
    [McpServerTool, Description("Fetch reviews for a specific app from App Store Connect or Google Play. Supports pagination (use cursor from previous response), filtering by country (ISO 3166-1 alpha-2 code like 'US'), and various sort orders. Returns reviews with ratings, text, developer responses, dates, and reviewer info. Use smaller page limits (20-50) for quick previews, larger limits (100-200) for comprehensive analysis.")]
    public static async Task<string> FetchReviews(
        IAppReviewService reviewService,
        [Description("The app identifier: app ID, bundle/package ID (e.g., com.example.app), or app name. Use ListApps to discover apps.")] string appId,
        [Description("Sort order: NewestFirst, OldestFirst, HighestRatingFirst, LowestRatingFirst, MostHelpful")] string? sortOrder = "NewestFirst",
        [Description("ISO 3166-1 alpha-2 country code (e.g., US, GB, JP) to filter reviews by territory")] string? country = null,
        [Description("Number of reviews per page (1-200, default: 50)")] int limit = 50,
        [Description("Pagination cursor from previous response (leave empty for first page)")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate and parse sort order
            if (!Enum.TryParse<ReviewSortOrder>(sortOrder, true, out var parsedSortOrder))
            {
                parsedSortOrder = ReviewSortOrder.NewestFirst;
            }

            // Validate limit
            if (limit < 1 || limit > 200)
            {
                limit = 50;
            }

            var request = new ReviewRequest
            {
                SortOrder = parsedSortOrder,
                Country = country,
                Limit = limit,
                Cursor = cursor
            };

            var response = await reviewService.GetReviewsAsync(appId, request, cancellationToken);

            // Calculate summary statistics
            var avgRating = response.Reviews.Any() 
                ? Math.Round(response.Reviews.Average(r => r.Rating), 2) 
                : 0;

            var ratingDistribution = response.Reviews
                .GroupBy(r => r.Rating)
                .OrderByDescending(g => g.Key)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            var result = new
            {
                summary = new
                {
                    totalReviewsInPage = response.Reviews.Count,
                    averageRating = avgRating,
                    ratingDistribution,
                    hasMorePages = response.Pagination.HasMorePages,
                    nextCursor = response.Pagination.NextCursor
                },
                pagination = new
                {
                    currentPageSize = response.Reviews.Count,
                    limit,
                    hasMorePages = response.Pagination.HasMorePages,
                    nextCursor = response.Pagination.NextCursor,
                    instructions = response.Pagination.HasMorePages 
                        ? $"To fetch the next page, use cursor: {response.Pagination.NextCursor}"
                        : "No more pages available"
                },
                warnings = response.Warnings,
                reviews = response.Reviews.Select(review => new
                {
                    id = review.Id,
                    rating = review.Rating,
                    title = review.Title,
                    body = review.Body,
                    reviewerNickname = review.ReviewerNickname,
                    createdDate = review.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    territory = review.Territory,
                    hasDeveloperResponse = review.DeveloperResponse != null,
                    developerResponse = review.DeveloperResponse != null ? new
                    {
                        id = review.DeveloperResponse.Id,
                        body = review.DeveloperResponse.Body,
                        modifiedDate = review.DeveloperResponse.ModifiedDate?.ToString("yyyy-MM-dd HH:mm:ss")
                    } : null
                }).ToList()
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = true,
                message = ex.Message,
                type = ex.GetType().Name,
                appId,
                parameters = new
                {
                    sortOrder,
                    country,
                    limit,
                    cursor
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Responds to a customer review by creating or updating a developer response.
    /// Use the review ID from FetchReviews to identify which review to respond to.
    /// The response will be published to the app store after a brief delay.
    /// </summary>
    /// <param name="reviewService">The app review service instance (injected).</param>
    /// <param name="reviewId">The unique identifier of the review to respond to (from FetchReviews).</param>
    /// <param name="responseText">The text of your response. Keep it concise and professional. Maximum ~350 characters for Google Play.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON object with the created/updated response details including response ID, body, and timestamps.</returns>
    [McpServerTool, Description("Respond to a customer review. Creates a new response or updates an existing one. Use the review ID from FetchReviews. Response text should be professional and concise (max ~350 chars for Google Play). Returns the response details including ID for future reference.")]
    public static async Task<string> RespondToReview(
        IAppReviewService reviewService,
        [Description("The review ID from FetchReviews output")] string reviewId,
        [Description("Your response text - keep it professional and concise")] string responseText,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await reviewService.RespondToReviewAsync(reviewId, responseText, cancellationToken);

            var result = new
            {
                success = true,
                response = new
                {
                    id = response.Id,
                    body = response.Body,
                    createdDate = response.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    modifiedDate = response.ModifiedDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                    state = response.State
                },
                message = "Response submitted successfully. It may take a few moments to appear in the app store."
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = true,
                message = ex.Message,
                type = ex.GetType().Name,
                reviewId,
                responseText
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>
    /// Deletes a developer response to a review.
    /// Note: Google Play does not support deleting responses - only App Store supports this.
    /// Use the response ID from the review's developerResponse.id field.
    /// </summary>
    /// <param name="reviewService">The app review service instance (injected).</param>
    /// <param name="responseId">The unique identifier of the response to delete (from review's developerResponse.id).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON object confirming deletion or describing any errors.</returns>
    [McpServerTool, Description("Delete a developer response to a review. Only supported for App Store (not Google Play). Use the response ID from the review's developerResponse.id field in FetchReviews output.")]
    public static async Task<string> DeleteReviewResponse(
        IAppReviewService reviewService,
        [Description("The response ID from review's developerResponse.id")] string responseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await reviewService.DeleteReviewResponseAsync(responseId, cancellationToken);

            var result = new
            {
                success = true,
                message = "Response deleted successfully.",
                responseId
            };

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (NotSupportedException ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = true,
                notSupported = true,
                message = ex.Message,
                type = ex.GetType().Name,
                responseId
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = true,
                message = ex.Message,
                type = ex.GetType().Name,
                responseId
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}

