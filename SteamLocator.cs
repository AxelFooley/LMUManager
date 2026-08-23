using System.IO;
using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace LMUManager;

public static partial class SteamLocator
{
    [GeneratedRegex(@"""path""\s+""(?<p>[^""]+)""")]
    private static partial Regex LibraryPathRegex();

    // ponytail: order matters - EAC launcher first (official online flow), then real game exe, then legacy guesses
    internal static readonly string[] ExeCandidates =
    [
        "start_protected_game.exe",
        "Le Mans Ultimate.exe",
        "LMU.exe",
        "lmu.exe",
    ];

    public static string? FindGame()
    {
        foreach (var library in GetLibraries())
        {
            foreach (var candidate in ExeCandidates)
            {
                var full = Path.Combine(library, "steamapps", "common", "Le Mans Ultimate", candidate);
                if (File.Exists(full))
                    return full;
            }
        }

        return null;
    }

    internal static List<string> GetLibraries()
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<string>();

        var registryPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (!string.IsNullOrWhiteSpace(registryPath))
            candidates.Add(registryPath);

        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));

        foreach (var root in candidates)
        {
            if (!Directory.Exists(root) || !seen.Add(root))
                continue;

            found.Add(root);

            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;

            foreach (var path in ParseLibraryFolders(File.ReadAllText(vdf)))
            {
                if (Directory.Exists(path) && seen.Add(path))
                    found.Add(path);
            }
        }

        return found;
    }

    internal static List<string> ParseLibraryFolders(string vdfContent)
    {
        var paths = new List<string>();
        foreach (Match match in LibraryPathRegex().Matches(vdfContent))
        {
            var value = match.Groups["p"].Value.Replace("\\\\", "\\");
            if (value.Length > 0)
                paths.Add(value);
        }

        return paths;
    }
}
