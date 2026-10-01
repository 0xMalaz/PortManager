using PortManager.Services;

namespace PortManager.Tests;

[TestClass]
public sealed class StartupRegistrationServiceTests
{
    [TestMethod]
    public void BuildCommand_quotes_path_and_starts_hidden()
    {
        const string executablePath = @"C:\Apps With Spaces\PortManager.exe";

        var command = StartupRegistrationService.BuildCommand(executablePath);

        Assert.AreEqual("\"C:\\Apps With Spaces\\PortManager.exe\" --startup", command);
    }
}

