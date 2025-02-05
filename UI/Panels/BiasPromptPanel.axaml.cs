#region

using Avalonia;
using Avalonia.Controls;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class BiasPromptPanel : PromptPanel
{
    private TextBox _extraTextBox;
    private string _validatedExtraPrompt;

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

        _extraTextBox = new TextBox() { Name = "ExtraTextBox", Margin = new Thickness(0, 8, 0, 0) };
        PromptDockPanel.Children.Insert(0, _extraTextBox);
        DockPanel.SetDock(_extraTextBox, Dock.Bottom);
    }

    protected override bool ValidateUserInput(string? prompt)
    {
        // TODO: make popup work
        if (prompt == null || prompt.Split(' ').Length > 6 || _extraTextBox.Text == null || _extraTextBox.Text.Split(' ').Length > 6)
        {
            return false;
        }

        _validatedExtraPrompt = _extraTextBox.Text;
        return true;
    }

    protected override async Task<List<Dictionary<string, float>>> ProcessUserInput()
    {
        List<Dictionary<string, float>> modelOutput = await AwaitableTask.AwaitNotifyUi(Robbert.Process(ValidatedPrompt, KCountBox.Value != null ? (int)KCountBox.Value : 1, true));

        modelOutput.AddRange(await AwaitableTask.AwaitNotifyUi(Robbert.Process(_validatedExtraPrompt, KCountBox.Value != null ? (int)KCountBox.Value : 1, true)));

        return modelOutput;
    }
}