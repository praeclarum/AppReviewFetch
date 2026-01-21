using Spectre.Console;

namespace AppReviewFetchCli;

/// <summary>
/// Main entry point for the App Review Fetch CLI tool.
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Display welcome banner
        DisplayBanner();

        // Create the REPL
        var repl = new ReplLoop();
        
        // Run the REPL
        await repl.RunAsync();

        return 0;
    }

    private static void DisplayBanner()
    {
        var rule = new Rule("[bold cyan]App Review Fetch[/]")
        {
            Justification = Justify.Left
        };
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine("[dim]Fetch app reviews from App Store Connect and more[/]");
        AnsiConsole.MarkupLine("[dim]Type 'help' for available commands, 'exit' to quit[/]");
        AnsiConsole.WriteLine();
    }
}
