using Avalonia.Controls;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Avalonia.VisualTree;
using RobBERT_2023_BIAS.UI.Windows;

namespace RobBERT_2023_BIAS;

public class AwaitableTask
{
    private AwaitableTask() { }

    /// <summary>
    /// Performs an asynchronous task while notifying the UI (to show loading indicator)
    /// </summary>
    public static async Task<TResult> AwaitNotifyUI<TResult>(Task<TResult> awaitableTask, Control sender)
    {
        if (!(sender.GetVisualRoot() is HomeWindow window))
            throw new Exception("Control not in a HomeWindow hierarchy");
        
        window.LoadingStarted.Invoke(sender, EventArgs.Empty);
        var result = await awaitableTask;
        window.LoadingFinished.Invoke(sender, EventArgs.Empty);
        
        return result;
    }
}