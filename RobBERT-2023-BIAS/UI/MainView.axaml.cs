#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using RobBERT_2023_BIAS.UI.Panels;

#endregion

namespace RobBERT_2023_BIAS.UI;

public partial class MainView : UserControl
{
    public readonly Action LoadingFinished;
    public readonly Action LoadingStarted;

    public MainView()
    {
        InitializeComponent();

        if (OperatingSystem.IsBrowser())
        {
            this.Width = 400;
            this.Height = 700;
        }

        FlexiblePanel.Children.Add(new HomePanel()
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });

        FlexiblePanel.Children.CollectionChanged += (_, _) =>
        {
            if (FlexiblePanel.Children.FirstOrDefault() is UserControl child)
                MainMenuButton.IsVisible = child.GetType() != typeof(HomePanel);
        };

        LoadingStarted += () =>
        {
            ReadyIndicator.IsVisible = false;
            LoadingIndicator.IsVisible = true;
        };

        LoadingFinished += () =>
        {
            ReadyIndicator.IsVisible = true;
            LoadingIndicator.IsVisible = false;
        };
    }

    private void MainMenuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        FlexiblePanel.Children.Clear();
        FlexiblePanel.Children.Add(new HomePanel());

        if (!OperatingSystem.IsBrowser())
        {
            var desktopWindow = ((ClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;

            desktopWindow.WindowState = WindowState.Normal;
            desktopWindow.SystemDecorations = SystemDecorations.Full;
        }
    }
}