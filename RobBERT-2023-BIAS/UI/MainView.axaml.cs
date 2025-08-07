// Copyright (c) Joppe27 <joppe27.be>. Licensed under the MIT Licence.
// See LICENSE file in repository root for full license text.

#region

using System.Numerics;
using Avalonia;
using Avalonia.Animation;
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
    private static readonly Vector2 HomePanelSize = new(400, 700);
    public readonly Action ExceptionThrown;
    public readonly Action LoadingFinished;
    public readonly Action LoadingStarted;

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
            LoadingIndicator.IsVisible = false;
            ReadyIndicator.IsVisible = true;
        };

        ExceptionThrown += async () =>
        {
            ErrorIndicatorText.Text = "Exception thrown: request failed!";

            ReadyIndicator.IsVisible = false;
            ErrorIndicator.IsVisible = true;

            var animation = (Animation)App.Current.Resources["ErrorAnimation"] ?? throw new NullReferenceException();
            await animation.RunAsync(ErrorIndicatorIcon);

            await Task.Delay(5000);

            ErrorIndicator.IsVisible = false;
            ReadyIndicator.IsVisible = true;
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