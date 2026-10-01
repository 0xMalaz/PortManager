using PortManager.Models;

namespace PortManager.Services;

public interface IUserDialogService
{
    bool ConfirmTermination(TerminationPreview preview);

    bool ConfirmRestartAsAdministrator();

    void ShowError(string message);
}

