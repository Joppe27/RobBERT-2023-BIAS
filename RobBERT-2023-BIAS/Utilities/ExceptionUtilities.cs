#region

using Avalonia;
using Avalonia.VisualTree;
using RobBERT_2023_BIAS.UI;

#endregion

namespace RobBERT_2023_BIAS.Utilities;

public static class ExceptionUtilities
{
    public static void ExceptionNotify(Visual sender, Exception ex)
    {
        MainView mainView = sender.GetVisualAncestors().SingleOrDefault(v => v is MainView) as MainView ??
                            throw new InvalidOperationException("Sender is not a child of a MainView");

        mainView.ExceptionThrown.Invoke();
    }
}