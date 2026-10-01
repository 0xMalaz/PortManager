using PortManager.Models;

namespace PortManager.Services;

internal static class PortListenerFilter
{
    internal static bool Matches(PortListener listener, string? searchText)
    {
        var query = searchText?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return true;
        }

        return listener.Port.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
               listener.ProcessId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
               listener.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               listener.ServiceName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               (listener.FrameworkName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
               listener.AddressDisplay.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
