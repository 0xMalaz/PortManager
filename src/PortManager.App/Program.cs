using PortManager.Services;
using Velopack;

namespace PortManager;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .OnBeforeUninstallFastCallback(_ => StartupRegistrationService.RemoveRegistration())
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
