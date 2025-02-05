using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using RobBERT_2023_BIAS.UI.Windows;

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class HomePanel : UserControl
{
    public HomePanel()
    {
        InitializeComponent();
    }

    private async void ModelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!(this.GetVisualRoot() is HomeWindow window))
            throw new NullReferenceException("Panel not in a HomeWindow hierarchy");
        
        if (this.Parent is Panel flexPanel)
        {
            PromptPanel promptPanel = await AwaitableTask.AwaitNotifyUI(PromptPanel.CreateAsync(PromptPanel.PromptMode.DefaultMode));
            
            flexPanel.Children.Clear(); 
            flexPanel.Children.Add(promptPanel);
        }
    }

    private async void JouJouwButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!(this.GetVisualRoot() is HomeWindow window))
            throw new NullReferenceException("Panel not in a HomeWindow hierarchy");
            
        if (this.Parent is Panel flexPanel)
        {
            PromptPanel jouJouwPanel = await AwaitableTask.AwaitNotifyUI(PromptPanel.CreateAsync(PromptPanel.PromptMode.JouJouwMode));
            
            flexPanel.Children.Clear(); 
            flexPanel.Children.Add(jouJouwPanel);
        }
    }

    private async void BiasButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!(this.GetVisualRoot() is HomeWindow window))
            throw new NullReferenceException("Panel not in a HomeWindow hierarchy");
            
        if (this.Parent is Panel flexPanel)
        {
            BiasPanel biasPanel = await AwaitableTask.AwaitNotifyUI(BiasPanel.CreateAsync());
            
            flexPanel.Children.Clear(); 
            flexPanel.Children.Add(biasPanel);
            
            if ((Application.Current!.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!.MainWindow is HomeWindow homeWindow)
            {
                homeWindow.WindowState = WindowState.Maximized;
                homeWindow.SystemDecorations = SystemDecorations.BorderOnly;
            }
            else
            {
                throw new NullReferenceException();
            }
        }
    }
}