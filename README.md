# WinLive

WinLive is a Windows 11 Live Activity prototype: a small Fluent-style island that appears above the notification area when there is useful ongoing information.

## MVP Features

- WPF transparent island, always-on-top and hidden from the taskbar.
- Aqua Dynamics-style expansion with same-size tiles that can unfold upward or downward.
- Media activity source through Windows Global System Media Transport Controls.
- Localhost JSON API for supported tools and apps to publish progress.
- Experimental UI Automation progress detector, disabled by default.
- Tray menu with settings, reset position, and exit.
- Drag the island itself to move it; use the tray reset command to return it near the notification area.
- Full-screen suppression for games and videos.
- Short startup hint so a successful launch is visible even when no activity is active.

## Build

```powershell
dotnet build WinLive.slnx
dotnet test WinLive.slnx
dotnet run --project WinLive.App
```

WinLive targets .NET SDK `10.0.202` and Windows 11 first.

## Publish

WPF apps can be published the same way as other .NET desktop apps. For a single executable that also carries the .NET runtime:

```powershell
.\scripts\publish-onefile.ps1
```

The output is written to `artifacts\publish\win-x64\WinLive_v1.0.0.exe`.

To publish a different versioned filename:

```powershell
.\scripts\publish-onefile.ps1 -Version 1.0.1
```

That creates `WinLive_v1.0.1.exe`.

For a smaller executable that requires the target PC to have the .NET 10 Desktop Runtime installed:

```powershell
.\scripts\publish-onefile.ps1 -FrameworkDependent
```

Equivalent manual command:

```powershell
dotnet publish WinLive.App\WinLive.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None `
  -p:DebugSymbols=false
```

Diagnostics logging is disabled by default for v1 performance. To collect startup/window logs:

```powershell
$env:WINLIVE_DIAGNOSTICS = "1"
dotnet run --project WinLive.App
```

## Local API

The API listens on `http://127.0.0.1:8765` by default. The bearer token is generated on first launch and is visible in the settings window.

```powershell
$token = "<token from settings>"
$headers = @{ Authorization = "Bearer $token" }

Invoke-RestMethod `
  -Method Put `
  -Uri "http://127.0.0.1:8765/api/v1/activities/demo-download" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body '{
    "type": "download",
    "state": "active",
    "title": "Demo download",
    "subtitle": "42%",
    "progress": 0.42,
    "priority": 40,
    "sourceApp": { "name": "PowerShell" }
  }'
```

Endpoints:

- `GET /api/v1/health`
- `GET /api/v1/activities`
- `PUT /api/v1/activities/{id}`
- `PATCH /api/v1/activities/{id}`
- `DELETE /api/v1/activities/{id}`

You can also run the demo client:

```powershell
dotnet run --project tools/WinLive.ApiDemo -- --token "<token from settings>"
```

## Known Limits

- Existing taskbar progress from arbitrary apps is not generally readable through a public Windows API.
- UI Automation progress detection is best-effort, opt-in, and may miss apps or create noisy beta activities.
- API server, port, token, and experimental detector changes are saved immediately but require restarting WinLive to fully apply.
- Initial placement is tuned for a bottom Windows 11 taskbar and notification-area-adjacent display.
- When there is no media or progress, WinLive intentionally hides the island and remains in the tray.
