#region

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RobBERT_2023_BIAS.UI.Windows;

#endregion

namespace RobBERT_2023_BIAS;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new HomeWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}