using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using AppReviewFetch;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (required for MCP)
builder.Logging.AddConsole(consoleLogOptions =>
{
    // All logs go to stderr to keep stdout clean for MCP protocol
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IAppReviewService, AppStoreConnectService>();

// Configure MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
