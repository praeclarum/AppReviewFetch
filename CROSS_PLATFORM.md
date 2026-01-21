# Cross-Platform Compatibility Checklist

## ✅ Verified Cross-Platform Components

### File System Operations
- ✅ Uses `Path.Combine()` for all path construction (cross-platform)
- ✅ Uses `Environment.GetFolderPath()` for user directories (cross-platform)
- ✅ Uses platform-specific config directories:
  - Windows: `%LOCALAPPDATA%\AppReviewFetch\Credentials.json`
  - macOS/Linux: `~/.config/AppReviewFetch/Credentials.json`
- ✅ Uses `Directory.CreateDirectory()` (creates all intermediate directories, cross-platform)
- ✅ Uses `File.Exists()`, `File.ReadAllText()`, `File.WriteAllTextAsync()` (all cross-platform)

### Unix-Specific Code (Properly Guarded)
- ✅ `File.SetUnixFileMode()` wrapped in `if (!OperatingSystem.IsWindows())` check
- ✅ Unix file permissions wrapped in try-catch to handle filesystems without Unix permissions
- ✅ No bash/shell script dependencies

### Console/Terminal Operations
- ✅ Uses Spectre.Console (cross-platform library)
- ✅ `AnsiConsole.Clear()` works on Windows, macOS, and Linux
- ✅ No platform-specific terminal escape codes
- ✅ No Console.Beep() or other Windows-specific console APIs

### HTTP/Networking
- ✅ Uses `HttpClient` (cross-platform)
- ✅ All URLs use proper URI encoding

### Cryptography
- ✅ Uses `ECDsa.Create()` and `ECDsa.ImportPkcs8PrivateKey()` (cross-platform)
- ✅ JWT token generation using Microsoft.IdentityModel.Tokens (cross-platform)

### JSON Serialization
- ✅ Uses `System.Text.Json` (cross-platform)

### Line Endings
- ✅ Uses `Environment.NewLine` or lets .NET handle it automatically
- ✅ No hardcoded `\n` or `\r\n` for file writes

## 🧪 Platform-Specific Testing

### Windows Testing
To test on Windows:
```powershell
cd AppReviewFetchCli
dotnet run
```

Expected credentials path:
```
C:\Users\<username>\AppData\Local\AppReviewFetch\Credentials.json
```

### macOS Testing
To test on macOS:
```bash
cd AppReviewFetchCli
dotnet run
```

Expected credentials path:
```
/Users/<username>/.config/AppReviewFetch/Credentials.json
```

### Linux Testing
To test on Linux:
```bash
cd AppReviewFetchCli
dotnet run
```

Expected credentials path:
```
/home/<username>/.config/AppReviewFetch/Credentials.json
```

## 📝 Platform-Specific Notes

### Windows
- File permissions are handled by NTFS/ACLs (UnixFileMode is skipped)
- Config stored in LocalApplicationData (same as %LOCALAPPDATA%)
- Paths use backslashes (automatically handled by Path.Combine)

### macOS/Linux
- File permissions set to user-read/write only (600)
- Config follows XDG Base Directory specification (~/.config)
- Paths use forward slashes (automatically handled by Path.Combine)

## 🚀 Installation as .NET Tool

The tool can be installed globally on any platform:

```bash
cd AppReviewFetchCli
dotnet pack
dotnet tool install --global --add-source ./bin/Debug AppReviewFetch.Cli
```

Then run from anywhere:
```bash
arfetch
```

Works identically on Windows (PowerShell/CMD), macOS (Terminal/bash/zsh/fish), and Linux (any shell).

## 🔍 No Platform-Specific Code Found

The following were checked and confirmed absent:
- ❌ No bash scripts
- ❌ No PowerShell scripts
- ❌ No hardcoded Unix paths (/, /home/, /Users/)
- ❌ No hardcoded Windows paths (C:\, %USERPROFILE%)
- ❌ No platform-specific Process.Start() calls
- ❌ No P/Invoke or native interop
- ❌ No platform-specific assemblies

## ✅ All Cross-Platform!

Both the library (`AppReviewFetch`) and the CLI tool (`AppReviewFetchCli`) are fully cross-platform and will work identically on:
- ✅ Windows 10/11
- ✅ macOS (Intel and Apple Silicon)
- ✅ Linux (any distribution with .NET 8.0)
