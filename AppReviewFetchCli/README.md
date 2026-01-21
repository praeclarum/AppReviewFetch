# App Review Fetch CLI

A beautiful, interactive REPL tool for fetching app reviews from App Store Connect.

## Installation

### As a .NET Tool (Global)

```bash
cd AppReviewFetchCli
dotnet pack
dotnet tool install --global --add-source ./bin/Debug AppReviewFetch.Cli
```

Then run from anywhere:
```bash
arfetch
```

### Run Locally (Development)

**Windows (PowerShell/CMD):**
```
cd AppReviewFetchCli
dotnet run
```

**macOS/Linux:**
```bash
cd AppReviewFetchCli
dotnet run
```

## Quick Start

1. **Launch the REPL:**
   ```bash
   arfetch
   ```

2. **Set up your credentials:**
   ```
   arfetch> setup
   ```
   Follow the interactive wizard to enter your App Store Connect API credentials.

3. **Fetch reviews:**
   ```
   arfetch> fetch 123456789
   ```

## Commands

| Command | Aliases | Description |
|---------|---------|-------------|
| `help` | `h`, `?` | Show help message with all commands |
| `status` | `s` | Show credentials and authentication status |
| `setup` | | Interactive wizard to configure credentials |
| `list` | `l` | List all apps accessible with current credentials |
| `fetch <appId>` | `f <appId>` | Fetch reviews for an app (supports pagination) |
| `export [file]` | `e [file]` | Export all fetched reviews to CSV |
| `clear` | `cls` | Clear the screen |
| `exit` | `quit`, `q` | Exit the application |

## Examples

### List Your Apps
```
arfetch> list
```
Displays a table of all apps accessible with your credentials:
- App ID (use this for fetching reviews)
- App Name
- Bundle ID
- Platform(s)
- Store

### Check Status
```
arfetch> status
```
Shows:
- ✓ Credentials file location and existence
- ✓ Validation of credentials fields
- ✓ Authentication test
- Session statistics

### Fetch Reviews
```
arfetch> fetch 123456789
```
Fetches reviews for app ID `123456789` with:
- Beautiful, colored output
- Star ratings visualization
- Developer responses included
- Interactive pagination

Filter by country:
```
arfetch> f 123456789 US
```

### Export to CSV
After fetching reviews, export them:
```
arfetch> export
```

Or specify a filename:
```
arfetch> export my-reviews.csv
```

CSV includes:
- ID, Rating, Title, Body
- Reviewer, Date, Territory
- Developer response (if any)

## Features

✅ **Interactive REPL** - Natural command-line experience  
✅ **Beautiful Output** - Colored, formatted display with Spectre.Console  
✅ **Setup Wizard** - Guided credentials configuration  
✅ **Status Checking** - Validate credentials and authentication  
✅ **Pagination** - Interactive page-by-page fetching  
✅ **CSV Export** - Export all reviews in session  
✅ **Session Tracking** - Keep all reviews in memory for export  
✅ **Error Handling** - Clear, helpful error messages  

## Credentials Setup

The `setup` command walks you through creating your credentials file:

1. Key ID (from App Store Connect)
2. Issuer ID (from App Store Connect)
3. Private Key (paste entire P8 file content)
4. Default App ID (optional)

Credentials are saved to:
- **Windows:** `%LOCALAPPDATA%\AppReviewFetch\Credentials.json`
- **macOS/Linux:** `~/.config/AppReviewFetch/Credentials.json`
★★★★★ (5/5)
2026-01-15 10:30 • US • JohnDoe

Amazing App!
This app is fantastic and works perfectly. Highly recommended!

→ Developer Response (2026-01-16)
Thank you for your kind words! We're thrilled you're enjoying the app.
```

## Tips

- Use short aliases for faster commands: `f` instead of `fetch`, `s` instead of `status`
- Fetch multiple pages and export all at once
- The CLI keeps all fetched reviews in memory during the session
- Use `clear` to clean up the screen between operations

## Uninstall

```bash
dotnet tool uninstall -g AppReviewFetch.Cli
```
