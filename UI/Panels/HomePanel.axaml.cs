using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using RobBERT_2023_BIAS.UI.Windows;

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class HomePanel : UserControl
{
    public HomePanel()
    {
        InitializeComponent();
    }

    private void ModelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.Parent is Panel flexPanel)
        {
            flexPanel.Children.Clear(); 
            flexPanel.Children.Add(new PromptPanel(PromptPanel.PromptMode.DefaultMode));
        }
            
    }

    private void JouJouwButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.Parent is Panel flexPanel)
        {
            flexPanel.Children.Clear(); 
            flexPanel.Children.Add(new PromptPanel(PromptPanel.PromptMode.JouJouwMode));
        }
    }
}