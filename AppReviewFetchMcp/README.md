# AppReviewFetch MCP Server

A Model Context Protocol (MCP) server that provides AI assistants with access to App Store Connect and Google Play review data. This enables AI tools like GitHub Copilot, Claude, and other MCP clients to analyze app reviews, monitor sentiment, and help with customer feedback management.

## 🤖 What is MCP?

[Model Context Protocol](https://modelcontextprotocol.io/) is a standardized protocol that allows AI assistants to securely access external data sources and tools. This MCP server exposes your app review data to compatible AI clients.

## 🚀 Quick Start

### Installation

Install as a global .NET tool:

```bash
dotnet tool install -g AppReviewFetch.Mcp
```

### Prerequisites

1. **App Store Connect API Credentials** - See the [main README](../README.md#setup) for setup instructions
2. Configure credentials by running the CLI tool first:

```bash
# Install CLI if you haven't already
dotnet tool install -g AppReviewFetch.Cli

# Run setup
arfetch setup
```

## 📦 Configuration

### VS Code with GitHub Copilot

Create or edit `.vscode/mcp.json` in your workspace (or user settings):

```json
{
  "inputs": [],
  "servers": {
    "appreviewfetch": {
      "type": "stdio",
      "command": "arfetch-mcp"
    }
  }
}
```

Restart VS Code or reload the MCP servers, then toggle **Agent mode** (@ icon) in GitHub Copilot Chat to see the AppReviewFetch tools.

### Claude Desktop

Edit your Claude Desktop config file:

**macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`  
**Windows:** `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "appreviewfetch": {
      "command": "arfetch-mcp"
    }
  }
}
```

### Cline (VS Code Extension)

Add to your MCP settings:

```json
{
  "mcpServers": {
    "appreviewfetch": {
      "command": "arfetch-mcp"
    }
  }
}
```

## 🛠️ Available MCP Tools

The server exposes three powerful tools for AI assistants:

### 1. **ListApps**
Lists all apps accessible through your App Store Connect account.

**Returns:**
- App IDs (needed for other tools)
- App names
- Bundle IDs
- SKUs

**Example AI query:** *"Show me all my apps"*

### 2. **FetchReviews**
Fetches reviews for a specific app with advanced filtering and pagination.

**Parameters:**
- `appId` (required) - App Store Connect app ID
- `sortOrder` (optional) - NewestFirst (default), OldestFirst, HighestRatingFirst, LowestRatingFirst, MostHelpful
- `country` (optional) - ISO 3166-1 alpha-2 code (e.g., "US", "GB", "JP")
- `limit` (optional) - Reviews per page (1-200, default: 50)
- `cursor` (optional) - Pagination cursor from previous response

**Returns:**
- Review details (rating, title, body, date, reviewer)
- Developer responses
- Pagination info with next cursor
- Summary statistics (average rating, rating distribution)

**Example AI queries:**
- *"Fetch the latest 20 reviews for app ID 123456789"*
- *"Show me 1-star reviews from the US"*
- *"Get the next page of reviews using cursor XYZ"*

### 3. **AnalyzeReviews**
Performs comprehensive statistical analysis of app reviews.

**Parameters:**
- `appId` (required) - App Store Connect app ID
- `country` (optional) - Filter by territory
- `maxReviews` (optional) - Maximum reviews to analyze (default: 500)

**Returns:**
- Average rating and rating distribution
- Developer response rate
- 30-day trend analysis
- Sample reviews for each rating level
- Actionable recommendations

**Example AI queries:**
- *"Analyze the reviews for my app and tell me the sentiment"*
- *"What are users saying about app 123456789 in the last month?"*
- *"Give me insights on review trends"*

## 💡 Example Interactions

### With GitHub Copilot (Agent Mode)

```
You: @appreviewfetch List all my apps

