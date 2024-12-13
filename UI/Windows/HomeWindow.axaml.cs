using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Skia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using RobBERT_2023_BIAS.UI.Panels;
using Svg.Skia;

namespace RobBERT_2023_BIAS.UI.Windows;

public partial class HomeWindow : Window
{
    public readonly EventHandler LoadingStarted;
    public readonly EventHandler LoadingFinished;
    
    public HomeWindow()
    {
        InitializeComponent();
        
        FlexiblePanel.Children.Add(new HomePanel() { VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch });

        FlexiblePanel.Children.CollectionChanged += (sender, args) =>
        {
            if (FlexiblePanel.Children.FirstOrDefault() is UserControl child)
                MainMenuButton.IsVisible = child.GetType() != typeof(HomePanel);
        };

        LoadingStarted += (sender, args) =>
        {
            ReadyIndicator.IsVisible = false;
            LoadingIndicator.IsVisible = true;
        };

        LoadingFinished += (sender, args) =>
        {
            ReadyIndicator.IsVisible = true;
            LoadingIndicator.IsVisible = false;
        };
    }

    private void MainMenuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        FlexiblePanel.Children.Clear();
        FlexiblePanel.Children.Add(new HomePanel());
    }
}