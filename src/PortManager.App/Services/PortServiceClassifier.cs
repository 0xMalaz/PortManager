using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PortManager.Models;

namespace PortManager.Services;

internal interface IPortServiceClassifier
{
    PortClassification Classify(ProcessMetadata process, int port);
}

internal sealed partial class PortServiceClassifier : IPortServiceClassifier
{
    private const long MaximumManifestBytes = 512 * 1024;
    private const int MaximumParentDepth = 8;
    private const int MaximumManifestFiles = 4;

    private static readonly string[] ManifestNames =
    [
        "package.json",
        "pyproject.toml",
        "requirements.txt",
        "composer.json",
        "Gemfile",
        "Cargo.toml",
        "go.mod",
        "pom.xml",
        "build.gradle",
        "build.gradle.kts"
    ];

    private static readonly IReadOnlyDictionary<int, PortClassification> DedicatedPortServices =
        new Dictionary<int, PortClassification>
        {
            [1433] = Dev("SQL Server", "SQL Server"),
            [3306] = Dev("MySQL Server", "MySQL"),
            [5432] = Dev("PostgreSQL Server", "PostgreSQL"),
            [5672] = Dev("RabbitMQ Server", "RabbitMQ"),
            [6379] = Dev("Redis Server", "Redis"),
            [9200] = Dev("Elasticsearch Server", "Elasticsearch"),
            [15672] = Dev("RabbitMQ Management", "RabbitMQ"),
            [27017] = Dev("MongoDB Server", "MongoDB")
        };

    public PortClassification Classify(ProcessMetadata process, int port)
    {
        var processName = Normalize(process.ProcessName);
        var commandLine = Normalize(process.CommandLine);
        var executablePath = Normalize(process.ExecutablePath);
        var manifests = Normalize(ReadManifestEvidence(process));
        var evidence = $"{commandLine}\n{executablePath}\n{manifests}";

        var service = ClassifyKnownService(processName, evidence);
        if (service is not null)
        {
            return service;
        }

        var framework = ClassifyFramework(processName, commandLine, manifests, evidence);
        if (framework is not null)
        {
            return framework;
        }

        if (DedicatedPortServices.TryGetValue(port, out var portService))
        {
            return portService;
        }

        return new PortClassification(FriendlyProcessName(process.ProcessName), null, PortCategory.Other);
    }

    private static PortClassification? ClassifyKnownService(string processName, string evidence)
    {
        if (ContainsAny(processName, "postgres", "postmaster"))
        {
            return Dev("PostgreSQL Server", "PostgreSQL");
        }

        if (ContainsAny(processName, "mysqld", "mariadbd"))
        {
            return Dev("MySQL Server", processName.Contains("maria") ? "MariaDB" : "MySQL");
        }

        if (ContainsAny(processName, "mongod"))
        {
            return Dev("MongoDB Server", "MongoDB");
        }

        if (ContainsAny(processName, "redis-server", "redis_server"))
        {
            return Dev("Redis Server", "Redis");
        }

        if (ContainsAny(processName, "sqlservr"))
        {
            return Dev("SQL Server", "SQL Server");
        }

        if (ContainsAny(processName, "nginx"))
        {
            return Dev("Nginx Server", "Nginx");
        }

        if (ContainsAny(processName, "httpd", "apache2"))
        {
            return Dev("Apache Server", "Apache");
        }

        if (ContainsAny(processName, "elasticsearch") || evidence.Contains("org.elasticsearch"))
        {
            return Dev("Elasticsearch Server", "Elasticsearch");
        }

        if (ContainsAny(processName, "rabbitmq") || evidence.Contains("rabbitmq"))
        {
            return Dev("RabbitMQ Server", "RabbitMQ");
        }

        return null;
    }

