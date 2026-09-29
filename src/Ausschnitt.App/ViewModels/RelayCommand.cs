using System.Windows.Input;

namespace Ausschnitt.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Action _action;
    private readonly Func<bool>? _can;

    public RelayCommand(Action action, Func<bool>? can = null)
    {
        _action = action;
        _can = can;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _can?.Invoke() ?? true;

    public void Execute(object? parameter) => _action();

    public void RaiseCanExecute() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
