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
            
            var result = new
            {
                totalApps = appsResponse.Apps.Count,
                apps = appsResponse.Apps.Select(app => new
                {
                    id = app.Id,
                    name = app.Name,
                    bundleId = app.BundleId,
                    sku = app.Sku,
                    store = app.Store
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
    /// </summary>
    /// <param name="reviewService">The app review service instance (injected).</param>
    /// <param name="appId">The unique app identifier. For App Store: numeric ID; for Google Play: package name (e.g., com.example.app).</param>
    /// <param name="sortOrder">Sort order: "NewestFirst" (default), "OldestFirst", "HighestRatingFirst", "LowestRatingFirst", or "MostHelpful".</param>
    /// <param name="country">Optional ISO 3166-1 alpha-2 country/territory code (e.g., "US", "GB", "JP") to filter reviews by region.</param>
    /// <param name="limit">Number of reviews per page (default: 50, max: 200 for App Store, 100 for Google Play). Consider using smaller values for initial exploration.</param>
    /// <param name="cursor">Pagination cursor from a previous response's nextCursor field. Leave empty for first page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON object with reviews array, pagination info (nextCursor, hasMorePages), and summary statistics (totalReviews, averageRating).</returns>
    [McpServerTool, Description("Fetch reviews for a specific app from App Store Connect or Google Play. Supports pagination (use cursor from previous response), filtering by country (ISO 3166-1 alpha-2 code like 'US'), and various sort orders. Returns reviews with ratings, text, developer responses, dates, and reviewer info. Use smaller page limits (20-50) for quick previews, larger limits (100-200) for comprehensive analysis.")]
    public static async Task<string> FetchReviews(
        IAppReviewService reviewService,
        [Description("The app identifier (use ListApps to find this). For App Store: numeric ID; for Google Play: package name")] string appId,
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
    /// Analyzes review sentiment and provides statistical insights for an app.
    /// Automatically handles pagination to analyze all available reviews.
    /// Use this for comprehensive sentiment analysis and trend identification.
    /// </summary>
    /// <param name="reviewService">The app review service instance (injected).</param>
    /// <param name="appId">The App Store Connect app ID to analyze.</param>
    /// <param name="country">Optional country code to filter reviews by territory.</param>
    /// <param name="maxReviews">Maximum number of reviews to analyze (default: 500 for performance).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON object with detailed statistics including rating distribution, response rate, recent trends, and sample reviews.</returns>
    [McpServerTool, Description("Analyze app reviews to provide comprehensive statistics and insights. Returns rating distribution, average rating, developer response rate, recent review trends, and sample reviews for each rating level. Automatically handles pagination up to maxReviews limit. Use this for sentiment analysis and identifying common themes in user feedback.")]
    public static async Task<string> AnalyzeReviews(
        IAppReviewService reviewService,
        [Description("The App Store Connect app ID to analyze")] string appId,
        [Description("Optional ISO 3166-1 alpha-2 country code to filter by territory")] string? country = null,
        [Description("Maximum number of reviews to analyze (default: 500, use lower values for faster results)")] int maxReviews = 500,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var allReviews = new List<AppReview>();
            string? cursor = null;
            var pageLimit = Math.Min(200, maxReviews);

            // Fetch reviews with pagination
            do
            {
                var request = new ReviewRequest
                {
                    SortOrder = ReviewSortOrder.NewestFirst,
                    Country = country,
                    Limit = pageLimit,
                    Cursor = cursor
                };

                var response = await reviewService.GetReviewsAsync(appId, request, cancellationToken);
                allReviews.AddRange(response.Reviews);

                cursor = response.Pagination.HasMorePages ? response.Pagination.NextCursor : null;

                if (allReviews.Count >= maxReviews || !response.Pagination.HasMorePages)
                {
                    break;
                }
            } while (cursor != null);

            // Calculate statistics
            var totalReviews = allReviews.Count;
            var avgRating = totalReviews > 0 ? Math.Round(allReviews.Average(r => r.Rating), 2) : 0;

            var ratingDistribution = allReviews
                .GroupBy(r => r.Rating)
                .OrderByDescending(g => g.Key)
                .ToDictionary(
                    g => $"{g.Key}-star",
                    g => new
                    {
                        count = g.Count(),
                        percentage = Math.Round((double)g.Count() / totalReviews * 100, 1)
                    });

            var reviewsWithResponses = allReviews.Count(r => r.DeveloperResponse != null);
            var responseRate = totalReviews > 0 
                ? Math.Round((double)reviewsWithResponses / totalReviews * 100, 1) 
                : 0;

            // Recent trends (last 30 days vs previous 30 days)
            var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);
            var sixtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-60);

            var recentReviews = allReviews.Where(r => r.CreatedDate >= thirtyDaysAgo).ToList();
            var previousReviews = allReviews.Where(r => r.CreatedDate >= sixtyDaysAgo && r.CreatedDate < thirtyDaysAgo).ToList();

            var recentAvg = recentReviews.Any() ? Math.Round(recentReviews.Average(r => r.Rating), 2) : 0;
            var previousAvg = previousReviews.Any() ? Math.Round(previousReviews.Average(r => r.Rating), 2) : 0;
            var trend = recentAvg - previousAvg;

            // Sample reviews for each rating
            var sampleReviews = allReviews
                .GroupBy(r => r.Rating)
                .OrderByDescending(g => g.Key)
                .ToDictionary(
                    g => $"{g.Key}-star",
                    g => g.Take(2).Select(r => new
                    {
                        title = r.Title,
                        body = r.Body?.Length > 200 ? r.Body.Substring(0, 200) + "..." : r.Body,
                        date = r.CreatedDate.ToString("yyyy-MM-dd"),
                        hasResponse = r.DeveloperResponse != null
                    }).ToList());

            var result = new
            {
                appId,
                analyzedReviews = totalReviews,
                maxReviewsLimit = maxReviews,
                reachedLimit = totalReviews >= maxReviews,
                country = country ?? "all territories",
                statistics = new
                {
                    averageRating = avgRating,
                    totalReviews,
                    ratingDistribution,
                    developerResponseRate = new
                    {
                        percentage = responseRate,
                        responded = reviewsWithResponses,
                        total = totalReviews
                    }
                },
                trends = new
                {
                    last30Days = new
                    {
                        averageRating = recentAvg,
                        reviewCount = recentReviews.Count
                    },
                    previous30Days = new
                    {
                        averageRating = previousAvg,
                        reviewCount = previousReviews.Count
                    },
                    ratingTrend = trend > 0 ? $"Up {trend:+0.00}" : trend < 0 ? $"Down {trend:0.00}" : "Stable",
                    trendValue = Math.Round(trend, 2)
                },
                sampleReviews,
                recommendations = new List<string>
                {
                    responseRate < 50 ? "Consider responding to more reviews to improve engagement" : "Good developer response rate",
                    avgRating < 3.5 ? "Low average rating - investigate common complaints" : avgRating >= 4.5 ? "Excellent rating!" : "Good rating",
                    trend < -0.3 ? "Rating declining - recent reviews show lower satisfaction" : trend > 0.3 ? "Rating improving - recent updates well received" : "Rating stable"
                }
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
                appId
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
