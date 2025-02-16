#region

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using RobBERT_2023_BIAS.UI.Panels;

#endregion

namespace RobBERT_2023_BIAS.UI.Windows;

public partial class HomeWindow : Window
{
    public readonly Action LoadingFinished;
    public readonly Action LoadingStarted;

    public HomeWindow()
    {
        InitializeComponent();

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

        this.WindowState = WindowState.Normal;
        this.SystemDecorations = SystemDecorations.Full;
    }
}