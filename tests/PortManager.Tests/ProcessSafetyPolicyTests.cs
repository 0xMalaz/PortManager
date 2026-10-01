using PortManager.Services;

namespace PortManager.Tests;

[TestClass]
public sealed class ProcessSafetyPolicyTests
{
    [TestMethod]
    public void Evaluate_blocks_system_pid()
    {
        var result = ProcessSafetyPolicy.Evaluate(4, "System", null, currentProcessId: 9000);

        Assert.IsTrue(result.IsProtected);
    }

    [TestMethod]
    public void Evaluate_blocks_current_process()
    {
        var result = ProcessSafetyPolicy.Evaluate(1200, "PortManager", @"C:\Apps\PortManager.exe", 1200);

        Assert.IsTrue(result.IsProtected);
    }

    [TestMethod]
    public void Evaluate_blocks_windows_directory_process()
    {
        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var result = ProcessSafetyPolicy.Evaluate(
            2222,
            "some-service",
            Path.Combine(windowsDirectory, "System32", "some-service.exe"),
            currentProcessId: 9000);

        Assert.IsTrue(result.IsProtected);
    }

    [TestMethod]
    public void Evaluate_allows_developer_process()
    {
        var result = ProcessSafetyPolicy.Evaluate(
            2222,
            "node",
            @"C:\Program Files\nodejs\node.exe",
            currentProcessId: 9000);

        Assert.IsFalse(result.IsProtected);
        Assert.IsNull(result.Reason);
    }
}

