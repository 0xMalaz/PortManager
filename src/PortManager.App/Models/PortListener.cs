namespace PortManager.Models;

public enum PortCategory
{
    Dev,
    Other
}

public enum PortActivitySource
{
    ObservedConnection,
    ProcessStart,
    FirstSeen
}

public sealed record PortClassification(
    string ServiceName,
    string? FrameworkName,
    PortCategory Category);

public sealed record PortListener(
    int Port,
    int ProcessId,
    string ProcessName,
    string? ExecutablePath,
    DateTimeOffset? ProcessStartTimeUtc,
    IReadOnlyList<string> Addresses,
    bool IsProtected,
    string? ProtectionReason,
    string ServiceName,
    string? FrameworkName,
    PortCategory Category,
    DateTimeOffset LastActiveUtc,
    PortActivitySource LastActivitySource)
{
    public string AddressDisplay
    {
        get
        {
            var distinct = Addresses.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            if (distinct.Any(address => address is "0.0.0.0" or "::"))
            {
                return "All interfaces";
            }

            if (distinct.Length > 0 && distinct.All(address => address is "127.0.0.1" or "::1"))
            {
                return "localhost";
            }

            return distinct.Length == 0 ? "Unknown address" : string.Join(", ", distinct);
        }
    }

    public string ProcessDisplay => $"{ProcessName}  ·  PID {ProcessId}";

    public string FrameworkDisplay => string.IsNullOrWhiteSpace(FrameworkName)
        ? "—"
        : FrameworkName;

    public string LastActiveDisplay => PortActivityFormatter.Format(LastActiveUtc, DateTimeOffset.UtcNow);

    public string LastActiveToolTip
    {
        get
        {
            var localTime = LastActiveUtc.ToLocalTime().ToString("g");
            return LastActivitySource switch
            {
                PortActivitySource.ObservedConnection => $"Last observed TCP connection: {localTime}",
                PortActivitySource.ProcessStart => $"No connection observed yet; using process start: {localTime}",
                _ => $"No connection observed yet; first seen: {localTime}"
            };
        }
    }

    public string DetailsToolTip
    {
        get
        {
            var details = $"{ProcessName} · PID {ProcessId}\n{AddressDisplay}";
            return IsProtected && !string.IsNullOrWhiteSpace(ProtectionReason)
                ? $"{details}\n{ProtectionReason}"
                : details;
        }
    }

    public string KillToolTip => IsProtected
        ? ProtectionReason ?? "This process is protected."
        : $"Terminate {ProcessName} and its child processes";
}

internal static class PortActivityFormatter
{
    internal static string Format(DateTimeOffset timestamp, DateTimeOffset now)
    {
        var elapsed = now - timestamp;
        if (elapsed < TimeSpan.Zero || elapsed < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{Math.Max(1, (int)elapsed.TotalMinutes)}m ago";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return $"{Math.Max(1, (int)elapsed.TotalHours)}h ago";
        }

        return timestamp.ToLocalTime().ToString("g");
    }
}
