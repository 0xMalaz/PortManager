using PortManager.Models;
using PortManager.Services;
using PortManager.ViewModels;

namespace PortManager.Tests;

[TestClass]
public sealed class PopupWindowViewModelTests
{
    [TestMethod]
    public void Refresh_populates_every_listener_without_collection_view_errors()
    {
        Exception? capturedException = null;
        PopupWindowViewModel? viewModel = null;
        var thread = new Thread(() =>
        {
            try
            {
                var provider = new MutableSnapshotProvider(
                [
                    TestData.Listener(port: 3000),
                    TestData.Listener(port: 5000)
                ]);
                viewModel = new PopupWindowViewModel(
                    provider,
                    new ProcessTerminationService(provider),
                    new NoOpDialogService(),
                    new NoOpElevationService(),
                    () => { });

                viewModel.RefreshAsync().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                capturedException = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)), "The STA refresh test timed out.");

        Assert.IsNull(capturedException, capturedException?.ToString());
        Assert.IsNotNull(viewModel);
        Assert.AreEqual(2, viewModel.ListenerCount);
        Assert.IsFalse(viewModel.HasError, viewModel.ErrorMessage);
    }

    [TestMethod]
    public void Refresh_exposes_all_dev_and_other_counts()
    {
        RunOnSta(() =>
        {
            var viewModel = CreateViewModel(
            [
                TestData.Listener(port: 3000, category: PortCategory.Dev),
                TestData.Listener(
                    port: 5000,
                    processName: "custom-agent",
                    serviceName: "Custom Agent",
                    frameworkName: null,
                    category: PortCategory.Other)
            ]);

            viewModel.RefreshAsync().GetAwaiter().GetResult();

            Assert.AreEqual(2, viewModel.ListenerCount);
            Assert.AreEqual(1, viewModel.DevCount);
            Assert.AreEqual(1, viewModel.OtherCount);
            Assert.AreEqual("All  2", viewModel.AllFilterText);
            Assert.AreEqual("Dev  1", viewModel.DevFilterText);
            Assert.AreEqual("Other  1", viewModel.OtherFilterText);
        });
    }

    [TestMethod]
    public void New_popup_defaults_to_the_dev_filter()
    {
        RunOnSta(() =>
        {
            var dev = TestData.Listener(port: 3000, category: PortCategory.Dev);
            var other = TestData.Listener(
                port: 5000,
                processName: "custom-agent",
                serviceName: "Custom Agent",
                frameworkName: null,
                category: PortCategory.Other);
            var viewModel = CreateViewModel([dev, other]);

            viewModel.RefreshAsync().GetAwaiter().GetResult();

            Assert.IsTrue(viewModel.IsDevSelected);
            Assert.IsFalse(viewModel.IsAllSelected);
            CollectionAssert.AreEqual(new[] { dev }, viewModel.Listeners.Cast<PortListener>().ToArray());
        });
    }

    [TestMethod]
    public void Repeated_background_refreshes_do_not_show_the_progress_indicator()
    {
        RunOnSta(() =>
        {
            var viewModel = CreateViewModel([TestData.Listener(port: 3000)]);
            var busyChanges = new List<bool>();
            viewModel.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(PopupWindowViewModel.IsBusy))
                {
                    busyChanges.Add(viewModel.IsBusy);
                }
            };

            for (var refreshIndex = 0; refreshIndex < 3; refreshIndex++)
            {
                viewModel.RefreshInBackgroundAsync().GetAwaiter().GetResult();
            }

            CollectionAssert.AreEqual(Array.Empty<bool>(), busyChanges);
            Assert.IsFalse(viewModel.IsBusy);
            Assert.AreEqual(1, viewModel.ListenerCount);
        });
    }

    [TestMethod]
    public void Category_filter_combines_with_service_and_framework_search()
    {
        RunOnSta(() =>
        {
            var vite = TestData.Listener(
                port: 5173,
                serviceName: "Vite Dev Server",
                frameworkName: "Vite",
                category: PortCategory.Dev);
            var other = TestData.Listener(
                port: 9000,
                processName: "custom-agent",
                serviceName: "Custom Agent",
                frameworkName: null,
                category: PortCategory.Other);
            var viewModel = CreateViewModel([vite, other]);
            viewModel.RefreshAsync().GetAwaiter().GetResult();

            viewModel.ShowDevCommand.Execute(null);
            CollectionAssert.AreEqual(new[] { vite }, viewModel.Listeners.Cast<PortListener>().ToArray());
            Assert.IsTrue(viewModel.IsDevSelected);

            viewModel.SearchText = "vite";
            CollectionAssert.AreEqual(new[] { vite }, viewModel.Listeners.Cast<PortListener>().ToArray());

            viewModel.SearchText = "custom";
            Assert.IsFalse(viewModel.Listeners.Cast<PortListener>().Any());

            viewModel.ShowOtherCommand.Execute(null);
            CollectionAssert.AreEqual(new[] { other }, viewModel.Listeners.Cast<PortListener>().ToArray());

            viewModel.ShowAllCommand.Execute(null);
            Assert.IsTrue(viewModel.IsAllSelected);
        });
    }

    private static PopupWindowViewModel CreateViewModel(IReadOnlyList<PortListener> listeners)
    {
        var provider = new MutableSnapshotProvider(listeners);
        return new PopupWindowViewModel(
            provider,
            new ProcessTerminationService(provider),
            new NoOpDialogService(),
            new NoOpElevationService(),
            () => { });
    }

    private static void RunOnSta(Action action)
    {
        Exception? capturedException = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                capturedException = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)), "The STA view-model test timed out.");
        Assert.IsNull(capturedException, capturedException?.ToString());
    }

    private sealed class NoOpDialogService : IUserDialogService
    {
        public bool ConfirmTermination(TerminationPreview preview) => false;

        public bool ConfirmRestartAsAdministrator() => false;

        public void ShowError(string message)
        {
        }
    }

    private sealed class NoOpElevationService : IElevationService
    {
        public bool TryRestartAsAdministrator(out string? errorMessage)
        {
            errorMessage = null;
            return false;
        }
    }
}
