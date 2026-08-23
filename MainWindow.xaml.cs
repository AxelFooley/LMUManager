using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace LMUManager;

public partial class MainWindow : Window
{
    private readonly AppConfig _config;
    private CancellationTokenSource? _sessionCts;
    private IReadOnlyList<Process>? _trackedCompanions;
    private bool _sessionActive;
    private bool _reallyExit;

    private readonly Forms.NotifyIcon _trayIcon = new()
    {
        Icon = System.Drawing.SystemIcons.Application,
        Visible = true,
        Text = "LMU Manager",
    };

    public MainWindow()
    {
        InitializeComponent();
        _config = ConfigStore.Load();
        DataContext = _config;
        ConfigStore.Save(_config);
        ConfigPathText.Text = ConfigStore.ConfigFilePath;

        _initializingUi = true;
        StartWithWindowsCheck.IsChecked = StartupManager.IsManagerInStartup();
        _initializingUi = false;

        BuildTrayMenu();
        AutoDetectGameIfMissing();

        Closing += Window_Closing;
        Closed += (_, _) => DisposeTray();
    }

    private bool _initializingUi;

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializingUi)
            return;

        StartupManager.SetManagerInStartup(StartWithWindowsCheck.IsChecked == true);
        SetStatus(StartWithWindowsCheck.IsChecked == true
            ? "LMU Manager will start with Windows."
            : "LMU Manager removed from Windows startup.", muted: true);
    }

    private void BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => RestoreFromTray());
        if (_sessionActive)
            menu.Items.Add("Stop companion apps", null, (_, _) => StopSession());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void AutoDetectGameIfMissing()
    {
        if (!string.IsNullOrWhiteSpace(_config.GamePath) && File.Exists(_config.GamePath))
            return;

        var found = SteamLocator.FindGame();
        if (found != null)
        {
            _config.GamePath = found;
            ConfigStore.Save(_config);
            StatusText.Text = "LMU detected automatically.";
        }
    }

    // ---------- actions ----------

    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionActive)
            return;

        ConfigStore.Save(_config);

        if (string.IsNullOrWhiteSpace(_config.GamePath) || !File.Exists(_config.GamePath))
        {
            Warn("Set the path to lmu.exe first (use Auto-detect or Browse).");
            return;
        }

        var broken = _config.Apps
            .Where(a => a.Enabled && !string.IsNullOrWhiteSpace(a.Path) && !File.Exists(a.Path))
            .Select(a => a.Name)
            .ToList();
        if (broken.Count > 0 && MessageBox.Show(
                $"These apps have invalid paths and will be skipped:\n\n{string.Join("\n", broken)}\n\nStart anyway?",
                "LMU Manager", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _sessionActive = true;
        _sessionCts = new CancellationTokenSource();
        LaunchButton.IsEnabled = false;
        SetStatus("Launching companion apps...", muted: false);
        BuildTrayMenu();

        try
        {
            var result = await Task.Run(
                () => LaunchService.RunSessionAsync(_config,
                    s => Dispatcher.Invoke(() => SetStatus(s, muted: false)),
                    _sessionCts.Token,
                    list => _trackedCompanions = list));

            if (result.Failed.Count > 0)
                Warn($"Some apps failed to start:\n{string.Join("\n", result.Failed)}");

            if (result.GameError != null)
            {
                Warn(result.GameError);
            }
            else
            {
                var closed = _config.CloseCompanionsOnExit ? $" Closed {result.ClosedCount} companion app(s)." : "";
                var message = $"Session ended.{closed}";
                SetStatus(message, muted: true);
                ShowBalloon("Session over", message);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("Stopped.", muted: true);
        }
        catch (Exception ex)
        {
            Warn($"Unexpected error: {ex.Message}");
        }
        finally
        {
            _sessionActive = false;
            _sessionCts.Dispose();
            _sessionCts = null;
            _trackedCompanions = null;
            LaunchButton.IsEnabled = true;
            BuildTrayMenu();
            ConfigStore.Save(_config);
        }
    }

    private void DetectGame_Click(object sender, RoutedEventArgs e)
    {
        var found = SteamLocator.FindGame();
        if (found == null)
        {
            Warn("Could not find Le Mans Ultimate automatically. Use Browse and pick lmu.exe.");
            return;
        }

        _config.GamePath = found;
        ConfigStore.Save(_config);
        SetStatus($"Found: {found}", muted: true);
    }

    private void BrowseGame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Executables|*.exe", Title = "Locate lmu.exe" };
        if (dialog.ShowDialog(this) == true)
        {
            _config.GamePath = dialog.FileName;
            ConfigStore.Save(_config);
        }
    }

    private void BrowseApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CompanionApp app })
            return;

        var dialog = new OpenFileDialog { Filter = "Executables|*.exe", Title = "Locate application" };
        if (dialog.ShowDialog(this) == true)
        {
            app.Path = dialog.FileName;
            if (string.IsNullOrWhiteSpace(app.Name) || IsDefaultName(app.Name))
                app.Name = Path.GetFileNameWithoutExtension(dialog.FileName);
            ConfigStore.Save(_config);

            if (StartupManager.IsAppInStartup(dialog.FileName))
            {
                var choice = MessageBox.Show(
                    $"{app.Name} is configured to start with Windows.\n\n" +
                    "Remove it from Windows startup? LMU Manager will start it for you when you launch a session instead.",
                    "Startup app detected", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (choice == MessageBoxResult.Yes)
                {
                    int removed = StartupManager.RemoveAppFromStartup(dialog.FileName);
                    SetStatus(removed > 0
                        ? $"Removed {app.Name} from Windows startup ({removed} entr{(removed == 1 ? "y" : "ies")})."
                        : $"Could not remove {app.Name} from startup.", muted: true);
                }
            }
        }
    }

    private void AddApp_Click(object sender, RoutedEventArgs e)
    {
        _config.Apps.Add(new CompanionApp { Name = "New app" });
        ConfigStore.Save(_config);
    }

    private void RemoveApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CompanionApp app })
            return;

        _config.Apps.Remove(app);
        ConfigStore.Save(_config);
    }

    // ---------- tray / lifecycle ----------

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_reallyExit || !_sessionActive)
            return;

        e.Cancel = true;
        HideToTray("Still racing - LMU Manager will close your apps when the session ends.");
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void HideToTray(string tipMessage)
    {
        Hide();
        ShowBalloon("LMU Manager in tray", tipMessage);
    }

    private void StopSession()
    {
        _sessionCts?.Cancel();
        SetStatus("Stopping companion apps...", muted: true);

        var tracked = _trackedCompanions;
        if (tracked == null || tracked.Count == 0)
            return;

        _ = LaunchService.CloseAllAsync(tracked).ContinueWith(
            t => Dispatcher.Invoke(() =>
            {
                if (t.IsCompletedSuccessfully)
                    SetStatus($"Closed {t.Result} companion app(s).", muted: true);
            }),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    private void ExitApplication()
    {
        _reallyExit = true;
        Close();
        Application.Current.Shutdown();
    }

    private void DisposeTray()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }

    // ---------- helpers ----------

    private void SetStatus(string message, bool muted)
    {
        StatusText.Text = message;
        StatusText.Foreground = FindResource(muted ? "MutedBrush" : "OkBrush") as System.Windows.Media.Brush;
    }

    private void Warn(string message)
    {
        SetStatus(message, muted: false);
        StatusText.Foreground = FindResource("AccentBrush") as System.Windows.Media.Brush;
    }

    private void ShowBalloon(string title, string message)
    {
        try
        {
            _trayIcon.ShowBalloonTip(3000, title, message, Forms.ToolTipIcon.Info);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
        }
    }

    private static bool IsDefaultName(string name) =>
        name is "TinyPedal" or "GO Fast" or "SimPro Manager" or "New app";
}
