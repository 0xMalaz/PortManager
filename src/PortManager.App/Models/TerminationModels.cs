namespace PortManager.Models;

public sealed record TerminationPreview(
    PortListener Listener,
    IReadOnlyList<int> OwnedPorts);

public enum TerminationOutcome
{
    Success,
    Stale,
    Protected,
    AccessDenied,
    StillListening,
    Failed
}

public sealed record TerminationResult(
    TerminationOutcome Outcome,
    string Message,
    PortListener? CurrentOwner = null)
{
    public bool IsSuccess => Outcome == TerminationOutcome.Success;
}

public sealed class ListenerStaleException(string message) : InvalidOperationException(message);

public sealed class ProtectedProcessException(string message) : InvalidOperationException(message);

