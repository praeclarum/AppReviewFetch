# AppReviewFetch

A .NET library, CLI tool, and MCP service for fetching app reviews from App Store Connect (with future support for Google Play and Windows Store).

## 🤖 MCP Server for AI Assistants

Access your app reviews directly from GitHub Copilot, Claude, and other AI assistants using the MCP server.

Make sure you establish your credentials using the CLI tool first.
See [AppReviewFetchMcp/README.md](AppReviewFetchMcp/README.md) for detailed setup instructions.

**Quick setup for VS Code:**
```json
// .vscode/mcp.json
{
  "servers": {
    "appreviewfetch": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "${workspaceFolder}/AppReviewFetchMcp/AppReviewFetchMcp.csproj"]
    }
  }
}
```

## 🚀 Quick Start (CLI)

```bash
# Build and run
cd AppReviewFetchCli
dotnet run

# In the REPL
arfetch> setup              # Configure credentials interactively
arfetch> list               # List all your apps
arfetch> fetch 123456789    # Fetch reviews
```

### Install as Global Tool

```bash
cd AppReviewFetchCli
dotnet pack
dotnet tool install --global --add-source ./bin/Debug AppReviewFetch.Cli

# Run from anywhere
arfetch
```

### CLI Commands

| Command | Aliases | Description |
|---------|---------|-------------|
| `setup` | | Configure App Store Connect credentials |
| `status` | `s` | Check credentials & auth |
| `list` | `l` | List all apps (excludes hidden) |
| `add-app` | | Manually add an app to database |
| `edit-app [query]` | | Edit app metadata (ProjectUrl, IsHidden, Notes) |
| `delete-app [query]` | | Remove an app from database |
| `fetch <query> [country]` | `f` | Fetch reviews (supports app ID, bundle ID, or name) |
| `export [file]` | `e` | Export to CSV |
| `help` | `h`, `?` | Show all commands |

**Query Support:** Most commands accept flexible queries that can match:
- **App ID** (e.g., `123456789`)
- **Bundle/Package ID** (e.g., `com.example.app`)
- **App Name** (case-insensitive, partial match)

Examples:
```bash
fetch 123456789              # By app ID
fetch com.example.app        # By bundle ID
fetch "My App Name"          # By name
edit-app Calca               # Edit by name
delete-app com.old.app       # Delete by bundle ID
```

### App Database

AppReviewFetch maintains a local database of apps at:
- **Windows:** `%LOCALAPPDATA%\AppReviewFetch\Apps.json`
- **macOS/Linux:** `~/.config/AppReviewFetch/Apps.json`

This database:
- **Caches apps** from API calls (App Store, Google Play)
- **Stores metadata** like project URLs and notes
- **Supports manual entries** for stores that can't list apps (e.g., Google Play)
- **Hides removed apps** to keep listings clean

Use `add-app` to manually add apps, `edit-app` to set project URLs or hide apps, and `delete-app` to remove entries.

## Setup

### 1. Get App Store Connect API Key

1. Go to [App Store Connect](https://appstoreconnect.apple.com/) → **Users and Access** → **Keys** → **App Store Connect API**
2. Generate or select an API key
3. Note: **Key ID**, **Issuer ID**, and download the **.p8 file**

**Required Role:** Only **Account Holders** and **Admins** can generate API keys.

**Required Key Access:** When creating the key, assign one of these access levels:
- **App Manager** - Full app management access (recommended)
- **Customer Support** - Can view and respond to reviews
- **Sales** - Can view reviews and analytics
- **Admin** - Complete access to all features

**Note:** Developer, Marketing, and Finance roles do NOT have access to customer reviews.

### 2. Configure Credentials

Run `arfetch setup` or manually create:

**Windows:** `%LOCALAPPDATA%\AppReviewFetch\Credentials.json`  
**macOS/Linux:** `~/.config/AppReviewFetch/Credentials.json`

```json
{
  "appStoreConnect": {
    "keyId": "YOUR_KEY_ID",
    "issuerId": "YOUR_ISSUER_ID",
    "privateKey": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----"
  }
}
```

## Library Usage

### Fetch Reviews

```csharp
using AppReviewFetch;

var service = new AppStoreConnectService();
var request = new ReviewRequest
{
    SortOrder = ReviewSortOrder.NewestFirst,
    Limit = 100,
    Country = "US" // Optional
};

var response = await service.GetReviewsAsync("YOUR_APP_ID", request);

foreach (var review in response.Reviews)
{
    Console.WriteLine($"{review.Rating}/5 - {review.Title}");
    Console.WriteLine(review.Body);
    
    if (review.DeveloperResponse != null)
        Console.WriteLine($"Reply: {review.DeveloperResponse.Body}");
}
```

### List Apps

```csharp
var apps = await service.GetAppsAsync();
foreach (var app in apps.Apps)
{
    Console.WriteLine($"{app.Name} ({app.Id})");
}
```

### Pagination

```csharp
var allReviews = new List<AppReview>();
var request = new ReviewRequest { Limit = 200 };

string? cursor = null;
do
{
    request.Cursor = cursor;
    var response = await service.GetReviewsAsync(appId, request);
    allReviews.AddRange(response.Reviews);
    cursor = response.Pagination.NextCursor;
} while (!string.IsNullOrEmpty(cursor));
```

### Dependency Injection

```csharp
services.AddHttpClient<IAppReviewService, AppStoreConnectService>();
```

## API Reference

### IAppReviewService

- `Task<ReviewPageResponse> GetReviewsAsync(string appId, ReviewRequest request)`
- `Task<AppListResponse> GetAppsAsync()`

### ReviewRequest

- `ReviewSortOrder SortOrder` - NewestFirst, OldestFirst, HighestRatingFirst, LowestRatingFirst, MostHelpful
- `string? Country` - ISO 3166-1 alpha-2 country code
- `string? Cursor` - Pagination cursor
- `int Limit` - Results per page (default: 100)

### AppReview

- `string Id`, `int Rating`, `string? Title`, `string? Body`
- `string? ReviewerNickname`, `DateTimeOffset CreatedDate`, `string? Territory`
- `ReviewResponse? DeveloperResponse`

### Exceptions

- `ApiErrorException` - API errors (includes StatusCode, ErrorCode, ErrorDetail)
- `AuthenticationException` - JWT authentication issues
- `CredentialsException` - Credentials file problems

## Features

- ✅ JWT authentication with automatic token generation
- ✅ List all accessible apps
- ✅ Pagination support
- ✅ Multiple sort orders and territory filtering
- ✅ Developer response support
- ✅ Interface-based (extensible for other stores)

## License

MIT
