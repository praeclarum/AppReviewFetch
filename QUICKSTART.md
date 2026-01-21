# App Review Fetch - Quick Start Guide

## 🚀 Getting Started with the CLI

### 1. Build the Projects

**All Platforms:**
```bash
cd /path/to/AppReviewFetch
dotnet build
```

### 2. Run the CLI (Local Development)

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

You should see:
```
── App Review Fetch ──────────────────────────────────────
Fetch app reviews from App Store Connect and more
Type 'help' for available commands, 'exit' to quit

arfetch> 
```

### 3. Set Up Your Credentials

At the `arfetch>` prompt, type:
```
setup
```

Follow the wizard to enter:
1. **Key ID** - From App Store Connect API keys
2. **Issuer ID** - From App Store Connect API keys  
3. **Private Key** - Paste the entire contents of your .p8 file
4. **Default App ID** - (Optional) Your app's ID

Get your API credentials: https://appstoreconnect.apple.com/access/api

### 4. Check Status

```
status
```

This will show:
- ✓ Credentials file location
- ✓ Credential fields validation
- ✓ Authentication test
- Session statistics

### 5. Fetch Reviews

```
fetch YOUR_APP_ID
```

Or with country filter:
```
f YOUR_APP_ID US
```

The CLI will:
- Display reviews in a beautiful format
- Show star ratings with colors
- Include developer responses
- Offer pagination for more results

### 6. Export to CSV

After fetching reviews:
```
export
```

Or specify a filename:
```
export my-reviews.csv
```

## 📦 Install as Global Tool (Optional)

To use `arfetch` from anywhere:

```bash
cd AppReviewFetchCli
dotnet pack
dotnet tool install --global --add-source ./bin/Debug AppReviewFetch.Cli
```

Then run from anywhere:
```bash
arfetch
```

To uninstall:
```bash
dotnet tool uninstall -g AppReviewFetch.Cli
```

## 🎯 Command Reference

| Command | Short | Description |
|---------|-------|-------------|
| `help` | `h`, `?` | Show all commands |
| `status` | `s` | Check credentials & auth |
| `setup` | - | Configure credentials |
| `fetch <appId> [country]` | `f` | Fetch reviews |
| `export [filename]` | `e` | Export to CSV |
| `clear` | `cls` | Clear screen |
| `exit` | `quit`, `q` | Exit REPL |

### 🔧 Troubleshooting

### Credentials Not Found
Run `setup` to create the credentials file at:
- **Windows:** `%LOCALAPPDATA%\AppReviewFetch\Credentials.json`
- **macOS/Linux:** `~/.config/AppReviewFetch/Credentials.json`

### Authentication Failed
1. Verify your Key ID and Issuer ID are correct
2. Make sure your private key is complete (includes BEGIN/END lines)
3. Check that your API key has the right permissions in App Store Connect

### No Reviews Returned
- Verify the App ID is correct
- Check if the app has any reviews
- Try without country filter first

## 💡 Tips

- Use command aliases for speed: `f` instead of `fetch`
- Fetch multiple pages before exporting - all reviews stay in session
- Export includes developer responses
- Use `status` to verify everything is working
- The CLI uses beautiful colored output - enjoy! 🎨

## 📚 For Library Users

If you want to use AppReviewFetch as a library in your own code, see the main [README.md](../README.md) for API documentation and examples.
