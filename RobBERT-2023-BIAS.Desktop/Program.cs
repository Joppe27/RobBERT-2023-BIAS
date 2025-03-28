﻿#region

using System;
using System.Linq;
using Avalonia;
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
        if (args.Contains("--useserver"))
            App.AddServices = serviceCollection => serviceCollection.AddSingleton<IRobbertFactory, OnlineRobbert.Factory>();
        else
            App.AddServices = serviceCollection => serviceCollection.AddSingleton<IRobbertFactory, LocalRobbert.Factory>();

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