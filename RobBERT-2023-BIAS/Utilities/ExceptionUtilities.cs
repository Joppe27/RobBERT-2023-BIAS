#region

using Avalonia;
using Avalonia.Logging;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using RobBERT_2023_BIAS.UI;

#endregion

namespace RobBERT_2023_BIAS.Utilities;

public static class ExceptionUtilities
{
    public static void LogNotify(Visual? sender, Exception ex)
    {
        var logger = App.ServiceProvider.GetRequiredService<ILogSink>();
        
        if (sender != null)
        {
            MainView mainView = sender.GetVisualAncestors().SingleOrDefault(v => v is MainView) as MainView ??
                                throw new InvalidOperationException("Sender is not a child of a MainView");

            mainView.ExceptionThrown.Invoke();
        }
        else
        {
            logger.Log(LogEventLevel.Warning, "NON-AVALONIA", null, "Exception thrown without notifying user!");
        }

        logger.Log(LogEventLevel.Error, "NON-AVALONIA", sender, ex.ToString());
    }
}