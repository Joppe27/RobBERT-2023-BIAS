using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RobBERT_2023_BIAS.UI.Panels;

namespace RobBERT_2023_BIAS.UI.Windows;

public partial class HomeWindow : Window
{
    public HomeWindow()
    {
        InitializeComponent();
        
        FlexiblePanel.Children.Add(new HomePanel() { VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch });

        LoadingIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://RobBERT-2023-BIAS/Resources/UI/Icons/circle-notch-solid.png")));
        Animate();
        
        async Task Animate()
        {
            int angle = 0;
            
            while (true)
            {
                if (angle == 340)
                    angle = 0;
                else
                    angle += 30;
                
                LoadingIcon.RenderTransform = new RotateTransform()
                {
                    Angle = angle
                };
                await Task.Delay(75);
            }
        };

        LoadingText.Text = "Busy...";

        // LoadingIcon.Source = new Bitmap(AssetLoader.Open(new Uri("avares://RobBERT-2023-BIAS/Resources/UI/Icons/circle-check-regular.png")));
        // LoadingText.Text = "Ready";
    }
}