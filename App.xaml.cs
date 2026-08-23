using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace LMUManager;

public partial class App : Application
{
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(
        string name, uint access, uint shareMode, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetStdHandle(int stdHandleId, IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int stdHandleId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr handle);

    private const uint GenericWrite = 0x40000000;
    private const int StdOutputHandle = -11;
    private const uint FileTypeDisk = 0x0001;
    private const uint FileTypeChar = 0x0002;
    private const uint FileTypePipe = 0x0003;

    /// <summary>
    /// CLI output setup. AttachConsole overwrites redirected standard handles with the
    /// console's, so the original stdout must be captured before attaching and restored
    /// for real file redirects. Hosted-shell pipes (never drained by anyone) are routed
    /// to the attached console instead.
    /// </summary>
    private static void ConfigureCliOutput()
    {
        var original = GetStdHandle(StdOutputHandle);
        bool originalValid = original != IntPtr.Zero && original != new IntPtr(-1);
        uint originalType = originalValid ? GetFileType(original) : 0;

        bool hasConsole = AttachConsole(AttachParentProcess);
        if (!hasConsole)
            return;

        if (originalType == FileTypeDisk)
        {
            SetStdHandle(StdOutputHandle, original);
            ResetConsoleWriter();
            return;
        }

        if (originalType == FileTypePipe)
        {
            ReopenConOut();
            return;
        }

        var current = GetStdHandle(StdOutputHandle);
        bool currentIsConsole = current != IntPtr.Zero && current != new IntPtr(-1)
            && GetFileType(current) == FileTypeChar;
        if (!currentIsConsole)
            ReopenConOut();
    }

    private static void ReopenConOut()
    {
        var handle = CreateFileW("CONOUT$", GenericWrite, shareMode: 2, IntPtr.Zero,
            disposition: 3 /* OPEN_EXISTING */, flags: 0, IntPtr.Zero);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return;

        SetStdHandle(StdOutputHandle, handle);
        ResetConsoleWriter();
    }

    private static void ResetConsoleWriter()
        => Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding) { AutoFlush = true });

    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureCliOutput();

        // ponytail: CLI modes print results and exit codes only - no dialogs, they can hang unattended runs
        if (e.Args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            int code = SelfTest.Run(Console.Out);
            Console.Out.WriteLine(code == 0 ? "SELFTEST OK" : "SELFTEST FAILED");
            Console.Out.Flush();
            Environment.Exit(code);
            return;
        }

        if (e.Args.Any(a => a.Equals("--startuptest", StringComparison.OrdinalIgnoreCase)))
        {
            int code = SelfTest.RunStartup(Console.Out);
            Console.Out.WriteLine(code == 0 ? "STARTUPTEST OK" : "STARTUPTEST FAILED");
            Console.Out.Flush();
            Environment.Exit(code);
            return;
        }

        if (e.Args.Length >= 2 && e.Args[0].Equals("--sessiontest", StringComparison.OrdinalIgnoreCase))
        {
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(e.Args[1]));
            var result = LaunchService
                .RunSessionAsync(config ?? new AppConfig(), s => Console.Out.WriteLine(s), CancellationToken.None)
                .GetAwaiter().GetResult();

            Console.Out.WriteLine($"started={result.Started.Count} skipped={result.AlreadyRunning.Count} failed={result.Failed.Count} closed={result.ClosedCount} gameError={result.GameError ?? "none"}");
            foreach (var failure in result.Failed)
                Console.Out.WriteLine("FAILED: " + failure);
            Console.Out.Flush();
            Environment.Exit(result.Failed.Count == 0 && result.GameError == null ? 0 : 1);
            return;
        }

        _singleInstanceMutex = new Mutex(true, @"Local\LMUManager_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("LMU Manager is already running (check the system tray).",
                "LMU Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
