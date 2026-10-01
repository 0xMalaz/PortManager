using System.Windows;
using PortManager.Models;

namespace PortManager.Services;

public sealed class WpfUserDialogService(
    Func<Window?> ownerProvider,
    Action<bool> setModalState) : IUserDialogService
{
    public bool ConfirmTermination(TerminationPreview preview)
    {
        var ports = string.Join(", ", preview.OwnedPorts.Select(port => $":{port}"));
        var message =
            $"Kill {preview.Listener.ProcessName} (PID {preview.Listener.ProcessId})?{Environment.NewLine}{Environment.NewLine}" +
            $"Listening TCP ports owned by this process: {ports}.{Environment.NewLine}{Environment.NewLine}" +
            "The process and all of its child processes will be forcibly terminated. Unsaved work may be lost.";

        return ShowConfirmation(message, MessageBoxImage.Warning);
    }

    public bool ConfirmRestartAsAdministrator() => ShowConfirmation(
        "Windows denied permission to terminate this process. Restart Port Manager as administrator and try again?",
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
