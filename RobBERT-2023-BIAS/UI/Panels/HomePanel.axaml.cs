#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RobBERT_2023_BIAS.Inference;
using RobBERT_2023_BIAS.UI.Windows;
using RobBERT_2023_BIAS.Utilities;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class HomePanel : UserControl
{
    public HomePanel()
    {
        InitializeComponent();

        ModelComboBox.Items.Insert((int)Robbert.RobbertVersion.Base2022, "RobBERT-2022-base");
        ModelComboBox.Items.Insert((int)Robbert.RobbertVersion.Base2023, "RobBERT-2023-base");
        ModelComboBox.Items.Insert((int)Robbert.RobbertVersion.Large2023, "RobBERT-2023-large");
    }

    private async void ModelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ValidateModelSelection())
            return;

        if (this.GetVisualRoot()!.GetType() != typeof(HomeWindow))
            throw new NullReferenceException("Panel not in a HomeWindow hierarchy");

        if (this.Parent is Panel flexPanel)
        {
            PromptPanel promptPanel = await TaskUtilities
                .AwaitNotifyUi(PromptPanel.CreateAsync((Robbert.RobbertVersion)ModelComboBox.SelectedIndex));

            flexPanel.Children.Clear();
            flexPanel.Children.Add(promptPanel);
        }
    }

    private async void JouJouwButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ValidateModelSelection())
            return;

        if (this.GetVisualRoot()!.GetType() != typeof(HomeWindow))
            throw new NullReferenceException("Panel not in a HomeWindow hierarchy");

        if (this.Parent is Panel flexPanel)
        {
            PronounPromptPanel jouJouwPanel = await TaskUtilities
                .AwaitNotifyUi(PronounPromptPanel.CreateAsync((Robbert.RobbertVersion)ModelComboBox.SelectedIndex));

            flexPanel.Children.Clear();
            flexPanel.Children.Add(jouJouwPanel);
        }
    }

    private async void BiasButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ValidateModelSelection())
            return;

        if (this.GetVisualRoot()!.GetType() != typeof(HomeWindow))
            throw new NullReferenceException("Panel not in a HomeWindow hierarchy");

        if (this.Parent is Panel flexPanel)
        {
            BiasPanel biasPanel = await TaskUtilities.AwaitNotifyUi(BiasPanel.CreateAsync((Robbert.RobbertVersion)ModelComboBox.SelectedIndex));

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

    private async void AnalyzeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.GetVisualRoot()!.GetType() != typeof(HomeWindow))
            throw new NullReferenceException("Panel not in a HomeWindow hierarchy");

        if (this.Parent is Panel flexPanel)
        {
            AnalyzePanel analyzePanel = await TaskUtilities.AwaitNotifyUi(AnalyzePanel.CreateAsync());

            flexPanel.Children.Clear();
            flexPanel.Children.Add(analyzePanel);

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

    private bool ValidateModelSelection()
    {
        if (ModelComboBox.SelectedIndex < 0)
        {
            FlyoutBase.ShowAttachedFlyout(ModelComboBox);
            return false;
        }

        return true;
    }
}