Copilot: [Uses ListApps tool]
I found 3 apps:
1. MyAwesomeApp (ID: 123456789)
2. CoolTool (ID: 987654321)
3. BestApp (ID: 555555555)

You: Show me the latest negative reviews for MyAwesomeApp

Copilot: [Uses FetchReviews with sortOrder=LowestRatingFirst]
Here are the recent 1-2 star reviews...
[Analysis of common issues]

You: Analyze all reviews and give me a summary

Copilot: [Uses AnalyzeReviews]
Based on 342 reviews:
- Average rating: 4.3/5
- 68% are 5-star reviews
- Developer response rate: 45%
- Recent trend: Rating improving (+0.3 in last 30 days)
[Detailed insights...]
```

### With Claude

```
You: Can you check my App Store reviews and tell me what users are complaining about?

Claude: I'll use the AppReviewFetch MCP server to analyze your reviews.
[Uses ListApps to find your apps]
[Uses AnalyzeReviews to get insights]
[Provides detailed analysis with quotes from actual reviews]
```

## 🐳 Deployment Options

### Docker Container

The project supports containerization for remote deployment:

```bash
# Build container
dotnet publish /t:PublishContainer

# Push to registry
dotnet publish /t:PublishContainer -p ContainerRegistry=docker.io -p ContainerRepository=yourusername/appreviewfetch-mcp

# Configure in mcp.json
{
  "servers": {
    "appreviewfetch": {
      "command": "docker",
      "args": ["run", "-i", "--rm", "yourusername/appreviewfetch-mcp"],
      "env": {
        "CREDENTIALS_PATH": "/path/to/Credentials.json"
      }
    }
  }
}
```

### Azure Functions / Remote MCP

For SSE (Server-Sent Events) transport and remote hosting, see the [Azure Functions MCP samples](https://github.com/Azure-Samples/remote-mcp-functions-dotnet/).

## 🔧 Development & Testing

### Testing with MCP Inspector

```bash
# Install MCP Inspector (Node.js required)
npm install -g @modelcontextprotocol/inspector

# Test your server
mcp-inspector dotnet run --project /path/to/AppReviewFetchMcp.csproj
```

### Debugging

To debug the MCP server in VS Code:

1. Create `.vscode/launch.json`:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Attach",
      "type": "coreclr",
      "request": "attach",
      "processName": "AppReviewFetchMcp"
    }
  ]
}
```

2. Start debugging and let GitHub Copilot trigger the tools - breakpoints will work!

### Logs

All logs are sent to `stderr` to keep `stdout` clean for MCP protocol communication. Check your MCP client's logs:

- **VS Code:** MCP output panel
- **Claude:** `~/Library/Logs/Claude/mcp*.log` (macOS)

## 🎯 Best Practices for Pagination

The MCP server implements smart pagination strategies:

1. **Default page sizes** are conservative (50 reviews) for quick responses
2. **AnalyzeReviews** automatically handles pagination up to `maxReviews` limit
3. **FetchReviews** returns `nextCursor` for manual pagination control
4. AI assistants can iteratively fetch pages when deep analysis is needed

**Tips for AI prompts:**
- Start with small page sizes for quick previews
- Use filters (country, rating) to narrow results
- Let AnalyzeReviews handle auto-pagination for comprehensive stats
- Use cursors for manual multi-page exploration

## 🔐 Security Notes

- Credentials are read from the standard AppReviewFetch location
- Never expose credentials in MCP configuration
- MCP servers run locally and communicate via stdio by default
- For remote deployment, use environment variables for credentials

## 📚 Learn More

- [Model Context Protocol Documentation](https://modelcontextprotocol.io/)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [Building MCP Servers in C#](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
- [Main AppReviewFetch README](../README.md)

## 🤝 Contributing

This is part of the [AppReviewFetch](https://github.com/praeclarum/AppReviewFetch) project. Contributions welcome!

## 📄 License

MIT - See [LICENSE](../LICENSE)