    private static PortClassification? ClassifyFramework(
        string processName,
        string commandLine,
        string manifests,
        string evidence)
    {
        if (ContainsAny(evidence, "@tauri-apps", "tauri dev", "tauri-driver"))
        {
            return Dev("Tauri Dev Server", "Tauri");
        }

        if (ContainsAny(commandLine, "next dev", "next\\dist", "next/dist") || manifests.Contains("\"next\""))
        {
            return Dev("Next.js Dev Server", "Next.js");
        }

        if (ContainsAny(evidence, "nuxt dev", "\"nuxt\"", "node_modules\\nuxt"))
        {
            return Dev("Nuxt Dev Server", "Nuxt");
        }

        if (ContainsAny(evidence, "@sveltejs/kit", "svelte-kit"))
        {
            return Dev("SvelteKit Dev Server", "SvelteKit");
        }

        if (ContainsAny(evidence, "@angular/core", " ng serve", "ng.cmd serve"))
        {
            return Dev("Angular Dev Server", "Angular");
        }

        if (ContainsAny(evidence, "\"astro\"", "astro dev", "node_modules\\astro"))
        {
            return Dev("Astro Dev Server", "Astro");
        }

        if (ContainsAny(evidence, "@remix-run", "remix dev"))
        {
            return Dev("Remix Dev Server", "Remix");
        }

        if (ContainsAny(commandLine, "vite", "vite.js") || manifests.Contains("\"vite\""))
        {
            return Dev("Vite Dev Server", "Vite");
        }

        if (ContainsAny(evidence, "react-scripts", "\"react\""))
        {
            return Dev("React Dev Server", "React");
        }

        if (ContainsAny(evidence, "\"vue\"", "vue-cli-service"))
        {
            return Dev("Vue Dev Server", "Vue");
        }

        if (ContainsAny(evidence, "@nestjs/core", " nest start"))
        {
            return Dev("NestJS Server", "NestJS");
        }

        if (ContainsAny(evidence, "\"fastify\""))
        {
            return Dev("Fastify Server", "Fastify");
        }

        if (ContainsAny(evidence, "\"express\""))
        {
            return Dev("Express Server", "Express");
        }

        if (ContainsAny(evidence, "\"koa\"", "\"@hapi/hapi\"", "\"hono\"", "\"elysia\""))
        {
            return Dev("Node Web Server", "Node");
        }

        if (IsProcess(processName, "node", "node.exe", "bun", "deno"))
        {
            var runtime = processName.Contains("bun") ? "Bun" : processName.Contains("deno") ? "Deno" : "Node";
            return Dev($"{runtime} Server", runtime);
        }

        if (ContainsAny(evidence, "jupyter-lab", "jupyter notebook", "jupyter_server") || processName.Contains("jupyter"))
        {
            return Dev("Jupyter Server", "Jupyter");
        }

        if (ContainsAny(evidence, "uvicorn", "fastapi"))
        {
            return Dev("FastAPI Server", "FastAPI");
        }

        if (ContainsAny(evidence, "manage.py runserver", "django"))
        {
            return Dev("Django Server", "Django");
        }

        if (ContainsAny(evidence, "flask run", "\"flask"))
        {
            return Dev("Flask Server", "Flask");
        }

        if (ContainsAny(evidence, "gunicorn"))
        {
            return Dev("Python Web Server", "Gunicorn");
        }

        if (IsProcess(processName, "python", "python.exe", "python3", "pythonw", "pythonw.exe", "py"))
        {
            return Dev("Python Server", "Python");
        }

        if (ContainsAny(evidence, "artisan serve", "laravel/framework"))
        {
            return Dev("Laravel Server", "Laravel");
        }

        if (ContainsAny(evidence, "symfony server", "symfony/framework"))
        {
            return Dev("Symfony Server", "Symfony");
        }

        if (IsProcess(processName, "php", "php.exe", "php-cgi", "php-cgi.exe"))
        {
            return Dev("PHP Server", "PHP");
        }

        if (ContainsAny(evidence, "microsoft.aspnetcore", "iisexpress") || processName.Contains("iisexpress"))
        {
            return Dev("ASP.NET Core Server", "ASP.NET Core");
        }

        if (IsProcess(processName, "dotnet", "dotnet.exe"))
        {
            return Dev(".NET Server", ".NET");
        }

        if (ContainsAny(evidence, "spring-boot", "org.springframework"))
        {
            return Dev("Spring Boot Server", "Spring Boot");
        }

        if (ContainsAny(evidence, "tomcat", "catalina"))
        {
            return Dev("Tomcat Server", "Tomcat");
        }

        if (IsProcess(processName, "java", "java.exe", "javaw", "javaw.exe"))
        {
            return Dev("Java Server", "Java");
        }

        if (ContainsAny(evidence, "rails server", "bin/rails", "bin\\rails"))
        {
            return Dev("Rails Server", "Rails");
        }

        if (ContainsAny(evidence, "puma", "unicorn"))
        {
            return Dev("Ruby Web Server", evidence.Contains("puma") ? "Puma" : "Unicorn");
        }

        if (IsProcess(processName, "ruby", "ruby.exe"))
        {
            return Dev("Ruby Server", "Ruby");
        }

        if (ContainsAny(evidence, "cargo run", "cargo.toml") || processName.Contains("cargo"))
        {
            return Dev("Rust Server", "Rust");
        }

        if (ContainsAny(evidence, "go run", "go.mod") || IsProcess(processName, "go", "go.exe"))
        {
            return Dev("Go Server", "Go");
        }

        return null;
    }

