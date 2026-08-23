# LMU Manager

One-click launcher for [Le Mans Ultimate](https://www.lemansultimate.com/) companion apps.
Starts your overlay, telemetry and hardware tools together with the game - and closes everything when you finish racing.

Inspired by iRacing Manager.

## Features

- Launch LMU plus any number of companion apps with one click
- Apps start minimized so they are out of your way
- Companion apps are closed automatically when LMU exits (graceful close first, force-kill as fallback) - including apps that were already running before launch
- Auto-detects your LMU install through Steam (all libraries), preferring the official EAC launcher
- Skips apps that are already running instead of starting a second copy
- Detects when a companion app is set to start with Windows and offers to remove it (LMU Manager starts it with your session instead)
- Optional "Start with Windows" toggle for LMU Manager itself
- Runs in the system tray during a session; close apps early or exit from the tray menu
- Dark themed UI, portable settings, no installer required

Preconfigured entries: **TinyPedal**, **GO Fast**, **SimPro Manager** - add or remove any app you like.

## Getting started

1. Download `LMUManager.exe` (self-contained, no .NET install needed) from the [releases page](../../releases) and run it.
2. Your LMU install is detected automatically - it picks the official EAC launcher (`start_protected_game.exe`) when present, so online play works as usual. Click **Auto-detect** or **Browse** to override.
3. For each companion app, click `...` and select its executable. Untick the checkbox to leave an app out of launches.
4. Hit **LAUNCH**. The window hides to the tray while you race.
5. When you quit LMU, your companion apps close automatically.

Settings are stored in `%APPDATA%\LMUManager\config.json`.

## Building and testing

Requires the .NET 8 SDK (Windows).

```
dotnet build
dotnet test
dotnet publish LMUManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full development workflow, and [docs/ci-cd.md](docs/ci-cd.md) for how CI/CD is designed.

## Command line

| Argument | Purpose |
|----------|---------|
| `--selftest` | Validates config handling and Steam detection, prints results |
| `--startuptest` | End-to-end test of startup-entry detection/removal (isolated entries, self-cleaning) |
| `--sessiontest <path-to-config.json>` | Runs a launch/close cycle headless against the given config |

## Architecture (short version)

- `MainWindow` - WPF UI, tray integration, session orchestration
- `LaunchService` - launches apps minimized, waits for the game (launcher-stub aware), closes everything on exit
- `SteamLocator` - finds LMU via registry + `libraryfolders.vdf`
- `ConfigStore` - JSON settings in `%APPDATA%\LMUManager`
- `StartupManager` - Windows autostart detection/removal (Run key + Startup folder)

## Notes

- Keep LMU Manager running while you race (it waits in the tray); closing it fully also stops the automatic cleanup.
- Not affiliated with or endorsed by Studio 397, Motorsport Games, TinyPedal, GO Fast or Simagic.
