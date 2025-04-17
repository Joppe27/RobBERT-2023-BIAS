#region

using System;
using System.Diagnostics;
using System.Net.Http;
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
        App.AddServices = collection =>
        {
            collection
                .AddSingleton<IRobbertFactory, OnlineRobbert.Factory>()
                .AddSingleton(new HttpClient()
                {
                    // TODO: instead of increasing timeout, avoid long-running function in the first place https://learn.microsoft.com/en-us/azure/azure-functions/performance-reliability#avoid-long-running-functions
                    BaseAddress = new Uri("https://api.bias.joppe27.be/"),
                    Timeout = TimeSpan.FromMinutes(5),
                });
        };

        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }
    
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().LogToTrace();
}