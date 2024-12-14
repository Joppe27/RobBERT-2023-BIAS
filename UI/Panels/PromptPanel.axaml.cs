using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RobBERT_2023_BIAS.Inference;
using RobBERT_2023_BIAS.Inference.Demos;
using RobBERT_2023_BIAS.UI.Windows;

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class PromptPanel : UserControl
{
    private Robbert _robbert = null!;
    private readonly PromptMode _promptMode;
    private readonly DemoJouJouw _demoProcessor = null!;
    
    private PromptPanel(PromptMode mode)
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
            KCountBox.IsEnabled = false;
            // Avalonia limitation causes difference border between disabled styled and non-styled elements
            KCountBox.BorderBrush = Brushes.LightGray;
        }

        this.DetachedFromVisualTree += (sender, args) => _robbert.Dispose();
    }

    public static async Task<PromptPanel> CreateAsync(PromptMode mode)
    {
        PromptPanel panel = new(mode);

        await panel.InitializeAsync();

        return panel;
    }

    private async Task InitializeAsync() => _robbert = await Robbert.CreateAsync();
    
    public enum PromptMode
    {
        DefaultMode,
        JouJouwMode,
    }

    private async void SendButton_OnClick(object? sender, RoutedEventArgs e)
    {
        string? prompt = PromptTextBox.Text;
        if (!ValidateInput(prompt))
            return;
        
        ConversationPanel.Children.Add(MakeTextBlock(prompt!, true));
        ScrollViewer.ScrollToEnd();

        string[] answers = _promptMode == PromptMode.DefaultMode ? ProcessMultiWordOutput(await AwaitableTask.AwaitNotifyUI(_robbert.Prompt(prompt!, (int)(KCountBox.Value ?? 1)), this)) : [await AwaitableTask.AwaitNotifyUI(_demoProcessor.Process(_robbert, prompt!), this)] ;

        foreach (string answer in answers)
        {
            ConversationPanel.Children.Add(MakeTextBlock(answer, false));
            ScrollViewer.ScrollToEnd();
        }
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

    private string[] ProcessMultiWordOutput(Dictionary<string, float> robbertOutput)
    {
        string[] conversationOutputs = new string[robbertOutput.Count];
        
        for (int i = 0; i < robbertOutput.Count; i++)
            conversationOutputs[i] = $"{robbertOutput.Keys.ElementAt(i)} (zekerheid: {Math.Round(robbertOutput.Values.ElementAt(i), 2)})";

        return conversationOutputs;
    }

    private void MaskButton_OnClick(object? sender, RoutedEventArgs e)
    {
        PromptTextBox.Text += "<mask>";
    }
}

