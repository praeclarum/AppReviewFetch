using System.Text.Json;
using Spectre.Console;
using AppReviewFetch;
using AppReviewFetch.Exceptions;

namespace AppReviewFetchCli;

/// <summary>
/// Handles all command execution for the CLI.
/// </summary>
public class CommandHandler
{
    private readonly List<AppReview> _allFetchedReviews = new();
    private readonly string _credentialsPath;

    public CommandHandler()
    {
        // Use platform-appropriate config directory
        // Windows: %LOCALAPPDATA%\AppReviewFetch\Credentials.json
        // macOS/Linux: ~/.config/AppReviewFetch/Credentials.json
        var configDir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        
        _credentialsPath = Path.Combine(configDir, "AppReviewFetch", "Credentials.json");
    }

    public void ShowHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn(new TableColumn("[bold cyan]Command[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold cyan]Aliases[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold cyan]Description[/]").LeftAligned());

        table.AddRow("help", "h, ?", "Show this help message");
        table.AddRow("status", "s", "Show credentials and authentication status");
        table.AddRow("setup", "", "Interactive wizard to configure credentials");
        table.AddRow("list", "l", "List all apps accessible with current credentials");
        table.AddRow("add-app", "", "Add an app to the database manually");
        table.AddRow("edit-app [[query]]", "", "Edit app metadata (supports app ID, bundle ID, or name)");
        table.AddRow("delete-app [[query]]", "", "Delete an app from the database (supports app ID, bundle ID, or name)");
        table.AddRow("fetch [[query]]", "f [[query]]", "Fetch reviews (supports app ID, bundle ID, or name)");
        table.AddRow("respond [[reviewId]]", "r [[reviewId]]", "Respond to a review");
        table.AddRow("delete-response [[responseId]]", "", "Delete a response to a review");
        table.AddRow("export [[file]]", "e [[file]]", "Export all fetched reviews to CSV");
        table.AddRow("clear", "cls", "Clear the screen");
        table.AddRow("exit", "quit, q", "Exit the application");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Examples:[/]");
        AnsiConsole.MarkupLine("  [cyan]list[/] - List all your apps");
        AnsiConsole.MarkupLine("  [cyan]fetch 123456789[/] - Fetch reviews by app ID");
        AnsiConsole.MarkupLine("  [cyan]fetch com.example.app[/] - Fetch reviews by bundle/package ID");
        AnsiConsole.MarkupLine("  [cyan]fetch \"My App Name\"[/] - Fetch reviews by app name");
        AnsiConsole.MarkupLine("  [cyan]f 123456789 US[/] - Fetch US reviews only");
        AnsiConsole.MarkupLine("  [cyan]export reviews.csv[/] - Export to specific file");
    }

    public async Task ShowStatusAsync()
    {
        var panel = new Panel(await GetStatusContentAsync())
            .Header("[bold yellow]Status[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Yellow);

        AnsiConsole.Write(panel);
    }

    private async Task<string> GetStatusContentAsync()
    {
        var content = new System.Text.StringBuilder();

        // Check credentials file
        content.AppendLine($"[bold]Credentials File:[/] [dim]{_credentialsPath}[/]");
        
        if (!File.Exists(_credentialsPath))
        {
            content.AppendLine($"[red]✗ Not found[/]");
            content.AppendLine();
            content.AppendLine("[yellow]Run 'setup' to create credentials[/]");
            return content.ToString();
        }

        content.AppendLine($"[green]✓ Found[/]");
        content.AppendLine();

        // Try to load and validate credentials
        try
        {
            var json = await File.ReadAllTextAsync(_credentialsPath);
            var rootCredentials = JsonSerializer.Deserialize<Credentials>(json);

            // App Store Connect
            if (rootCredentials?.AppStoreConnect != null)
            {
                var credentials = rootCredentials.AppStoreConnect;
                content.AppendLine("[bold]App Store Connect:[/]");
                content.AppendLine($"  Key ID: [cyan]{MaskString(credentials.KeyId)}[/]");
                content.AppendLine($"  Issuer ID: [cyan]{MaskString(credentials.IssuerId)}[/]");
                content.AppendLine($"  Private Key: {(string.IsNullOrWhiteSpace(credentials.PrivateKey) ? "[red]✗ Missing[/]" : "[green]✓ Present[/]")}");
                content.AppendLine();
            }

            // Google Play
            if (rootCredentials?.GooglePlay != null)
            {
                var credentials = rootCredentials.GooglePlay;
                content.AppendLine("[bold]Google Play:[/]");
                content.AppendLine($"  Service Account: {(string.IsNullOrWhiteSpace(credentials.ServiceAccountJson) ? "[red]✗ Missing[/]" : "[green]✓ Present[/]")}");
                
                // Try to extract client email from JSON for display
                try
                {
                    var doc = JsonDocument.Parse(credentials.ServiceAccountJson);
                    if (doc.RootElement.TryGetProperty("client_email", out var emailElement))
                    {
                        content.AppendLine($"  Email: [cyan]{emailElement.GetString()}[/]");
                    }
                }
                catch
                {
                    // Ignore parsing errors
                }
                
                content.AppendLine();
            }

            if (rootCredentials?.AppStoreConnect == null && rootCredentials?.GooglePlay == null)
            {
                content.AppendLine("[yellow]⚠ No credentials configured[/]");
                content.AppendLine();
                content.AppendLine("[yellow]Run 'setup' to create credentials[/]");
                return content.ToString();
            }
        }
        catch (Exception ex)
        {
            content.AppendLine($"[red]✗ Error reading credentials: {ex.Message}[/]");
        }

        // Show session stats
        if (_allFetchedReviews.Count > 0)
        {
            content.AppendLine();
            content.AppendLine("[bold]Session:[/]");
            content.AppendLine($"  Reviews fetched: [cyan]{_allFetchedReviews.Count}[/]");
        }

        return content.ToString();
    }

    public async Task SetupCredentialsAsync()
    {
        AnsiConsole.Write(new Rule("[bold cyan]Credentials Setup[/]") { Justification = Justify.Left });
        AnsiConsole.WriteLine();

        // Load existing credentials if they exist
        Credentials credentials;
        if (File.Exists(_credentialsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_credentialsPath);
                credentials = JsonSerializer.Deserialize<Credentials>(json) ?? new Credentials();
            }
            catch
            {
                credentials = new Credentials();
            }
        }
        else
        {
            credentials = new Credentials();
        }

        // Ask which store to configure
        var store = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Which store would you like to configure?[/]")
                .AddChoices(new[] { "App Store Connect", "Google Play", "Both" }));

        if (store == "App Store Connect" || store == "Both")
        {
            await SetupAppStoreCredentialsAsync(credentials);
        }

        if (store == "Google Play" || store == "Both")
        {
            await SetupGooglePlayCredentialsAsync(credentials);
        }

        // Save credentials
        await SaveCredentialsAsync(credentials);
    }

    private async Task SetupAppStoreCredentialsAsync(Credentials credentials)
    {
        AnsiConsole.MarkupLine("[bold yellow]App Store Connect Setup[/]");
        AnsiConsole.MarkupLine("[dim]Get credentials from: https://appstoreconnect.apple.com/access/integrations/api[/]");
        AnsiConsole.MarkupLine("[dim]Required key access: App Manager, Customer Support, Sales, or Admin[/]");
        AnsiConsole.WriteLine();

        var keyId = AnsiConsole.Ask<string>("[cyan]Key ID[/] (e.g., 2X9R4HXF34):");
        var issuerId = AnsiConsole.Ask<string>("[cyan]Issuer ID[/] (e.g., 57246542-96fe-1a63-...):");

        AnsiConsole.MarkupLine("[cyan]Private Key[/] (paste entire P8 file content, press Enter twice when done):");
        var privateKeyLines = new List<string>();
        
        while (true)
        {
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line) && privateKeyLines.Count > 0)
            {
                break;
            }
            if (!string.IsNullOrWhiteSpace(line))
            {
                privateKeyLines.Add(line);
            }
        }

        var privateKey = string.Join("\n", privateKeyLines);

        credentials.AppStoreConnect = new AppStoreConnectCredentials
        {
            KeyId = keyId,
            IssuerId = issuerId,
            PrivateKey = privateKey
        };

        AnsiConsole.MarkupLine("[green]✓ App Store Connect credentials configured[/]");
        AnsiConsole.WriteLine();
    }

    private async Task SetupGooglePlayCredentialsAsync(Credentials credentials)
    {
        AnsiConsole.MarkupLine("[bold yellow]Google Play Setup[/]");
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine("[bold cyan]Step 1: Create Service Account (Google Cloud Console)[/]");
        AnsiConsole.MarkupLine("[dim]1. Go to: https://console.cloud.google.com/iam-admin/serviceaccounts[/]");
        AnsiConsole.MarkupLine("[dim]2. Select/create a project (any name)[/]");
        AnsiConsole.MarkupLine("[dim]3. Click 'CREATE SERVICE ACCOUNT'[/]");
        AnsiConsole.MarkupLine("[dim]4. Enter a name (e.g., 'app-reviews')[/]");
        AnsiConsole.MarkupLine("[dim]5. SKIP the 'Permissions' step (click CONTINUE)[/]");
        AnsiConsole.MarkupLine("[dim]6. SKIP the 'Principals with access' step (click DONE)[/]");
        AnsiConsole.MarkupLine("[dim]7. Click on the service account → KEYS tab → ADD KEY → Create new key[/]");
        AnsiConsole.MarkupLine("[dim]8. Choose JSON format and download the file[/]");
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine("[bold cyan]Step 2: Enable Google Play API[/]");
        AnsiConsole.MarkupLine("[dim]1. Go to: https://console.cloud.google.com/apis/library/androidpublisher.googleapis.com[/]");
        AnsiConsole.MarkupLine("[dim]2. Click ENABLE[/]");
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine("[bold cyan]Step 3: Grant Access in Play Console (IMPORTANT!)[/]");
        AnsiConsole.MarkupLine("[dim]1. Go to: https://play.google.com/console/[/]");
        AnsiConsole.MarkupLine("[dim]2. Select 'Users and permissions' (left sidebar)[/]");
        AnsiConsole.MarkupLine("[dim]3. Click 'Invite new users'[/]");
        AnsiConsole.MarkupLine("[dim]4. Enter the service account email (from the JSON: client_email)[/]");
        AnsiConsole.MarkupLine("[dim]5. Under 'App permissions', select your app(s)[/]");
        AnsiConsole.MarkupLine("[dim]6. Check 'View app information' and 'Reply to reviews'[/]");
        AnsiConsole.MarkupLine("[dim]7. Click 'Invite user'[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[cyan]Service Account JSON[/] (paste entire JSON content, press Enter twice when done):");
        var jsonLines = new List<string>();
        
        while (true)
        {
            var line = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(line) && jsonLines.Count > 0)
            {
                break;
            }
            if (!string.IsNullOrWhiteSpace(line))
            {
                jsonLines.Add(line);
            }
        }

        var serviceAccountJson = string.Join("\n", jsonLines);

        // Validate it's valid JSON
        try
        {
            JsonDocument.Parse(serviceAccountJson);
        }
        catch
        {
            AnsiConsole.MarkupLine("[red]✗ Invalid JSON format[/]");
            return;
        }

        credentials.GooglePlay = new GooglePlayCredentials
        {
            ServiceAccountJson = serviceAccountJson
        };

        AnsiConsole.MarkupLine("[green]✓ Google Play credentials configured[/]");
        AnsiConsole.WriteLine();
    }

    private async Task SaveCredentialsAsync(Credentials credentials)
    {
        // Save to file
        await AnsiConsole.Status()
            .StartAsync("Saving credentials...", async ctx =>
            {
                try
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(_credentialsPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Serialize and save
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    var json = JsonSerializer.Serialize(credentials, options);
                    await File.WriteAllTextAsync(_credentialsPath, json);

                    // Set file permissions to user-only (Unix-like systems only)
                    if (!OperatingSystem.IsWindows())
                    {
                        try
                        {
                            File.SetUnixFileMode(_credentialsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                        }
                        catch
                        {
                            // Ignore if filesystem doesn't support Unix permissions
                        }
                    }

                    AnsiConsole.MarkupLine($"[green]✓ Credentials saved to {_credentialsPath}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Failed to save credentials: {ex.Message}[/]");
                }
            });

        // Test credentials
        if (AnsiConsole.Confirm("[yellow]Test credentials now?[/]", true))
        {
            await TestCredentialsAsync();
        }
    }

    private async Task TestCredentialsAsync()
    {
        await AnsiConsole.Status()
            .StartAsync("Testing credentials...", async ctx =>
            {
                var hasAnyValid = false;

                // Test App Store Connect
                try
                {
                    var service = new AppStoreConnectService();
                    await service.GetAppsAsync();
                    AnsiConsole.MarkupLine("[green]✓ App Store Connect credentials are valid![/]");
                    hasAnyValid = true;
                }
                catch (CredentialsException)
                {
                    // Credentials not configured, skip
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗ App Store Connect test failed: {ex.Message}[/]");
                }

                // Test Google Play
                try
                {
                    var service = new GooglePlayService();
                    AnsiConsole.MarkupLine("[green]✓ Google Play credentials are valid![/]");
                    hasAnyValid = true;
                }
                catch (CredentialsException)
                {
                    // Credentials not configured, skip
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Google Play test failed: {ex.Message}[/]");
                }

                if (!hasAnyValid)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ No valid credentials configured[/]");
                }
            });
    }

    private string MaskString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "[dim]not set[/]";
        }

        if (value.Length <= 8)
        {
            return new string('*', value.Length);
        }

        var visibleChars = 4;
        return value.Substring(0, visibleChars) + new string('*', value.Length - visibleChars * 2) + value.Substring(value.Length - visibleChars);
    }

    public async Task FetchReviewsAsync(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] App identifier required");
            AnsiConsole.MarkupLine("[dim]Usage: fetch <appId|bundleId|appName> [country][/]");
            AnsiConsole.MarkupLine("[dim]Examples:[/]");
            AnsiConsole.MarkupLine("[dim]  fetch 123456789[/]");
            AnsiConsole.MarkupLine("[dim]  fetch com.example.app[/]");
            AnsiConsole.MarkupLine("[dim]  fetch \"My App Name\"[/]");
            return;
        }

        var appQuery = arguments[0];
        var country = arguments.Length > 1 ? arguments[1] : null;

        try
        {
            var service = new OmniService();
            var request = new ReviewRequest
            {
                SortOrder = ReviewSortOrder.NewestFirst,
                Limit = 50,
                Country = country
            };

            ReviewPageResponse? response = null;
            var pageNumber = 1;

            do
            {
                // Fetch reviews with progress indicator
                response = await AnsiConsole.Status()
                    .StartAsync($"Fetching reviews (page {pageNumber})...", async ctx =>
                    {
                        return await service.GetReviewsAsync(appQuery, request);
                    });

                if (response.Reviews.Count == 0)
                {
                    if (pageNumber == 1)
                    {
                        AnsiConsole.MarkupLine("[yellow]No reviews found[/]");
                    }
                    break;
                }

                // Add to global collection
                _allFetchedReviews.AddRange(response.Reviews);

                // Display reviews
                DisplayReviews(response.Reviews, pageNumber);

                // Show pagination info
                AnsiConsole.WriteLine();
                var paginationInfo = new Panel(GetPaginationInfo(response.Pagination, _allFetchedReviews.Count))
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Grey);
                AnsiConsole.Write(paginationInfo);

                // Ask for next page
                if (response.Pagination.HasMorePages)
                {
                    if (!AnsiConsole.Confirm("[yellow]Fetch next page?[/]", true))
                    {
                        break;
                    }

                    request.Cursor = response.Pagination.NextCursor;
                    pageNumber++;
                    AnsiConsole.WriteLine();
                }
                else
                {
                    break;
                }

            } while (response?.Pagination.HasMorePages == true);

            AnsiConsole.MarkupLine($"[green]Total reviews in session: {_allFetchedReviews.Count}[/]");
            AnsiConsole.MarkupLine("[dim]Use 'export' to save all reviews to CSV[/]");
        }
        catch (CredentialsException ex)
        {
            AnsiConsole.MarkupLine($"[red]Credentials Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine("[yellow]Run 'setup' to configure credentials[/]");
        }
        catch (ApiErrorException ex)
        {
            AnsiConsole.MarkupLine($"[red]API Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine($"[dim]Status: {ex.StatusCode}, Code: {ex.ErrorCode}[/]");
        }
        catch (AppReviewFetchException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }

    private void DisplayReviews(List<AppReview> reviews, int pageNumber)
    {
        AnsiConsole.Write(new Rule($"[bold cyan]Page {pageNumber} - {reviews.Count} Reviews[/]") 
            { Justification = Justify.Left });

        foreach (var review in reviews)
        {
            DisplayReview(review);
        }
    }

    private void DisplayReview(AppReview review)
    {
        var content = new System.Text.StringBuilder();

        // Rating
        var stars = new string('★', review.Rating) + new string('☆', 5 - review.Rating);
        var ratingColor = review.Rating >= 4 ? "green" : review.Rating >= 3 ? "yellow" : "red";
        content.AppendLine($"[{ratingColor} bold]{stars}[/] ({review.Rating}/5)");

        // Metadata
        content.AppendLine($"[dim]ID: {review.Id}[/]");
        content.AppendLine($"[dim]{review.CreatedDate:yyyy-MM-dd HH:mm} • {review.Territory ?? "Unknown"} • {review.ReviewerNickname ?? "Anonymous"}[/]");
        content.AppendLine();

        // Title
        if (!string.IsNullOrWhiteSpace(review.Title))
        {
            content.AppendLine($"[bold cyan]{Markup.Escape(review.Title)}[/]");
        }

        // Body
        if (!string.IsNullOrWhiteSpace(review.Body))
        {
            content.AppendLine(Markup.Escape(review.Body));
        }

        // Developer response
        if (review.DeveloperResponse != null)
        {
            content.AppendLine();
            content.AppendLine($"[magenta bold]→ Developer Response[/] [dim]({review.DeveloperResponse.CreatedDate:yyyy-MM-dd})[/]");
            content.AppendLine($"[magenta]{Markup.Escape(review.DeveloperResponse.Body)}[/]");
        }

        var panel = new Panel(content.ToString())
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Grey);

        AnsiConsole.Write(panel);
    }

    private string GetPaginationInfo(PaginationMetadata pagination, int totalInSession)
    {
        var info = new System.Text.StringBuilder();
        
        info.AppendLine($"[bold]Session Total:[/] [cyan]{totalInSession}[/] reviews");
        
        if (pagination.TotalCount.HasValue)
        {
            info.AppendLine($"[bold]Overall Total:[/] [cyan]{pagination.TotalCount}[/] reviews");
        }

        info.AppendLine($"[bold]More Pages:[/] {(pagination.HasMorePages ? "[green]Yes[/]" : "[yellow]No[/]")}");

        return info.ToString();
    }

    public void ExportToCSV(string[] arguments)
    {
        if (_allFetchedReviews.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No reviews to export. Fetch some reviews first![/]");
            return;
        }

        var filename = arguments.Length > 0 ? arguments[0] : $"reviews_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        try
        {
            AnsiConsole.Status()
                .Start("Exporting to CSV...", ctx =>
                {
                    using var writer = new StreamWriter(filename);

                    // Write header
                    writer.WriteLine("ID,Rating,Title,Body,Reviewer,Date,Territory,HasResponse,ResponseBody,ResponseDate");

                    // Write reviews
                    foreach (var review in _allFetchedReviews)
                    {
                        var title = EscapeCSV(review.Title ?? "");
                        var body = EscapeCSV(review.Body ?? "");
                        var reviewer = EscapeCSV(review.ReviewerNickname ?? "");
                        var hasResponse = review.DeveloperResponse != null ? "Yes" : "No";
                        var responseBody = review.DeveloperResponse != null ? EscapeCSV(review.DeveloperResponse.Body) : "";
                        var responseDate = review.DeveloperResponse != null ? review.DeveloperResponse.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss") : "";

                        writer.WriteLine($"{review.Id},{review.Rating},{title},{body},{reviewer},{review.CreatedDate:yyyy-MM-dd HH:mm:ss},{review.Territory},{hasResponse},{responseBody},{responseDate}");
                    }
                });

            AnsiConsole.MarkupLine($"[green]✓ Exported {_allFetchedReviews.Count} reviews to {filename}[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Export failed: {ex.Message}[/]");
        }
    }

    private string EscapeCSV(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        // Escape quotes and wrap in quotes if contains comma, quote, or newline
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    public async Task ListAppsAsync()
    {
        try
        {
            var service = new OmniService();
            
            // Fetch apps with progress indicator
            var response = await AnsiConsole.Status()
                .StartAsync("Fetching apps...", async ctx =>
                {
                    return await service.GetAppsAsync();
                });

            // Filter out hidden apps
            var visibleApps = response.Apps.Where(a => !a.IsHidden).ToList();

            if (visibleApps.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No apps found[/]");
                if (response.Apps.Count > visibleApps.Count)
                {
                    AnsiConsole.MarkupLine($"[dim]({response.Apps.Count - visibleApps.Count} hidden app(s) not shown)[/]");
                }
                return;
            }

            // Sort apps by name
            var sortedApps = visibleApps.OrderBy(a => a.Name).ToList();

            // Display apps in a table
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Blue);

            table.AddColumn(new TableColumn("[bold cyan]App ID[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold cyan]Name[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold cyan]Bundle ID[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold cyan]Platform(s)[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold cyan]Store[/]").LeftAligned());

            foreach (var app in sortedApps)
            {
                var platforms = app.Platforms.Count > 0 
                    ? string.Join(", ", app.Platforms) 
                    : "[dim]Unknown[/]";

                table.AddRow(
                    $"[cyan]{app.Id}[/]",
                    Markup.Escape(app.Name),
                    $"[dim]{app.BundleId ?? "N/A"}[/]",
                    platforms,
                    $"[green]{app.Store}[/]"
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Total: {visibleApps.Count} app(s)[/]");
            
            if (response.Apps.Count > visibleApps.Count)
            {
                AnsiConsole.MarkupLine($"[dim]({response.Apps.Count - visibleApps.Count} hidden app(s) not shown)[/]");
            }
            
            // Display any warnings from the services
            if (response.Warnings.Count > 0)
            {
                AnsiConsole.WriteLine();
                foreach (var warning in response.Warnings)
                {
                    AnsiConsole.MarkupLine($"[yellow]ℹ {Markup.Escape(warning)}[/]");
                }
            }
            
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Use the App ID or package name with 'fetch <appId>' to get reviews[/]");
        }
        catch (CredentialsException ex)
        {
            AnsiConsole.MarkupLine($"[red]Credentials Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine("[yellow]Run 'setup' to configure credentials[/]");
        }
        catch (ApiErrorException ex)
        {
            AnsiConsole.MarkupLine($"[red]API Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine($"[dim]Status: {ex.StatusCode}, Code: {ex.ErrorCode}[/]");
        }
    }

    public async Task AddAppAsync()
    {
        try
        {
            AnsiConsole.Write(new Rule("[bold cyan]Add App Manually[/]") { Justification = Justify.Left });
            AnsiConsole.WriteLine();

            // Get store
            var store = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Select store:[/]")
                    .AddChoices(new[] { "App Store", "Google Play" }));

            // Get app details
            var name = AnsiConsole.Ask<string>("[cyan]App name:[/]");
            var bundleId = AnsiConsole.Ask<string>($"[cyan]{(store == "App Store" ? "Bundle ID" : "Package name")}:[/]");
            
            // Create app info
            var app = new AppInfo
            {
                Id = bundleId,
                Name = name,
                BundleId = bundleId,
                Store = store,
                Platforms = new List<string>()
            };

            // Optional fields
            if (AnsiConsole.Confirm("[yellow]Add project URL?[/]", false))
            {
                app.ProjectUrl = AnsiConsole.Ask<string>("[cyan]Project URL:[/]");
            }

            if (AnsiConsole.Confirm("[yellow]Add notes?[/]", false))
            {
                app.Notes = AnsiConsole.Ask<string>("[cyan]Notes:[/]");
            }

            app.IsHidden = AnsiConsole.Confirm("[yellow]Hide this app from listings?[/]", false);

            // Save to database
            var service = new OmniService();
            var database = service.GetAppDatabase();
            await database.LoadAsync();
            database.AddOrUpdateApp(app);
            await database.SaveAsync();

            AnsiConsole.MarkupLine($"[green]✓ App '{Markup.Escape(name)}' added successfully![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }

    public async Task EditAppAsync(string[] arguments)
    {
        try
        {
            AnsiConsole.Write(new Rule("[bold cyan]Edit App Metadata[/]") { Justification = Justify.Left });
            AnsiConsole.WriteLine();

            // Load database
            var service = new OmniService();
            var database = service.GetAppDatabase();
            await database.LoadAsync();

            var allApps = database.GetAllApps(includeHidden: true);
            if (allApps.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No apps in database. Use 'list' or 'add-app' first.[/]");
                return;
            }

            AppInfo app;

            // If argument provided, try to resolve it
            if (arguments.Length > 0)
            {
                var query = string.Join(" ", arguments);
                var resolved = database.ResolveApp(query, includeHidden: true);
                
                if (resolved == null)
                {
                    AnsiConsole.MarkupLine($"[red]App not found:[/] {Markup.Escape(query)}");
                    AnsiConsole.MarkupLine("[yellow]Use 'list' to see available apps or run 'edit-app' without arguments to select interactively.[/]");
                    return;
                }
                
                app = resolved;
                AnsiConsole.MarkupLine($"[green]Found:[/] {Markup.Escape(app.Name)} ({app.Store})");
                AnsiConsole.WriteLine();
            }
            else
            {
                // Interactive selection
                var appChoices = allApps
                    .Select(a => $"{a.Name} ({a.BundleId ?? a.Id}) - {a.Store}")
                    .ToList();

                var selectedChoice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan]Select app to edit:[/]")
                        .PageSize(10)
                        .AddChoices(appChoices));

                var selectedIndex = appChoices.IndexOf(selectedChoice);
                app = allApps[selectedIndex];
            }

            // Edit fields
            var editOptions = new List<string> 
            { 
                "Project URL",
                "Notes", 
                "Hide/Unhide App",
                "Done"
            };

            while (true)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[bold]Editing: {Markup.Escape(app.Name)}[/]");
                AnsiConsole.MarkupLine($"[dim]Store: {app.Store}[/]");
                AnsiConsole.MarkupLine($"[dim]Bundle ID: {app.BundleId ?? app.Id}[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"Project URL: [cyan]{app.ProjectUrl ?? "[dim]not set[/]"}[/]");
                AnsiConsole.MarkupLine($"Notes: [cyan]{app.Notes ?? "[dim]not set[/]"}[/]");
                AnsiConsole.MarkupLine($"Hidden: [cyan]{(app.IsHidden ? "Yes" : "No")}[/]");
                AnsiConsole.WriteLine();

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan]What would you like to edit?[/]")
                        .AddChoices(editOptions));

                if (choice == "Done")
                {
                    break;
                }

                switch (choice)
                {
                    case "Project URL":
                        var newUrl = AnsiConsole.Ask<string>(
                            "[cyan]Project URL (leave empty to clear):[/]",
                            app.ProjectUrl ?? string.Empty);
                        app.ProjectUrl = string.IsNullOrWhiteSpace(newUrl) ? null : newUrl;
                        break;

                    case "Notes":
                        var newNotes = AnsiConsole.Ask<string>(
                            "[cyan]Notes (leave empty to clear):[/]",
                            app.Notes ?? string.Empty);
                        app.Notes = string.IsNullOrWhiteSpace(newNotes) ? null : newNotes;
                        break;

                    case "Hide/Unhide App":
                        app.IsHidden = !app.IsHidden;
                        AnsiConsole.MarkupLine($"[green]App is now {(app.IsHidden ? "hidden" : "visible")}[/]");
                        break;
                }
            }

            // Save changes
            database.AddOrUpdateApp(app);
            await database.SaveAsync();

            AnsiConsole.MarkupLine($"[green]✓ Changes saved successfully![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }

    public async Task DeleteAppAsync(string[] arguments)
    {
        try
        {
            AnsiConsole.Write(new Rule("[bold cyan]Delete App[/]") { Justification = Justify.Left });
            AnsiConsole.WriteLine();

            // Load database
            var service = new OmniService();
            var database = service.GetAppDatabase();
            await database.LoadAsync();

            var allApps = database.GetAllApps(includeHidden: true);
            if (allApps.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No apps in database.[/]");
                return;
            }

            AppInfo app;

            // If argument provided, try to resolve it
            if (arguments.Length > 0)
            {
                var query = string.Join(" ", arguments);
                var resolved = database.ResolveApp(query, includeHidden: true);
                
                if (resolved == null)
                {
                    AnsiConsole.MarkupLine($"[red]App not found:[/] {Markup.Escape(query)}");
                    AnsiConsole.MarkupLine("[yellow]Use 'list' to see available apps or run 'delete-app' without arguments to select interactively.[/]");
                    return;
                }
                
                app = resolved;
                AnsiConsole.MarkupLine($"[green]Found:[/] {Markup.Escape(app.Name)} ({app.Store})");
                AnsiConsole.WriteLine();
            }
            else
            {
                // Interactive selection
                var appChoices = allApps
                    .Select(a => $"{a.Name} ({a.BundleId ?? a.Id}) - {a.Store}")
                    .ToList();

                var selectedChoice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan]Select app to delete:[/]")
                        .PageSize(10)
                        .AddChoices(appChoices));

                var selectedIndex = appChoices.IndexOf(selectedChoice);
                app = allApps[selectedIndex];
            }

            // Confirm deletion
            var confirmed = AnsiConsole.Confirm(
                $"[red]Are you sure you want to delete '{Markup.Escape(app.Name)}'?[/]",
                false);

            if (!confirmed)
            {
                AnsiConsole.MarkupLine("[yellow]Deletion cancelled.[/]");
                return;
            }

            // Delete app
            database.DeleteApp(app.Store, app.BundleId ?? app.Id);
            await database.SaveAsync();

            AnsiConsole.MarkupLine($"[green]✓ App '{Markup.Escape(app.Name)}' deleted successfully![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }

    public async Task RespondToReviewAsync(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Review ID required");
            AnsiConsole.MarkupLine("[dim]Usage: respond <reviewId>[/]");
            AnsiConsole.MarkupLine("[dim]Example: respond 1234567890[/]");
            AnsiConsole.MarkupLine("[dim]Review IDs are displayed when you fetch reviews.[/]");
            return;
        }

        var reviewId = arguments[0];

        try
        {
            var rule = new Rule($"[bold cyan]Respond to Review {reviewId}[/]")
            {
                Justification = Justify.Left
            };
            AnsiConsole.Write(rule);
            AnsiConsole.WriteLine();

            // Get response text from user
            var responseText = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Enter your response:[/]")
                    .Validate(text =>
                    {
                        if (string.IsNullOrWhiteSpace(text))
                            return ValidationResult.Error("[red]Response cannot be empty[/]");
                        if (text.Length > 350)
                            return ValidationResult.Error($"[red]Response too long ({text.Length}/350 characters)[/]");
                        return ValidationResult.Success();
                    }));

            // Show preview
            AnsiConsole.WriteLine();
            var preview = new Panel(Markup.Escape(responseText))
                .Header("[yellow]Preview[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Yellow);
            AnsiConsole.Write(preview);
            AnsiConsole.WriteLine();

            // Confirm
            if (!AnsiConsole.Confirm("[yellow]Send this response?[/]", true))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                return;
            }

            // Send response
            var service = new OmniService();
            ReviewResponse? response = null;
            await AnsiConsole.Status()
                .StartAsync("Sending response...", async ctx =>
                {
                    response = await service.RespondToReviewAsync(reviewId, responseText);
                });

            AnsiConsole.MarkupLine("[green]✓ Response sent successfully![/]");
            if (response != null)
            {
                AnsiConsole.MarkupLine($"[dim]Response ID: {response.Id}[/]");
            }
        }
        catch (CredentialsException ex)
        {
            AnsiConsole.MarkupLine($"[red]Credentials Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine("[yellow]Run 'setup' to configure credentials[/]");
        }
        catch (ApiErrorException ex)
        {
            AnsiConsole.MarkupLine($"[red]API Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine($"[dim]Status: {ex.StatusCode}, Code: {ex.ErrorCode}[/]");
        }
        catch (AppReviewFetchException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }

    public async Task DeleteReviewResponseAsync(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Response ID required");
            AnsiConsole.MarkupLine("[dim]Usage: delete-response <responseId>[/]");
            AnsiConsole.MarkupLine("[dim]Example: delete-response abc-123-def[/]");
            AnsiConsole.MarkupLine("[dim]Response IDs are shown when you respond to reviews.[/]");
            return;
        }

        var responseId = arguments[0];

        try
        {
            var rule = new Rule($"[bold cyan]Delete Response {responseId}[/]")
            {
                Justification = Justify.Left
            };
            AnsiConsole.Write(rule);
            AnsiConsole.WriteLine();

            // Confirm deletion
            if (!AnsiConsole.Confirm("[red]Are you sure you want to delete this response?[/]", false))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                return;
            }

            // Delete response
            var service = new OmniService();
            await AnsiConsole.Status()
                .StartAsync("Deleting response...", async ctx =>
                {
                    await service.DeleteReviewResponseAsync(responseId);
                });

            AnsiConsole.MarkupLine("[green]✓ Response deleted successfully![/]");
        }
        catch (NotSupportedException ex)
        {
            AnsiConsole.MarkupLine($"[red]Not Supported:[/] {ex.Message}");
        }
        catch (CredentialsException ex)
        {
            AnsiConsole.MarkupLine($"[red]Credentials Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine("[yellow]Run 'setup' to configure credentials[/]");
        }
        catch (ApiErrorException ex)
        {
            AnsiConsole.MarkupLine($"[red]API Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine($"[dim]Status: {ex.StatusCode}, Code: {ex.ErrorCode}[/]");
        }
        catch (AppReviewFetchException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }
}
