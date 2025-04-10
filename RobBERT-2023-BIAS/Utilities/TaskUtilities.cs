#region

using Avalonia;
using Avalonia.VisualTree;
using RobBERT_2023_BIAS.UI;

#endregion

namespace RobBERT_2023_BIAS.Utilities;

public static class TaskUtilities
{
    /// <summary>
    /// Performs an asynchronous task while notifying the UI (to show loading indicator)
    /// </summary>
    public static async Task<TResult> AwaitNotify<TResult>(Visual sender, Task<TResult> awaitableTask)
    {
        MainView mainView = sender.GetVisualAncestors().SingleOrDefault(v => v is MainView) as MainView ??
                            throw new InvalidOperationException("Sender is not a child of a MainView");

        mainView.LoadingStarted.Invoke();

        TResult result;
        try
        {
            result = await awaitableTask;
        }
        finally
        {
            mainView.LoadingFinished.Invoke();
        }
        
        return result;
    }

    /// <summary>
    /// Performs an asynchronous task while notifying the UI (to show loading indicator)
    /// </summary>
    public static async Task AwaitNotify(Visual sender, Task awaitableTask)
    {
        MainView mainView = sender.GetVisualAncestors().SingleOrDefault(v => v is MainView) as MainView ??
                            throw new InvalidOperationException("Sender is not a child of a MainView");

        mainView.LoadingStarted.Invoke();

        try
        {
            await awaitableTask;
        }
        finally
        {
            mainView.LoadingFinished.Invoke();
        }
    }
}