    private static string ReadManifestEvidence(ProcessMetadata process)
    {
        var builder = new StringBuilder();
        var readFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in CandidateDirectories(process))
        {
            var current = directory;
            for (var depth = 0; depth < MaximumParentDepth && current is not null; depth++)
            {
                foreach (var manifestPath in FindManifests(current))
                {
                    if (!readFiles.Add(manifestPath))
                    {
                        continue;
                    }

                    var contents = SafeReadManifest(manifestPath);
                    if (!string.IsNullOrWhiteSpace(contents))
                    {
                        builder.AppendLine(manifestPath);
                        builder.AppendLine(contents);
                    }

                    if (readFiles.Count >= MaximumManifestFiles)
                    {
                        return builder.ToString();
                    }
                }

                current = Directory.GetParent(current)?.FullName;
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<string> CandidateDirectories(ProcessMetadata process)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddPathCandidate(process.ExecutablePath, candidates);

        if (!string.IsNullOrWhiteSpace(process.CommandLine))
        {
            foreach (Match match in WindowsPathRegex().Matches(process.CommandLine))
            {
                AddPathCandidate(match.Groups["path"].Value, candidates);
            }
        }

        return candidates;
    }

    private static void AddPathCandidate(string? value, ISet<string> candidates)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var path = value.Trim().Trim('"').TrimEnd(',', ';');
        try
        {
            if (File.Exists(path))
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    candidates.Add(directory);
                }
            }
            else if (Directory.Exists(path))
            {
                candidates.Add(Path.GetFullPath(path));
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException or
            UnauthorizedAccessException)
        {
        }
    }

    private static IEnumerable<string> FindManifests(string directory)
    {
        foreach (var name in ManifestNames)
        {
            var path = Path.Combine(directory, name);
            if (File.Exists(path))
            {
                yield return path;
            }
        }

        IEnumerable<string> projectFiles;
        try
        {
            projectFiles = Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly).Take(1).ToArray();
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            DirectoryNotFoundException)
        {
            projectFiles = [];
        }

        foreach (var projectFile in projectFiles)
        {
            yield return projectFile;
        }
    }

    private static string? SafeReadManifest(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Length <= MaximumManifestBytes
                ? File.ReadAllText(path)
                : null;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsProcess(string processName, params string[] names) =>
        names.Any(name => string.Equals(processName, name, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(value.Contains);

    private static PortClassification Dev(string serviceName, string frameworkName) =>
        new(serviceName, frameworkName, PortCategory.Dev);

    private static string Normalize(string? value) => value?.ToLowerInvariant() ?? string.Empty;

    private static string FriendlyProcessName(string processName)
    {
        var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;
        name = FriendlyNameBoundaryRegex()
            .Replace(name, " ")
            .Replace('.', ' ')
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Trim();

        return string.IsNullOrWhiteSpace(name)
            ? "Unknown service"
            : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower(CultureInfo.CurrentCulture));
    }

    [GeneratedRegex("(?:\"(?<path>[A-Za-z]:\\\\[^\"]+)\"|(?<path>[A-Za-z]:\\\\[^\\s\"]+))", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPathRegex();

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.CultureInvariant)]
    private static partial Regex FriendlyNameBoundaryRegex();
}
