#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using RobBERT_2023_BIAS.Inference;
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

        if (this.Parent is Panel flexPanel)
        {
            PromptPanel promptPanel = await TaskUtilities
                .AwaitNotifyUi(this, PromptPanel.CreateAsync((Robbert.RobbertVersion)ModelComboBox.SelectedIndex));

            flexPanel.Children.Clear();
            flexPanel.Children.Add(promptPanel);
        }
    }

    private async void JouJouwButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ValidateModelSelection())
            return;

        if (this.Parent is Panel flexPanel)
        {
            PronounPromptPanel jouJouwPanel = await TaskUtilities
                .AwaitNotifyUi(this, PronounPromptPanel.CreateAsync((Robbert.RobbertVersion)ModelComboBox.SelectedIndex));

            flexPanel.Children.Clear();
            flexPanel.Children.Add(jouJouwPanel);
        }
    }

    private async void BiasButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ValidateModelSelection())
            return;

        if (this.Parent is Panel flexPanel)
        {
            BiasPanel biasPanel = await TaskUtilities.AwaitNotifyUi(this, BiasPanel.CreateAsync((Robbert.RobbertVersion)ModelComboBox.SelectedIndex));

            flexPanel.Children.Clear();
            flexPanel.Children.Add(biasPanel);

            TryMaximizeWindow();
        }
    }

    private async void AnalyzeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.Parent is Panel flexPanel)
        {
            AnalyzePanel analyzePanel = await TaskUtilities.AwaitNotifyUi(this, AnalyzePanel.CreateAsync());

            flexPanel.Children.Clear();
            flexPanel.Children.Add(analyzePanel);

            TryMaximizeWindow();
        }
    }

    private void TryMaximizeWindow()
    {
        if (!OperatingSystem.IsBrowser())
        {
            var desktopWindow = ((ClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;

            desktopWindow.WindowState = WindowState.Maximized;
            desktopWindow.SystemDecorations = SystemDecorations.BorderOnly;
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