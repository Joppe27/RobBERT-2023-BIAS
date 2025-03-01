#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using RobBERT_2023_BIAS.Inference;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class BiasPromptPanel : PromptPanel
{
    private Robbert _robbert = null!;

    private BiasPromptPanel()
    {
        InitializeComponent();
    }

    public event EventHandler<BiasOutputEventArgs> OnModelOutput = null!;

    public new static async Task<BiasPromptPanel> CreateAsync(Robbert.RobbertVersion version)
    {
        BiasPromptPanel biasPromptPanel = new();

        await biasPromptPanel.InitializeAsync(version);

        return biasPromptPanel;
    }

    private new async Task InitializeAsync(Robbert.RobbertVersion version)
    {
        await base.InitializeAsync(version);

        _robbert = Robbert;

        InsertMaskButton.IsEnabled = false;
        KCountBox.IsEnabled = false;

        PromptTextBox.Watermark = "Enter a prompt";

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

    protected override bool ValidateUserInput(string? prompt) => prompt != null;

    protected override async Task<List<Dictionary<string, float>>> ProcessUserInput()
    {
        if (ValidatedPrompts.Count != 2)
            throw new InvalidOperationException($"Input {ValidatedPrompts.Count} prompts while only 2 is supported");

        List<Dictionary<string, float>> firstOutput = await _robbert.Process(ValidatedPrompts[0], 10, true);
        List<Dictionary<string, float>> secondOutput = await _robbert.Process(ValidatedPrompts[1], 10, true);

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