using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using PortManager.Infrastructure;
using PortManager.Models;
using PortManager.Services;

namespace PortManager.ViewModels;

public sealed class PopupWindowViewModel : ObservableObject
{
    private readonly IPortSnapshotProvider _snapshotProvider;
    private readonly IProcessTerminationService _terminationService;
    private readonly IUserDialogService _dialogService;
    private readonly IElevationService _elevationService;
    private readonly Action _shutdownApplication;
    private readonly ObservableCollection<PortListener> _listeners = [];
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private string _searchText = string.Empty;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;
    private string _updatedText = "Not updated yet";
    private bool _isBusy;
    private PortCategory? _selectedCategory = PortCategory.Dev;
    private IReadOnlyList<PortListener> _snapshot = [];
    private DateTime? _snapshotTakenAt;
    private int _devCount;
    private int _otherCount;
    private bool _isViewVisible = true;
    private DateTimeOffset _currentTimeUtc = DateTimeOffset.UtcNow;

    public PopupWindowViewModel(
        IPortSnapshotProvider snapshotProvider,
        IProcessTerminationService terminationService,
        IUserDialogService dialogService,
        IElevationService elevationService,
        Action shutdownApplication)
    {
        _snapshotProvider = snapshotProvider;
        _terminationService = terminationService;
        _dialogService = dialogService;
        _elevationService = elevationService;
        _shutdownApplication = shutdownApplication;

        Listeners = CollectionViewSource.GetDefaultView(_listeners);
        Listeners.Filter = FilterListener;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        KillCommand = new AsyncRelayCommand<PortListener>(KillAsync, listener => !listener.IsProtected);
        ShowAllCommand = new RelayCommand(() => SelectCategory(null));
        ShowDevCommand = new RelayCommand(() => SelectCategory(PortCategory.Dev));
        ShowOtherCommand = new RelayCommand(() => SelectCategory(PortCategory.Other));
    }

    public ICollectionView Listeners { get; }

    public ICommand RefreshCommand { get; }

    public ICommand KillCommand { get; }

    public ICommand ShowAllCommand { get; }

    public ICommand ShowDevCommand { get; }

    public ICommand ShowOtherCommand { get; }

    public int ListenerCount => _snapshot.Count;

    public string ListenerCountText => $"{ListenerCount} listening";

    public int DevCount => _devCount;

    public int OtherCount => _otherCount;

    public string AllFilterText => $"All  {ListenerCount}";

    public string DevFilterText => $"Dev  {DevCount}";

    public string OtherFilterText => $"Other  {OtherCount}";

    public bool IsAllSelected => _selectedCategory is null;

    public bool IsDevSelected => _selectedCategory == PortCategory.Dev;

    public bool IsOtherSelected => _selectedCategory == PortCategory.Other;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
            {
                return;
            }

