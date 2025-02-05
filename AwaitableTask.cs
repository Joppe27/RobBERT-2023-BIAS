#region

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using RobBERT_2023_BIAS.UI.Windows;

#endregion

namespace RobBERT_2023_BIAS;

public class AwaitableTask
{
    private AwaitableTask()
    {
    }

    /// <summary>
    /// Performs an asynchronous task while notifying the UI (to show loading indicator)
    /// </summary>
    public static async Task<TResult> AwaitNotifyUi<TResult>(Task<TResult> awaitableTask)
    {
        if (!((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow is HomeWindow homeWindow))
            throw new Exception("Main window was not HomeWindow");

        homeWindow.LoadingStarted.Invoke();
        var result = await awaitableTask;
        homeWindow.LoadingFinished.Invoke();

        return result;
    }
}