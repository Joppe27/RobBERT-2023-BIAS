#region

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Logging;
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
                    BaseAddress = new Uri("https://api.bias.joppe27.be/"),
                })
                .AddSingleton(Logger.Sink ?? throw new NullReferenceException());
            ;
        };

        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }
    
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().LogToTrace();
}