#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using RobBERT_2023_BIAS.Inference;
using RobBERT_2023_BIAS.Utilities;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class PromptPanel : UserControl
{
    protected readonly List<string> ValidatedPrompts = new();
    protected IRobbert Robbert = null!;

    protected PromptPanel()
    {
        InitializeComponent();
    }

    public static async Task<PromptPanel> CreateAsync(RobbertVersion version)
    {
        PromptPanel panel = new();

        await panel.InitializeAsync(version);

        return panel;
    }

    protected virtual async Task InitializeAsync(RobbertVersion version)
    {
        var robbertFactory = App.ServiceProvider.GetRequiredService<IRobbertFactory>();

        Robbert = await robbertFactory.Create(version);

        PromptTextBox.Watermark = "Voer een prompt in (vergeet geen <mask>)";
    }

    protected async void SendButton_OnClick(object? sender, RoutedEventArgs? e)
    {
        ValidatedPrompts.Clear();

        foreach (TextBox textBox in PromptDockPanel.Children.OfType<TextBox>().OrderBy(c => c.Bounds.Top))
        {
            if (ValidateUserInput(textBox.Text) && textBox.Text != null)
            {
                ValidatedPrompts.Add(textBox.Text);
            }
            else
            {
                FlyoutBase.ShowAttachedFlyout(textBox);
                return;
            }
        }

        foreach (string prompt in ValidatedPrompts)
        {
            ConversationPanel.Children.Add(MakeTextBlock(prompt, true));
            ScrollViewer.ScrollToEnd();
        }

        string[] answers = ProcessModelOutput(await TaskUtilities.AwaitNotifyUi(this, ProcessUserInput()));

        foreach (string answer in answers)
        {
            ConversationPanel.Children.Add(MakeTextBlock(answer, false));
            ScrollViewer.ScrollToEnd();
        }
    }

    private void PromptTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
            SendButton_OnClick(this, null);
    }

    protected virtual bool ValidateUserInput(string? prompt) => prompt != null && prompt.Contains("mask");

    protected virtual async Task<List<Dictionary<string, float>>> ProcessUserInput() =>
        await Robbert.Process(ValidatedPrompts.Count == 1
                ? ValidatedPrompts[0]
                : throw new InvalidOperationException($"Input {ValidatedPrompts.Count} prompts while only 1 is supported"),
            KCountBox.Value != null ? (int)KCountBox.Value : 1);

    protected virtual string[] ProcessModelOutput(List<Dictionary<string, float>> robbertOutput)
    {
        string[] conversationOutputs = new string[robbertOutput.Count];

        for (int mask = 0; mask < robbertOutput.Count; mask++)
        {
            string maskOutput = "";

            for (int i = 0; i < robbertOutput[mask].Keys.Count; i++)
            {
                maskOutput += String.Format("{0}{1} (zekerheid: {2}%)",
                    i == 0 ? "" : "\n",
                    robbertOutput[mask].Keys.ElementAt(i),
                    MathUtilities.RoundSignificant(robbertOutput[mask].Values.ElementAt(i), 2));
            }

            conversationOutputs[mask] = maskOutput;
        }

        return conversationOutputs;
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

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        if (Robbert != null)
            Robbert.Dispose();

        base.OnDetachedFromLogicalTree(e);
    }
}