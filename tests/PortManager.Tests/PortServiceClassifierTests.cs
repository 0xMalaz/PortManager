using PortManager.Models;
using PortManager.Services;

namespace PortManager.Tests;

[TestClass]
public sealed class PortServiceClassifierTests
{
    private readonly PortServiceClassifier _classifier = new();

    [TestMethod]
    [DataRow("node", "node next dev", "Next.js Dev Server", "Next.js")]
    [DataRow("node", "node vite", "Vite Dev Server", "Vite")]
    [DataRow("python", "python -m jupyter-lab", "Jupyter Server", "Jupyter")]
    [DataRow("python", "python -m uvicorn api:app", "FastAPI Server", "FastAPI")]
    [DataRow("php", "php artisan serve", "Laravel Server", "Laravel")]
    [DataRow("dotnet", "dotnet MyApi.dll", ".NET Server", ".NET")]
    [DataRow("java", "java -jar spring-boot-app.jar", "Spring Boot Server", "Spring Boot")]
    [DataRow("ruby", "ruby bin/rails server", "Rails Server", "Rails")]
    [DataRow("cargo", "cargo run", "Rust Server", "Rust")]
    [DataRow("go", "go run ./cmd/server", "Go Server", "Go")]
    public void Classify_recognizes_passive_command_line_evidence(
        string processName,
        string commandLine,
        string expectedService,
        string expectedFramework)
    {
        var result = _classifier.Classify(Process(processName, commandLine), port: 3000);

        Assert.AreEqual(expectedService, result.ServiceName);
        Assert.AreEqual(expectedFramework, result.FrameworkName);
        Assert.AreEqual(PortCategory.Dev, result.Category);
    }

    [TestMethod]
    public void Classify_process_identity_takes_precedence_over_generic_port()
    {
        var result = _classifier.Classify(Process("postgres", null), port: 3000);

        Assert.AreEqual("PostgreSQL Server", result.ServiceName);
        Assert.AreEqual("PostgreSQL", result.FrameworkName);
    }

    [TestMethod]
    public void Classify_uses_only_dedicated_ports_as_a_port_fallback()
    {
        var generic = _classifier.Classify(Process("custom-agent", null), port: 3000);
        var database = _classifier.Classify(Process("custom-agent", null), port: 5432);

        Assert.AreEqual(PortCategory.Other, generic.Category);
        Assert.IsNull(generic.FrameworkName);
        Assert.AreEqual("PostgreSQL Server", database.ServiceName);
        Assert.AreEqual(PortCategory.Dev, database.Category);
    }

    [TestMethod]
    public void Classify_reads_a_nearby_project_manifest()
    {
        var directory = Directory.CreateTempSubdirectory("portmanager-classifier-");
        try
        {
            var script = Path.Combine(directory.FullName, "server.js");
            File.WriteAllText(script, string.Empty);
            File.WriteAllText(
                Path.Combine(directory.FullName, "package.json"),
                "{\"dependencies\":{\"vite\":\"latest\"}}");
            var process = Process("node", $"node \"{script}\"");

            var result = _classifier.Classify(process, port: 4173);

            Assert.AreEqual("Vite Dev Server", result.ServiceName);
            Assert.AreEqual("Vite", result.FrameworkName);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void Classify_malformed_or_missing_manifests_degrade_to_a_friendly_fallback()
    {
        var directory = Directory.CreateTempSubdirectory("portmanager-classifier-");
        try
        {
            var executable = Path.Combine(directory.FullName, "custom-agent.exe");
            File.WriteAllText(executable, string.Empty);
            File.WriteAllText(Path.Combine(directory.FullName, "package.json"), "{ definitely not json }");
            var process = Process("custom-agent.exe", null) with { ExecutablePath = executable };

            var result = _classifier.Classify(process, port: 45678);

            Assert.AreEqual("Custom Agent", result.ServiceName);
            Assert.IsNull(result.FrameworkName);
            Assert.AreEqual(PortCategory.Other, result.Category);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void Classify_preserves_dotted_and_camel_case_process_names_in_the_fallback()
    {
        var result = _classifier.Classify(Process("PortManager.TestHost", null), port: 45678);

        Assert.AreEqual("Port Manager Test Host", result.ServiceName);
        Assert.AreEqual(PortCategory.Other, result.Category);
    }

    private static ProcessMetadata Process(string processName, string? commandLine) =>
        new(
            ProcessId: 1234,
            ProcessName: processName,
            ExecutablePath: null,
            StartTimeUtc: new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero),
            CommandLine: commandLine);
}
