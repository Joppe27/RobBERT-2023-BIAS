#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RobBERT_2023_BIAS.Inference;
using RobBERT_2023_BIAS.Utilities;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class HomePanel : UserControl
{
    public HomePanel()
    {
        InitializeComponent();

        ModelComboBox.Items.Insert((int)RobbertVersion.Base2022, "RobBERT-2022-base");
        ModelComboBox.Items.Insert((int)RobbertVersion.Base2023, "RobBERT-2023-base");
        ModelComboBox.Items.Insert((int)RobbertVersion.Large2023, "RobBERT-2023-large");
    }

    private async void ModelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!ValidateModelSelection())
            return;

        if (this.Parent is Panel flexPanel)
        {
            PromptPanel promptPanel;
            try
            {
                promptPanel = await TaskUtilities
                    .AwaitNotify(this, PromptPanel.CreateAsync((RobbertVersion)ModelComboBox.SelectedIndex));
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                ExceptionUtilities.ExceptionNotify(this, exception);
                return;
            }

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
                .AwaitNotify(this, PronounPromptPanel.CreateAsync((RobbertVersion)ModelComboBox.SelectedIndex));

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
            BiasPanel biasPanel = await TaskUtilities.AwaitNotify(this, BiasPanel.CreateAsync((RobbertVersion)ModelComboBox.SelectedIndex));

            flexPanel.Children.Clear();
            flexPanel.Children.Add(biasPanel);

            TryMaximizeWindow(biasPanel);
        }
    }

    private async void AnalyzeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.Parent is Panel flexPanel)
        {
            AnalyzePanel analyzePanel = await TaskUtilities.AwaitNotify(this, AnalyzePanel.CreateAsync());

            flexPanel.Children.Clear();
            flexPanel.Children.Add(analyzePanel);

            TryMaximizeWindow(analyzePanel);
        }
    }

    private void TryMaximizeWindow(UserControl newPanel)
    {
        if (OperatingSystem.IsBrowser())
        {
            MainView mainView = newPanel.GetVisualAncestors().SingleOrDefault(v => v is MainView) as MainView ??
                                throw new InvalidOperationException("AnalyzePanel is not a child of a MainView");

            mainView.Width = Double.NaN;
            mainView.Height = Double.NaN;
        }
        else
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