using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using PortManager.Models;
using PortManager.Services;

namespace PortManager.Tests;

[TestClass]
[DoNotParallelize]
public sealed class NativeIntegrationTests
{
    [TestMethod]
    public async Task Discovers_and_terminates_a_real_multi_port_process()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The Windows IP Helper integration test requires Windows.");
        }

        using var runner = StartTestHost();
        var childProcessId = 0;

        try
        {
            using var startupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            string? startupLine;
            try
            {
                startupLine = await runner.StandardOutput.ReadLineAsync(startupTimeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (!runner.HasExited)
                {
                    runner.Kill(entireProcessTree: true);
                    await runner.WaitForExitAsync();
                }

                var error = await runner.StandardError.ReadToEndAsync();
                Assert.Fail($"The TCP test host did not start in time. {error}");
                return;
            }

            if (startupLine is null)
            {
                var error = await runner.StandardError.ReadToEndAsync();
                Assert.Fail($"The TCP test host exited before becoming ready. {error}");
                return;
            }

            var startupParts = startupLine.Split(';');
            Assert.HasCount(2, startupParts, $"Unexpected test host output: {startupLine}");
            childProcessId = int.Parse(startupParts[0]);
            var ports = startupParts[1].Split(',').Select(int.Parse).OrderBy(port => port).ToArray();
            Assert.HasCount(2, ports);

            var provider = new WindowsPortSnapshotProvider();
            var snapshot = await WaitForListenersAsync(provider, childProcessId, ports);
            var selected = snapshot.Single(listener => listener.ProcessId == childProcessId && listener.Port == ports[0]);
            var service = new ProcessTerminationService(provider);

            var preview = await service.PrepareAsync(selected);
            CollectionAssert.AreEqual(ports, preview.OwnedPorts.ToArray());

            var result = await service.TerminateAsync(preview);
            Assert.AreEqual(TerminationOutcome.Success, result.Outcome, result.Message);

            foreach (var port in ports)
            {
                using var probe = new TcpListener(IPAddress.Loopback, port);
                probe.Start();
                probe.Stop();
            }
        }
        finally
        {
            TryKill(childProcessId);
            if (!runner.HasExited)
            {
                runner.Kill(entireProcessTree: true);
                await runner.WaitForExitAsync();
            }
        }
    }

    private static Process StartTestHost()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(
            repositoryRoot,
            "tests",
            "PortManager.TestHost",
            "PortManager.TestHost.csproj");
        var configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif
        var startInfo = new ProcessStartInfo
        {
            FileName = FindDotnetHost(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = repositoryRoot
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(configuration);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--count");
        startInfo.ArgumentList.Add("2");

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the TCP test host.");
    }

    private static async Task<IReadOnlyList<PortListener>> WaitForListenersAsync(
        IPortSnapshotProvider provider,
        int processId,
        IReadOnlyCollection<int> expectedPorts)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var snapshot = await provider.GetListenersAsync();
            var actualPorts = snapshot
                .Where(listener => listener.ProcessId == processId)
                .Select(listener => listener.Port)
                .ToHashSet();

            if (expectedPorts.All(actualPorts.Contains))
            {
                return snapshot;
            }

            await Task.Delay(100);
        }

        Assert.Fail($"The native listener table did not expose PID {processId} in time.");
        return [];
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PortManager.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Port Manager repository root.");
    }

    private static string FindDotnetHost()
    {
        var installedHost = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe");

        return File.Exists(installedHost) ? installedHost : "dotnet";
    }

    private static void TryKill(int processId)
    {
        if (processId <= 0)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // The test host was already terminated by Port Manager.
        }
    }
}
