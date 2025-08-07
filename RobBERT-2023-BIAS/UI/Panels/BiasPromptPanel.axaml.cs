// Copyright (c) Joppe27 <joppe27.be>. Licensed under the MIT Licence.
// See LICENSE file in repository root for full license text.

#region

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using RobBERT_2023_BIAS.Inference;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class BiasPromptPanel : PromptPanel
{
    private IRobbert _robbert = null!;

    private BiasPromptPanel()
    {
        InitializeComponent();
    }

    public event EventHandler<BiasOutputEventArgs> OnModelOutput = null!;

    public new static async Task<BiasPromptPanel> CreateAsync(RobbertVersion version)
    {
        BiasPromptPanel biasPromptPanel = new();

        await biasPromptPanel.InitializeAsync(version);

        return biasPromptPanel;
    }

    private new async Task InitializeAsync(RobbertVersion version)
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
            TabIndex = 1,
        };
        extraTextBox.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Return)
                base.SendButton_OnClick(this, null);
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

        List<RobbertPrompt> prompts = new List<RobbertPrompt>() { new(ValidatedPrompts[0]), new(ValidatedPrompts[1]) };
        var output = await _robbert.ProcessBatch(prompts, 10, CancellationToken.None);

        OnModelOutput.Invoke(this, new BiasOutputEventArgs(output[0], output[1]));

        return output.SelectMany(l => l).ToList();
    }
}

public class BiasOutputEventArgs(List<Dictionary<string, float>> firstPrompt, List<Dictionary<string, float>> secondPrompt) : EventArgs
{
    public List<Dictionary<string, float>> FirstPrompt { get; } = firstPrompt;
    public List<Dictionary<string, float>> SecondPrompt { get; } = secondPrompt;
}