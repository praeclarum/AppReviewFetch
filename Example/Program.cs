using AppReviewFetch;
using AppReviewFetch.Exceptions;

namespace AppReviewFetch.Example;

/// <summary>
/// Example usage of the AppStoreConnectService.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Parse command line arguments
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: AppReviewFetch.Example <appId> [country]");
            Console.WriteLine("Example: AppReviewFetch.Example 123456789 US");
            return;
        }

        var appId = args[0];
        var country = args.Length > 1 ? args[1] : null;

        await FetchReviewsExample(appId, country);
    }

    private static async Task FetchReviewsExample(string appId, string? country)
    {
        try
        {
            // Create the service (loads credentials from ~/.config/AppReviewFetch/Credentials.json)
            var service = new AppStoreConnectService();

            Console.WriteLine($"Fetching reviews for App ID: {appId}");
            if (!string.IsNullOrEmpty(country))
            {
                Console.WriteLine($"Filtering by country: {country}");
            }
            Console.WriteLine();

            // Prepare the request
            var request = new ReviewRequest
            {
                SortOrder = ReviewSortOrder.NewestFirst,
                Limit = 20, // Fetch 20 reviews per page
                Country = country
            };

            // Fetch first page
            var response = await service.GetReviewsAsync(appId, request);

            Console.WriteLine($"Found {response.Reviews.Count} reviews");
            if (response.Pagination.TotalCount.HasValue)
            {
                Console.WriteLine($"Total reviews: {response.Pagination.TotalCount}");
            }
            Console.WriteLine(new string('-', 80));

            // Display reviews
            foreach (var review in response.Reviews)
            {
                DisplayReview(review);
            }

            // Pagination example
            if (response.Pagination.HasMorePages)
            {
                Console.WriteLine("\nMore reviews available. Fetch next page? (y/n)");
                var key = Console.ReadKey();
                Console.WriteLine();

                if (key.Key == ConsoleKey.Y)
                {
                    // Fetch next page
                    var nextRequest = new ReviewRequest
                    {
                        SortOrder = request.SortOrder,
                        Limit = request.Limit,
                        Country = request.Country,
                        Cursor = response.Pagination.NextCursor
                    };

                    var nextPage = await service.GetReviewsAsync(appId, nextRequest);
                    
                    Console.WriteLine($"\nPage 2: Found {nextPage.Reviews.Count} reviews");
                    Console.WriteLine(new string('-', 80));
                    
                    foreach (var review in nextPage.Reviews)
                    {
                        DisplayReview(review);
                    }
                }
            }
            else
            {
                Console.WriteLine("\nNo more pages available.");
            }
        }
        catch (CredentialsException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Credentials Error: {ex.Message}");
            Console.WriteLine("\nPlease ensure you have created ~/.config/AppReviewFetch/Credentials.json");
            Console.WriteLine("See the README.md for setup instructions.");
            Console.ResetColor();
        }
        catch (ApiErrorException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"API Error: {ex.Message}");
            Console.WriteLine($"Status Code: {ex.StatusCode}");
            Console.WriteLine($"Error Code: {ex.ErrorCode}");
            Console.WriteLine($"Details: {ex.ErrorDetail}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Unexpected Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
        }
    }

    private static void DisplayReview(AppReview review)
    {
        // Display rating with stars
        var stars = new string('★', review.Rating) + new string('☆', 5 - review.Rating);
        Console.ForegroundColor = review.Rating >= 4 ? ConsoleColor.Green : 
                                 review.Rating >= 3 ? ConsoleColor.Yellow : 
                                 ConsoleColor.Red;
        Console.WriteLine($"{stars} ({review.Rating}/5)");
        Console.ResetColor();

        // Display review details
        Console.WriteLine($"Date: {review.CreatedDate:yyyy-MM-dd HH:mm:ss} | Territory: {review.Territory ?? "N/A"}");
        Console.WriteLine($"Reviewer: {review.ReviewerNickname ?? "Anonymous"}");
        
        if (!string.IsNullOrWhiteSpace(review.Title))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Title: {review.Title}");
            Console.ResetColor();
        }

        if (!string.IsNullOrWhiteSpace(review.Body))
        {
            Console.WriteLine($"Review: {review.Body}");
        }

        // Display developer response if present
        if (review.DeveloperResponse != null)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n  → Developer Response ({review.DeveloperResponse.CreatedDate:yyyy-MM-dd}):");
            Console.WriteLine($"     {review.DeveloperResponse.Body}");
            Console.ResetColor();
        }

        Console.WriteLine(new string('-', 80));
    }
}
