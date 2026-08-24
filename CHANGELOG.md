# Changelog

All notable changes to LMU Manager are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [SemVer](https://semver.org/).

## [1.0.1] - 2026-08-23

### Changed
- Test dependencies refreshed: Microsoft.NET.Test.Sdk 18.9.0, xunit.runner.visualstudio 4.0.0, coverlet.collector 10.0.1, plus a minor/patch group
- CI Actions refreshed: actions/checkout v7, actions/setup-dotnet v6, gitleaks-action v3, action-gh-release v3
- Release notes are generated from this changelog, with an automatic commit-log fallback

### Fixed
- Release body rendering (multiline markdown) for future releases

No application code changes - LMUManager.exe is functionally identical to v1.0.0.

## [1.0.0] - 2026-08-23

### Added
- One-click launch of Le Mans Ultimate together with companion apps (preconfigured: TinyPedal, GO Fast, SimPro Manager)
- Automatic close of all configured companion apps when the game exits, including apps that were already running before launch
- Graceful window-close first, force-kill fallback for companion shutdown
- Steam library auto-detection (all libraries via `libraryfolders.vdf`), preferring the official EAC launcher (`start_protected_game.exe`)
- Launcher-stub aware session waiting: the real game process is tracked even when the launcher exits early
- Windows startup integration: detects companion apps configured to autostart and offers removal; "Start with Windows" toggle for LMU Manager itself
- System tray presence during sessions with "Stop companion apps" and "Exit" actions
- Single-instance guard; dark themed WPF UI; portable JSON settings in `%APPDATA%\LMUManager`
- Custom application icon (multi-resolution, Le Mans racing theme) for the exe, window, taskbar and tray
- Headless modes for automation: `--selftest`, `--startuptest`, `--sessiontest <config>`
- CI/CD: parallel build / unit tests / integration tests / CodeQL / Gitleaks / security gate on PRs to `dev` and `main`; blocking on high/critical findings; `main` accepts merges from `dev` only
