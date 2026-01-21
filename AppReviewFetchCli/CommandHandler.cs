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
        table.AddRow("fetch <appId>", "f <appId>", "Fetch reviews for an app (supports pagination)");
        table.AddRow("export [file]", "e [file]", "Export all fetched reviews to CSV");
        table.AddRow("clear", "cls", "Clear the screen");
        table.AddRow("exit", "quit, q", "Exit the application");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Examples:[/]");
        AnsiConsole.MarkupLine("  [cyan]list[/] - List all your apps");
        AnsiConsole.MarkupLine("  [cyan]fetch 123456789[/] - Fetch reviews for app 123456789");
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
            var credentials = JsonSerializer.Deserialize<AppStoreConnectCredentials>(json);

            if (credentials != null)
            {
                content.AppendLine("[bold]Credentials:[/]");
                content.AppendLine($"  Key ID: [cyan]{MaskString(credentials.KeyId)}[/]");
                content.AppendLine($"  Issuer ID: [cyan]{MaskString(credentials.IssuerId)}[/]");
                content.AppendLine($"  Private Key: {(string.IsNullOrWhiteSpace(credentials.PrivateKey) ? "[red]✗ Missing[/]" : "[green]✓ Present[/]")}");
                
                if (!string.IsNullOrWhiteSpace(credentials.AppId))
                {
                    content.AppendLine($"  Default App ID: [cyan]{credentials.AppId}[/]");
                }

                content.AppendLine();

                // Test authentication
                content.Append("[bold]Authentication:[/] ");
                
                await AnsiConsole.Status()
                    .StartAsync("Testing...", async ctx =>
                    {
                        try
                        {
                            var service = new AppStoreConnectService();
                            // The service will validate credentials when created
                            content.Append("[green]✓ Valid[/]");
                        }
                        catch (Exception ex)
                        {
                            content.Append($"[red]✗ Failed[/]");
                            content.AppendLine();
                            content.Append($"[dim]{ex.Message}[/]");
                        }
                    });
            }
            else
            {
                content.AppendLine("[red]✗ Failed to parse credentials file[/]");
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

        // Check if credentials already exist
        if (File.Exists(_credentialsPath))
        {
            if (!AnsiConsole.Confirm("[yellow]Credentials already exist. Overwrite?[/]", false))
            {
                AnsiConsole.MarkupLine("[yellow]Setup cancelled[/]");
                return;
            }
        }

        // Guided setup
        AnsiConsole.MarkupLine("[dim]You'll need your App Store Connect API credentials.[/]");
        AnsiConsole.MarkupLine("[dim]Get them from: https://appstoreconnect.apple.com/access/api[/]");
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

        var appId = AnsiConsole.Ask<string>("[cyan]Default App ID[/] (optional, press Enter to skip):", string.Empty);

        // Create credentials object
        var credentials = new AppStoreConnectCredentials
        {
            KeyId = keyId,
            IssuerId = issuerId,
            PrivateKey = privateKey,
            AppId = string.IsNullOrWhiteSpace(appId) ? null : appId
        };

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
                try
                {
                    var service = new AppStoreConnectService();
                    AnsiConsole.MarkupLine("[green]✓ Credentials are valid![/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Credentials test failed: {ex.Message}[/]");
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
            AnsiConsole.MarkupLine("[red]Error:[/] App ID required");
            AnsiConsole.MarkupLine("[dim]Usage: fetch <appId> [country][/]");
            return;
        }

        var appId = arguments[0];
        var country = arguments.Length > 1 ? arguments[1] : null;

        try
        {
            var service = new AppStoreConnectService();
            var request = new ReviewRequest
            {
                SortOrder = ReviewSortOrder.NewestFirst,
                Limit = 20,
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
                        return await service.GetReviewsAsync(appId, request);
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
                    if (!AnsiConsole.Confirm("[yellow]Fetch next page?[/]", false))
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
            var service = new AppStoreConnectService();
            
            // Fetch apps with progress indicator
            var response = await AnsiConsole.Status()
                .StartAsync("Fetching apps...", async ctx =>
                {
                    return await service.GetAppsAsync();
                });

            if (response.Apps.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No apps found[/]");
                return;
            }

            // Display apps in a table
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Blue);

            table.AddColumn(new TableColumn("[bold cyan]App ID[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold cyan]Name[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold cyan]Bundle ID[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold cyan]Platform(s)[/]").LeftAligned());
            table.AddColumn(new TableColumn("[bold cyan]Store[/]").LeftAligned());

            foreach (var app in response.Apps)
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
            AnsiConsole.MarkupLine($"[green]Total: {response.Apps.Count} app(s)[/]");
            AnsiConsole.MarkupLine("[dim]Use the App ID with 'fetch <appId>' to get reviews[/]");
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
}
