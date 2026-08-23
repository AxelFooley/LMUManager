using System.IO;
using LMUManager;

namespace LMUManager.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lmu_tests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void RoundTrip_PreservesConfig()
    {
        var config = new AppConfig
        {
            GamePath = @"X:\games\Le Mans Ultimate\lmu.exe",
            GameArguments = "-flag",
            CloseCompanionsOnExit = false,
            Apps = new()
            {
                new CompanionApp { Name = "TinyPedal", Path = @"X:\tools\tinypedal.exe", Arguments = "-p", Enabled = false },
            },
        };

        ConfigStore.SaveTo(_dir, config);
        var loaded = ConfigStore.LoadFrom(_dir);

        Assert.Equal(config.GamePath, loaded.GamePath);
        Assert.Equal(config.GameArguments, loaded.GameArguments);
        Assert.False(loaded.CloseCompanionsOnExit);
        var app = Assert.Single(loaded.Apps);
        Assert.Equal("TinyPedal", app.Name);
        Assert.False(app.Enabled);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var config = ConfigStore.LoadFrom(_dir);

        Assert.Equal("", config.GamePath);
        Assert.Equal(3, config.Apps.Count);
    }

    [Fact]
    public void Load_CorruptFile_FallsBackToDefaultsAndBacksUp()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "config.json"), "{ definitely not json");

        var config = ConfigStore.LoadFrom(_dir);

        Assert.Equal(3, config.Apps.Count);
        Assert.True(File.Exists(Path.Combine(_dir, "config.json.bak")));
    }
}
