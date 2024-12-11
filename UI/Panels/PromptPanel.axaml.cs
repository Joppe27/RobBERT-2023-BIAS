using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using RobBERT_2023_BIAS.Inference;

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class PromptPanel : UserControl
{
    private readonly Robbert _robbert = new();
    
    public PromptPanel(PromptMode mode)
    {
        InitializeComponent();
    }
    
    public enum PromptMode
    {
        DefaultMode,
        JouJouwMode,
    }

    private void SendButton_OnClick(object? sender, RoutedEventArgs e)
    {
        string prompt = PromptTextBox.Text ?? throw new NullReferenceException("Empty prompt!");
        
        ConversationPanel.Children.Add(MakeTextBlock(prompt, true));
        ScrollViewer.ScrollToEnd();
        
        // TODO: make Robbert.Prompt async so this stupidity isn't necessary anymore
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);

        string answer = _robbert.Prompt(prompt, 1).Keys.First();
        
        ConversationPanel.Children.Add(MakeTextBlock(answer, false));
        ScrollViewer.ScrollToEnd();
    }

    private Border MakeTextBlock(string text, bool user)
    {
        return new Border()
        {
            MinHeight = 32,
            MaxWidth = 356,
            HorizontalAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = user ? Brushes.BlueViolet : Brushes.WhiteSmoke,
            CornerRadius = new CornerRadius(6),
            BoxShadow = new BoxShadows(new BoxShadow()
            {
                Color = user ? Colors.Purple : Colors.LightGray,
                IsInset = true,
                OffsetY = -2,
                Blur = 2,
            }),
            Child = new TextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = user ? Brushes.White : Brushes.Black,
                Margin = new Thickness(8, 8),
                Text = text,
            }
        };
    }

    private void MaskButton_OnClick(object? sender, RoutedEventArgs e)
    {
        PromptTextBox.Text += "<mask>";
    }

    private void PasteButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is TopLevel topLevel && topLevel.Clipboard is IClipboard clipboard)
            PromptTextBox.Text += clipboard.GetTextAsync().Result;
    }
}

