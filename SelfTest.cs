using System.IO;
using Microsoft.Win32;

namespace LMUManager;

public static class SelfTest
{
    private const string TestRunKeyPath = @"Software\LMUManager_SelfTest_Run";

    public static int RunStartup(TextWriter output)
    {
        int failures = 0;

        void Check(string name, bool condition, string detail = "")
        {
            if (condition)
                output.WriteLine($"[PASS] {name}");
            else
            {
                failures++;
                output.WriteLine($"[FAIL] {name} {detail}");
            }
        }

        var fakeExe = @"C:\__lmu_selftest__\fakeapp.exe";

        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(TestRunKeyPath, writable: true))
            {
                key.SetValue("LMUManager_SelfTest_Fake", $"\"{fakeExe}\" -quiet");
            }

            Check("detects Run-key entry for exe",
                StartupManager.IsAppInStartup(fakeExe, TestRunKeyPath, StartupManager.StartupFolder));

            Check("ignores unrelated Run-key entries",
                !StartupManager.IsAppInStartup(@"C:\other\pad.exe", TestRunKeyPath, StartupManager.StartupFolder));

            int removed = StartupManager.RemoveAppFromStartup(fakeExe, TestRunKeyPath, StartupManager.StartupFolder);
            Check("removes Run-key entry", removed == 1, $"removed {removed}");
            Check("entry gone after removal",
                !StartupManager.IsAppInStartup(fakeExe, TestRunKeyPath, StartupManager.StartupFolder));

            var tempLinks = Path.Combine(Path.GetTempPath(), "LMUManager_SelfTest_Startup");
            Directory.CreateDirectory(tempLinks);
            var linkPath = Path.Combine(tempLinks, "fakeapp.lnk");
            File.WriteAllText(linkPath, "not a real lnk - name matching only");
            try
            {
                Check("detects Startup-folder link",
                    StartupManager.IsAppInStartup(fakeExe, TestRunKeyPath + "_missing", tempLinks));
                int linkRemoved = StartupManager.RemoveAppFromStartup(fakeExe, TestRunKeyPath + "_missing", tempLinks);
                Check("removes Startup-folder link", linkRemoved == 1 && !File.Exists(linkPath));
            }
            finally
            {
                try { Directory.Delete(tempLinks, recursive: true); } catch (IOException) { }
            }

            StartupManager.SetManagerInStartup(true);
            bool managerWasAdded = StartupManager.IsManagerInStartup();
            StartupManager.SetManagerInStartup(false);
            bool managerRemoved = !StartupManager.IsManagerInStartup();
            Check("manager toggle adds+removes Run value", managerWasAdded && managerRemoved);
        }
        catch (Exception ex)
        {
            Check("startup selftest completes", false, ex.Message);
        }
        finally
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(TestRunKeyPath, writable: true);
                key?.DeleteValue("LMUManager_SelfTest_Fake", throwOnMissingValue: false);
                if (key != null && key.GetValueNames().Length == 0)
                    Registry.CurrentUser.DeleteSubKey(TestRunKeyPath);
            }
            catch (Exception ex) when (ex is IOException or SystemException)
            {
            }
        }

        return failures == 0 ? 0 : 1;
    }

    public static int Run(TextWriter output)
    {
        int failures = 0;

        void Check(string name, bool condition, string detail = "")
        {
            if (condition)
                output.WriteLine($"[PASS] {name}");
            else
            {
                failures++;
                output.WriteLine($"[FAIL] {name} {detail}");
            }
        }

        var vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                }
                "1"
                {
                    "path"		"D:\\Games\\Steam2"
                }
            }
            """;

        var parsed = SteamLocator.ParseLibraryFolders(vdf);
        Check("vdf parses 2 library paths", parsed.Count == 2, $"got {parsed.Count}");
        Check("vdf unescapes backslashes", parsed.Count == 2 && parsed[1] == @"D:\Games\Steam2",
              parsed.Count == 2 ? $"got '{parsed[1]}'" : "");

        try
        {
            _ = SteamLocator.GetLibraries();
            Check("GetLibraries does not throw", true);
        }
        catch (Exception ex)
        {
            Check("GetLibraries does not throw", false, ex.Message);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "LMUManager_SelfTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            ConfigStore.SaveTo(tempDir, AppConfig.CreateDefault());
            var loaded = ConfigStore.LoadFrom(tempDir);

            Check("config round trip keeps game path empty", loaded.GamePath == "");
            Check("config round trip keeps 3 default apps", loaded.Apps.Count == 3,
                  $"got {loaded.Apps.Count}");
            Check("default apps are TinyPedal / GO Fast / SimPro Manager",
                  loaded.Apps.Count == 3
                  && loaded.Apps[0].Name == "TinyPedal"
                  && loaded.Apps[1].Name == "GO Fast"
                  && loaded.Apps[2].Name == "SimPro Manager");

            loaded.GamePath = @"X:\fake\lmu.exe";
            loaded.Apps.Add(new CompanionApp { Name = "Extra", Path = @"X:\fake\tool.exe" });
            ConfigStore.SaveTo(tempDir, loaded);
            var reloaded = ConfigStore.LoadFrom(tempDir);
            Check("config round trip preserves edits",
                  reloaded.GamePath == @"X:\fake\lmu.exe" && reloaded.Apps.Count == 4);

            var corruptDir = Path.Combine(tempDir, "corrupt");
            Directory.CreateDirectory(corruptDir);
            File.WriteAllText(Path.Combine(corruptDir, "config.json"), "{ not json !!!");
            var recovered = ConfigStore.LoadFrom(corruptDir);
            Check("corrupt config falls back to defaults", recovered.Apps.Count == 3 && File.Exists(Path.Combine(corruptDir, "config.json.bak")));
        }
        catch (Exception ex)
        {
            Check("config round trip completes", false, ex.Message);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch (IOException) { }
        }

        var detected = SteamLocator.FindGame();
        output.WriteLine(detected is null
            ? "[INFO] LMU install not found on this machine (fine on dev boxes)"
            : $"[INFO] LMU found: {detected}");

        return failures == 0 ? 0 : 1;
    }
}
