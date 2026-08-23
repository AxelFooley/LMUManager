using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LMUManager;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class CompanionApp : ObservableObject
{
    private string _name = "";
    private string _path = "";
    private string _arguments = "";
    private bool _enabled = true;

    public string Name { get => _name; set => Set(ref _name, value); }
    public string Path { get => _path; set => Set(ref _path, value); }
    public string Arguments { get => _arguments; set => Set(ref _arguments, value); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
}

public class AppConfig : ObservableObject
{
    private string _gamePath = "";
    private string _gameArguments = "";
    private bool _closeCompanionsOnExit = true;

    public string GamePath { get => _gamePath; set => Set(ref _gamePath, value); }
    public string GameArguments { get => _gameArguments; set => Set(ref _gameArguments, value); }
    public bool CloseCompanionsOnExit { get => _closeCompanionsOnExit; set => Set(ref _closeCompanionsOnExit, value); }
    public ObservableCollection<CompanionApp> Apps { get; set; } = new();

    public static AppConfig CreateDefault() => new()
    {
        Apps = new ObservableCollection<CompanionApp>
        {
            new() { Name = "TinyPedal" },
            new() { Name = "GO Fast" },
            new() { Name = "SimPro Manager" },
        },
    };
}
