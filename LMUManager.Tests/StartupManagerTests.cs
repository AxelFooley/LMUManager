using System.IO;
using LMUManager;

namespace LMUManager.Tests;

public class StartupManagerTests
{
    [Theory]
    [InlineData(@"""C:\Program Files\TinyPedal\tinypedal.exe"" -flag", @"C:\Tools\TinyPedal\tinypedal.exe", true)]
    [InlineData(@"C:\Program Files\TinyPedal\tinypedal.exe", @"C:\Tools\TinyPedal\tinypedal.exe", true)]
    [InlineData(@"C:\Tools/TinyPedal/tinypedal.exe -x", @"C:\Tools\TinyPedal\tinypedal.exe", true)]
    [InlineData(@"tinypedal.exe", @"C:\Tools\TinyPedal\tinypedal.exe", true)]
    [InlineData(@"""C:\Program Files\TinyPedal\tinypedal.exe""", @"C:\Other\pad.exe", false)]
    [InlineData(@"C:\Program Files\TinyPedal\tinypedal.exe", @"C:\Other\pad.exe", false)]
    [InlineData(@"""C:\Program Files\SimPro\manager.exe""", @"C:\Tools\TinyPedal\tinypedal.exe", false)]
    [InlineData("", @"C:\Tools\app.exe", false)]
    public void CommandReferencesExe_MatchesPathSegmentsOnly(string command, string exePath, bool expected)
    {
        Assert.Equal(expected, StartupManager.CommandReferencesExe(command, Path.GetFileName(exePath)));
    }
}
