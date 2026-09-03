using System.Windows.Input;

namespace Lapper.Shell.Services;

/// <summary>Minimal ICommand wrapper (no MVVM framework per CLAUDE.md).</summary>
public sealed class DelegateCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
