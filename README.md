# AppReviewFetch

A .NET library for fetching app reviews from various app stores, starting with Apple App Store Connect.

## Setup

### 1. Create App Store Connect API Key

1. Go to [App Store Connect](https://appstoreconnect.apple.com/)
2. Navigate to **Users and Access** → **Keys** → **App Store Connect API**
3. Click **Generate API Key** or use an existing one
4. Note down:
   - **Key ID** (e.g., `2X9R4HXF34`)
   - **Issuer ID** (e.g., `57246542-96fe-1a63-e053-0824d011072a`)
   - Download the **Private Key** (.p8 file)

### 2. Configure Credentials

Create the credentials file at `~/.config/AppReviewFetch/Credentials.json`:

```bash
mkdir -p ~/.config/AppReviewFetch
```

Create the file with the following structure:

```json
{
  "keyId": "YOUR_KEY_ID",
  "issuerId": "YOUR_ISSUER_ID",
  "privateKey": "-----BEGIN PRIVATE KEY-----\nMIGTAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBHkwdwIBAQQg...\n-----END PRIVATE KEY-----",
  "appId": "YOUR_APP_ID"
}
```

**Note:** The `privateKey` should contain the entire content of your .p8 file, including the header and footer. You can keep the newlines as `\n` or use actual newlines.

### 3. Find Your App ID

You can find your App ID in App Store Connect or via the API:
- In App Store Connect: Go to your app → **App Information** → **General Information** → **Apple ID**
- Or use the [Apps API endpoint](https://developer.apple.com/documentation/appstoreconnectapi/list_apps) to list all your apps

## Usage

### Basic Example

```csharp
using AppReviewFetch;

// Create the service
var service = new AppStoreConnectService();

// Prepare the request
var request = new ReviewRequest
{
    SortOrder = ReviewSortOrder.NewestFirst,
    Limit = 100,
    Country = "US" // Optional: filter by territory
};

try
{
    // Fetch reviews
    var response = await service.GetReviewsAsync("YOUR_APP_ID", request);

    // Process reviews
    foreach (var review in response.Reviews)
    {
        Console.WriteLine($"Rating: {review.Rating}/5");
        Console.WriteLine($"Title: {review.Title}");
        Console.WriteLine($"Body: {review.Body}");
        Console.WriteLine($"Date: {review.CreatedDate}");
        
        if (review.DeveloperResponse != null)
        {
            Console.WriteLine($"Developer Response: {review.DeveloperResponse.Body}");
        }
        
        Console.WriteLine();
    }

    // Handle pagination
    if (response.Pagination.HasMorePages)
    {
        var nextRequest = new ReviewRequest
        {
            SortOrder = request.SortOrder,
            Limit = request.Limit,
            Cursor = response.Pagination.NextCursor
        };
        
        var nextPage = await service.GetReviewsAsync("YOUR_APP_ID", nextRequest);
        // Process next page...
    }
}
catch (ApiErrorException ex)
{
    Console.WriteLine($"API Error: {ex.Message}");
    Console.WriteLine($"Status Code: {ex.StatusCode}");
    Console.WriteLine($"Error Code: {ex.ErrorCode}");
    Console.WriteLine($"Details: {ex.ErrorDetail}");
}
catch (CredentialsException ex)
{
    Console.WriteLine($"Credentials Error: {ex.Message}");
}
```

### Using with Dependency Injection

```csharp
services.AddHttpClient<IAppReviewService, AppStoreConnectService>();
```

### Pagination Example

```csharp
var allReviews = new List<AppReview>();
var request = new ReviewRequest
{
    SortOrder = ReviewSortOrder.NewestFirst,
    Limit = 200
};

string? cursor = null;
do
{
    request.Cursor = cursor;
    var response = await service.GetReviewsAsync(appId, request);
    
    allReviews.AddRange(response.Reviews);
    cursor = response.Pagination.NextCursor;
    
} while (!string.IsNullOrEmpty(cursor));

Console.WriteLine($"Total reviews fetched: {allReviews.Count}");
```

### Filtering Options

```csharp
var request = new ReviewRequest
{
    SortOrder = ReviewSortOrder.HighestRatingFirst, // Sort by rating
    Country = "GB", // Filter by UK reviews
    Limit = 50 // Get 50 reviews per page
};
```

## Features

- ✅ **JWT Authentication** - Automatic token generation and caching
- ✅ **Pagination Support** - Navigate through large review sets
- ✅ **Multiple Sort Orders** - Sort by date or rating
- ✅ **Territory Filtering** - Filter reviews by country
- ✅ **Developer Responses** - Includes replies to reviews
- ✅ **Async/Await** - Modern async API
- ✅ **Exception Handling** - Detailed error information
- ✅ **Interface-based** - Extensible for other app stores

## API Reference

### IAppReviewService

Main interface for fetching reviews.

#### Methods

- `Task<ReviewPageResponse> GetReviewsAsync(string appId, ReviewRequest request, CancellationToken cancellationToken = default)`

### ReviewRequest

Request parameters for fetching reviews.

#### Properties

- `ReviewSortOrder SortOrder` - Sort order (default: NewestFirst)
- `string? Platform` - Platform filter (reserved for future use)
- `string? Country` - ISO 3166-1 alpha-2 country code
- `string? Cursor` - Pagination cursor
- `int Limit` - Results per page (default: 100)

### ReviewSortOrder

Enum for sort order options:
- `NewestFirst` - Most recent reviews first
- `OldestFirst` - Oldest reviews first
- `HighestRatingFirst` - 5-star reviews first
- `LowestRatingFirst` - 1-star reviews first
- `MostHelpful` - Most helpful reviews first

### ReviewPageResponse

Response containing reviews and pagination metadata.

#### Properties

- `List<AppReview> Reviews` - List of reviews
- `PaginationMetadata Pagination` - Pagination information

### AppReview

Represents a single review.

#### Properties

- `string Id` - Unique review ID
- `int Rating` - Star rating (1-5)
- `string? Title` - Review title
- `string? Body` - Review text
- `string? ReviewerNickname` - Reviewer's display name
- `DateTimeOffset CreatedDate` - When the review was posted
- `string? Territory` - Country code
- `ReviewResponse? DeveloperResponse` - Developer's reply (if any)

## Exception Types

- `AppReviewFetchException` - Base exception
- `ApiErrorException` - API returned an error
- `AuthenticationException` - Authentication/JWT issues
- `CredentialsException` - Credentials file issues

## Future Enhancements

- Google Play Store support
- Microsoft Store support
- Local caching layer
- Rate limiting and retry logic
- Webhooks for new reviews

## License

MIT
