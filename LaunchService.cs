using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace LMUManager;

public sealed class SessionResult
{
    public List<string> Started { get; } = new();
    public List<string> AlreadyRunning { get; } = new();
    public List<string> Failed { get; } = new();
    public bool GameStarted { get; set; }
    public bool GameWasAlreadyRunning { get; set; }
    public string? GameError { get; set; }
    public int ClosedCount { get; set; }
}

public static class LaunchService
{
    public static bool IsRunning(string exePath)
    {
        return SafeGetProcesses(Path.GetFileNameWithoutExtension(exePath)).Length > 0;
    }

    public static async Task<SessionResult> RunSessionAsync(
        AppConfig config, Action<string> report, CancellationToken cancellationToken,
        Action<IReadOnlyList<Process>>? onTracked = null)
    {
        var result = new SessionResult();
        var tracked = new List<Process>();

        foreach (var app in config.Apps)
        {
            if (!app.Enabled)
                continue;

            if (string.IsNullOrWhiteSpace(app.Path))
            {
                result.Failed.Add($"{app.Name}: no executable configured");
                continue;
            }

            if (!File.Exists(app.Path))
            {
                result.Failed.Add($"{app.Name}: file not found ({app.Path})");
                continue;
            }

            if (IsRunning(app.Path))
            {
                result.AlreadyRunning.Add(app.Name);
                foreach (var existing in SafeGetProcesses(Path.GetFileNameWithoutExtension(app.Path)))
                    tracked.Add(existing);
                continue;
            }

            try
            {
                var process = StartProcess(app.Path, app.Arguments, ProcessWindowStyle.Minimized);
                if (process != null)
                {
                    tracked.Add(process);
                    result.Started.Add(app.Name);
                    report($"Started {app.Name}");
                }
                else
                {
                    // ponytail: shell-launched process returned no handle; re-acquire by name so close-on-exit still works
                    var reacquired = AcquireByName(app.Path);
                    if (reacquired != null)
                    {
                        tracked.Add(reacquired);
                        result.Started.Add(app.Name);
                    }
                    else
                    {
                        result.Failed.Add($"{app.Name}: could not start");
                    }
                }
            }
            catch (Exception ex) when (ex is Win32Exception or IOException)
            {
                result.Failed.Add($"{app.Name}: {ex.Message}");
            }

            try
            {
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        onTracked?.Invoke(tracked);

        if (cancellationToken.IsCancellationRequested)
        {
            report("Stopped before launching the game.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(config.GamePath))
        {
            result.GameError = "No game executable configured.";
        }
        else if (!File.Exists(config.GamePath))
        {
            result.GameError = $"Game executable not found: {config.GamePath}";
        }
        else if (IsRunning(config.GamePath))
        {
            result.GameWasAlreadyRunning = true;
            report("LMU already running - waiting for it to exit");
        }
        else
        {
            try
            {
                var game = StartProcess(config.GamePath, config.GameArguments, ProcessWindowStyle.Normal);
                if (game == null)
                {
                    result.GameError = "Could not start the game process.";
                }
                else
                {
                    result.GameStarted = true;
                    report("Le Mans Ultimate is starting...");
                }
            }
            catch (Exception ex) when (ex is Win32Exception or IOException)
            {
                result.GameError = ex.Message;
            }
        }

        if (result.GameError == null)
        {
            var gameNames = GetGameProcessNames(config.GamePath!);
            report("Waiting for LMU to exit...");
            try
            {
                await WaitUntilAllExited(gameNames, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return result;
            }

            if (config.CloseCompanionsOnExit)
                result.ClosedCount = await CloseAllAsync(tracked).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Process names that mean "the game is still up". Covers launcher stubs
    /// (start_protected_game.exe) that spawn the real game binary and then exit.
    /// </summary>
    public static HashSet<string> GetGameProcessNames(string gameExePath)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFileNameWithoutExtension(gameExePath),
        };

        var folder = Path.GetDirectoryName(gameExePath);
        if (!string.IsNullOrEmpty(folder))
        {
            foreach (var known in SteamLocator.ExeCandidates)
            {
                if (File.Exists(Path.Combine(folder, known)))
                    names.Add(Path.GetFileNameWithoutExtension(known));
            }
        }

        return names;
    }

    private static async Task WaitUntilAllExited(HashSet<string> processNames, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            bool anyAlive = processNames.Any(IsRunningName);
            if (!anyAlive)
                return;
            await Task.Delay(2000, token).ConfigureAwait(false);
        }
    }

    private static bool IsRunningName(string processName)
    {
        try { return Process.GetProcessesByName(processName).Length > 0; }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
            return false;
        }
    }

    public static async Task<int> CloseAllAsync(IReadOnlyList<Process> processes)
    {
        int closed = 0;
        var pending = new List<Process>();

        foreach (var process in processes)
        {
            try
            {
                process.Refresh();
                if (process.HasExited)
                    continue;

                process.CloseMainWindow();
                closed++;
                pending.Add(process);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                // already gone or inaccessible - nothing left to do for this one
            }
        }

        var waitTasks = pending.Select(p => Task.Run(() =>
        {
            try { return p.WaitForExit(6000); } catch { return true; }
        }));

        await Task.WhenAll(waitTasks).ConfigureAwait(false);

        foreach (var process in pending)
        {
            try
            {
                process.Refresh();
                if (!process.HasExited)
                    KillSafe(process);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
            }
        }

        return closed;
    }

    private static Process? StartProcess(string exePath, string arguments, ProcessWindowStyle windowStyle)
    {
        var info = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
            UseShellExecute = false,
            WindowStyle = windowStyle,
        };

        return Process.Start(info);
    }

    private static Process? AcquireByName(string exePath)
    {
        var name = Path.GetFileNameWithoutExtension(exePath);
        return SafeGetProcesses(name).FirstOrDefault();
    }

    private static void KillSafe(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }

    private static Process[] SafeGetProcesses(string name)
    {
        try { return Process.GetProcessesByName(name); }
        catch (Exception ex) when (ex is InvalidOperationException or SystemException)
        {
            return Array.Empty<Process>();
        }
    }
}
