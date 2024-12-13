using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
            PromptPanel newPanel = await AwaitableTask.AwaitNotifyUI(PromptPanel.CreateAsync(PromptPanel.PromptMode.DefaultMode), this);
            
            flexPanel.Children.Clear(); 
            flexPanel.Children.Add(newPanel);
        }
    }

    private async void JouJouwButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!(this.GetVisualRoot() is HomeWindow window))
            throw new NullReferenceException("Panel not in a HomeWindow hierarchy");
            
        if (this.Parent is Panel flexPanel)
        {
            PromptPanel newPanel = await AwaitableTask.AwaitNotifyUI(PromptPanel.CreateAsync(PromptPanel.PromptMode.JouJouwMode), this);
            
            flexPanel.Children.Clear(); 
            flexPanel.Children.Add(newPanel);
        }
    }
}