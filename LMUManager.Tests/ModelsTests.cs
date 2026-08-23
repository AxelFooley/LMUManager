using System.IO;
using LMUManager;

namespace LMUManager.Tests;

public class ModelsTests
{
    [Fact]
    public void CreateDefault_SeedsSupportedCompanionApps()
    {
        var config = AppConfig.CreateDefault();

        var names = config.Apps.Select(a => a.Name).ToArray();
        Assert.Equal(["TinyPedal", "GO Fast", "SimPro Manager"], names);
        Assert.All(config.Apps, a => Assert.True(a.Enabled));
    }

    [Fact]
    public void NewApp_DefaultsToEnabled()
    {
        var app = new CompanionApp();

        Assert.True(app.Enabled);
        Assert.Equal("", app.Path);
    }
}
