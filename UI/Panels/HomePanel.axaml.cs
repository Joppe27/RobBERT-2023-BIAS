#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RobBERT_2023_BIAS.UI.Windows;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class HomePanel : UserControl
{
    public HomePanel()
    {
        InitializeComponent();
    }

    private async void ModelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.GetVisualRoot()!.GetType() != typeof(HomeWindow))
            throw new NullReferenceException("Panel not in a HomeWindow hierarchy");

        if (this.Parent is Panel flexPanel)
        {
            PromptPanel promptPanel = await AwaitableTask.AwaitNotifyUi(PromptPanel.CreateAsync());

            flexPanel.Children.Clear();
            flexPanel.Children.Add(promptPanel);
        }
    }

    private async void JouJouwButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.GetVisualRoot()!.GetType() != typeof(HomeWindow))
            throw new NullReferenceException("Panel not in a HomeWindow hierarchy");

        if (this.Parent is Panel flexPanel)
        {
            PronounPromptPanel jouJouwPanel = await AwaitableTask.AwaitNotifyUi(PronounPromptPanel.CreateAsync());

            flexPanel.Children.Clear();
            flexPanel.Children.Add(jouJouwPanel);
        }
    }

    private async void BiasButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.GetVisualRoot()!.GetType() != typeof(HomeWindow))
            throw new NullReferenceException("Panel not in a HomeWindow hierarchy");

        if (this.Parent is Panel flexPanel)
        {
            BiasPanel biasPanel = await AwaitableTask.AwaitNotifyUi(BiasPanel.CreateAsync());

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