using Spectre.Console;
using AppReviewFetch;

namespace AppReviewFetchCli;

/// <summary>
/// Main REPL (Read-Eval-Print Loop) for the CLI.
/// </summary>
public class ReplLoop
{
    private readonly CommandHandler _commandHandler;
    private bool _isRunning = true;

    public ReplLoop()
    {
        _commandHandler = new CommandHandler();
    }

    public async Task RunAsync()
    {
        while (_isRunning)
        {
            // Display prompt
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold magenta]arfetch>[/] ")
                    .AllowEmpty()
            );

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            // Parse command
            var parts = ParseCommand(input);
            if (parts.Length == 0)
            {
                continue;
            }

            var command = parts[0].ToLowerInvariant();
            var arguments = parts.Skip(1).ToArray();

            // Execute command
            try
            {
                await ExecuteCommandAsync(command, arguments);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                if (ex.InnerException != null)
                {
                    AnsiConsole.MarkupLine($"[dim]{ex.InnerException.Message}[/]");
                }
            }

            AnsiConsole.WriteLine();
        }
    }

    private async Task ExecuteCommandAsync(string command, string[] arguments)
    {
        switch (command)
        {
            case "help":
            case "h":
            case "?":
                _commandHandler.ShowHelp();
                break;

            case "exit":
            case "quit":
            case "q":
                _isRunning = false;
                AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                break;

            case "status":
            case "s":
                await _commandHandler.ShowStatusAsync();
                break;

            case "setup":
                await _commandHandler.SetupCredentialsAsync();
                break;

            case "fetch":
            case "f":
                await _commandHandler.FetchReviewsAsync(arguments);
                break;

            case "unanswered":
            case "u":
                await _commandHandler.FetchUnansweredReviewsAsync(arguments);
                break;

            case "list":
            case "l":
                await _commandHandler.ListAppsAsync();
                break;

            case "list-hidden":
            case "lh":
                await _commandHandler.ListHiddenAppsAsync();
                break;

            case "add-app":
                await _commandHandler.AddAppAsync();
                break;

            case "edit-app":
                await _commandHandler.EditAppAsync(arguments);
                break;

            case "delete-app":
                await _commandHandler.DeleteAppAsync(arguments);
                break;

            case "respond":
            case "r":
                await _commandHandler.RespondToReviewAsync(arguments);
                break;

            case "delete-response":
                await _commandHandler.DeleteReviewResponseAsync(arguments);
                break;

            case "export":
            case "e":
                _commandHandler.ExportToCSV(arguments);
                break;

            case "clear":
            case "cls":
                AnsiConsole.Clear();
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Unknown command:[/] {command}");
                AnsiConsole.MarkupLine("[dim]Type 'help' for available commands[/]");
                break;
        }
    }

    private string[] ParseCommand(string input)
    {
        // Simple space-based parsing with support for quoted strings
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts.ToArray();
    }
}
