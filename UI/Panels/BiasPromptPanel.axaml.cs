#region

using Avalonia;
using Avalonia.Controls;
using RobBERT_2023_BIAS.Utilities;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class BiasPromptPanel : PromptPanel
{
    private TextBox _extraTextBox;
    private string _validatedExtraPrompt;

    public event EventHandler<BiasOutputEventArgs> OnModelOutput; 

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
        // TODO: after pressing send both prompts should show instead of only first one
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
        List<Dictionary<string, float>> firstOutput = await TaskUtilities.AwaitNotifyUi(Robbert.Process(ValidatedPrompt, KCountBox.Value != null ? (int)KCountBox.Value : 1, true));
        List<Dictionary<string, float>> secondOutput = await TaskUtilities.AwaitNotifyUi(Robbert.Process(_validatedExtraPrompt, KCountBox.Value != null ? (int)KCountBox.Value : 1, true));

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