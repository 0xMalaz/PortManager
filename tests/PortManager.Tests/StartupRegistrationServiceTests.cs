using PortManager.Services;
using Microsoft.Win32;

namespace PortManager.Tests;

[TestClass]
public sealed class StartupRegistrationServiceTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Preference_survives_uninstall_and_reinstall(bool enabled)
    {
        var testRoot = @"Software\PortManager.Tests\" + Guid.NewGuid();
        var runPath = testRoot + @"\Run";
        var preferencesPath = testRoot + @"\Preferences";
        try
        {
            var original = new StartupRegistrationService(() => @"C:\old\PortManager.exe", runPath, preferencesPath);
            original.SetEnabled(enabled);
            StartupRegistrationService.RemoveRegistration(runPath);
            Assert.IsFalse(original.IsEnabled);
            using (var preferences = Registry.CurrentUser.OpenSubKey(preferencesPath))
                Assert.AreEqual(enabled ? 1 : 0, preferences!.GetValue("StartWithWindows"));

            var reinstalled = new StartupRegistrationService(() => @"C:\new\PortManager.exe", runPath, preferencesPath);
            reinstalled.EnsureCurrentExecutablePath();
            Assert.AreEqual(enabled, reinstalled.IsEnabled);
            if (enabled)
            {
                using var registration = Registry.CurrentUser.OpenSubKey(runPath);
                Assert.AreEqual(StartupRegistrationService.BuildCommand(@"C:\new\PortManager.exe"), registration!.GetValue("PortManager"));
            }
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(testRoot, throwOnMissingSubKey: false); }
    }

    [TestMethod]
    public void Legacy_enabled_registration_is_migrated_to_retained_preference()
    {
        var testRoot = @"Software\PortManager.Tests\" + Guid.NewGuid();
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(testRoot + @"\Run"))
                key.SetValue("PortManager", StartupRegistrationService.BuildCommand(@"C:\portable\PortManager.exe"));
            var service = new StartupRegistrationService(() => @"C:\installed\PortManager.exe",
                testRoot + @"\Run", testRoot + @"\Preferences");
            service.EnsureCurrentExecutablePath();
            using var preferences = Registry.CurrentUser.OpenSubKey(testRoot + @"\Preferences");
            Assert.AreEqual(1, preferences!.GetValue("StartWithWindows"));
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(testRoot, throwOnMissingSubKey: false); }
    }

    [TestMethod]
    public void BuildCommand_quotes_path_and_starts_hidden()
    {
        const string executablePath = @"C:\Apps With Spaces\PortManager.exe";

        var command = StartupRegistrationService.BuildCommand(executablePath);

        Assert.AreEqual("\"C:\\Apps With Spaces\\PortManager.exe\" --startup", command);
    }
}

