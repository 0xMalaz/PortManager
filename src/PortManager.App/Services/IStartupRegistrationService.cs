namespace PortManager.Services;

public interface IStartupRegistrationService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);

    void EnsureCurrentExecutablePath();
}

