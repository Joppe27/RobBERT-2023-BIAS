#region

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Microsoft.Extensions.DependencyInjection;
using RobBERT_2023_BIAS;
using RobBERT_2023_BIAS.Browser;
using RobBERT_2023_BIAS.Inference;

#endregion

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        App.AddServices = serviceCollection => serviceCollection.AddSingleton<IRobbertFactory, OnlineRobbert.Factory>();

        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }
    
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().LogToTrace();
}