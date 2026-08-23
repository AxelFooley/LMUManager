using System.IO;
using LMUManager;

namespace LMUManager.Tests;

public class SteamLocatorTests
{
    private const string SampleVdf = """
        "libraryfolders"
        {
            "0"
            {
                "path"		"C:\\Program Files (x86)\\Steam"
            }
            "1"
            {
                "path"		"D:\\Games\\SteamLibrary"
            }
        }
        """;

    [Fact]
    public void ParseLibraryFolders_ExtractsAllPaths()
    {
        var paths = SteamLocator.ParseLibraryFolders(SampleVdf);

        Assert.Equal(2, paths.Count);
    }

    [Fact]
    public void ParseLibraryFolders_UnescapesBackslashes()
    {
        var paths = SteamLocator.ParseLibraryFolders(SampleVdf);

        Assert.Contains(@"D:\Games\SteamLibrary", paths);
    }

    [Fact]
    public void ParseLibraryFolders_EmptyContent_ReturnsEmpty()
    {
        Assert.Empty(SteamLocator.ParseLibraryFolders(""));
    }

    [Fact]
    public void GetLibraries_DoesNotThrow()
    {
        var libraries = SteamLocator.GetLibraries();

        Assert.NotNull(libraries);
    }
}
