#region

using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using Avalonia;
using Avalonia.Logging;
using Microsoft.Extensions.DependencyInjection;
using RobBERT_2023_BIAS.Browser;
using RobBERT_2023_BIAS.Inference;

#endregion

namespace RobBERT_2023_BIAS;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        App.AddServices = collection =>
        {
            if (args.Contains("--useserver"))
                collection.AddSingleton<IRobbertFactory, OnlineRobbert.Factory>();
            else
                collection.AddSingleton<IRobbertFactory, LocalRobbert.Factory>();

            collection
                .AddSingleton(new HttpClient()
                {
                    BaseAddress = new Uri(App.Configuration.GetSection("ApiUri").Value ?? throw new NullReferenceException()),
                    Timeout = TimeSpan.FromMinutes(5),
                })
                .AddSingleton(Logger.Sink ?? throw new NullReferenceException());
        };

        Trace.Listeners.Add(new ConsoleTraceListener());

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }
    
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}