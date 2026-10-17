using System.Windows;
using PortManager.Infrastructure;
using PortManager.Models;

namespace PortManager.Services;

public sealed class WpfUserDialogService(
    Func<Window?> ownerProvider,
    Action<bool> setModalState) : IUserDialogService
{
    public bool ConfirmTermination(TerminationPreview preview)
    {
        var ports = string.Join(", ", preview.OwnedPorts.Select(port => $":{port}"));
        var message = Strings.ConfirmKill(preview.Listener.ProcessName, preview.Listener.ProcessId, ports);

        return ShowConfirmation(message, MessageBoxImage.Warning);
    }

    public bool ConfirmRestartAsAdministrator() => ShowConfirmation(
        Strings.ConfirmRestartAsAdministrator,
        MessageBoxImage.Question);

    public void ShowError(string message)
    {
        RunModal(() =>
        {
            System.Windows.MessageBox.Show(
                ownerProvider(),
                message,
                "Port Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return true;
        });
    }

    private bool ShowConfirmation(string message, MessageBoxImage image) => RunModal(() =>
        System.Windows.MessageBox.Show(
            ownerProvider(),
            message,
            "Port Manager",
            MessageBoxButton.YesNo,
            image,
            MessageBoxResult.No) == MessageBoxResult.Yes);

    private T RunModal<T>(Func<T> action)
    {
        setModalState(true);
        try
        {
            return action();
        }
        finally
        {
            setModalState(false);
        }
    }
}
