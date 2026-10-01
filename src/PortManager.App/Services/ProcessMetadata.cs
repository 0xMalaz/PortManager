namespace PortManager.Services;

internal sealed record ProcessMetadata(
    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    DateTimeOffset? StartTimeUtc,
    string? CommandLine = null);

internal readonly record struct ProcessIdentity(
    int ProcessId,
    DateTimeOffset? StartTimeUtc,
    string ProcessName,
    string? ExecutablePath);
