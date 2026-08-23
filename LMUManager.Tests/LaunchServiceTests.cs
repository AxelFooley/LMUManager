using System.IO;
using LMUManager;

namespace LMUManager.Tests;

public class LaunchServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lmu_tests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string CreateFile(string name)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "dummy");
        return path;
    }

    [Fact]
    public void GetGameProcessNames_IncludesLauncherAndRealGame()
    {
        var launcher = CreateFile("start_protected_game.exe");
        CreateFile("Le Mans Ultimate.exe");

        var names = LaunchService.GetGameProcessNames(launcher);

        Assert.Equal(["Le Mans Ultimate", "start_protected_game"], names.OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void GetGameProcessNames_OnlyConfiguredExe_WhenNoKnownSiblings()
    {
        var game = CreateFile("custom_game.exe");

        var names = LaunchService.GetGameProcessNames(game);

        var single = Assert.Single(names);
        Assert.Equal("custom_game", single);
    }

    [Fact]
    public void IsRunning_NonExistentProcess_ReturnsFalse()
    {
        Assert.False(LaunchService.IsRunning(@"C:\definitely\not\running_lmu_manager_test_process.exe"));
    }
}
