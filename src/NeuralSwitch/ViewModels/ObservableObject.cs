using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NeuralSwitch.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A command that runs an async action and is unavailable while it runs.</summary>
public sealed class AsyncCommand(Func<object?, Task> run, Func<object?, bool>? canRun = null) : ICommand
{
    private bool _running;

    public AsyncCommand(Func<Task> run, Func<bool>? canRun = null)
        : this(_ => run(), canRun is null ? null : _ => canRun())
    {
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_running && (canRun?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _running = true;
        CommandManager.InvalidateRequerySuggested();

        try
        {
            await run(parameter);
        }
        finally
        {
            _running = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
