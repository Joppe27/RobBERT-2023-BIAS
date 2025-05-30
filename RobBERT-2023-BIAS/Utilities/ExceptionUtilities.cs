// Copyright (c) Joppe27 <joppe27.be>. Licensed under the MIT Licence.
// See LICENSE file in repository root for full license text.

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

        if (sender != null && sender.GetVisualAncestors().SingleOrDefault(v => v is MainView) is MainView mainView)
            mainView.ExceptionThrown.Invoke();
        else
            logger.Log(LogEventLevel.Warning, "NON-AVALONIA", null, "Exception thrown without notifying user!");

        logger.Log(LogEventLevel.Error, "NON-AVALONIA", sender, ex.ToString());
    }
}