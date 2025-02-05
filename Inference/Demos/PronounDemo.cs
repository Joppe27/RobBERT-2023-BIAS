namespace RobBERT_2023_BIAS.Inference.Demos;

public class PronounDemo : IDisposable
{
    private Robbert _robbert = null!;

    public readonly string[] PossiblePronouns = ["jou", "jouw", "u", "uw"];
    public readonly string[] PolitePronouns = ["u", "uw"];
    private readonly string[] _familiarPronouns = ["jou", "jouw"];

    public readonly List<(string Pronoun, bool PoliteForm)> UserPronouns = new();
    private readonly List<Dictionary<string, float>> _modelPronouns = new();

    private PronounDemo()
    {
    }

    public static async Task<PronounDemo> CreateAsync()
    {
        PronounDemo pronounDemo = new();

        await pronounDemo.InitializeAsync();

        return pronounDemo;
    }

    private async Task InitializeAsync()
    {
        _robbert = await Robbert.CreateAsync();
    }

    public async Task<List<Dictionary<string, float>>> Process(string preparedPrompt)
    {
        // kCount = 200 as this is sufficient to calculate an answer in most cases and keeps loading times short for this demo.
        List<Dictionary<string, float>> modelOutput = await _robbert.Process(preparedPrompt, 200);

        _modelPronouns.Clear();

        for (int mask = 0; mask < modelOutput.Count; mask++)
        {
            for (int i = 0; mask < modelOutput[mask].Count; i++)
            {
                string pronoun = "";
                string currentCandidateToken = modelOutput[mask].Keys.ElementAt(i);
                float currentCandidateProbability = modelOutput[mask].Values.ElementAt(i);

                if (UserPronouns[mask].PoliteForm && PolitePronouns.Contains(currentCandidateToken, StringComparer.CurrentCultureIgnoreCase))
                    pronoun = currentCandidateToken;
                else if (!UserPronouns[mask].PoliteForm && _familiarPronouns.Contains(currentCandidateToken, StringComparer.CurrentCultureIgnoreCase))
                    pronoun = currentCandidateToken;

                if (pronoun == "" && i < modelOutput[mask].Count - 1)
                    continue;

                // If none of the pronouns are found in the dictionary (i.e. when kCount isn't large enough), create empty tuple.
                if (pronoun == "" && i == modelOutput[mask].Count - 1)
                {
                    _modelPronouns.Add(new Dictionary<string, float>() { { string.Empty, -1 } });
                    break;
                }

                float incorrectModelConfidence = modelOutput[mask].GetValueOrDefault(UserPronouns[mask].PoliteForm ? PolitePronouns.First(p => p != currentCandidateToken) : _familiarPronouns.First(p => p != currentCandidateToken), 0);
                float total = currentCandidateProbability + incorrectModelConfidence;
                float confidence = currentCandidateProbability / total * 100;

                _modelPronouns.Add(new Dictionary<string, float>() { { pronoun, confidence } });
                break;
            }
        }

        return _modelPronouns;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _robbert.Dispose();
    }
}