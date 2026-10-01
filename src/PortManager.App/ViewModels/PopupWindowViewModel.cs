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

    public int ListenerCount => _listeners.Count;

    public string ListenerCountText => $"{ListenerCount} listening";

    public int DevCount => _listeners.Count(listener => listener.Category == PortCategory.Dev);

    public int OtherCount => _listeners.Count(listener => listener.Category == PortCategory.Other);

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
        private set => SetProperty(ref _isBusy, value);
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
        OnPropertyChanged(nameof(ShowEmptyState));

        try
        {
            var snapshot = await _snapshotProvider.GetListenersAsync();

            _listeners.Clear();
            foreach (var listener in snapshot.OrderBy(listener => listener.Port).ThenBy(listener => listener.ProcessName))
            {
                _listeners.Add(listener);
            }

            Listeners.Refresh();

            UpdatedText = $"Updated {DateTime.Now:t}";
            OnPropertyChanged(nameof(ListenerCount));
            OnPropertyChanged(nameof(ListenerCountText));
            OnPropertyChanged(nameof(DevCount));
            OnPropertyChanged(nameof(OtherCount));
            OnPropertyChanged(nameof(AllFilterText));
            OnPropertyChanged(nameof(DevFilterText));
            OnPropertyChanged(nameof(OtherFilterText));
            NotifyFilterStateChanged();
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

            OnPropertyChanged(nameof(ShowEmptyState));
            _refreshGate.Release();
        }
    }

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
