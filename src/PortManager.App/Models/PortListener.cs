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
            if (Addresses.Count == 0)
            {
                return "Unknown address";
            }

            var allLoopback = true;
            for (var index = 0; index < Addresses.Count; index++)
            {
                var address = Addresses[index];
                if (address is "0.0.0.0" or "::")
                {
                    return "All interfaces";
                }

                allLoopback &= address is "127.0.0.1" or "::1";
            }

            if (allLoopback)
            {
                return "localhost";
            }

            return string.Join(", ", Addresses.Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }

    public string ProcessDisplay => $"{ProcessName}  ·  PID {ProcessId}";

    public string FrameworkDisplay => string.IsNullOrWhiteSpace(FrameworkName)
        ? "—"
        : FrameworkName;
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
