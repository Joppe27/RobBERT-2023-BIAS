#region

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using RobBERT_2023_BIAS.UI.Windows;

#endregion

namespace RobBERT_2023_BIAS.Utilities;

public static class TaskUtilities
{
    /// <summary>
    /// Performs an asynchronous task while notifying the UI (to show loading indicator)
    /// </summary>
    public static async Task<TResult> AwaitNotifyUi<TResult>(Task<TResult> awaitableTask)
    {
        if (!((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow is HomeWindow homeWindow))
            throw new InvalidOperationException("Main window is not a HomeWindow");

        homeWindow.LoadingStarted.Invoke();
        var result = await awaitableTask;
        homeWindow.LoadingFinished.Invoke();

        return result;
    }

    /// <summary>
    /// Performs an asynchronous task while notifying the UI (to show loading indicator)
    /// </summary>
    public static async Task AwaitNotifyUi(Task awaitableTask)
    {
        if (!((Application.Current!.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)!.MainWindow is HomeWindow homeWindow))
            throw new InvalidOperationException("Main window is not a HomeWindow");

        homeWindow.LoadingStarted.Invoke();
        await awaitableTask;
        homeWindow.LoadingFinished.Invoke();
    }
}