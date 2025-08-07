// Copyright (c) Joppe27 <joppe27.be>. Licensed under the MIT Licence.
// See LICENSE file in repository root for full license text.

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
                ExceptionUtilities.LogNotify(this, exception);
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
            PronounPromptPanel jouJouwPanel;

            try
            {
                jouJouwPanel = await TaskUtilities
                    .AwaitNotify(this, PronounPromptPanel.CreateAsync((RobbertVersion)ModelComboBox.SelectedIndex));
            }
            catch (Exception ex)
            {
                ExceptionUtilities.LogNotify(this, ex);
                return;
            }

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
            BiasPanel biasPanel;

            try
            {
                biasPanel = await TaskUtilities.AwaitNotify(this, BiasPanel.CreateAsync((RobbertVersion)ModelComboBox.SelectedIndex));
            }
            catch (Exception ex)
            {
                ExceptionUtilities.LogNotify(this, ex);
                return;
            }

            flexPanel.Children.Clear();
            flexPanel.Children.Add(biasPanel);

            MaximizeView(biasPanel);
        }
    }

    private async void AnalyzeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (this.Parent is Panel flexPanel)
        {
            AnalyzePanel analyzePanel;

            try
            {
                analyzePanel = await TaskUtilities.AwaitNotify(this, AnalyzePanel.CreateAsync());
            }
            catch (Exception ex)
            {
                ExceptionUtilities.LogNotify(this, ex);
                return;
            }

            flexPanel.Children.Clear();
            flexPanel.Children.Add(analyzePanel);

            MaximizeView(analyzePanel);
        }
    }

    private void MaximizeView(UserControl newPanel)
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