            Listeners.Refresh();
            NotifyFilterStateChanged();
            OnPropertyChanged(nameof(IsSearchEmpty));
        }
    }

    public bool IsSearchEmpty => string.IsNullOrWhiteSpace(SearchText);

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public string UpdatedText
    {
        get => _updatedText;
        private set => SetProperty(ref _updatedText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    /// <summary>
    /// While the popup is hidden, refreshes only record the latest snapshot (and the tray count);
    /// the list itself is brought up to date when the popup is shown again.
    /// </summary>
    public bool IsViewVisible
    {
        get => _isViewVisible;
        set
        {
            if (_isViewVisible == value)
            {
                return;
            }

            _isViewVisible = value;
            if (value)
            {
                ApplySnapshotToView();
            }
        }
    }

    /// <summary>Reference time for the relative "last active" column; advanced on each visible refresh.</summary>
    public DateTimeOffset CurrentTimeUtc
    {
        get => _currentTimeUtc;
        private set => SetProperty(ref _currentTimeUtc, value);
    }

    public bool HasVisibleListeners => _listeners.Any(FilterListener);

    public bool ShowEmptyState => !IsBusy && !HasError && !HasVisibleListeners;

    public string EmptyMessage => string.IsNullOrWhiteSpace(SearchText)
        ? "No TCP listeners are active."
        : "No listeners match your search.";

    public Task RefreshAsync() => RefreshAsync(showProgress: true);

    public Task RefreshInBackgroundAsync() => RefreshAsync(showProgress: false);

    private async Task RefreshAsync(bool showProgress)
    {
        if (!await _refreshGate.WaitAsync(0))
        {
            return;
        }

        if (showProgress)
        {
            IsBusy = true;
        }

        ErrorMessage = string.Empty;

        try
        {
            var snapshot = await _snapshotProvider.GetListenersAsync();
            SetSnapshot(snapshot.OrderBy(listener => listener.Port).ThenBy(listener => listener.ProcessName).ToArray());
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Could not read active ports: {exception.Message}";
        }
        finally
        {
            if (showProgress)
            {
                IsBusy = false;
            }

            _refreshGate.Release();
        }
    }

    private void SetSnapshot(IReadOnlyList<PortListener> snapshot)
    {
        var previousCount = _snapshot.Count;
        var devCount = snapshot.Count(listener => listener.Category == PortCategory.Dev);
        var otherCount = snapshot.Count(listener => listener.Category == PortCategory.Other);
        _snapshot = snapshot;
        _snapshotTakenAt = DateTime.Now;

        // Only announce counts that changed; the tray tooltip and filter chips listen for these.
        if (previousCount != snapshot.Count)
        {
            OnPropertyChanged(nameof(ListenerCount));
            OnPropertyChanged(nameof(ListenerCountText));
            OnPropertyChanged(nameof(AllFilterText));
        }

        if (_devCount != devCount)
        {
            _devCount = devCount;
            OnPropertyChanged(nameof(DevCount));
            OnPropertyChanged(nameof(DevFilterText));
        }

        if (_otherCount != otherCount)
        {
            _otherCount = otherCount;
            OnPropertyChanged(nameof(OtherCount));
            OnPropertyChanged(nameof(OtherFilterText));
        }

        if (_isViewVisible)
        {
            ApplySnapshotToView();
        }
    }

    private void ApplySnapshotToView()
    {
        if (_snapshotTakenAt is { } snapshotTakenAt)
        {
            UpdatedText = $"Updated {snapshotTakenAt:t}";
        }

        if (SyncListeners())
        {
            NotifyFilterStateChanged();
        }

        CurrentTimeUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates the bound collection in place: unchanged rows keep their containers, selection and tooltips.
    /// </summary>
    private bool SyncListeners()
    {
        var changed = false;
        var snapshotKeys = _snapshot.Select(ListenerKey).ToHashSet();
        for (var index = _listeners.Count - 1; index >= 0; index--)
        {
            if (!snapshotKeys.Contains(ListenerKey(_listeners[index])))
            {
                _listeners.RemoveAt(index);
                changed = true;
            }
        }

        for (var index = 0; index < _snapshot.Count; index++)
        {
            var listener = _snapshot[index];
            if (index < _listeners.Count && Equals(_listeners[index], listener))
            {
                continue;
            }

            var existingIndex = IndexOfListener(ListenerKey(listener), startIndex: index);
            if (existingIndex < 0)
            {
                _listeners.Insert(index, listener);
            }
            else
            {
                if (existingIndex != index)
                {
                    _listeners.Move(existingIndex, index);
                }

                if (!Equals(_listeners[index], listener))
                {
                    _listeners[index] = listener;
                }
            }

            changed = true;
        }

        while (_listeners.Count > _snapshot.Count)
        {
            _listeners.RemoveAt(_listeners.Count - 1);
            changed = true;
        }

        return changed;
    }

    private int IndexOfListener((int Port, int ProcessId) key, int startIndex)
    {
        for (var index = startIndex; index < _listeners.Count; index++)
        {
            if (ListenerKey(_listeners[index]) == key)
            {
                return index;
            }
        }

        return -1;
    }

    private static (int Port, int ProcessId) ListenerKey(PortListener listener) => (listener.Port, listener.ProcessId);

    private async Task KillAsync(PortListener listener)
    {
        var shouldRefresh = true;
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;

        try
        {
            var preview = await _terminationService.PrepareAsync(listener);
            if (!_dialogService.ConfirmTermination(preview))
            {
                StatusMessage = "Termination cancelled.";
                return;
            }

            var result = await _terminationService.TerminateAsync(preview);
            StatusMessage = result.Message;

            if (result.Outcome == TerminationOutcome.AccessDenied &&
                _dialogService.ConfirmRestartAsAdministrator())
            {
                if (_elevationService.TryRestartAsAdministrator(out var errorMessage))
                {
                    shouldRefresh = false;
                    _shutdownApplication();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    _dialogService.ShowError(errorMessage);
                }
            }
        }
        catch (ListenerStaleException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (ProtectedProcessException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Could not terminate the process: {exception.Message}";
        }
        finally
        {
            if (shouldRefresh)
            {
                await RefreshAsync();
            }
        }
    }

    private bool FilterListener(object item)
    {
        if (item is not PortListener listener)
        {
            return false;
        }

        return (_selectedCategory is null || listener.Category == _selectedCategory) &&
               PortListenerFilter.Matches(listener, SearchText);
    }

    private void SelectCategory(PortCategory? category)
    {
        if (_selectedCategory == category)
        {
            return;
        }

        _selectedCategory = category;
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsDevSelected));
        OnPropertyChanged(nameof(IsOtherSelected));
        Listeners.Refresh();
        NotifyFilterStateChanged();
    }

    private void NotifyFilterStateChanged()
    {
        OnPropertyChanged(nameof(HasVisibleListeners));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(EmptyMessage));
    }
}
