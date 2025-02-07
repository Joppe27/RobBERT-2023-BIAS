#region

using Avalonia.Controls.Primitives;
using Avalonia.Media;
using RobBERT_2023_BIAS.Inference.Demos;
using RobBERT_2023_BIAS.Utilities;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class PronounPromptPanel : PromptPanel
{
    private PronounDemo _pronounDemo = null!;

    private PronounPromptPanel()
    {
        InitializeComponent();
    }

    public new static async Task<PronounPromptPanel> CreateAsync()
    {
        PronounPromptPanel pronounPanel = new();

        await pronounPanel.InitializeAsync();

        return pronounPanel;
    }

    protected override async Task InitializeAsync()
    {
        _pronounDemo = await TaskUtilities.AwaitNotifyUi(PronounDemo.CreateAsync());

        PromptTextBox.Watermark = "Voer een zin in die één voornaamwoord bevat";
        InsertMaskButton.IsEnabled = false;
        KCountBox.IsEnabled = false;
        // Avalonia limitation causes difference border between disabled styled and non-styled elements
        KCountBox.BorderBrush = Brushes.LightGray;

        this.DetachedFromVisualTree += (_, _) => _pronounDemo.Dispose();
    }

    protected override bool ValidateUserInput(string? prompt)
    {
        if (prompt == null || !_pronounDemo.PossiblePronouns.Any(p => prompt.Contains(p, StringComparison.CurrentCultureIgnoreCase)))
        {
            FlyoutBase.ShowAttachedFlyout(PromptTextBox);
            return false;
        }

        return true;
    }

    private string PrepareUserInput(string userInput)
    {
        _pronounDemo.UserPronouns.Clear();

        string[] split = userInput.Split(' ', StringSplitOptions.TrimEntries);
        for (var i = 0; i < split.Length; i++)
        {
            if (_pronounDemo.PossiblePronouns.Contains(split[i], StringComparer.CurrentCultureIgnoreCase))
            {
                _pronounDemo.UserPronouns.Add((split[i], _pronounDemo.PolitePronouns.Contains(split[i], StringComparer.CurrentCultureIgnoreCase)));
                split[i] = "<mask>";
            }
        }

        return String.Join(' ', split);
    }

    protected override async Task<List<Dictionary<string, float>>> ProcessUserInput() =>
        await TaskUtilities.AwaitNotifyUi(_pronounDemo.Process(PrepareUserInput(ValidatedPrompt)));

    protected override string[] ProcessModelOutput(List<Dictionary<string, float>> robbertOutput)
    {
        string[] processedModelOutput = new string[robbertOutput.Count];

        for (int pronoun = 0; pronoun < robbertOutput.Count; pronoun++)
        {
            // Warn user that kCount wasn't large enough to calculate an answer.
            if (robbertOutput[pronoun].Values.ElementAt(0) < 0)
            {
                processedModelOutput[pronoun] = $"Het {pronoun + 1}{(pronoun == 0 ? "ste" : "de")} voornaamwoord werd niet berekend om de laadtijd te beperken.";
                break;
            }

            processedModelOutput[pronoun] = String.Format("Het {0}{1} voornaamwoord is {2}. Het correcte voornaamwoord is {3} (met {4}% zekerheid).",
                pronoun + 1,
                pronoun == 0 ? "ste" : "de",
                _pronounDemo.UserPronouns[pronoun].Pronoun.Equals(robbertOutput[pronoun].Keys.ElementAt(0), StringComparison.CurrentCultureIgnoreCase) ? "juist" : "fout",
                robbertOutput[pronoun].Keys.ElementAt(0),
                Math.Round(robbertOutput[pronoun].Values.ElementAt(0), 2));
        }

        return processedModelOutput;
    }
}