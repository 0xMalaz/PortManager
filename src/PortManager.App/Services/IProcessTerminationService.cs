using PortManager.Models;

namespace PortManager.Services;

public interface IProcessTerminationService
{
    Task<TerminationPreview> PrepareAsync(
        PortListener listener,
        CancellationToken cancellationToken = default);

    Task<TerminationResult> TerminateAsync(
        TerminationPreview preview,
        CancellationToken cancellationToken = default);
}

