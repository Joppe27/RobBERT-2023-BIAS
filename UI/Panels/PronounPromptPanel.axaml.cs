#region

using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using RobBERT_2023_BIAS.Inference;

#endregion

namespace RobBERT_2023_BIAS.UI.Panels;

public partial class PronounPromptPanel : PromptPanel
{
    private Robbert _robbert = null!;

    private readonly string[] _possiblePronouns = ["jou", "jouw", "u", "uw"];
    private readonly string[] _politePronouns = ["u", "uw"];
    private readonly string[] _familiarPronouns = ["jou", "jouw"];
    private readonly List<(string Pronoun, bool PoliteForm)> _userPronouns = new();
    
    private PronounPromptPanel()
    {
        InitializeComponent();
    }

    public new static async Task<PronounPromptPanel> CreateAsync(Robbert.RobbertVersion version)
    {
        PronounPromptPanel pronounPanel = new();

        await pronounPanel.InitializeAsync(version);

        return pronounPanel;
    }

    protected override async Task InitializeAsync(Robbert.RobbertVersion version)
    {
        await base.InitializeAsync(version);

        _robbert = Robbert;

        PromptTextBox.Watermark = "Voer een zin in die één voornaamwoord bevat";
        InsertMaskButton.IsEnabled = false;
        KCountBox.IsEnabled = false;
    }

    protected override bool ValidateUserInput(string? prompt)
    {
        if (prompt == null || !_possiblePronouns.Any(p => prompt.Contains(p, StringComparison.CurrentCultureIgnoreCase)))
        {
            FlyoutBase.ShowAttachedFlyout(PromptTextBox);
            return false;
        }

        return true;
    }

    private string PrepareUserInput(string userInput)
    {
        _userPronouns.Clear();

        string[] split = userInput.Split(' ', StringSplitOptions.TrimEntries);
        for (var i = 0; i < split.Length; i++)
        {
            if (_possiblePronouns.Contains(split[i], StringComparer.CurrentCultureIgnoreCase))
            {
                _userPronouns.Add((split[i], _politePronouns.Contains(split[i], StringComparer.CurrentCultureIgnoreCase)));
                split[i] = "<mask>";
            }
        }

        return String.Join(' ', split);
    }

    protected override async Task<List<Dictionary<string, float>>> ProcessUserInput()
    {
        List<Dictionary<string, float>> modelOutput = await _robbert.Process(PrepareUserInput(ValidatedPrompts.Count == 1 ? ValidatedPrompts[0] : throw new Exception()), 200);
        List<Dictionary<string, float>> modelPronouns = new();

        for (int mask = 0; mask < modelOutput.Count; mask++)
        {
            for (int i = 0; mask < modelOutput[mask].Count; i++)
            {
                string pronoun = "";
                string currentCandidateToken = modelOutput[mask].Keys.ElementAt(i);
                float currentCandidateProbability = modelOutput[mask].Values.ElementAt(i);

                if (_userPronouns[mask].PoliteForm && _politePronouns.Contains(currentCandidateToken, StringComparer.CurrentCultureIgnoreCase))
                    pronoun = currentCandidateToken;
                else if (!_userPronouns[mask].PoliteForm && _familiarPronouns.Contains(currentCandidateToken, StringComparer.CurrentCultureIgnoreCase))
                    pronoun = currentCandidateToken;

                if (pronoun == "" && i < modelOutput[mask].Count - 1)
                    continue;

                // If none of the pronouns are found in the dictionary (i.e. when kCount isn't large enough), create empty tuple.
                if (pronoun == "" && i == modelOutput[mask].Count - 1)
                {
                    modelPronouns.Add(new Dictionary<string, float>() { { string.Empty, -1 } });
                    break;
                }

                float incorrectModelConfidence = modelOutput[mask].GetValueOrDefault(_userPronouns[mask].PoliteForm ? _politePronouns.First(p => p != currentCandidateToken) : _familiarPronouns.First(p => p != currentCandidateToken), 0);
                float total = currentCandidateProbability + incorrectModelConfidence;
                float confidence = currentCandidateProbability / total * 100;

                modelPronouns.Add(new Dictionary<string, float>() { { pronoun, confidence } });
                break;
            }
        }

        return modelPronouns;
    }

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
                _userPronouns[pronoun].Pronoun.Equals(robbertOutput[pronoun].Keys.ElementAt(0), StringComparison.CurrentCultureIgnoreCase) ? "juist" : "fout",
                robbertOutput[pronoun].Keys.ElementAt(0),
                Math.Round(robbertOutput[pronoun].Values.ElementAt(0), 2));
        }

        return processedModelOutput;
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        if (_robbert != null)
            _robbert.Dispose();

        base.OnDetachedFromLogicalTree(e);
    }
}