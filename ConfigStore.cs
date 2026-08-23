using System.IO;
using System.Text.Json;

namespace LMUManager;

public static class ConfigStore
{
    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LMUManager");

    public static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppConfig Load() => LoadFrom(ConfigDirectory);

    public static void Save(AppConfig config) => SaveTo(ConfigDirectory, config);

    public static AppConfig LoadFrom(string directory)
    {
        var file = Path.Combine(directory, "config.json");
        try
        {
            if (File.Exists(file))
            {
                var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(file));
                if (config != null)
                {
                    config.Apps ??= new();
                    return config;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            TryBackup(file);
        }

        return AppConfig.CreateDefault();
    }

    public static void SaveTo(string directory, AppConfig config)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "config.json"), JsonSerializer.Serialize(config, JsonOptions));
    }

    private static void TryBackup(string file)
    {
        try
        {
            if (File.Exists(file))
                File.Copy(file, file + ".bak", overwrite: true);
        }
        catch (IOException)
        {
        }
    }
}
