#region

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RobBERT_2023_BIAS.UI;

#endregion

namespace RobBERT_2023_BIAS;

public partial class App : Application
{
    public static ServiceProvider ServiceProvider { get; private set; } = null!;
    public static Action<IServiceCollection> AddServices { get; set; } = null!;
    public static IConfiguration Configuration { get; private set; } = null!;
    public static Guid Guid { get; private set; } = Guid.NewGuid();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development" ? "Development" : "Production";
            Configuration = new ConfigurationBuilder().AddJsonFile($"appsettings.{environment}.json", false, false).Build();
            
            var desktopServiceCollection = new ServiceCollection();
            AddServices.Invoke(desktopServiceCollection);
            ServiceProvider = desktopServiceCollection.BuildServiceProvider();

            desktop.MainWindow = new DesktopWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime browser)
        {
            var browserServiceCollection = new ServiceCollection();
            AddServices.Invoke(browserServiceCollection);
            ServiceProvider = browserServiceCollection.BuildServiceProvider();

            browser.MainView = new MainView();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}