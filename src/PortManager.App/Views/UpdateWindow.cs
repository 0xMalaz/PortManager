using System.Windows;
using System.Windows.Controls;
using PortManager.Services;

namespace PortManager.Views;

internal sealed class UpdateWindow : Window
{
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
    private readonly System.Windows.Controls.ProgressBar _progress = new() { Minimum = 0, Maximum = 100, Height = 8 };

    public UpdateWindow()
    {
        Title = "Port Manager updates";
        SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
        Width = 430;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(_status);
        panel.Children.Add(_progress);
        Content = panel;
    }

    public void Render(UpdateStatus status)
    {
        _status.Text = status.Message;
        _progress.IsIndeterminate = status.Stage == UpdateStage.Checking;
        _progress.Value = status.Progress;
        _progress.Visibility = status.Stage is UpdateStage.Checking or UpdateStage.Downloading
            ? Visibility.Visible : Visibility.Collapsed;
    }
}
