#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using RobBERT_2023_BIAS.Utilities;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class BiasPromptPanel : PromptPanel
{
    public event EventHandler<BiasOutputEventArgs> OnModelOutput = null!; 

    private BiasPromptPanel()
    {
        InitializeComponent();
    }

    public new static async Task<BiasPromptPanel> CreateAsync()
    {
        BiasPromptPanel biasPromptPanel = new();

        await biasPromptPanel.InitializeAsync();

        return biasPromptPanel;
    }

    private new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        PromptTextBox.Watermark = "Enter a prompt with a length of max. 6 words";

        TextBox extraTextBox = new TextBox()
        {
            Name = "ExtraTextBox",
            Margin = new Thickness(0, 8, 0, 0),
            Watermark = PromptTextBox.Watermark,
        };

        FlyoutBase flyout = FlyoutBase.GetAttachedFlyout(PromptTextBox) ?? throw new NullReferenceException();
        flyout.SetValue(PopupFlyoutBase.HorizontalOffsetProperty, flyout.GetValue(PopupFlyoutBase.HorizontalOffsetProperty) * 2);

        FlyoutBase.SetAttachedFlyout(extraTextBox, flyout);

        PromptDockPanel.Children.Insert(0, extraTextBox);
        DockPanel.SetDock(extraTextBox, Dock.Bottom);
    }

    protected override bool ValidateUserInput(string? prompt) => prompt != null && prompt.Split(' ').Length < 6;

    protected override async Task<List<Dictionary<string, float>>> ProcessUserInput()
    {
        if (ValidatedPrompts.Count != 2)
            throw new Exception();

        List<Dictionary<string, float>> firstOutput = await TaskUtilities.AwaitNotifyUi(Robbert.Process(ValidatedPrompts[0], KCountBox.Value != null ? (int)KCountBox.Value : 1, true));
        List<Dictionary<string, float>> secondOutput = await TaskUtilities.AwaitNotifyUi(Robbert.Process(ValidatedPrompts[1], KCountBox.Value != null ? (int)KCountBox.Value : 1, true));

        OnModelOutput.Invoke(this, new BiasOutputEventArgs(firstOutput, secondOutput));

        firstOutput.AddRange(secondOutput);
        return firstOutput;
    }
}

public class BiasOutputEventArgs(List<Dictionary<string, float>> firstPrompt, List<Dictionary<string, float>> secondPrompt) : EventArgs
{
    public List<Dictionary<string, float>> FirstPrompt { get; } = firstPrompt;
    public List<Dictionary<string, float>> SecondPrompt { get; } = secondPrompt;
}