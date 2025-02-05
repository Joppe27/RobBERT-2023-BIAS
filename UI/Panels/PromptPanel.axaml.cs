#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using RobBERT_2023_BIAS.Inference;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class PromptPanel : UserControl
{
    private Robbert _robbert = null!;
    protected string ValidatedPrompt = null!;

    protected PromptPanel()
    {
        InitializeComponent();
    }

    public static async Task<PromptPanel> CreateAsync()
    {
        PromptPanel panel = new();

        await panel.InitializeAsync();

        return panel;
    }

    protected virtual async Task InitializeAsync()
    {
        _robbert = await Robbert.CreateAsync();

        PromptTextBox.Watermark = "Voer een prompt in (vergeet geen <mask>)";

        this.DetachedFromVisualTree += (_, _) => _robbert.Dispose();
    }

    private async void SendButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ValidateUserInput(PromptTextBox.Text) && PromptTextBox.Text != null)
            ValidatedPrompt = PromptTextBox.Text;
        else
            return;

        ConversationPanel.Children.Add(MakeTextBlock(ValidatedPrompt, true));
        ScrollViewer.ScrollToEnd();

        string[] answers = ProcessModelOutput(await ProcessUserInput());

        foreach (string answer in answers)
        {
            ConversationPanel.Children.Add(MakeTextBlock(answer, false));
            ScrollViewer.ScrollToEnd();
        }
    }

    protected virtual bool ValidateUserInput(string? prompt)
    {
        if (prompt == null || !prompt.Contains("<mask>"))
        {
            FlyoutBase.ShowAttachedFlyout(PromptTextBox);
            return false;
        }

        return true;
    }

    protected virtual async Task<List<Dictionary<string, float>>> ProcessUserInput() =>
        await AwaitableTask.AwaitNotifyUi(_robbert.Process(ValidatedPrompt, KCountBox.Value != null ? (int)KCountBox.Value : 1));

    protected virtual string[] ProcessModelOutput(List<Dictionary<string, float>> robbertOutput)
    {
        string[] conversationOutputs = new string[robbertOutput.Count];

        for (int mask = 0; mask < robbertOutput.Count; mask++)
        {
            string maskOutput = "";

            for (int i = 0; i < robbertOutput[mask].Keys.Count; i++)
                maskOutput += $"{(i == 0 ? "" : "\n")}{robbertOutput[mask].Keys.ElementAt(i)} (zekerheid: {RoundSignificant(robbertOutput[mask].Values.ElementAt(i), 2)}%)";

            conversationOutputs[mask] = maskOutput;
        }

        return conversationOutputs;
    }

    private string RoundSignificant(float number, int significantDigits = 0)
    {
        // Turn probability (total = 1) into percentage (total = 100).
        number *= 100;

        // Make sure to always round to a significant figure, i.e. never round to 0.
        float tempNumber = number;
        int actualDigits = 0;

        while (tempNumber <= Math.Pow(10, significantDigits - 1) && actualDigits < 6) // actualDigits < 6 because of float precision limit.
        {
            if (number > 1 || number <= 0)
            {
                actualDigits = significantDigits;
                break;
            }

            tempNumber *= 10;
            actualDigits++;
        }

        return float.Round(number, actualDigits).ToString($"F{actualDigits}");
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
}