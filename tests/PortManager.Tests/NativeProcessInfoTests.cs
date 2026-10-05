using System.Diagnostics;
using PortManager.Native;
using PortManager.Services;

namespace PortManager.Tests;

[TestClass]
public sealed class NativeProcessInfoTests
{
    [TestMethod]
    public void Reads_the_current_process_like_the_Process_class()
    {
        using var current = Process.GetCurrentProcess();
        using var handle = NativeProcessInfo.OpenForQuery(Environment.ProcessId);

        Assert.IsNotNull(handle);
        Assert.AreEqual(Environment.ProcessPath, NativeProcessInfo.QueryImagePath(handle), ignoreCase: true);
        Assert.AreEqual(
            new DateTimeOffset(current.StartTime.ToUniversalTime(), TimeSpan.Zero),
            NativeProcessInfo.QueryStartTimeUtc(handle));
        Assert.IsFalse(string.IsNullOrWhiteSpace(NativeProcessInfo.QueryCommandLine(handle)));
    }

    [TestMethod]
    public void Metadata_reader_matches_the_Process_class_name()
    {
        using var current = Process.GetCurrentProcess();

        var metadata = new WindowsProcessMetadataReader().Read(Environment.ProcessId);

        Assert.AreEqual(current.ProcessName, metadata.ProcessName);
        Assert.AreEqual(Environment.ProcessPath, metadata.ExecutablePath, ignoreCase: true);
        Assert.IsNotNull(metadata.StartTimeUtc);
    }

    [TestMethod]
    public void GetProcessName_drops_only_the_exe_extension()
    {
        Assert.AreEqual("node", NativeProcessInfo.GetProcessName(@"C:\Tools\node.exe"));
        Assert.AreEqual("vite.dev", NativeProcessInfo.GetProcessName(@"C:\Tools\vite.dev.EXE"));
        Assert.AreEqual("server.bin", NativeProcessInfo.GetProcessName(@"C:\Tools\server.bin"));
    }

    [TestMethod]
    public void Command_line_reader_skips_unavailable_process_ids()
    {
        var commandLines = new WindowsProcessCommandLineReader().ReadCommandLines([0, Environment.ProcessId]);

        Assert.IsFalse(commandLines.ContainsKey(0));
        Assert.IsFalse(string.IsNullOrWhiteSpace(commandLines[Environment.ProcessId]));
    }
}
