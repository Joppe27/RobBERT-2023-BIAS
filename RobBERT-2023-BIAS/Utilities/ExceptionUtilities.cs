#region

using Avalonia;
using Avalonia.VisualTree;
using RobBERT_2023_BIAS.UI;

#endregion

namespace RobBERT_2023_BIAS.Utilities;

public static class ExceptionUtilities
{
    public static void LogNotify(Visual? sender, Exception ex)
    {
        if (sender != null)
        {
            MainView mainView = sender.GetVisualAncestors().SingleOrDefault(v => v is MainView) as MainView ??
                                throw new InvalidOperationException("Sender is not a child of a MainView");

            mainView.ExceptionThrown.Invoke();
        }
        else
        {
            Console.WriteLine("Exception thrown without notifying user!");
        }

        // TODO: ILOGGER HERE https://learn.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line#get-an-ilogger-from-di
        Console.WriteLine(ex);
    }
}