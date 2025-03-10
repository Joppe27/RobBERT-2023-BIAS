#region

using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using RobBERT_2023_BIAS.UI.Panels;

#endregion

namespace RobBERT_2023_BIAS.UI;

public partial class MainView : UserControl
{
    public readonly Action LoadingFinished;
    public readonly Action LoadingStarted;

    private static readonly Vector2 HomePanelSize = new(400, 700);

    public MainView()
    {
        InitializeComponent();

        if (OperatingSystem.IsBrowser())
        {
            this.Width = HomePanelSize.X;
            this.Height = HomePanelSize.Y;
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
        var homePanel = new HomePanel();
        
        FlexiblePanel.Children.Clear();
        FlexiblePanel.Children.Add(homePanel);

        if (OperatingSystem.IsBrowser())
        {
            MainView mainView = homePanel.GetVisualAncestors().SingleOrDefault(v => v is MainView) as MainView ??
                                throw new InvalidOperationException("HomePanel is not a child of a MainView");

            mainView.Width = HomePanelSize.X;
            mainView.Height = HomePanelSize.Y;
        }
        else
        {
            var desktopWindow = ((ClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;

            desktopWindow.WindowState = WindowState.Normal;
            desktopWindow.SystemDecorations = SystemDecorations.Full;
        }
    }
}