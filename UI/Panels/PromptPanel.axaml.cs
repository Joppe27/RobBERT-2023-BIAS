using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using RobBERT_2023_BIAS.Inference;
using RobBERT_2023_BIAS.Inference.Demos;

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class PromptPanel : UserControl
{
    private readonly Robbert _robbert = new();
    private readonly PromptMode _promptMode;
    private readonly DemoJouJouw _demoProcessor = null!;
    
    public PromptPanel(PromptMode mode)
    {
        InitializeComponent();

        _promptMode = mode;

        if (_promptMode == PromptMode.DefaultMode)
        {
            PromptTextBox.Watermark = "Voer een prompt in (vergeet geen <mask>)";
        }
        else
        {
            _demoProcessor = new DemoJouJouw();
            PromptTextBox.Watermark = "Voer een zin in die één voornaamwoord bevat";
            InsertMaskButton.IsEnabled = false;
        }

        this.DetachedFromVisualTree += (sender, args) => _robbert.Dispose();
    }
    
    public enum PromptMode
    {
        DefaultMode,
        JouJouwMode,
    }

    private void SendButton_OnClick(object? sender, RoutedEventArgs e)
    {
        string? prompt = PromptTextBox.Text;
        if (!ValidateInput(prompt))
            return;
        
        ConversationPanel.Children.Add(MakeTextBlock(prompt, true));
        ScrollViewer.ScrollToEnd();
        
        // TODO: make Robbert.Prompt async so this stupidity isn't necessary anymore
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);

        string answer = _promptMode == PromptMode.DefaultMode ? _robbert.Prompt(prompt, 1).Keys.First() : _demoProcessor.Process(_robbert, prompt);
        
        ConversationPanel.Children.Add(MakeTextBlock(answer, false));
        ScrollViewer.ScrollToEnd();
    }

    private bool ValidateInput(string? prompt)
    {
        if (_promptMode == PromptMode.DefaultMode && (prompt == null || !prompt.Contains("<mask>")))
        {
            FlyoutBase.ShowAttachedFlyout(PromptTextBox);
            return false;
        }
        
        if (_promptMode == PromptMode.JouJouwMode && (prompt == null || !new[] { "u", "uw", "jou", "jouw" }.Any(p => prompt.Contains(p, StringComparison.CurrentCultureIgnoreCase))))
        {
            FlyoutBase.ShowAttachedFlyout(PromptTextBox);
            return false;
        }
        
        return true;
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

