using PortManager.Models;

namespace PortManager.Services;

public interface IPortSnapshotProvider
{
    Task<IReadOnlyList<PortListener>> GetListenersAsync(CancellationToken cancellationToken = default);
}

